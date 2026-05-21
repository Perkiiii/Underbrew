using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JournalPotionsPageUI : JournalTabPageUI
{
    private const int DefaultVisibleSlotCount = 20;

    [Header("Catalog")]
    [SerializeField] private PotionJournalCatalogAsset potionCatalogAsset;
    [SerializeField] private Transform itemGridContainer;
    [SerializeField] private JournalItemSlotUI itemSlotPrefab;
    [SerializeField] private GameObject unknownItemVisual;
    [SerializeField] private int visibleSlotCount = DefaultVisibleSlotCount;

    [Header("Detail Page")]
    [SerializeField] private Image detailIconImage;
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_Text emptyStateText;

    [Header("Ingredients")]
    [SerializeField] private GameObject ingredientsRoot;
    [SerializeField] private Image ingredientAIconImage;
    [SerializeField] private Image ingredientBIconImage;
    [SerializeField] private TMP_Text plusText;
    [SerializeField] private TMP_Text equalsText;

    [Header("Fallback Copy")]
    [SerializeField] private string undiscoveredPotionTitle = "???";
    [SerializeField, TextArea(2, 5)] private string undiscoveredPotionDescription = "You have not discovered this recipe yet.";
    [SerializeField] private string emptyJournalMessage = "No potion entries yet.";

    private readonly List<JournalItemSlotUI> spawnedSlots = new();
    private readonly Dictionary<JournalItemSlotUI, BrewingRecipe> slotRecipeLookup = new();
    private PotionRecipeDiscoverySystem discoverySystem;
    private BrewingRecipe selectedRecipe;
    private bool isSubscribed;

    protected override void OnShown()
    {
        EnsureDiscoverySystem();
        SubscribeToDiscoverySystem();
    }

    protected override void OnHidden()
    {
        UnsubscribeFromDiscoverySystem();
    }

    protected override void OnRefreshPage()
    {
        EnsureDiscoverySystem();
        BuildGrid();
        UpdateDetailPanel();
    }

    private void OnDisable()
    {
        UnsubscribeFromDiscoverySystem();
    }

    private void HandleDiscoveryChanged()
    {
        if (!isActiveAndEnabled)
            return;

        RefreshPage();
    }

    private void EnsureDiscoverySystem()
    {
        if (discoverySystem != null)
            return;

        discoverySystem = PotionRecipeDiscoverySystem.Instance;
        if (discoverySystem == null)
            discoverySystem = FindFirstObjectByType<PotionRecipeDiscoverySystem>(FindObjectsInactive.Include);
    }

    private void SubscribeToDiscoverySystem()
    {
        if (isSubscribed || discoverySystem == null)
            return;

        discoverySystem.OnDiscoveryChanged += HandleDiscoveryChanged;
        isSubscribed = true;
    }

    private void UnsubscribeFromDiscoverySystem()
    {
        if (!isSubscribed)
            return;

        if (discoverySystem != null)
            discoverySystem.OnDiscoveryChanged -= HandleDiscoveryChanged;

        isSubscribed = false;
    }

    private void BuildGrid()
    {
        ClearSpawnedSlots();

        if (itemGridContainer == null || itemSlotPrefab == null)
        {
            SetEmptyState(emptyJournalMessage);
            return;
        }

        var orderedRecipes = GetOrderedRecipes();
        var visibleEntries = BuildVisibleEntries(orderedRecipes);
        if (visibleEntries.Count == 0)
        {
            SetEmptyState(emptyJournalMessage);
            return;
        }

        if (selectedRecipe == null || !orderedRecipes.Contains(selectedRecipe))
            selectedRecipe = GetDefaultSelection(orderedRecipes);

        for (var i = 0; i < visibleEntries.Count; i++)
        {
            var recipe = visibleEntries[i];
            var outputItem = recipe != null ? recipe.OutputItem : null;
            var slot = Instantiate(itemSlotPrefab, itemGridContainer);
            slot.Configure(outputItem, IsDiscovered(recipe), recipe == selectedRecipe, HandleSlotClicked);
            spawnedSlots.Add(slot);
            slotRecipeLookup[slot] = recipe;
        }

        SetEmptyState(string.Empty);
    }

    private void UpdateDetailPanel()
    {
        var hasSelection = selectedRecipe != null;
        var isDiscovered = hasSelection && IsDiscovered(selectedRecipe);
        var outputItem = hasSelection ? selectedRecipe.OutputItem : null;

        if (!hasSelection)
        {
            if (emptyStateText != null)
                emptyStateText.text = string.Empty;

            SetUnknownItemVisualVisible(true);
            SetDetailVisible(true);
            SetIngredientsVisible(false);

            if (detailIconImage != null)
                detailIconImage.gameObject.SetActive(false);

            if (detailTitleText != null)
                detailTitleText.text = undiscoveredPotionTitle;

            if (detailDescriptionText != null)
                detailDescriptionText.text = undiscoveredPotionDescription;

            return;
        }

        if (emptyStateText != null)
            emptyStateText.text = string.Empty;

        SetDetailVisible(true);

        if (!isDiscovered)
        {
            SetUnknownItemVisualVisible(true);
            SetIngredientsVisible(false);

            if (detailIconImage != null)
                detailIconImage.gameObject.SetActive(false);

            if (detailTitleText != null)
                detailTitleText.text = undiscoveredPotionTitle;

            if (detailDescriptionText != null)
                detailDescriptionText.text = undiscoveredPotionDescription;

            return;
        }

        SetUnknownItemVisualVisible(false);
        SetIngredientsVisible(true);

        if (detailIconImage != null)
        {
            detailIconImage.gameObject.SetActive(true);
            detailIconImage.sprite = outputItem != null ? outputItem.JournalLargeIcon : null;
            detailIconImage.enabled = detailIconImage.sprite != null;
        }

        if (detailTitleText != null)
            detailTitleText.text = outputItem != null ? outputItem.ItemName : selectedRecipe.RecipeName;

        if (detailDescriptionText != null)
            detailDescriptionText.text = outputItem != null ? outputItem.JournalEntry : string.Empty;

        BindIngredient(ingredientAIconImage, GetIngredientItem(selectedRecipe, 0));
        BindIngredient(ingredientBIconImage, GetIngredientItem(selectedRecipe, 1));
    }

    private void HandleSlotClicked(JournalItemSlotUI slot)
    {
        if (slot == null || slot.IsPlaceholder)
            return;

        if (!slotRecipeLookup.TryGetValue(slot, out var recipe) || recipe == null)
            return;

        selectedRecipe = recipe;
        RefreshSelectionStates();
        UpdateDetailPanel();
    }

    private void RefreshSelectionStates()
    {
        for (var i = 0; i < spawnedSlots.Count; i++)
        {
            var slot = spawnedSlots[i];
            if (slot == null)
                continue;

            slot.SetSelected(slotRecipeLookup.TryGetValue(slot, out var recipe) && recipe == selectedRecipe);
        }
    }

    private void ClearSpawnedSlots()
    {
        for (var i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] != null)
                Destroy(spawnedSlots[i].gameObject);
        }

        spawnedSlots.Clear();
        slotRecipeLookup.Clear();
    }

    private List<BrewingRecipe> GetOrderedRecipes()
    {
        var orderedRecipes = new List<BrewingRecipe>();
        var seen = new HashSet<BrewingRecipe>();

        if (potionCatalogAsset == null)
            return orderedRecipes;

        var source = potionCatalogAsset.Recipes;
        for (var i = 0; i < source.Count; i++)
        {
            var recipe = source[i];
            if (recipe == null || !seen.Add(recipe))
                continue;

            orderedRecipes.Add(recipe);
        }

        orderedRecipes.Sort(CompareRecipes);
        return orderedRecipes;
    }

    private List<BrewingRecipe> BuildVisibleEntries(List<BrewingRecipe> orderedRecipes)
    {
        var targetCount = Mathf.Max(0, visibleSlotCount);
        if (targetCount == 0)
            return new List<BrewingRecipe>();

        var visibleEntries = new List<BrewingRecipe>(targetCount);
        if (orderedRecipes != null)
        {
            for (var i = 0; i < orderedRecipes.Count && visibleEntries.Count < targetCount; i++)
            {
                var recipe = orderedRecipes[i];
                if (recipe == null)
                    continue;

                visibleEntries.Add(recipe);
            }
        }

        while (visibleEntries.Count < targetCount)
            visibleEntries.Add(null);

        return visibleEntries;
    }

    private BrewingRecipe GetDefaultSelection(List<BrewingRecipe> orderedRecipes)
    {
        if (orderedRecipes == null || orderedRecipes.Count == 0)
            return null;

        for (var i = 0; i < orderedRecipes.Count; i++)
        {
            var recipe = orderedRecipes[i];
            if (recipe != null && IsDiscovered(recipe))
                return recipe;
        }

        return orderedRecipes[0];
    }

    private bool IsDiscovered(BrewingRecipe recipe)
    {
        if (recipe == null)
            return false;

        EnsureDiscoverySystem();
        return discoverySystem != null && discoverySystem.IsDiscovered(recipe);
    }

    private static int CompareRecipes(BrewingRecipe left, BrewingRecipe right)
    {
        var leftItem = left != null ? left.OutputItem : null;
        var rightItem = right != null ? right.OutputItem : null;

        if (leftItem != null && rightItem != null)
        {
            var sortOrder = leftItem.JournalSortOrder.CompareTo(rightItem.JournalSortOrder);
            if (sortOrder != 0)
                return sortOrder;

            return string.Compare(leftItem.ItemName, rightItem.ItemName, StringComparison.OrdinalIgnoreCase);
        }

        if (leftItem != null)
            return -1;

        if (rightItem != null)
            return 1;

        var leftName = left != null ? left.RecipeName : string.Empty;
        var rightName = right != null ? right.RecipeName : string.Empty;
        return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
    }

    private static ItemData GetIngredientItem(BrewingRecipe recipe, int index)
    {
        if (recipe == null)
            return null;

        var ingredients = recipe.Ingredients;
        if (ingredients == null || index < 0 || index >= ingredients.Length)
            return null;

        return ingredients[index].Item;
    }

    private void BindIngredient(Image iconImage, ItemData item)
    {
        if (iconImage != null)
        {
            iconImage.sprite = item != null ? item.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }
    }

    private void SetEmptyState(string message)
    {
        if (emptyStateText != null)
            emptyStateText.text = message;
    }

    private void SetUnknownItemVisualVisible(bool visible)
    {
        if (unknownItemVisual != null)
            unknownItemVisual.SetActive(visible);
    }

    private void SetDetailVisible(bool visible)
    {
        if (detailIconImage != null)
            detailIconImage.gameObject.SetActive(visible);

        if (detailTitleText != null)
            detailTitleText.gameObject.SetActive(visible);

        if (detailDescriptionText != null)
            detailDescriptionText.gameObject.SetActive(visible);

        if (!visible)
            SetUnknownItemVisualVisible(false);
    }

    private void SetIngredientsVisible(bool visible)
    {
        if (ingredientsRoot != null)
        {
            ingredientsRoot.SetActive(visible);
            return;
        }

        SetIngredientParentVisible(ingredientAIconImage, visible);
        SetIngredientParentVisible(ingredientBIconImage, visible);

        if (plusText != null)
            plusText.gameObject.SetActive(visible);

        if (equalsText != null)
            equalsText.gameObject.SetActive(visible);
    }

    private static void SetIngredientParentVisible(Image iconImage, bool visible)
    {
        if (iconImage == null)
            return;

        var target = iconImage.transform.parent != null
            ? iconImage.transform.parent.gameObject
            : iconImage.gameObject;

        target.SetActive(visible);
    }
}
