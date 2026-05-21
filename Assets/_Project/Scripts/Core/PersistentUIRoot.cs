using UnityEngine;
using UnityEngine.SceneManagement;

namespace Underbrew.Core
{
    public class PersistentUIRoot : MonoBehaviour
    {
        public static PersistentUIRoot Instance { get; private set; }

        [SerializeField] private string[] nonPersistentSceneNames = new string[] { "UG_MENU_MAIN" };

        private void Awake()
        {
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (IsNonPersistentScene(activeSceneName))
            {
                return;
            }

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private bool IsNonPersistentScene(string sceneName)
        {
            foreach (string nonPersistentScene in nonPersistentSceneNames)
            {
                if (sceneName == nonPersistentScene)
                    return true;
            }

            return false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
