using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Underbrew.Core
{
    public class PersistenceDiagnostics : MonoBehaviour
    {
        public static PersistenceDiagnostics Instance { get; private set; }

        [Header("Diagnostics")]
        [SerializeField] private bool enableSnapshots = false;
        [SerializeField] private bool logOnSceneLoaded = true;
        [SerializeField] private bool includeDontDestroyRoots = true;
        [SerializeField] private bool includeDuplicateCounts = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
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

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!enableSnapshots)
                return;

            if (!logOnSceneLoaded)
                return;

            LogSnapshot($"SceneLoaded -> {scene.name} ({mode})");
        }

        public void LogSnapshot(string context)
        {
            var sb = new StringBuilder(1024);
            var activeScene = SceneManager.GetActiveScene();
            sb.AppendLine($"[PersistenceDiagnostics] Snapshot: {context}");
            sb.AppendLine($"ActiveScene='{activeScene.name}' isLoaded={activeScene.isLoaded}");

            AppendObjectInfo(sb, "ManagersRoot", ManagersRoot.Instance);
            AppendObjectInfo(sb, "SceneTransitionManager", SceneTransitionManager.Instance);
            AppendObjectInfo(sb, "SaveManager", SaveManager.Instance);
            AppendObjectInfo(sb, "CheckpointManager", CheckpointManager.Instance);
            AppendObjectInfo(sb, "GameStateFlags", GameStateFlags.Instance);
            AppendObjectInfo(sb, "DialogueManager", DialogueManager.Instance);
            AppendObjectInfo(sb, "PersistentCamera", PersistentCamera.Instance);
            AppendObjectInfo(sb, "PersistentUIRoot", PersistentUIRoot.Instance);

            var player = FindFirstObjectByType<Player>(FindObjectsInactive.Include);
            AppendObjectInfo(sb, "Player", player);

            if (includeDuplicateCounts)
            {
                sb.AppendLine("Duplicate Counts:");
                AppendCountLine<ManagersRoot>(sb, "ManagersRoot");
                AppendCountLine<SceneTransitionManager>(sb, "SceneTransitionManager");
                AppendCountLine<SaveManager>(sb, "SaveManager");
                AppendCountLine<CheckpointManager>(sb, "CheckpointManager");
                AppendCountLine<GameStateFlags>(sb, "GameStateFlags");
                AppendCountLine<DialogueManager>(sb, "DialogueManager");
                AppendCountLine<PersistentCamera>(sb, "PersistentCamera");
                AppendCountLine<PersistentUIRoot>(sb, "PersistentUIRoot");
                AppendCountLine<Player>(sb, "Player");
            }

            if (includeDontDestroyRoots)
                AppendDontDestroyOnLoadRoots(sb);

            Debug.Log(sb.ToString());
        }

        public static void LogSnapshotStatic(string context)
        {
            if (Instance == null)
                return;

            if (!Instance.enableSnapshots)
                return;

            Instance.LogSnapshot(context);
        }

        private static void AppendObjectInfo(StringBuilder sb, string label, Object obj)
        {
            if (obj == null)
            {
                sb.AppendLine($"- {label}: <null>");
                return;
            }

            var component = obj as Component;
            if (component != null)
            {
                sb.AppendLine($"- {label}: '{component.gameObject.name}' scene='{component.gameObject.scene.name}' active={component.gameObject.activeInHierarchy}");
                return;
            }

            sb.AppendLine($"- {label}: '{obj.name}'");
        }

        private static void AppendCountLine<T>(StringBuilder sb, string label) where T : Component
        {
            var count = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            sb.AppendLine($"  {label}: {count}");
        }

        private static void AppendDontDestroyOnLoadRoots(StringBuilder sb)
        {
            sb.AppendLine("DontDestroyOnLoad Roots:");

            var objects = Resources.FindObjectsOfTypeAll<GameObject>();
            bool foundAny = false;

            foreach (var gameObject in objects)
            {
                if (gameObject == null)
                    continue;

                if (!gameObject.scene.IsValid())
                    continue;

                if (gameObject.scene.name != "DontDestroyOnLoad")
                    continue;

                if (gameObject.transform.parent != null)
                    continue;

                if ((gameObject.hideFlags & HideFlags.HideAndDontSave) != 0)
                    continue;

                foundAny = true;
                sb.AppendLine($"  - {gameObject.name}");
            }

            if (!foundAny)
                sb.AppendLine("  <none>");
        }
    }
}
