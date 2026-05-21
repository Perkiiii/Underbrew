using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Underbrew.Core
{
    public class ManagersRoot : MonoBehaviour
    {
        public static ManagersRoot Instance { get; private set; }

        [SerializeField] private bool autoBootstrapDialogueSystems = true;
        [SerializeField] private bool autoBootstrapSaveSystems = true;
        [SerializeField] private bool autoBootstrapSceneTransitionSystem = true;
        [SerializeField] private bool autoBootstrapAudioSystem = true;
        [SerializeField] private bool autoBootstrapPersistenceDiagnostics = true;
        [SerializeField] private bool autoBootstrapBootSceneController = true;
        [SerializeField] private GameObject transitionRootPrefab;

        private EventSystem persistentEventSystem;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Ensure there's an EventSystem for UI raycasting.
            EnsureEventSystem();

            if (autoBootstrapDialogueSystems)
                BootstrapDialogueSystems();

            if (autoBootstrapSaveSystems)
                BootstrapSaveSystems();

            if (autoBootstrapSceneTransitionSystem)
                BootstrapSceneTransitionSystem();

            if (autoBootstrapAudioSystem)
                BootstrapAudioSystem();

            if (autoBootstrapPersistenceDiagnostics)
                BootstrapPersistenceDiagnostics();

            if (autoBootstrapBootSceneController)
                BootstrapBootSceneController();


            CleanupDuplicatePersistentRoots();
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
            CleanupDuplicateEventSystems();
            EnsureEventSystem();

            if (autoBootstrapSceneTransitionSystem)
                BootstrapSceneTransitionSystem();

            if (autoBootstrapAudioSystem)
                BootstrapAudioSystem();

            CleanupDuplicatePersistentRoots();
        }

        public void SetPersistentEventSystemActive(bool isActive)
        {
            EnsureEventSystem();

            if (persistentEventSystem == null)
                return;

            if (persistentEventSystem.gameObject.activeSelf != isActive)
                persistentEventSystem.gameObject.SetActive(isActive);
        }

        private void EnsureEventSystem()
        {
            if (persistentEventSystem == null)
                persistentEventSystem = GetComponentInChildren<EventSystem>(true);

            if (persistentEventSystem == null)
                persistentEventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

            if (persistentEventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.transform.SetParent(transform);
                persistentEventSystem = eventSystemObject.AddComponent<EventSystem>();
                Debug.Log("[ManagersRoot] Created missing EventSystem for UI raycasting.");
            }

            if (!persistentEventSystem.gameObject.activeSelf)
                persistentEventSystem.gameObject.SetActive(true);

            if (!persistentEventSystem.enabled)
                persistentEventSystem.enabled = true;

#if ENABLE_INPUT_SYSTEM
            if (persistentEventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                persistentEventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                Debug.Log("[ManagersRoot] Added InputSystemUIInputModule for UI input.");
            }

            var standaloneInputModule = persistentEventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneInputModule != null)
                Destroy(standaloneInputModule);
#else
            if (persistentEventSystem.GetComponent<StandaloneInputModule>() == null)
                persistentEventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private void CleanupDuplicateEventSystems()
        {
            var allEventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allEventSystems.Length <= 1)
            {
                if (allEventSystems.Length == 1)
                    persistentEventSystem = allEventSystems[0];

                return;
            }

            EventSystem keeper = persistentEventSystem;

            if (keeper == null)
            {
                for (var i = 0; i < allEventSystems.Length; i++)
                {
                    var candidate = allEventSystems[i];
                    if (candidate == null)
                        continue;

                    if (candidate.gameObject.scene.name == "DontDestroyOnLoad")
                    {
                        keeper = candidate;
                        break;
                    }
                }
            }

            if (keeper == null)
                keeper = allEventSystems[0];

            persistentEventSystem = keeper;

            for (var i = 0; i < allEventSystems.Length; i++)
            {
                var eventSystem = allEventSystems[i];
                if (eventSystem == null || eventSystem == keeper)
                    continue;

                Debug.Log($"[ManagersRoot] Destroying duplicate EventSystem from scene '{eventSystem.gameObject.scene.name}'");
                Destroy(eventSystem.gameObject);
            }
        }

        private void BootstrapDialogueSystems()
        {
            if (GetComponentInChildren<DialogueManager>(true) == null)
            {
                var managerObject = new GameObject("DialogueManager");
                managerObject.transform.SetParent(transform);
                managerObject.AddComponent<DialogueManager>();
            }

            if (GetComponentInChildren<GameStateFlags>(true) == null)
            {
                var flagsObject = new GameObject("GameStateFlags");
                flagsObject.transform.SetParent(transform);
                flagsObject.AddComponent<GameStateFlags>();
            }
        }

        private void BootstrapSaveSystems()
        {
            if (GetComponentInChildren<JournalDiscoverySystem>(true) == null)
            {
                var journalDiscoveryObject = new GameObject("JournalDiscoverySystem");
                journalDiscoveryObject.transform.SetParent(transform);
                journalDiscoveryObject.AddComponent<JournalDiscoverySystem>();
            }

            if (GetComponentInChildren<PotionRecipeDiscoverySystem>(true) == null)
            {
                var potionRecipeDiscoveryObject = new GameObject("PotionRecipeDiscoverySystem");
                potionRecipeDiscoveryObject.transform.SetParent(transform);
                potionRecipeDiscoveryObject.AddComponent<PotionRecipeDiscoverySystem>();
            }

            if (GetComponentInChildren<SaveManager>(true) == null)
            {
                var saveManagerObject = new GameObject("SaveManager");
                saveManagerObject.transform.SetParent(transform);
                saveManagerObject.AddComponent<SaveManager>();
            }

            if (GetComponentInChildren<ResourceRespawnState>(true) == null)
            {
                var resourceRespawnStateObject = new GameObject("ResourceRespawnState");
                resourceRespawnStateObject.transform.SetParent(transform);
                resourceRespawnStateObject.AddComponent<ResourceRespawnState>();
            }

            if (GetComponentInChildren<CheckpointManager>(true) == null)
            {
                var checkpointManagerObject = new GameObject("CheckpointManager");
                checkpointManagerObject.transform.SetParent(transform);
                checkpointManagerObject.AddComponent<CheckpointManager>();
            }
        }

        private void BootstrapPersistenceDiagnostics()
        {
            if (GetComponentInChildren<PersistenceDiagnostics>(true) == null)
            {
                var diagnosticsObject = new GameObject("PersistenceDiagnostics");
                diagnosticsObject.transform.SetParent(transform);
                diagnosticsObject.AddComponent<PersistenceDiagnostics>();
            }
        }

        private void BootstrapSceneTransitionSystem()
        {
            if (FindFirstObjectByType<SceneTransitionManager>(FindObjectsInactive.Include) != null)
                return;

            if (transitionRootPrefab != null)
            {
                var instance = Instantiate(transitionRootPrefab, transform);
                instance.name = transitionRootPrefab.name;

                if (instance.GetComponentInChildren<SceneTransitionManager>(true) == null)
                {
                    Debug.LogWarning("[ManagersRoot] TransitionRoot prefab has no SceneTransitionManager. Added one to prefab root instance.");
                    instance.AddComponent<SceneTransitionManager>();
                }

                Debug.Log("[ManagersRoot] Instantiated missing SceneTransitionManager from TransitionRoot prefab.");
                return;
            }

            var transitionRootObject = new GameObject("TransitionRoot");
            transitionRootObject.transform.SetParent(transform);
            transitionRootObject.AddComponent<SceneTransitionManager>();
            Debug.LogWarning("[ManagersRoot] TransitionRoot prefab not assigned. Created fallback SceneTransitionManager without fade hierarchy.");
        }

        private void BootstrapAudioSystem()
        {
            var existingAudioManager = FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (existingAudioManager != null)
            {
                if (existingAudioManager.transform.parent != transform)
                    existingAudioManager.transform.SetParent(transform, true);

                return;
            }

            var audioManagerObject = new GameObject("AudioManager");
            audioManagerObject.transform.SetParent(transform);
            audioManagerObject.AddComponent<AudioManager>();
        }

        private void BootstrapBootSceneController()
        {
            if (FindFirstObjectByType<BootSceneController>(FindObjectsInactive.Include) != null)
                return;

            var controllerObject = new GameObject("BootSceneController");
            controllerObject.transform.SetParent(transform);
            controllerObject.AddComponent<BootSceneController>();
        }

        private void CleanupDuplicatePersistentRoots()
        {
            CleanupDuplicateRootsFor(ManagersRoot.Instance);
            CleanupDuplicateRootsFor(SceneTransitionManager.Instance);
            CleanupDuplicateRootsFor(PersistentCamera.Instance);
            CleanupDuplicateRootsFor(PersistentUIRoot.Instance);
            CleanupDuplicateRootsFor<Player>(null);
        }

        private static void CleanupDuplicateRootsFor<T>(T preferredKeeper) where T : Component
        {
            var instances = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (instances.Length <= 1)
                return;

            T keeper = null;

            if (preferredKeeper != null)
            {
                for (var i = 0; i < instances.Length; i++)
                {
                    if (instances[i] == preferredKeeper)
                    {
                        keeper = preferredKeeper;
                        break;
                    }
                }
            }

            if (keeper == null)
            {
                for (var i = 0; i < instances.Length; i++)
                {
                    var candidate = instances[i];
                    if (candidate == null)
                        continue;

                    if (candidate.gameObject.scene.name == "DontDestroyOnLoad")
                    {
                        keeper = candidate;
                        break;
                    }
                }
            }

            if (keeper == null)
                keeper = instances[0];

            for (var i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance == null || instance == keeper)
                    continue;

                Destroy(instance.gameObject);
            }
        }
    }
}
