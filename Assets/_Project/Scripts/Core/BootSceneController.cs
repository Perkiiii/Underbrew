using System.Collections;
using Underbrew.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Underbrew.Core
{
    public class BootSceneController : MonoBehaviour
    {
        private const string BootLaunchModePlayerPrefsKey = "Underbrew.BootLaunchMode";

        [Header("Boot Scene")]
        [SerializeField] private string bootSceneName = "UG_BOOT";

        [Header("Scene Targets")]
        [SerializeField] private string introSceneName = "UG_CAVE_START";
        [SerializeField] private string introSpawnId = NewGameSpawnPoint.SpawnToken;

        [Header("Boot Timing")]
        [SerializeField] private float bootHoldSeconds = 0.15f;

        [Header("Transition Retry")]
        [SerializeField] private float transitionRetryTimeoutSeconds = 8f;
        [SerializeField] private float transitionRetryIntervalSeconds = 0.05f;

        [Header("Diagnostics")]
        [SerializeField] private bool verboseLogging = true;

        private Coroutine activeBootRoutine;

        private void Start()
        {
            TryStartBootSequence(SceneManager.GetActiveScene().name);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryStartBootSequence(scene.name);
        }

        private void TryStartBootSequence(string sceneName)
        {
            if (!string.Equals(sceneName, bootSceneName, System.StringComparison.Ordinal))
                return;

            if (activeBootRoutine != null)
                return;

            activeBootRoutine = StartCoroutine(BootSequence());
        }

        private IEnumerator BootSequence()
        {
            if (bootHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(bootHoldSeconds);

            if (!string.Equals(SceneManager.GetActiveScene().name, bootSceneName, System.StringComparison.Ordinal))
            {
                activeBootRoutine = null;
                yield break;
            }

            int launchMode = ConsumeLaunchMode();
            string targetScene = ResolveTargetScene(launchMode);
            string targetSpawnId = ResolveTargetSpawnId(launchMode);
            if (string.IsNullOrWhiteSpace(targetScene))
            {
                Debug.LogWarning("[BootSceneController] Target scene is blank. Staying in boot scene.");
                activeBootRoutine = null;
                yield break;
            }

            if (verboseLogging)
                Debug.Log($"[BootSceneController] Loading '{targetScene}' with spawn '{targetSpawnId}'.");

            yield return TryTransitionToTargetScene(targetScene, targetSpawnId);
        }

        private IEnumerator TryTransitionToTargetScene(string targetScene, string targetSpawnId)
        {
            float timeout = Mathf.Max(0.1f, transitionRetryTimeoutSeconds);
            float interval = Mathf.Max(0.01f, transitionRetryIntervalSeconds);
            float elapsed = 0f;
            bool sawBusyTransitionManager = false;

            while (elapsed < timeout)
            {
                if (!string.Equals(SceneManager.GetActiveScene().name, bootSceneName, System.StringComparison.Ordinal))
                {
                    activeBootRoutine = null;
                    yield break;
                }

                var transitionManager = SceneTransitionManager.Instance;
                if (transitionManager != null)
                {
                    if (transitionManager.IsTransitioning)
                    {
                        sawBusyTransitionManager = true;
                    }
                    else
                    {
                        bool started = transitionManager.RequestTransition(targetScene, targetSpawnId, Direction.Down);
                        if (started)
                        {
                            activeBootRoutine = null;
                            yield break;
                        }
                    }
                }

                yield return new WaitForSecondsRealtime(interval);
                elapsed += interval;
            }

            // If a transition manager exists but is still busy, do not bypass it with direct load.
            // Direct load here can strand the fade panel in black.
            if (SceneTransitionManager.Instance != null && (SceneTransitionManager.Instance.IsTransitioning || sawBusyTransitionManager))
            {
                if (verboseLogging)
                    Debug.LogWarning("[BootSceneController] Transition manager remained busy during timeout; continuing to wait to avoid black-screen direct-load fallback.");

                while (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)
                    yield return new WaitForSecondsRealtime(interval);

                if (SceneTransitionManager.Instance != null)
                {
                    bool started = SceneTransitionManager.Instance.RequestTransition(targetScene, targetSpawnId, Direction.Down);
                    if (started)
                    {
                        activeBootRoutine = null;
                        yield break;
                    }
                }
            }

            if (verboseLogging)
                Debug.LogWarning("[BootSceneController] Transition manager unavailable. Falling back to direct scene load.");

            SceneManager.LoadScene(targetScene);
            activeBootRoutine = null;
        }

        private int ConsumeLaunchMode()
        {
            int launchMode = PlayerPrefs.GetInt(BootLaunchModePlayerPrefsKey, 0);
            PlayerPrefs.DeleteKey(BootLaunchModePlayerPrefsKey);
            PlayerPrefs.Save();
            return launchMode;
        }

        private string ResolveTargetScene(int launchMode)
        {
            if (launchMode == 2)
            {
                if (SaveManager.Instance != null && SaveManager.Instance.TryBeginContinue(out var savedSceneName))
                    return savedSceneName;

                Debug.LogWarning("[BootSceneController] Continue requested but no valid save found. Falling back to intro scene.");
                return introSceneName;
            }

            return introSceneName;
        }

        private string ResolveTargetSpawnId(int launchMode)
        {
            // New Game should start at a deterministic intro entrance.
            if (launchMode == 2)
                return string.Empty;

            return introSpawnId;
        }
    }
}
