using System;
using System.Collections.Generic;
using UnityEngine;

namespace Underbrew.Core
{
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 6;

        public int version = CurrentVersion;
        public string sceneName;
        public string checkpointId;
        public SerializableVector3 checkpointPosition;
        public SerializableVector3 playerPosition;
        // Optional future-proof fields for later combat/health systems.
        public float playerHealth = 100f;
        public float playerMaxHealth = 100f;
        public float playerFacing = 1f; // 1 = right, -1 = left
        public string savedAtUtc;
        public string integrityHash;
        public List<SaveItemStack> inventory = new();
        public List<SaveFlagEntry> flags = new();
        public List<SaveResourceNodeEntry> resourceNodes = new();
        public List<string> discoveredJournalItemIds = new();
        public List<string> discoveredPotionRecipeIds = new();
        // Optional convenience list derived from flags for dialogue systems.
        public List<string> appliedDialogueOutcomeKeys = new();

        /// <summary>
        /// Validates that all required fields are present and well-formed.
        /// </summary>
        public bool IsValid()
        {
            if (version < 1 || version > CurrentVersion)
                return false;

            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            if (inventory == null)
                return false;

            if (flags == null)
                return false;

            if (resourceNodes == null)
                return false;

            if (discoveredJournalItemIds == null)
                return false;

            if (discoveredPotionRecipeIds == null)
                return false;

            return true;
        }

        public override string ToString()
        {
            return $"SaveData v{version} | Scene: {sceneName} | Checkpoint: {checkpointId} | " +
                                         $"Inventory: {inventory.Count} items | Flags: {flags.Count} | Resources: {resourceNodes.Count} | Journal: {discoveredJournalItemIds.Count} | Potion Recipes: {discoveredPotionRecipeIds.Count} | " +
                   $"Saved: {savedAtUtc}";
        }
    }

    [Serializable]
    public struct SaveItemStack
    {
        public string itemId;
        public int quantity;
        // -1 means legacy/unspecified ordering.
        public int slotIndex;
    }

    [Serializable]
    public struct SaveFlagEntry
    {
        public string key;
        public bool value;
    }

    [Serializable]
    public struct SaveResourceNodeEntry
    {
        public string nodeId;
        public long nextAvailableUnixSeconds;
    }

    [Serializable]
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(float xValue, float yValue, float zValue)
        {
            x = xValue;
            y = yValue;
            z = zValue;
        }

        public static SerializableVector3 FromVector3(Vector3 value)
        {
            return new SerializableVector3(value.x, value.y, value.z);
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    /// <summary>
    /// Checkpoint data for bench/rest point locations.
    /// Health fields are optional and reserved for potential future systems.
    /// </summary>
    [Serializable]
    public class CheckpointData
    {
        public string checkpointID = "";
        public string sceneName = "";
        public SerializableVector3 position = new SerializableVector3(0, 0, 0);
        public float facing = 1f; // 1 = right, -1 = left
        public float restoredHealth = 100f;
        public float maxHealth = 100f;
        public string activatedAtUtc = "";

        public CheckpointData() { }

        public CheckpointData(string id, string scene, Vector3 pos, float face, float health, float maxHp)
        {
            checkpointID = id;
            sceneName = scene;
            position = SerializableVector3.FromVector3(pos);
            facing = face;
            restoredHealth = health;
            maxHealth = maxHp;
            activatedAtUtc = System.DateTime.UtcNow.ToString("O");
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(checkpointID) && !string.IsNullOrWhiteSpace(sceneName);
        }

        public override string ToString()
        {
            return $"Checkpoint '{checkpointID}' | Scene: {sceneName} | Health: {restoredHealth}/{maxHealth} | Activated: {activatedAtUtc}";
        }
    }
}
