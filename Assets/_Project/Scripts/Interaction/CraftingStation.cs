using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CraftingStation : MonoBehaviour, IInteractable
{
    [SerializeField] private string stationDisplayName;
    [SerializeField] private string promptActionText = "Press E to use";
    [SerializeField] private CraftingStationType stationType;
    [SerializeField] private bool useProcessingUI;
    [SerializeField] private bool useBrewingUI;
    [SerializeField] private ProcessingRecipe[] processingRecipes;
    [SerializeField] private BrewingRecipe[] brewingRecipes;
    [SerializeField] private ProcessingStationUI processingStationUI;
    [SerializeField] private BrewingStationUI brewingStationUI;

    public CraftingStationType StationType => stationType;
    public ProcessingRecipe[] ProcessingRecipes => processingRecipes;
    public BrewingRecipe[] BrewingRecipes => brewingRecipes;
    public string StationDisplayName => GetDisplayName();

    public string PromptText => $"{promptActionText} {GetDisplayName()}";

    public void Interact()
    {
        if (useProcessingUI)
        {
            if (processingStationUI == null)
                processingStationUI = ResolveBestProcessingUI();

            if (processingStationUI != null)
            {
                processingStationUI.Open(this);
                return;
            }

            Debug.LogWarning("[CraftingStation] ProcessingStationUI not found in scene.");
        }

        if (useBrewingUI)
        {
            if (brewingStationUI == null)
                brewingStationUI = ResolveBestBrewingUI();

            if (brewingStationUI != null)
            {
                brewingStationUI.Open(this);
                return;
            }

            Debug.LogWarning("[CraftingStation] BrewingStationUI not found in scene.");
        }

        Debug.LogWarning("[CraftingStation] Station has no enabled specialized UI path (processing or brewing). Interact did nothing.");
    }

    public void CancelInteract()
    {
    }

    public List<ICraftingRecipe> GetAvailableRecipes()
    {
        var results = new List<ICraftingRecipe>();

        AddMatchingRecipes(processingRecipes, results);
        AddMatchingRecipes(brewingRecipes, results);

        return results;
    }

    private void AddMatchingRecipes<T>(T[] recipes, List<ICraftingRecipe> results) where T : ScriptableObject, ICraftingRecipe
    {
        if (recipes == null)
            return;

        for (var i = 0; i < recipes.Length; i++)
        {
            var recipe = recipes[i];
            if (recipe == null)
                continue;

            if (recipe.StationType != stationType)
                continue;

            results.Add(recipe);
        }
    }

    private string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(stationDisplayName))
            return stationDisplayName;

        return stationType.ToString();
    }

    private static BrewingStationUI ResolveBestBrewingUI()
    {
        var candidates = FindObjectsByType<BrewingStationUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return ResolveBestUiCandidate(candidates);
    }

    private static ProcessingStationUI ResolveBestProcessingUI()
    {
        var candidates = FindObjectsByType<ProcessingStationUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return ResolveBestUiCandidate(candidates);
    }

    private static T ResolveBestUiCandidate<T>(T[] candidates) where T : MonoBehaviour
    {
        if (candidates == null || candidates.Length == 0)
            return null;

        var activeScene = SceneManager.GetActiveScene();

        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject.activeInHierarchy && candidate.gameObject.scene.name == "DontDestroyOnLoad")
                return candidate;
        }

        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject.activeInHierarchy && candidate.gameObject.scene == activeScene)
                return candidate;
        }

        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject.scene.name == "DontDestroyOnLoad")
                return candidate;
        }

        return candidates[0];
    }
}
