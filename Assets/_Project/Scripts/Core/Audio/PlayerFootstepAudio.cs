using UnityEngine;

namespace Underbrew.Core
{
    [RequireComponent(typeof(Player))]
    public class PlayerFootstepAudio : MonoBehaviour
    {
        [SerializeField] [Min(0f)] private float movementThreshold = 0.1f;
        [SerializeField] [Min(0f)] private float fadeInSeconds = 0.08f;
        [SerializeField] [Min(0f)] private float fadeOutSeconds = 0.12f;
        [SerializeField] [Min(0.1f)] private float referenceMoveSpeed = 4f;
        [SerializeField] [Range(0.85f, 1.25f)] private float minMoveSpeedPitchMultiplier = 0.94f;
        [SerializeField] [Range(0.85f, 1.25f)] private float maxMoveSpeedPitchMultiplier = 1.06f;

        private Player player;
        private AudioSource loopingSource;
        private float targetVolume;
        private float currentBasePitch = 1f;

        private void Awake()
        {
            player = GetComponent<Player>();
            EnsureLoopingSource();
        }

        private void Update()
        {
            if (AudioManager.Instance == null || player == null)
                return;

            EnsureLoopingSource();

            bool shouldPlay = ShouldPlayFootsteps(out var horizontalSpeed);
            if (shouldPlay)
                EnsurePlayback(horizontalSpeed);
            else
                targetVolume = 0f;

            UpdateLoopingSource(horizontalSpeed);
        }

        private bool ShouldPlayFootsteps(out float horizontalSpeed)
        {
            horizontalSpeed = 0f;

            if (player == null || player.rb == null || !player.groundDetected)
                return false;

            horizontalSpeed = Mathf.Abs(player.rb.linearVelocity.x);
            return horizontalSpeed >= movementThreshold;
        }

        private void EnsurePlayback(float horizontalSpeed)
        {
            if (loopingSource == null)
                return;

            if (loopingSource.isPlaying && loopingSource.clip != null)
                return;

            if (!AudioManager.Instance.TryGetLoopingSfxSettings(AudioCueId.Footsteps, out var clip, out var volume, out var pitch, out var mixerGroup))
                return;

            loopingSource.outputAudioMixerGroup = mixerGroup;
            loopingSource.clip = clip;
            loopingSource.loop = true;
            loopingSource.volume = 0f;
            currentBasePitch = pitch;
            loopingSource.pitch = ApplySpeedToPitch(horizontalSpeed, currentBasePitch);
            targetVolume = volume;
            loopingSource.Play();
        }

        private void UpdateLoopingSource(float horizontalSpeed)
        {
            if (loopingSource == null)
                return;

            if (loopingSource.isPlaying)
            {
                loopingSource.pitch = ApplySpeedToPitch(horizontalSpeed, currentBasePitch);
            }

            float fadeDuration = targetVolume > loopingSource.volume ? fadeInSeconds : fadeOutSeconds;
            if (fadeDuration <= 0f)
            {
                loopingSource.volume = targetVolume;
            }
            else
            {
                loopingSource.volume = Mathf.MoveTowards(loopingSource.volume, targetVolume, Time.deltaTime / fadeDuration);
            }

            if (loopingSource.isPlaying && loopingSource.volume <= 0.001f && targetVolume <= 0.001f)
            {
                loopingSource.Stop();
                loopingSource.clip = null;
                currentBasePitch = 1f;
            }
        }

        private void EnsureLoopingSource()
        {
            if (loopingSource != null)
                return;

            loopingSource = gameObject.GetComponent<AudioSource>();
            if (loopingSource == null)
                loopingSource = gameObject.AddComponent<AudioSource>();

            loopingSource.playOnAwake = false;
            loopingSource.loop = true;
            loopingSource.spatialBlend = 0f;
            loopingSource.volume = 0f;
        }

        private float ApplySpeedToPitch(float horizontalSpeed, float basePitch)
        {
            if (horizontalSpeed <= 0f)
                return basePitch;

            float speedRatio = Mathf.Clamp01(horizontalSpeed / Mathf.Max(0.1f, referenceMoveSpeed));
            float speedPitchMultiplier = Mathf.Lerp(minMoveSpeedPitchMultiplier, maxMoveSpeedPitchMultiplier, speedRatio);
            return basePitch * speedPitchMultiplier;
        }
    }
}
