using System.Collections.Generic;
using System.Text;
using Underbrew.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PersistenceIdAuditTool
{
    [MenuItem("Tools/Underbrew/Audit Persistence IDs")]
    public static void AuditPersistenceIds()
    {
        var report = new AuditReport();
        var originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            AuditItemData(report);
            AuditBrewingRecipes(report);
            AuditPrefabs(report);
            AuditScenes(report);
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        report.LogToConsole();
    }

    private static void AuditItemData(AuditReport report)
    {
        var guids = AssetDatabase.FindAssets("t:ItemData");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null)
                continue;

            var serialized = new SerializedObject(item);
            var rawSaveId = serialized.FindProperty("saveId")?.stringValue ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rawSaveId))
                report.AddWarning($"Blank item saveId: {path} -> fallback '{item.SaveId}'");

            report.TrackDuplicate("ItemData.saveId", item.SaveId, path);
        }
    }

    private static void AuditBrewingRecipes(AuditReport report)
    {
        var guids = AssetDatabase.FindAssets("t:BrewingRecipe");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var recipe = AssetDatabase.LoadAssetAtPath<BrewingRecipe>(path);
            if (recipe == null)
                continue;

            var serialized = new SerializedObject(recipe);
            var rawSaveId = serialized.FindProperty("recipeSaveId")?.stringValue ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rawSaveId))
                report.AddWarning($"Blank recipeSaveId: {path} -> fallback '{recipe.RecipeSaveId}'");

            report.TrackDuplicate("BrewingRecipe.recipeSaveId", recipe.RecipeSaveId, path);
        }
    }

    private static void AuditPrefabs(AuditReport report)
    {
        var guids = AssetDatabase.FindAssets("t:Prefab");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabRoot == null)
                continue;

            AuditHierarchy(prefabRoot, path, report);
        }
    }

    private static void AuditScenes(AuditReport report)
    {
        var scenePaths = AssetDatabase.FindAssets("t:Scene");
        for (var i = 0; i < scenePaths.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(scenePaths[i]);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    AuditHierarchy(roots[rootIndex], path, report);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }

    private static void AuditHierarchy(GameObject root, string assetPath, AuditReport report)
    {
        AuditComponents(root.GetComponentsInChildren<SceneEntrance>(true), assetPath, report);
        AuditComponents(root.GetComponentsInChildren<BenchCheckpoint>(true), assetPath, report);
        AuditComponents(root.GetComponentsInChildren<Pickupable>(true), assetPath, report);
        AuditComponents(root.GetComponentsInChildren<OneShotPickupable>(true), assetPath, report);
        AuditComponents(root.GetComponentsInChildren<RecipeScrapPickup>(true), assetPath, report);
    }

    private static void AuditComponents(SceneEntrance[] entrances, string assetPath, AuditReport report)
    {
        for (var i = 0; i < entrances.Length; i++)
        {
            var entrance = entrances[i];
            if (entrance == null)
                continue;

            var id = entrance.entranceID ?? string.Empty;
            var context = $"{assetPath} :: {GetTransformPath(entrance.transform)}";

            if (string.IsNullOrWhiteSpace(id))
                report.AddError($"Blank entranceID: {context}");
            else
                report.TrackDuplicate("SceneEntrance.entranceID", id, context);
        }
    }

    private static void AuditComponents(BenchCheckpoint[] checkpoints, string assetPath, AuditReport report)
    {
        for (var i = 0; i < checkpoints.Length; i++)
        {
            var checkpoint = checkpoints[i];
            if (checkpoint == null)
                continue;

            var serialized = new SerializedObject(checkpoint);
            var id = serialized.FindProperty("checkpointId")?.stringValue ?? string.Empty;
            var context = $"{assetPath} :: {GetTransformPath(checkpoint.transform)}";

            if (string.IsNullOrWhiteSpace(id))
                report.AddError($"Blank checkpointId: {context}");
            else
                report.TrackDuplicate("BenchCheckpoint.checkpointId", id, context);
        }
    }

    private static void AuditComponents(Pickupable[] pickupables, string assetPath, AuditReport report)
    {
        for (var i = 0; i < pickupables.Length; i++)
        {
            var pickupable = pickupables[i];
            if (pickupable == null)
                continue;

            var serialized = new SerializedObject(pickupable);
            var useTracking = serialized.FindProperty("useResourceRespawnTracking")?.boolValue ?? false;
            if (!useTracking)
                continue;

            var id = serialized.FindProperty("resourceNodeId")?.stringValue ?? string.Empty;
            var context = $"{assetPath} :: {GetTransformPath(pickupable.transform)}";

            if (string.IsNullOrWhiteSpace(id))
                report.AddError($"Blank resourceNodeId with respawn tracking enabled: {context}");
            else
                report.TrackDuplicate("Pickupable.resourceNodeId", id, context);
        }
    }

    private static void AuditComponents(OneShotPickupable[] pickups, string assetPath, AuditReport report)
    {
        for (var i = 0; i < pickups.Length; i++)
        {
            var pickup = pickups[i];
            if (pickup == null)
                continue;

            var serialized = new SerializedObject(pickup);
            var consumedFlagKey = serialized.FindProperty("consumedFlagKey")?.stringValue ?? string.Empty;
            var context = $"{assetPath} :: {GetTransformPath(pickup.transform)}";

            if (string.IsNullOrWhiteSpace(consumedFlagKey))
                report.AddError($"Blank OneShotPickupable.consumedFlagKey: {context}");
            else
                report.TrackDuplicate("OneShotPickupable.consumedFlagKey", consumedFlagKey, context);
        }
    }

    private static void AuditComponents(RecipeScrapPickup[] scraps, string assetPath, AuditReport report)
    {
        for (var i = 0; i < scraps.Length; i++)
        {
            var scrap = scraps[i];
            if (scrap == null)
                continue;

            var serialized = new SerializedObject(scrap);
            var consumedFlagKey = serialized.FindProperty("consumedFlagKey")?.stringValue ?? string.Empty;
            var context = $"{assetPath} :: {GetTransformPath(scrap.transform)}";

            if (string.IsNullOrWhiteSpace(consumedFlagKey))
                report.AddError($"Blank RecipeScrapPickup.consumedFlagKey: {context}");
            else
                report.TrackDuplicate("RecipeScrapPickup.consumedFlagKey", consumedFlagKey, context);
        }
    }

    private static string GetTransformPath(Transform transform)
    {
        if (transform == null)
            return "<missing>";

        var parts = new Stack<string>();
        var current = transform;
        while (current != null)
        {
            parts.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", parts.ToArray());
    }

    private sealed class AuditReport
    {
        private readonly Dictionary<string, Dictionary<string, List<string>>> seenIdsByDomain = new();
        private readonly List<string> warnings = new();
        private readonly List<string> errors = new();

        public void AddWarning(string message)
        {
            warnings.Add(message);
        }

        public void AddError(string message)
        {
            errors.Add(message);
        }

        public void TrackDuplicate(string domain, string id, string location)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            if (!seenIdsByDomain.TryGetValue(domain, out var idsByValue))
            {
                idsByValue = new Dictionary<string, List<string>>();
                seenIdsByDomain[domain] = idsByValue;
            }

            if (!idsByValue.TryGetValue(id, out var locations))
            {
                locations = new List<string>();
                idsByValue[id] = locations;
            }

            locations.Add(location);
        }

        public void LogToConsole()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PersistenceIdAudit] Audit complete.");

            for (var i = 0; i < warnings.Count; i++)
                sb.AppendLine($"[Warning] {warnings[i]}");

            for (var i = 0; i < errors.Count; i++)
                sb.AppendLine($"[Error] {errors[i]}");

            foreach (var domainEntry in seenIdsByDomain)
            {
                foreach (var idEntry in domainEntry.Value)
                {
                    if (idEntry.Value.Count < 2)
                        continue;

                    sb.AppendLine($"[Error] Duplicate {domainEntry.Key} '{idEntry.Key}':");
                    for (var i = 0; i < idEntry.Value.Count; i++)
                        sb.AppendLine($"  - {idEntry.Value[i]}");
                }
            }

            var hasErrors = sb.ToString().Contains("[Error]");
            if (hasErrors)
                Debug.LogError(sb.ToString());
            else
                Debug.Log(sb.ToString());
        }
    }
}
