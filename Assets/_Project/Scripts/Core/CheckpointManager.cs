using UnityEngine;
using UnityEngine.SceneManagement;

namespace Underbrew.Core
{
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }

        public string CurrentCheckpointId { get; private set; }
        public string CurrentCheckpointScene { get; private set; }
        public Vector3 CurrentCheckpointPosition { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool ActivateCheckpoint(string checkpointId, Vector3 worldPosition, string sceneName = null, bool saveImmediately = true)
        {
            CurrentCheckpointId = checkpointId ?? string.Empty;
            CurrentCheckpointPosition = worldPosition;
            CurrentCheckpointScene = string.IsNullOrWhiteSpace(sceneName)
                ? SceneManager.GetActiveScene().name
                : sceneName;

            if (saveImmediately && SaveManager.Instance != null)
                return SaveManager.Instance.SaveCurrentToDisk();

            return true;
        }

        public void ApplyFromSaveData(SaveData data)
        {
            if (data == null)
                return;

            CurrentCheckpointId = data.checkpointId ?? string.Empty;
            CurrentCheckpointScene = data.sceneName ?? string.Empty;
            var position = data.checkpointPosition.ToVector3();
            if (position == Vector3.zero)
                position = data.playerPosition.ToVector3();

            CurrentCheckpointPosition = position;
        }

        public void ClearActiveCheckpoint()
        {
            CurrentCheckpointId = string.Empty;
            CurrentCheckpointScene = string.Empty;
            CurrentCheckpointPosition = Vector3.zero;
        }
    }
}
