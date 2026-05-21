using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Underbrew.Core
{
    public class AudioManager : MonoBehaviour
    {
        private const string DefaultAudioLibraryResourcesPath = "Audio/DefaultAudioLibrary";
        private const float DefaultUiVolume = 0.4f;
        private const float DefaultSfxVolume = 0.6f;
        private const float DefaultAmbienceVolume = 0.25f;

        public static AudioManager Instance { get; private set; }

        [Header("Library")]
        [SerializeField] private AudioLibrary audioLibrary;

        [Header("Lifecycle")]
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Sources")]
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource uiSource;
        [SerializeField] private List<AudioSource> sfxSources = new();
        [SerializeField] [Min(1)] private int sfxPoolSize = 4;

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixerGroup masterMixerGroup;
        [SerializeField] private AudioMixerGroup musicMixerGroup;
        [SerializeField] private AudioMixerGroup ambienceMixerGroup;
        [SerializeField] private AudioMixerGroup uiMixerGroup;
        [SerializeField] private AudioMixerGroup sfxMixerGroup;

        [Header("One-Shot Smoothing")]
        [SerializeField] [Min(0f)] private float oneShotFadeInSeconds = 0.01f;
        [SerializeField] [Min(0f)] private float oneShotFadeOutSeconds = 0.04f;
        [SerializeField] [Min(0f)] private float oneShotMinimumTailSeconds = 0.02f;

        [Header("Scene Routing")]
        [SerializeField] private string mainMenuSceneName = "UG_MENU_MAIN";
        [SerializeField] private string apothecarySceneName = "UG_INT_APOTHECARY";
        [SerializeField] private string bootSceneName = "UG_BOOT";
        [SerializeField] private string[] forestSceneNames = { "UG_PATH_01", "UG_PATH_02" };

        [Header("Diagnostics")]
        [SerializeField] private bool logUiMenuClickDiagnostics;
        [SerializeField] private float duplicateUiMenuClickWindowSeconds = 0.2f;

        private readonly Dictionary<AudioCueId, float> nextAllowedPlayTimeByCue = new();
        private int nextSfxSourceIndex;
        private AudioCueId currentAmbienceCue = AudioCueId.None;
        private AudioCueId currentMusicCue = AudioCueId.None;
        private float lastUiMenuClickRequestTime = -10f;
        private Coroutine currentMusicFadeCoroutine;

        private bool IsUiMenuClickDiagnosticsEnabled => logUiMenuClickDiagnostics || Application.isEditor || Debug.isDebugBuild;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes && transform.parent == null)
                DontDestroyOnLoad(gameObject);

            ResolveAudioLibrary();
            EnsureAudioSources();
            ApplySceneAudio(SceneManager.GetActiveScene().name);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void PlaySfx(AudioCueId cueId)
        {
            Play(cueId, resolveSource: ResolveNextSfxSource, DefaultSfxVolume, sfxMixerGroup, "SfxTransientSource");
        }

        public void PlayUi(AudioCueId cueId)
        {
            LogUiMenuClickRequest(cueId, "PlayUi");
            Play(cueId, () => uiSource, DefaultUiVolume, uiMixerGroup, "UiTransientSource");
        }

        public void PlayUiTransitionSafe(AudioCueId cueId)
        {
            LogUiMenuClickRequest(cueId, "PlayUiTransitionSafe");

            if (cueId == AudioCueId.None)
                return;

            EnsureAudioSources();

            if (!TryResolveCue(cueId, out var cue))
                return;

            if (Time.unscaledTime < GetNextAllowedPlayTime(cueId))
                return;

            var clip = ChooseClip(cue);
            if (clip == null)
                return;

            float pitch = ResolvePitch(cue);
            var source = CreateTransientUiSource();
            if (source == null)
                return;

            source.pitch = pitch;
            float volume = cue.Volume > 0f ? cue.Volume : DefaultUiVolume;
            source.PlayOneShot(clip, volume);

            if (IsUiMenuClickDiagnosticsEnabled && cueId == AudioCueId.UIMenuClick)
            {
                Debug.Log($"[AudioManager] Transition-safe UI click started cue={cueId} clip='{clip.name}' scene='{SceneManager.GetActiveScene().name}' frame={Time.frameCount} time={Time.unscaledTime:F3}");
            }

            float duration = clip.length;
            float safePitch = Mathf.Abs(pitch);
            if (safePitch > 0.01f)
                duration /= safePitch;

            Destroy(source.gameObject, duration + 0.1f);
            nextAllowedPlayTimeByCue[cueId] = Time.unscaledTime + cue.CooldownSeconds;
        }

        public void ClearSharedUiPlaybackForTransition()
        {
            EnsureAudioSources();

            if (uiSource == null)
                return;

            if (IsUiMenuClickDiagnosticsEnabled)
            {
                Debug.Log($"[AudioManager] Clearing shared UI source for transition at frame={Time.frameCount} time={Time.unscaledTime:F3} scene='{SceneManager.GetActiveScene().name}'");
            }

            uiSource.Stop();
            uiSource.clip = null;
        }

        public void SetAmbience(AudioCueId cueId)
        {
            EnsureAudioSources();

            if (ambienceSource == null)
                return;

            if (cueId == AudioCueId.None)
            {
                StopAmbience();
                return;
            }

            if (currentAmbienceCue == cueId && ambienceSource.isPlaying)
                return;

            if (!TryResolveCue(cueId, out var cue))
            {
                StopAmbience();
                return;
            }

            var clip = ChooseClip(cue);
            if (clip == null)
            {
                StopAmbience();
                return;
            }

            currentAmbienceCue = cueId;
            ambienceSource.clip = clip;
            ambienceSource.loop = true;
            ambienceSource.volume = cue.Volume > 0f ? cue.Volume : DefaultAmbienceVolume;
            ambienceSource.pitch = 1f;
            ambienceSource.Play();
        }

        public void SetMusic(AudioCueId cueId)
        {
            EnsureAudioSources();

            if (musicSource == null)
                return;

            if (cueId == AudioCueId.None)
            {
                StopMusic();
                return;
            }

            if (currentMusicCue == cueId && musicSource.isPlaying)
                return;

            if (!TryResolveCue(cueId, out var cue))
            {
                StopMusic();
                return;
            }

            var clip = ChooseClip(cue);
            if (clip == null)
            {
                StopMusic();
                return;
            }

            currentMusicCue = cueId;
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.volume = cue.Volume > 0f ? cue.Volume : 0.15f;
            musicSource.pitch = 1f;
            musicSource.Play();
        }

        public void StopAmbience()
        {
            EnsureAudioSources();

            currentAmbienceCue = AudioCueId.None;

            if (ambienceSource == null)
                return;

            ambienceSource.Stop();
            ambienceSource.clip = null;
        }

        public void StopMusic()
        {
            EnsureAudioSources();

            currentMusicCue = AudioCueId.None;

            if (musicSource == null)
                return;

            musicSource.Stop();
            musicSource.clip = null;
        }

        public void SetMusicWithFade(AudioCueId cueId, float fadeDuration = 1f)
        {
            if (currentMusicFadeCoroutine != null)
                StopCoroutine(currentMusicFadeCoroutine);

            currentMusicFadeCoroutine = StartCoroutine(CrossfadeMusic(cueId, fadeDuration));
        }

        private IEnumerator CrossfadeMusic(AudioCueId newCueId, float fadeDuration)
        {
            EnsureAudioSources();

            if (musicSource == null)
                yield break;

            if (newCueId == AudioCueId.None)
            {
                if (musicSource.isPlaying)
                    yield return StartCoroutine(FadeVolume(musicSource, musicSource.volume, 0f, fadeDuration));
                StopMusic();
                yield break;
            }

            if (!TryResolveCue(newCueId, out var newCue))
            {
                StopMusic();
                yield break;
            }

            var newClip = ChooseClip(newCue);
            if (newClip == null)
            {
                StopMusic();
                yield break;
            }

            float targetVolume = newCue.Volume > 0f ? newCue.Volume : 0.15f;

            // Fade out current music if playing
            if (musicSource.isPlaying && currentMusicCue != newCueId)
            {
                yield return StartCoroutine(FadeVolume(musicSource, musicSource.volume, 0f, fadeDuration));
            }

            // Switch to new music
            currentMusicCue = newCueId;
            musicSource.clip = newClip;
            musicSource.loop = true;
            musicSource.volume = 0f;
            musicSource.pitch = 1f;
            musicSource.Play();

            // Fade in new music
            yield return StartCoroutine(FadeVolume(musicSource, 0f, targetVolume, fadeDuration));

            currentMusicFadeCoroutine = null;
        }

        private IEnumerator FadeVolume(AudioSource source, float startVolume, float endVolume, float duration)
        {
            if (source == null || duration <= 0f)
                yield break;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                source.volume = Mathf.Lerp(startVolume, endVolume, t);
                yield return null;
            }

            source.volume = endVolume;
        }

        public void PlayFootstepForCurrentScene()
        {
            var cueId = ResolveFootstepCue();
            if (cueId != AudioCueId.None)
                PlaySfx(cueId);
        }

        public bool TryGetLoopingSfxSettings(AudioCueId cueId, out AudioClip clip, out float volume, out float pitch, out AudioMixerGroup mixerGroup)
        {
            clip = null;
            volume = DefaultSfxVolume;
            pitch = 1f;
            mixerGroup = sfxMixerGroup;

            var resolvedCueId = cueId == AudioCueId.Footsteps ? ResolveFootstepCue() : cueId;
            if (resolvedCueId == AudioCueId.None)
                return false;

            if (!TryResolveCue(resolvedCueId, out var cue))
                return false;

            clip = ChooseClip(cue);
            if (clip == null)
                return false;

            volume = cue.Volume > 0f ? cue.Volume : DefaultSfxVolume;
            pitch = ResolvePitch(cue);
            return true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySceneAudio(scene.name);
        }

        private void ApplySceneAudio(string sceneName)
        {
            SetAmbience(ResolveAmbienceCueForScene(sceneName));
            SetMusic(ResolveMusicCueForScene(sceneName));
        }

        private void Play(AudioCueId cueId, System.Func<AudioSource> resolveSource, float fallbackVolume, AudioMixerGroup mixerGroup, string transientSourceName)
        {
            if (cueId == AudioCueId.None || resolveSource == null)
                return;

            EnsureAudioSources();

            if (!TryResolveCue(cueId, out var cue))
                return;

            if (Time.unscaledTime < GetNextAllowedPlayTime(cueId))
                return;

            var source = resolveSource.Invoke();
            if (source == null)
                return;

            var clip = ChooseClip(cue);
            if (clip == null)
                return;

            float pitch = ResolvePitch(cue);

            if (IsUiMenuClickDiagnosticsEnabled && cueId == AudioCueId.UIMenuClick)
            {
                Debug.Log($"[AudioManager] Shared UI play started cue={cueId} clip='{clip.name}' scene='{SceneManager.GetActiveScene().name}' frame={Time.frameCount} time={Time.unscaledTime:F3}");
            }

            float volume = cue.Volume > 0f ? cue.Volume : fallbackVolume;

            if (ShouldUseSmoothedOneShot(cueId))
            {
                var transientSource = CreateTransientSource(transientSourceName, mixerGroup);
                if (transientSource == null)
                    return;

                transientSource.pitch = pitch;
                StartCoroutine(PlaySmoothedOneShot(transientSource, clip, volume));
            }
            else
            {
                source.pitch = pitch;
                source.PlayOneShot(clip, volume);
            }

            nextAllowedPlayTimeByCue[cueId] = Time.unscaledTime + cue.CooldownSeconds;
        }

        private AudioSource CreateTransientUiSource()
        {
            return CreateTransientSource("UiTransientSource", uiMixerGroup);
        }

        private AudioSource CreateTransientSource(string objectName, AudioMixerGroup mixerGroup)
        {
            var host = new GameObject(objectName);
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
                SceneManager.MoveGameObjectToScene(host, activeScene);

            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = mixerGroup;
            return source;
        }

        private IEnumerator PlaySmoothedOneShot(AudioSource source, AudioClip clip, float targetVolume)
        {
            if (source == null || clip == null)
                yield break;

            source.clip = clip;
            source.volume = 0f;
            source.Play();

            float pitch = Mathf.Max(0.01f, Mathf.Abs(source.pitch));
            float clipDuration = clip.length / pitch;
            float fadeIn = Mathf.Max(0f, oneShotFadeInSeconds);
            float fadeOut = Mathf.Max(0f, oneShotFadeOutSeconds);
            float minTail = Mathf.Max(0f, oneShotMinimumTailSeconds);
            float sustainDuration = Mathf.Max(0f, clipDuration - fadeIn - fadeOut - minTail);

            if (fadeIn > 0f)
            {
                yield return FadeVolume(source, 0f, targetVolume, fadeIn);
            }
            else
            {
                source.volume = targetVolume;
            }

            if (sustainDuration > 0f)
                yield return new WaitForSecondsRealtime(sustainDuration);

            if (fadeOut > 0f)
            {
                yield return FadeVolume(source, source.volume, 0f, fadeOut);
            }
            else
            {
                source.volume = 0f;
            }

            source.Stop();
            source.clip = null;
            Destroy(source.gameObject);
        }

        private void LogUiMenuClickRequest(AudioCueId cueId, string path)
        {
            if (!IsUiMenuClickDiagnosticsEnabled || cueId != AudioCueId.UIMenuClick)
                return;

            float now = Time.unscaledTime;
            bool isDuplicate = (now - lastUiMenuClickRequestTime) <= Mathf.Max(0.01f, duplicateUiMenuClickWindowSeconds);
            string duplicateSuffix = isDuplicate ? " POSSIBLE_DUPLICATE" : string.Empty;
            Debug.Log($"[AudioManager] UI click requested via {path} scene='{SceneManager.GetActiveScene().name}' frame={Time.frameCount} time={now:F3}{duplicateSuffix}");
            lastUiMenuClickRequestTime = now;
        }

        private float GetNextAllowedPlayTime(AudioCueId cueId)
        {
            return nextAllowedPlayTimeByCue.TryGetValue(cueId, out var value) ? value : 0f;
        }

        private bool TryResolveCue(AudioCueId cueId, out AudioCueEntry cue)
        {
            cue = null;

            ResolveAudioLibrary();

            if (audioLibrary == null)
                return false;

            return audioLibrary.TryGetCue(cueId, out cue);
        }

        private AudioClip ChooseClip(AudioCueEntry cue)
        {
            var clips = cue?.Clips;
            if (clips == null || clips.Length == 0)
                return null;

            if (clips.Length == 1)
                return clips[0];

            return clips[Random.Range(0, clips.Length)];
        }

        private float ResolvePitch(AudioCueEntry cue)
        {
            if (cue == null)
                return 1f;

            var min = cue.PitchMin;
            var max = cue.PitchMax;
            if (max < min)
            {
                var temp = min;
                min = max;
                max = temp;
            }

            if (Mathf.Approximately(min, max))
                return min;

            return Random.Range(min, max);
        }

        private AudioSource ResolveNextSfxSource()
        {
            if (sfxSources.Count == 0)
                return null;

            if (nextSfxSourceIndex >= sfxSources.Count)
                nextSfxSourceIndex = 0;

            var source = sfxSources[nextSfxSourceIndex];
            nextSfxSourceIndex = (nextSfxSourceIndex + 1) % sfxSources.Count;
            return source;
        }

        private AudioCueId ResolveAmbienceCueForScene(string sceneName)
        {
            ResolveAudioLibrary();

            if (audioLibrary != null)
            {
                var libraryCue = audioLibrary.GetAmbienceForScene(sceneName);
                if (libraryCue != AudioCueId.None)
                    return libraryCue;
            }

            if (string.IsNullOrWhiteSpace(sceneName) || string.Equals(sceneName, bootSceneName, System.StringComparison.Ordinal))
                return AudioCueId.None;

            if (string.Equals(sceneName, mainMenuSceneName, System.StringComparison.Ordinal))
                return AudioCueId.AmbienceMenuLoop;

            if (string.Equals(sceneName, apothecarySceneName, System.StringComparison.Ordinal))
                return AudioCueId.AmbienceApothecaryLoop;

            if (MatchesScene(sceneName, forestSceneNames))
                return AudioCueId.AmbienceForestLoop;

            return AudioCueId.AmbienceCaveLoop;
        }

        private AudioCueId ResolveMusicCueForScene(string sceneName)
        {
            ResolveAudioLibrary();

            if (audioLibrary != null)
            {
                var libraryCue = audioLibrary.GetMusicForScene(sceneName);
                if (libraryCue != AudioCueId.None)
                    return libraryCue;
            }

            if (string.IsNullOrWhiteSpace(sceneName) || string.Equals(sceneName, bootSceneName, System.StringComparison.Ordinal))
                return AudioCueId.None;

            if (string.Equals(sceneName, mainMenuSceneName, System.StringComparison.Ordinal))
                return AudioCueId.MusicMenuLoop;

            return AudioCueId.None;
        }

        private AudioCueId ResolveFootstepCue()
        {
            if (TryResolveCue(AudioCueId.Footsteps, out _))
                return AudioCueId.Footsteps;

            if (TryResolveCue(AudioCueId.FootstepCave, out _))
                return AudioCueId.FootstepCave;

            if (TryResolveCue(AudioCueId.FootstepWood, out _))
                return AudioCueId.FootstepWood;

            return AudioCueId.None;
        }

        private static bool ShouldUseSmoothedOneShot(AudioCueId cueId)
        {
            return cueId != AudioCueId.Footsteps
                && cueId != AudioCueId.FootstepCave
                && cueId != AudioCueId.FootstepWood;
        }

        private static bool MatchesScene(string sceneName, string[] sceneNames)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || sceneNames == null)
                return false;

            for (var i = 0; i < sceneNames.Length; i++)
            {
                var candidate = sceneNames[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (string.Equals(sceneName, candidate, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void EnsureAudioSources()
        {
            if (ambienceSource == null)
                ambienceSource = CreateChildSource("AmbienceSource", loop: true, ambienceMixerGroup);

            if (musicSource == null)
                musicSource = CreateChildSource("MusicSource", loop: true, musicMixerGroup);

            if (uiSource == null)
                uiSource = CreateChildSource("UISource", loop: false, uiMixerGroup);

            sfxSources.RemoveAll(source => source == null);
            while (sfxSources.Count < Mathf.Max(1, sfxPoolSize))
                sfxSources.Add(CreateChildSource($"SfxSource_{sfxSources.Count}", loop: false, sfxMixerGroup));
        }

        private AudioSource CreateChildSource(string objectName, bool loop, AudioMixerGroup mixerGroup = null)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);

            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.outputAudioMixerGroup = mixerGroup;
            return source;
        }

        private void ResolveAudioLibrary()
        {
            if (audioLibrary != null)
                return;

            audioLibrary = Resources.Load<AudioLibrary>(DefaultAudioLibraryResourcesPath);
        }
    }
}
