using System;
using System.Collections.Generic;
using UnityEngine;

public class PotionRecipeDiscoverySystem : MonoBehaviour
{
    public static PotionRecipeDiscoverySystem Instance { get; private set; }

    [SerializeField] private List<string> discoveredRecipeIds = new();

    private readonly HashSet<string> discoveredRecipeLookup = new(StringComparer.Ordinal);

    public event Action OnDiscoveryChanged;

    public IReadOnlyCollection<string> DiscoveredRecipeIds => discoveredRecipeIds;

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

        RebuildLookupFromSerializedState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool Discover(BrewingRecipe recipe)
    {
        if (recipe == null)
            return false;

        return DiscoverRecipeId(recipe.RecipeSaveId);
    }

    public bool DiscoverRecipeId(string recipeId)
    {
        if (string.IsNullOrWhiteSpace(recipeId))
            return false;

        if (!discoveredRecipeLookup.Add(recipeId))
            return false;

        discoveredRecipeIds.Add(recipeId);
        OnDiscoveryChanged?.Invoke();
        return true;
    }

    public bool IsDiscovered(BrewingRecipe recipe)
    {
        if (recipe == null)
            return false;

        return IsDiscoveredRecipeId(recipe.RecipeSaveId);
    }

    public bool IsDiscoveredRecipeId(string recipeId)
    {
        if (string.IsNullOrWhiteSpace(recipeId))
            return false;

        return discoveredRecipeLookup.Contains(recipeId);
    }

    public List<string> CreateSaveSnapshot()
    {
        return new List<string>(discoveredRecipeIds);
    }

    public void LoadFromSaveSnapshot(IReadOnlyList<string> snapshot)
    {
        discoveredRecipeIds.Clear();
        discoveredRecipeLookup.Clear();

        if (snapshot != null)
        {
            for (var i = 0; i < snapshot.Count; i++)
            {
                var recipeId = snapshot[i];
                if (string.IsNullOrWhiteSpace(recipeId))
                    continue;

                if (!discoveredRecipeLookup.Add(recipeId))
                    continue;

                discoveredRecipeIds.Add(recipeId);
            }
        }

        OnDiscoveryChanged?.Invoke();
    }

    public void Clear()
    {
        if (discoveredRecipeIds.Count == 0)
            return;

        discoveredRecipeIds.Clear();
        discoveredRecipeLookup.Clear();
        OnDiscoveryChanged?.Invoke();
    }

    private void RebuildLookupFromSerializedState()
    {
        discoveredRecipeLookup.Clear();

        if (discoveredRecipeIds == null)
        {
            discoveredRecipeIds = new List<string>();
            return;
        }

        for (var i = discoveredRecipeIds.Count - 1; i >= 0; i--)
        {
            var recipeId = discoveredRecipeIds[i];
            if (string.IsNullOrWhiteSpace(recipeId) || !discoveredRecipeLookup.Add(recipeId))
                discoveredRecipeIds.RemoveAt(i);
        }
    }
}
