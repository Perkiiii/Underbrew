using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Underbrew.World;

namespace Underbrew.Core
{
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("Persistence")]
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Animator Setup")]
        [SerializeField] private Animator transitionAnimator;
        [SerializeField] private bool useAnimatorForFadeOut = false;
        [SerializeField] private string fadeOutTriggerName = "End";
        [SerializeField] private string fadeInTriggerName = "Start";

        [Header("Clip Lengths")]
        [SerializeField] private float fadeOutClipDuration = 1f;
        [SerializeField] private float fadeInClipDuration = 1f;
        [SerializeField] private float postLoadBlackHoldDuration = 0.2f;
        [SerializeField] private float postPlacementBlackHoldDuration = 0.25f;
        [SerializeField] private int postPlacementSettleFrames = 2;
        [SerializeField] private float playerControlRestoreDelayAfterLoad = 0.9f;

        [Header("Scene Placement")]
        [SerializeField] private float entranceResolveTimeout = 1f;

        [Header("Menu Scenes")]
        [SerializeField] private string[] menuSceneNames = new string[] { "UG_MENU_MAIN" };

        [Header("Placement Exceptions")]
        [SerializeField] private string[] scenesWithoutEntrancePlacement = new string[] { "UG_BOOT" };

        [Header("Blackout Target Scenes")]
        [SerializeField] private string[] scenesToKeepBlackAfterLoad = new string[] { "UG_BOOT" };

        public string TargetSceneName { get; private set; }
        public string TargetSpawnID { get; private set; }
        public Direction EntryDirection { get; private set; }
        public bool IsTransitioning => isTransitioning;

        private bool isTransitioning;
        private Image fadePanelImage;
        private AudioListener transitionAudioListener;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes)
            {
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
                else if (GetComponentInParent<ManagersRoot>() == null)
                {
                    Debug.LogWarning("[SceneTransitionManager] Persistence is enabled, but this object is not a root and not under ManagersRoot. Make it a root object or parent it under ManagersRoot.");
                }
            }

            if (GetComponentInParent<ManagersRoot>() == null)
            {
                if (!persistAcrossScenes)
                {
                    Debug.LogWarning("[SceneTransitionManager] Persistence is disabled and this object is not under ManagersRoot.");
                }
            }

            if (transitionAnimator == null)
            {
                Debug.LogWarning("[SceneTransitionManager] No transition animator assigned. Assign the persistent fade panel Animator here.");
            }

            fadePanelImage = transitionAnimator != null ? transitionAnimator.GetComponent<Image>() : null;
            SetAnimatorFadeControlEnabled(false);
            SetFadePanelAlpha(0f);
            SetFadePanelRaycastBlocking(false);
        }

        public bool RequestTransition(string sceneName, string spawnID, Direction direction)
        {
            if (isTransitioning)
            {
                return false;
            }

            AudioManager.Instance?.ClearSharedUiPlaybackForTransition();

            TargetSceneName = sceneName;
            TargetSpawnID = spawnID;
            EntryDirection = direction;
            isTransitioning = true;

            StartCoroutine(LoadSceneTransition());
            return true;
        }

        public void ForceBlackOverlay(bool visible, bool blockRaycasts = true)
        {
            SetAnimatorFadeControlEnabled(false);

            if (visible)
            {
                SetFadePanelAlpha(1f);
                SetFadePanelRaycastBlocking(blockRaycasts);
                return;
            }

            SetFadePanelAlpha(0f);
            SetFadePanelRaycastBlocking(false);
        }

        public IEnumerator FadeOverlayToBlack(bool blockRaycasts = true)
        {
            SetFadePanelRaycastBlocking(blockRaycasts);
            yield return PlayFadeOutAnimation();
            SetFadePanelAlpha(1f);
            SetFadePanelRaycastBlocking(blockRaycasts);
        }

        public bool TryGetOverlaySorting(out int sortingLayerId, out int sortingOrder)
        {
            sortingLayerId = 0;
            sortingOrder = 0;

            if (fadePanelImage == null)
                return false;

            var fadeCanvas = fadePanelImage.canvas;
            if (fadeCanvas == null)
                return false;

            sortingLayerId = fadeCanvas.sortingLayerID;
            sortingOrder = fadeCanvas.sortingOrder;
            return true;
        }

        private IEnumerator LoadSceneTransition()
        {
            string sourceSceneName = SceneManager.GetActiveScene().name;
            bool isMenuSourceScene = IsMenuScene(sourceSceneName);
            bool isMenuTargetScene = IsMenuScene(TargetSceneName);
            bool skipEntrancePlacement = ShouldSkipEntrancePlacement(TargetSceneName);
            bool keepBlackOnTargetScene = ShouldKeepBlackOnScene(TargetSceneName);

            PersistenceDiagnostics.LogSnapshotStatic($"TransitionStart -> source='{sourceSceneName}' target='{TargetSceneName}' menuSource={isMenuSourceScene} menuTarget={isMenuTargetScene} skipPlacement={skipEntrancePlacement}");

            Player player = FindFirstObjectByType<Player>();
            SetPlayerInputEnabled(player, false);
            SetFadePanelRaycastBlocking(true);

            yield return PlayFadeOutAnimation();
            SetFadePanelAlpha(1f);

            if (isMenuSourceScene && !isMenuTargetScene)
            {
                // Starting gameplay from a menu should not keep any old persistent gameplay shells from previous runs.
                CleanupPersistentGameplayObjects();
                PersistenceDiagnostics.LogSnapshotStatic($"AfterSourceMenuCleanup -> source='{sourceSceneName}' target='{TargetSceneName}'");
            }

            if (isMenuTargetScene)
            {
                // Clear persistent gameplay objects before loading a menu scene so we don't affect menu-native objects.
                CleanupPersistentGameplayObjects();
                EnsureTransitionAudioListener();
                PersistenceDiagnostics.LogSnapshotStatic($"AfterMenuCleanup -> target='{TargetSceneName}'");
            }

            ManagersRoot.Instance?.SetPersistentEventSystemActive(false);

            var asyncLoad = SceneManager.LoadSceneAsync(TargetSceneName, LoadSceneMode.Single);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            ReleaseTransitionAudioListener();
            ManagersRoot.Instance?.SetPersistentEventSystemActive(true);

            PersistenceDiagnostics.LogSnapshotStatic($"AfterSceneLoad -> active='{SceneManager.GetActiveScene().name}'");

            yield return null;

            if (postLoadBlackHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(postLoadBlackHoldDuration);
            }

            if (keepBlackOnTargetScene)
            {
                // Keep the overlay black for transient bootstrap scenes so they never flash on screen.
                FinalizeTransitionState(keepBlackOverlay: true);
                yield break;
            }

            player = FindFirstObjectByType<Player>();

            if (isMenuTargetScene)
            {
                yield return PlayFadeInAnimation(player);
                FinalizeTransitionState();
                yield break;
            }

            if (skipEntrancePlacement)
            {
                yield return PlayFadeInAnimation(player);
                FinalizeTransitionState();
                yield break;
            }

            SceneEntrance entrance = null;
            NewGameSpawnPoint newGameSpawnPoint = null;
            float elapsed = 0f;
            bool isNewGameSpawn = string.Equals(TargetSpawnID, NewGameSpawnPoint.SpawnToken, System.StringComparison.Ordinal);

            while (elapsed < entranceResolveTimeout)
            {
                if (isNewGameSpawn)
                {
                    newGameSpawnPoint = NewGameSpawnPoint.FindSpawnPoint();
                    if (newGameSpawnPoint != null)
                        break;
                }
                else
                {
                    entrance = SceneEntrance.FindEntrance(TargetSpawnID, logWarning: false);
                    if (entrance != null)
                        break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (newGameSpawnPoint != null && player != null)
            {
                player.transform.position = newGameSpawnPoint.transform.position;

                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                if (PersistentCamera.Instance != null)
                {
                    PersistentCamera.Instance.SnapToPosition(player.transform.position);
                }
            }
            else if (entrance != null && player != null)
            {
                player.transform.position = entrance.transform.position + GetOffset(EntryDirection);

                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                if (PersistentCamera.Instance != null)
                {
                    PersistentCamera.Instance.SnapToPosition(player.transform.position);
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(TargetSpawnID))
                {
                    Debug.LogWarning($"[SceneTransitionManager] Could not place player. SpawnID='{TargetSpawnID}', Entrance='{(entrance != null ? entrance.name : "null")}', NewGameSpawn='{(newGameSpawnPoint != null ? newGameSpawnPoint.name : "null")}', Player='{(player != null ? player.name : "null")}'.");
                }
            }

            if (postPlacementBlackHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(postPlacementBlackHoldDuration);
            }

            var settleFrames = Mathf.Max(0, postPlacementSettleFrames);
            for (var i = 0; i < settleFrames; i++)
            {
                yield return new WaitForEndOfFrame();

                if (player != null && PersistentCamera.Instance != null)
                {
                    PersistentCamera.Instance.SnapToPosition(player.transform.position);
                }
            }

            yield return PlayFadeInAnimation(player);
            FinalizeTransitionState();
        }

        private IEnumerator PlayFadeOutAnimation()
        {
            if (fadePanelImage == null)
            {
                Debug.LogWarning("[SceneTransitionManager] Fade out skipped because fade panel image is missing.");
                yield break;
            }

            // Animator mode is optional; script mode is the default for deterministic transitions.
            if (useAnimatorForFadeOut && transitionAnimator != null && !string.IsNullOrWhiteSpace(fadeOutTriggerName))
            {
                SetAnimatorFadeControlEnabled(true);
                transitionAnimator.ResetTrigger(fadeInTriggerName);
                transitionAnimator.SetTrigger(fadeOutTriggerName);
                yield return null;
                yield return new WaitForSecondsRealtime(fadeOutClipDuration);
                SetFadePanelAlpha(1f);
                SetAnimatorFadeControlEnabled(false);
                yield break;
            }

            // Fallback: deterministic script-driven fade-out when animator is unavailable.
            float fadeDuration = Mathf.Max(0.01f, fadeOutClipDuration);
            float startAlpha = Mathf.Clamp01(fadePanelImage.color.a);
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                SetFadePanelAlpha(Mathf.Lerp(startAlpha, 1f, t));
                yield return null;
            }

            SetFadePanelAlpha(1f);
        }

        private IEnumerator PlayFadeInAnimation(Player player)
        {
            if (fadePanelImage == null)
            {
                Debug.LogWarning("[SceneTransitionManager] Fade in skipped because fade panel image is missing.");
                if (playerControlRestoreDelayAfterLoad > 0f)
                {
                    yield return new WaitForSecondsRealtime(playerControlRestoreDelayAfterLoad);
                }

                SetPlayerInputEnabled(player, true);
                SetFadePanelRaycastBlocking(false);
                yield break;
            }

            SetAnimatorFadeControlEnabled(false);

            // Fade-in is driven directly by panel alpha to avoid animator state timing edge-cases
            // across menu -> boot -> gameplay chained scene loads.
            float fadeDuration = Mathf.Max(0.01f, fadeInClipDuration);
            float elapsed = 0f;
            bool inputRestored = false;
            float restoreDelay = Mathf.Max(0f, playerControlRestoreDelayAfterLoad);

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                SetFadePanelAlpha(1f - t);

                if (!inputRestored && elapsed >= restoreDelay)
                {
                    SetPlayerInputEnabled(player, true);
                    inputRestored = true;
                }

                yield return null;
            }

            if (!inputRestored)
                SetPlayerInputEnabled(player, true);

            SetFadePanelAlpha(0f);
        }

        private void FinalizeTransitionState(bool keepBlackOverlay = false)
        {
            SetAnimatorFadeControlEnabled(false);
            ReleaseTransitionAudioListener();

            if (keepBlackOverlay)
            {
                SetFadePanelAlpha(1f);
                SetFadePanelRaycastBlocking(true);
            }
            else
            {
                SetFadePanelAlpha(0f);
                SetFadePanelRaycastBlocking(false);
            }

            isTransitioning = false;
        }

        private void SetAnimatorFadeControlEnabled(bool enabled)
        {
            if (transitionAnimator != null)
            {
                transitionAnimator.enabled = enabled;
            }
        }

        private void SetFadePanelRaycastBlocking(bool shouldBlock)
        {
            if (fadePanelImage != null)
            {
                fadePanelImage.raycastTarget = shouldBlock;
            }
        }

        private void SetFadePanelAlpha(float alpha)
        {
            if (fadePanelImage == null)
                return;

            var color = fadePanelImage.color;
            color.a = Mathf.Clamp01(alpha);
            fadePanelImage.color = color;
        }

        private void SetPlayerInputEnabled(Player player, bool enabled)
        {
            if (player == null)
            {
                return;
            }

            PlayerInput playerInput = player.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = enabled;
            }

            if (player.input != null)
            {
                if (enabled)
                {
                    player.input.Enable();
                    player.RefreshHeldInput();
                }
                else
                {
                    player.input.Disable();
                }
            }
        }

        private Vector3 GetOffset(Direction direction)
        {
            return direction switch
            {
                Direction.Left => new Vector3(0.5f, 0f, 0f),
                Direction.Right => new Vector3(-0.5f, 0f, 0f),
                Direction.Up => new Vector3(0f, -0.5f, 0f),
                Direction.Down => new Vector3(0f, 0.5f, 0f),
                _ => Vector3.zero,
            };
        }

        private void CleanupPersistentGameplayObjects()
        {
            // Destroy gameplay objects that should only exist during gameplay
            Player player = FindFirstObjectByType<Player>();
            if (player != null)
            {
                Destroy(player.gameObject);
            }

            PersistentCamera camera = PersistentCamera.Instance;
            if (camera != null)
            {
                Destroy(camera.gameObject);
            }

            PersistentUIRoot uiRoot = PersistentUIRoot.Instance;
            if (uiRoot != null)
            {
                Destroy(uiRoot.gameObject);
            }
        }

        private void EnsureTransitionAudioListener()
        {
            if (transitionAudioListener != null)
                return;

            if (FindFirstObjectByType<AudioListener>() != null)
                return;

            var host = new GameObject("TransitionAudioListener");
            transitionAudioListener = host.AddComponent<AudioListener>();
        }

        private void ReleaseTransitionAudioListener()
        {
            if (transitionAudioListener == null)
                return;

            Destroy(transitionAudioListener.gameObject);
            transitionAudioListener = null;
        }

        private bool ShouldSkipEntrancePlacement(string sceneName)
        {
            foreach (string noPlacementScene in scenesWithoutEntrancePlacement)
            {
                if (sceneName == noPlacementScene)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldKeepBlackOnScene(string sceneName)
        {
            foreach (string blackScene in scenesToKeepBlackAfterLoad)
            {
                if (sceneName == blackScene)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsMenuScene(string sceneName)
        {
            foreach (string menuScene in menuSceneNames)
            {
                if (sceneName == menuScene)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
