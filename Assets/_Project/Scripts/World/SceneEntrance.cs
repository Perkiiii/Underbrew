using Underbrew.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Underbrew.World
{
    public class SceneEntrance : MonoBehaviour
    {
        public string entranceID;
        public Direction entranceDirection;

        private static readonly Dictionary<string, SceneEntrance> entrances = new Dictionary<string, SceneEntrance>();

        private void Awake()
        {
            if (!string.IsNullOrEmpty(entranceID))
            {
                if (!entrances.TryGetValue(entranceID, out var existingEntrance) || existingEntrance == null)
                {
                    entrances[entranceID] = this;
                }
                else
                {
                    Debug.LogWarning($"[SceneEntrance] Duplicate entranceID detected: {entranceID}. This entrance will not be registered.");
                }
            }
            else
            {
                Debug.LogWarning("[SceneEntrance] entranceID is null or empty. This entrance will not be registered.");
            }
        }

        private void OnDestroy()
        {
            if (!string.IsNullOrEmpty(entranceID) && entrances.ContainsKey(entranceID))
            {
                entrances.Remove(entranceID);
            }
        }

        public static SceneEntrance FindEntrance(string id, bool logWarning = true)
        {
            if (entrances.TryGetValue(id, out var entrance))
            {
                return entrance;
            }

            if (logWarning)
            {
                Debug.LogWarning($"[SceneEntrance] No entrance found with ID: {id}");
            }

            return null;
        }
    }
}
