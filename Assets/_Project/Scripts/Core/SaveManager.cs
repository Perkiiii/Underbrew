using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Underbrew.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Underbrew.Core
{
    public class SaveManager : MonoBehaviour
    {
        private const string DefaultSaveFileName = "underbrew_save_v1.json";

        public static SaveManager Instance { get; private set; }

        [Header("Save File")]
        [SerializeField] private string saveFileName = DefaultSaveFileName;

        [Header("Item Resolution")]
        [SerializeField] private List<ItemData> itemCatalog = new();

        [Header("Diagnostics")]
        [SerializeField] private bool verboseLogging;

        private SaveData pendingRestore;
        private readonly Dictionary<string, ItemData> itemLookup = new(StringComparer.Ordinal);
        private readonly HashSet<string> warnedMissingItemIds = new(StringComparer.Ordinal);
        private bool runtimeCatalogScanned;

        private string SaveFilePath => GetSaveFilePath(GetEffectiveSaveFileName());
        private string BackupSaveFilePath => GetBackupSaveFilePath(GetEffectiveSaveFileName());
        private string TempSaveFilePath => GetTempSaveFilePath(GetEffectiveSaveFileName());

        [ContextMenu("Delete save file")]
        public void DeleteSaveFileFromInspector()
        {
            pendingRestore = null;

            bool deleted = DeleteSaveOnDisk(GetEffectiveSaveFileName());
            if (verboseLogging)
                Debug.Log(deleted
                    ? "[SaveManager] Save file deleted via inspector context menu."
                    : "[SaveManager] Failed to delete save file via inspector context menu.");
        }

        public static string GetSaveFilePath(string fileName = null)
        {
            var resolvedFileName = ResolveSaveFileName(fileName);
            return Path.Combine(Application.persistentDataPath, resolvedFileName);
        }

        public static string GetBackupSaveFilePath(string fileName = null)
        {
            return GetSaveFilePath(fileName) + ".bak";
        }

        public static string GetTempSaveFilePath(string fileName = null)
        {
            return GetSaveFilePath(fileName) + ".tmp";
        }

        public static bool HasValidSaveOnDisk(string fileName = null)
        {
            return TryReadValidSaveData(fileName, out _);
        }

        public static bool DeleteSaveOnDisk(string fileName = null)
        {
            try
            {
                var path = GetSaveFilePath(fileName);
                var backupPath = GetBackupSaveFilePath(fileName);
                var tempPath = GetTempSaveFilePath(fileName);

                if (File.Exists(path))
                    File.Delete(path);

                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveSaveFileName(string fileName)
        {
            if (!string.IsNullOrWhiteSpace(fileName))
                return fileName;

            if (Instance != null && !string.IsNullOrWhiteSpace(Instance.saveFileName))
                return Instance.saveFileName;

            return DefaultSaveFileName;
        }

        private string GetEffectiveSaveFileName()
        {
            return ResolveSaveFileName(saveFileName);
        }

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

            RebuildItemLookup();
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

        public bool HasSaveFile()
        {
            return File.Exists(SaveFilePath);
        }

        public bool HasValidSaveFile()
        {
            if (!TryLoadFromDisk(out var data))
                return false;

            return data != null && data.IsValid();
        }

        public bool TryBeginContinue(out string sceneName)
        {
            sceneName = string.Empty;

            if (!TryLoadFromDisk(out var loadedData))
                return false;

            if (string.IsNullOrWhiteSpace(loadedData.sceneName))
            {
                Debug.LogWarning("[SaveManager] Save exists but sceneName is empty. Continue aborted.");
                return false;
            }

            pendingRestore = loadedData;
            sceneName = loadedData.sceneName;
            return true;
        }

        public bool SaveCurrentToDisk()
        {
            var saveData = BuildCurrentSaveData();
            if (saveData == null)
                return false;

            return WriteSaveData(saveData);
        }

        public void DeleteSave()
        {
            pendingRestore = null;
            DeleteSaveOnDisk(GetEffectiveSaveFileName());
        }

        public void ResetRuntimeStateForNewGame()
        {
            pendingRestore = null;

            DialogueManager.Instance?.ForceClose();
            GameStateFlags.Instance?.ResetToDefaults();
            JournalDiscoverySystem.Instance?.Clear();
            PotionRecipeDiscoverySystem.Instance?.Clear();
            ResourceRespawnState.Instance?.ResetAll();
            CheckpointManager.Instance?.ClearActiveCheckpoint();

            var inventorySystem = FindFirstObjectByType<InventorySystem>(FindObjectsInactive.Include);
            if (inventorySystem != null)
                inventorySystem.Clear();
        }

        public bool TryLoadFromDisk(out SaveData saveData)
        {
            saveData = null;

            if (!TryReadValidSaveData(GetEffectiveSaveFileName(), out saveData))
                return false;

            var loadedVersion = saveData.version <= 0 ? 1 : saveData.version;

            saveData.inventory ??= new List<SaveItemStack>();
            saveData.flags ??= new List<SaveFlagEntry>();
            saveData.resourceNodes ??= new List<SaveResourceNodeEntry>();
            saveData.discoveredJournalItemIds ??= new List<string>();
            saveData.discoveredPotionRecipeIds ??= new List<string>();

            if (!TryMigrateToCurrentVersion(saveData, loadedVersion))
                return false;

            return true;
        }

        public void SetPendingRestore(SaveData saveData)
        {
            pendingRestore = saveData;
        }

        private SaveData BuildCurrentSaveData()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[SaveManager] Cannot save because there is no active scene.");
                return null;
            }

            var player = FindFirstObjectByType<Player>();
            var inventorySystem = FindFirstObjectByType<InventorySystem>();
            var flags = GameStateFlags.Instance;
            var resourceRespawnState = ResourceRespawnState.Instance;
            var checkpoints = CheckpointManager.Instance;
            var journalDiscovery = JournalDiscoverySystem.Instance ?? FindFirstObjectByType<JournalDiscoverySystem>(FindObjectsInactive.Include);
            var potionRecipeDiscovery = PotionRecipeDiscoverySystem.Instance ?? FindFirstObjectByType<PotionRecipeDiscoverySystem>(FindObjectsInactive.Include);

            var checkpointPosition = checkpoints != null
                ? checkpoints.CurrentCheckpointPosition
                : (player != null ? player.transform.position : Vector3.zero);

            var data = new SaveData
            {
                version = SaveData.CurrentVersion,
                sceneName = scene.name,
                checkpointId = checkpoints != null ? checkpoints.CurrentCheckpointId : string.Empty,
                checkpointPosition = SerializableVector3.FromVector3(checkpointPosition),
                playerPosition = SerializableVector3.FromVector3(player != null ? player.transform.position : checkpointPosition),
                playerFacing = player != null ? player.facingDir : 1f,
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                inventory = inventorySystem != null ? inventorySystem.CreateSaveSnapshot() : new List<SaveItemStack>(),
                flags = flags != null ? flags.CreateSaveSnapshot() : new List<SaveFlagEntry>(),
                resourceNodes = resourceRespawnState != null ? resourceRespawnState.CreateSaveSnapshot() : new List<SaveResourceNodeEntry>(),
                discoveredJournalItemIds = journalDiscovery != null ? journalDiscovery.CreateSaveSnapshot() : new List<string>(),
                discoveredPotionRecipeIds = potionRecipeDiscovery != null ? potionRecipeDiscovery.CreateSaveSnapshot() : new List<string>()
            };

            if (data.flags != null && data.flags.Count > 0)
            {
                for (var i = 0; i < data.flags.Count; i++)
                {
                    var flag = data.flags[i];
                    if (!flag.value || string.IsNullOrWhiteSpace(flag.key))
                        continue;

                    data.appliedDialogueOutcomeKeys.Add(flag.key);
                }
            }

            return data;
        }

        private bool WriteSaveData(SaveData saveData)
        {
            try
            {
                var directoryPath = Path.GetDirectoryName(SaveFilePath);
                if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                saveData.integrityHash = ComputeIntegrityHash(saveData);

                var json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(TempSaveFilePath, json);

                if (File.Exists(SaveFilePath))
                {
                    // Atomic replace where supported; keeps a backup for recovery.
                    File.Replace(TempSaveFilePath, SaveFilePath, BackupSaveFilePath, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(TempSaveFilePath, SaveFilePath);
                }

                if (verboseLogging)
                    Debug.Log($"[SaveManager] Saved to '{SaveFilePath}'.");

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveManager] Failed to write save file: {exception.Message}");

                try
                {
                    if (File.Exists(TempSaveFilePath))
                        File.Delete(TempSaveFilePath);
                }
                catch
                {
                    // Best effort cleanup only.
                }

                return false;
            }
        }

        private static bool TryReadValidSaveData(string fileName, out SaveData saveData)
        {
            saveData = null;

            var primaryPath = GetSaveFilePath(fileName);
            var backupPath = GetBackupSaveFilePath(fileName);

            if (TryReadSaveDataFromPath(primaryPath, out saveData, out _))
                return true;

            if (TryReadSaveDataFromPath(backupPath, out saveData, out _))
                return true;

            return false;
        }

        private static bool TryReadSaveDataFromPath(string path, out SaveData saveData, out string failureReason)
        {
            saveData = null;
            failureReason = string.Empty;

            if (!File.Exists(path))
            {
                failureReason = "file-missing";
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);
                saveData = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }

            if (saveData == null || !saveData.IsValid())
            {
                failureReason = "invalid-save-data";
                saveData = null;
                return false;
            }

            var loadedVersion = saveData.version <= 0 ? 1 : saveData.version;
            if (loadedVersion >= 3)
            {
                if (string.IsNullOrWhiteSpace(saveData.integrityHash))
                {
                    failureReason = "missing-integrity-hash";
                    saveData = null;
                    return false;
                }

                var expectedHash = saveData.integrityHash;
                var computedHash = ComputeIntegrityHash(saveData);
                if (!string.Equals(expectedHash, computedHash, StringComparison.OrdinalIgnoreCase))
                {
                    failureReason = "integrity-hash-mismatch";
                    saveData = null;
                    return false;
                }
            }

            return true;
        }

        private static string ComputeIntegrityHash(SaveData saveData)
        {
            if (saveData == null)
                return string.Empty;

            var originalHash = saveData.integrityHash;
            saveData.integrityHash = string.Empty;

            var jsonWithoutHash = JsonUtility.ToJson(saveData, false);

            saveData.integrityHash = originalHash;

            var bytes = Encoding.UTF8.GetBytes(jsonWithoutHash);
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(bytes);
            return BytesToHex(hashBytes);
        }

        private static string BytesToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var sb = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("X2"));

            return sb.ToString();
        }

        private bool TryMigrateToCurrentVersion(SaveData saveData, int loadedVersion)
        {
            if (saveData == null)
                return false;

            if (loadedVersion > SaveData.CurrentVersion)
            {
                Debug.LogWarning($"[SaveManager] Save version {loadedVersion} is newer than supported version {SaveData.CurrentVersion}.");
                return false;
            }

            var workingVersion = loadedVersion;
            while (workingVersion < SaveData.CurrentVersion)
            {
                switch (workingVersion)
                {
                    case 1:
                        MigrateV1ToV2(saveData);
                        workingVersion = 2;
                        break;
                    case 2:
                        MigrateV2ToV3(saveData);
                        workingVersion = 3;
                        break;
                    case 3:
                        MigrateV3ToV4(saveData);
                        workingVersion = 4;
                        break;
                    case 4:
                        MigrateV4ToV5(saveData);
                        workingVersion = 5;
                        break;
                    case 5:
                        MigrateV5ToV6(saveData);
                        workingVersion = 6;
                        break;
                    default:
                        Debug.LogWarning($"[SaveManager] No migration path from version {workingVersion}.");
                        return false;
                }
            }

            saveData.version = SaveData.CurrentVersion;
            return true;
        }

        private static void MigrateV1ToV2(SaveData saveData)
        {
            // SaveData v1 had no slotIndex; mark as unspecified so load order fallback is used.
            if (saveData.inventory == null)
                return;

            for (var i = 0; i < saveData.inventory.Count; i++)
            {
                var entry = saveData.inventory[i];
                entry.slotIndex = -1;
                saveData.inventory[i] = entry;
            }
        }

        private static void MigrateV2ToV3(SaveData saveData)
        {
            // SaveData v3 introduces integrityHash.
            saveData.integrityHash = string.Empty;
        }

        private static void MigrateV3ToV4(SaveData saveData)
        {
            // SaveData v4 introduces resourceNodes respawn state.
            saveData.resourceNodes ??= new List<SaveResourceNodeEntry>();
        }

        private static void MigrateV4ToV5(SaveData saveData)
        {
            // SaveData v5 introduces journal discovery state.
            saveData.discoveredJournalItemIds ??= new List<string>();
        }

        private static void MigrateV5ToV6(SaveData saveData)
        {
            // SaveData v6 introduces discovered potion recipe state.
            saveData.discoveredPotionRecipeIds ??= new List<string>();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (pendingRestore == null)
                return;

            if (!string.Equals(scene.name, pendingRestore.sceneName, StringComparison.Ordinal))
                return;

            ApplyPendingRestore();
        }

        private void ApplyPendingRestore()
        {
            if (pendingRestore == null)
                return;

            RebuildItemLookup();

            var data = pendingRestore;
            pendingRestore = null;

            if (CheckpointManager.Instance != null)
                CheckpointManager.Instance.ApplyFromSaveData(data);

            if (GameStateFlags.Instance != null)
                GameStateFlags.Instance.LoadFromSaveSnapshot(data.flags);

            var journalDiscovery = JournalDiscoverySystem.Instance ?? FindFirstObjectByType<JournalDiscoverySystem>(FindObjectsInactive.Include);
            if (journalDiscovery != null)
                journalDiscovery.LoadFromSaveSnapshot(data.discoveredJournalItemIds);

            var potionRecipeDiscovery = PotionRecipeDiscoverySystem.Instance ?? FindFirstObjectByType<PotionRecipeDiscoverySystem>(FindObjectsInactive.Include);
            if (potionRecipeDiscovery != null)
                potionRecipeDiscovery.LoadFromSaveSnapshot(data.discoveredPotionRecipeIds);

            if (ResourceRespawnState.Instance != null)
                ResourceRespawnState.Instance.LoadFromSaveSnapshot(data.resourceNodes);

            var inventorySystem = FindFirstObjectByType<InventorySystem>();
            if (inventorySystem != null)
                inventorySystem.LoadFromSaveSnapshot(data.inventory, ResolveItemById);

            var player = FindFirstObjectByType<Player>();
            if (player == null)
                return;

            var targetPosition = ResolvePlayerPosition(data);
            player.transform.position = targetPosition;

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            if (PersistentCamera.Instance != null)
                PersistentCamera.Instance.SnapToPosition(targetPosition);
        }

        private Vector3 ResolvePlayerPosition(SaveData data)
        {
            if (!string.IsNullOrWhiteSpace(data.checkpointId))
            {
                var entrance = SceneEntrance.FindEntrance(data.checkpointId, logWarning: false);
                if (entrance != null)
                    return entrance.transform.position;
            }

            var checkpointPosition = data.checkpointPosition.ToVector3();
            return checkpointPosition;
        }

        private ItemData ResolveItemById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            if (itemLookup.TryGetValue(itemId, out var itemData))
                return itemData;

            // Fallback for auto-bootstrapped managers where itemCatalog was not manually assigned.
            if (!runtimeCatalogScanned)
            {
                runtimeCatalogScanned = true;
                AddRuntimeLoadedItemsToLookup();

                if (itemLookup.TryGetValue(itemId, out itemData))
                    return itemData;
            }

            if (warnedMissingItemIds.Add(itemId))
                Debug.LogWarning($"[SaveManager] Could not resolve ItemData for saveId '{itemId}'. Item was skipped during load.");

            return null;
        }

        private void RebuildItemLookup()
        {
            itemLookup.Clear();
            warnedMissingItemIds.Clear();
            runtimeCatalogScanned = false;

            for (var i = 0; i < itemCatalog.Count; i++)
            {
                var item = itemCatalog[i];
                AddItemToLookup(item);
            }

            AddRuntimeLoadedItemsToLookup();
            runtimeCatalogScanned = true;

            if (verboseLogging)
                Debug.Log($"[SaveManager] Item lookup rebuilt with {itemLookup.Count} entries.");
        }

        private void AddRuntimeLoadedItemsToLookup()
        {
            var loadedItems = Resources.FindObjectsOfTypeAll<ItemData>();
            for (var i = 0; i < loadedItems.Length; i++)
                AddItemToLookup(loadedItems[i]);
        }

        private void AddItemToLookup(ItemData item)
        {
            if (item == null)
                return;

            var key = item.SaveId;
            if (string.IsNullOrWhiteSpace(key) || itemLookup.ContainsKey(key))
                return;

            itemLookup[key] = item;
        }
    }
}
