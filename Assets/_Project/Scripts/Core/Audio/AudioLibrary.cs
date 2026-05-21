using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Underbrew.Core
{
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Underbrew/Audio/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        [Header("UI Cues")]
        [SerializeField] private List<AudioCueEntry> uiCues = new();

        [Header("Gameplay Cues")]
        [SerializeField] private List<AudioCueEntry> gameplayCues = new();

        [Header("Footstep Cues")]
        [SerializeField] private List<AudioCueEntry> footstepCues = new();

        [Header("Ambience Cues")]
        [SerializeField] private List<AudioCueEntry> ambienceCues = new();

        [Header("Music Cues")]
        [SerializeField] private List<AudioCueEntry> musicCues = new();

        [Header("Scene Routing")]
        [SerializeField] private List<SceneAmbienceEntry> sceneAmbience = new();
        [SerializeField] private List<SceneMusicEntry> sceneMusic = new();

        public bool TryGetCue(AudioCueId cueId, out AudioCueEntry cue)
        {
            if (TryGetCueFromList(uiCues, cueId, out cue))
                return true;

            if (TryGetCueFromList(gameplayCues, cueId, out cue))
                return true;

            if (TryGetCueFromList(footstepCues, cueId, out cue))
                return true;

            if (TryGetCueFromList(ambienceCues, cueId, out cue))
                return true;

            if (TryGetCueFromList(musicCues, cueId, out cue))
                return true;

            cue = null;
            return false;
        }

        public AudioCueId GetAmbienceForScene(string sceneName)
        {
            return GetSceneCue(sceneAmbience, sceneName, entry => entry.AmbienceCueId);
        }

        public AudioCueId GetMusicForScene(string sceneName)
        {
            return GetSceneCue(sceneMusic, sceneName, entry => entry.MusicCueId);
        }

        private void OnValidate()
        {
            ApplySuggestedDefaults(uiCues);
            ApplySuggestedDefaults(gameplayCues);
            ApplySuggestedDefaults(footstepCues);
            ApplySuggestedDefaults(ambienceCues);
            ApplySuggestedDefaults(musicCues);
        }

        private static bool TryGetCueFromList(List<AudioCueEntry> cues, AudioCueId cueId, out AudioCueEntry cue)
        {
            for (var i = 0; i < cues.Count; i++)
            {
                var candidate = cues[i];
                if (candidate == null || candidate.CueId != cueId)
                    continue;

                cue = candidate;
                return true;
            }

            cue = null;
            return false;
        }

        private static AudioCueId GetSceneCue<TEntry>(List<TEntry> entries, string sceneName, Func<TEntry, AudioCueId> cueSelector) where TEntry : class
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return AudioCueId.None;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                var entrySceneName = entry switch
                {
                    SceneAmbienceEntry ambienceEntry => ambienceEntry.SceneName,
                    SceneMusicEntry musicEntry => musicEntry.SceneName,
                    _ => string.Empty
                };

                if (string.IsNullOrWhiteSpace(entrySceneName))
                    continue;

                if (string.Equals(entrySceneName, sceneName, StringComparison.Ordinal))
                    return cueSelector(entry);
            }

            return AudioCueId.None;
        }

        private static void ApplySuggestedDefaults(List<AudioCueEntry> cues)
        {
            if (cues == null)
                return;

            for (var i = 0; i < cues.Count; i++)
                cues[i]?.ApplySuggestedDefaults();
        }
    }

    [Serializable]
    public class AudioCueEntry
    {
        [SerializeField] private AudioCueId cueId;
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private float volume = 1f;
        [SerializeField] private float pitchMin = 1f;
        [SerializeField] private float pitchMax = 1f;
        [SerializeField] private float cooldownSeconds;
        [SerializeField, HideInInspector] private bool hasAppliedSuggestedDefaults;
        [FormerlySerializedAs("lastConfiguredCueId")]
        [SerializeField, HideInInspector] private AudioCueId lastSuggestedCueId = AudioCueId.None;

        public AudioCueId CueId => cueId;
        public AudioClip[] Clips => clips;
        public float Volume => Mathf.Clamp01(volume);
        public float PitchMin => pitchMin;
        public float PitchMax => pitchMax;
        public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);

        public void ApplySuggestedDefaults()
        {
            if (cueId == AudioCueId.None)
                return;

            if (hasAppliedSuggestedDefaults && lastSuggestedCueId == cueId)
                return;

            switch (cueId)
            {
                case AudioCueId.MusicMenuLoop:
                case AudioCueId.MusicGameplay01:
                case AudioCueId.MusicGameplay02:
                case AudioCueId.MusicGameplay03:
                case AudioCueId.MusicGameplay04:
                case AudioCueId.MusicGameplay05:
                case AudioCueId.MusicGameplay06:
                    SetSuggestedValues(0.15f, 1f, 1f, 0f);
                    break;

                case AudioCueId.AmbienceMenuLoop:
                case AudioCueId.AmbienceCaveLoop:
                case AudioCueId.AmbienceForestLoop:
                case AudioCueId.AmbienceApothecaryLoop:
                    SetSuggestedValues(0.25f, 1f, 1f, 0f);
                    break;

                case AudioCueId.Footsteps:
                case AudioCueId.FootstepCave:
                case AudioCueId.FootstepWood:
                    SetSuggestedValues(0.3f, 0.95f, 1.05f, 0.02f);
                    break;

                case AudioCueId.PickupGeneric:
                    SetSuggestedValues(0.5f, 0.97f, 1.03f, 0.05f);
                    break;

                case AudioCueId.PickupSpecial:
                case AudioCueId.RecipeUnlock:
                case AudioCueId.QuestUpdate:
                case AudioCueId.SignatureMagic:
                    SetSuggestedValues(0.65f, 0.98f, 1.02f, 0.1f);
                    break;

                case AudioCueId.ProcessAdd:
                    SetSuggestedValues(0.45f, 0.98f, 1.02f, 0.05f);
                    break;

                case AudioCueId.ProcessComplete:
                    SetSuggestedValues(0.6f, 1f, 1f, 0.08f);
                    break;

                case AudioCueId.BrewStart:
                    SetSuggestedValues(0.55f, 1f, 1f, 0.08f);
                    break;

                case AudioCueId.BrewComplete:
                    SetSuggestedValues(0.7f, 1f, 1f, 0.1f);
                    break;

                case AudioCueId.BenchCheckpointSave:
                    SetSuggestedValues(0.5f, 1f, 1f, 0.1f);
                    break;

                case AudioCueId.UIClick:
                case AudioCueId.UIMenuClick:
                    SetSuggestedValues(0.4f, 1f, 1f, 0.05f);
                    break;

                case AudioCueId.UITab:
                    SetSuggestedValues(0.4f, 1f, 1f, 0.04f);
                    break;

                case AudioCueId.UIJournalOpen:
                case AudioCueId.UIJournalClose:
                    SetSuggestedValues(0.4f, 1f, 1f, 0.06f);
                    break;

                case AudioCueId.UIBackpackOpen:
                case AudioCueId.UIBackpackClose:
                    SetSuggestedValues(0.4f, 1f, 1f, 0.06f);
                    break;

                case AudioCueId.UIBackpackMove:
                    SetSuggestedValues(0.35f, 0.98f, 1.02f, 0.05f);
                    break;

                default:
                    SetSuggestedValues(1f, 1f, 1f, 0f);
                    break;
            }

            hasAppliedSuggestedDefaults = true;
            lastSuggestedCueId = cueId;
        }

        private void SetSuggestedValues(float suggestedVolume, float suggestedPitchMin, float suggestedPitchMax, float suggestedCooldown)
        {
            volume = suggestedVolume;
            pitchMin = suggestedPitchMin;
            pitchMax = suggestedPitchMax;
            cooldownSeconds = suggestedCooldown;
        }
    }

    [Serializable]
    public class SceneAmbienceEntry
    {
        [SerializeField] private string sceneName;
        [SerializeField] private AudioCueId ambienceCueId = AudioCueId.None;

        public string SceneName => sceneName;
        public AudioCueId AmbienceCueId => ambienceCueId;
    }

    [Serializable]
    public class SceneMusicEntry
    {
        [SerializeField] private string sceneName;
        [SerializeField] private AudioCueId musicCueId = AudioCueId.None;

        public string SceneName => sceneName;
        public AudioCueId MusicCueId => musicCueId;
    }
}
