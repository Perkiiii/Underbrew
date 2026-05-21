using UnityEngine;

namespace Underbrew.World
{
    [DisallowMultipleComponent]
    public class NewGameSpawnPoint : MonoBehaviour
    {
        public const string SpawnToken = "__NEW_GAME_SPAWN__";

        [SerializeField] private bool isPrimary = true;

        public static NewGameSpawnPoint FindSpawnPoint()
        {
            var allSpawnPoints = FindObjectsByType<NewGameSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (allSpawnPoints == null || allSpawnPoints.Length == 0)
                return null;

            for (var i = 0; i < allSpawnPoints.Length; i++)
            {
                if (allSpawnPoints[i] != null && allSpawnPoints[i].isPrimary)
                    return allSpawnPoints[i];
            }

            return allSpawnPoints[0];
        }
    }
}
