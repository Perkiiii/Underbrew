using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JournalItemsPageUI : JournalTabPageUI
{
    private const int DefaultVisibleSlotCount = 20;

    public override bool CanRenderPage => true;

    [Header("Catalog")]
    [SerializeField] private JournalItemCatalogAsset itemCatalogAsset;
    [SerializeField] private Transform itemGridContainer;
    [SerializeField] private JournalItemSlotUI itemSlotPrefab;
    [SerializeField] private GameObject unknownItemVisual;
    [SerializeField] private int visibleSlotCount = DefaultVisibleSlotCount;
    [SerializeField] private bool includeRawResources = true;
    [SerializeField] private bool includeCraftingResources = true;
    [SerializeField] private bool includePotions;

    [Header("Detail Page")]
    [SerializeField] private Image detailIconImage;
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_Text emptyStateText;

    [Header("Fallback Copy")]
    [SerializeField] private string undiscoveredItemTitle = "???";
    [SerializeField, TextArea(2, 5)] private string undiscoveredItemDescription = "You have not discovered this item yet.";
    [SerializeField] private string emptyJournalMessage = "No journal entries yet.";

    private readonly List<JournalItemSlotUI> spawnedSlots = new();
    private JournalDiscoverySystem discoverySystem;
    private ItemData selectedItem;
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

        discoverySystem = JournalDiscoverySystem.Instance;
        if (discoverySystem == null)
            discoverySystem = FindFirstObjectByType<JournalDiscoverySystem>(FindObjectsInactive.Include);
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

        var orderedItems = GetOrderedItems();
        var visibleEntries = BuildVisibleEntries(orderedItems);
        if (visibleEntries.Count == 0)
        {
            SetEmptyState(emptyJournalMessage);
            return;
        }

        if (selectedItem == null || !orderedItems.Contains(selectedItem))
            selectedItem = GetDefaultSelection(orderedItems);

        for (var i = 0; i < visibleEntries.Count; i++)
        {
            var item = visibleEntries[i];
            var slot = Instantiate(itemSlotPrefab, itemGridContainer);
            slot.Configure(item, IsDiscovered(item), item == selectedItem, HandleSlotClicked);
            spawnedSlots.Add(slot);
        }

        SetEmptyState(string.Empty);
    }

    private void UpdateDetailPanel()
    {
        var hasSelection = selectedItem != null;
        var isDiscovered = hasSelection && IsDiscovered(selectedItem);

        if (!hasSelection)
        {
            SetEmptyState(emptyJournalMessage);
            SetUnknownItemVisualVisible(false);
            SetDetailVisible(false);
            return;
        }

        if (emptyStateText != null)
            emptyStateText.text = string.Empty;

        SetDetailVisible(true);

        if (!isDiscovered)
        {
            SetUnknownItemVisualVisible(true);

            if (detailIconImage != null)
                detailIconImage.gameObject.SetActive(false);

            if (detailTitleText != null)
                detailTitleText.text = undiscoveredItemTitle;

            if (detailDescriptionText != null)
                detailDescriptionText.text = undiscoveredItemDescription;

            return;
        }

        SetUnknownItemVisualVisible(false);

        if (detailIconImage != null)
        {
            detailIconImage.gameObject.SetActive(true);
            detailIconImage.sprite = selectedItem.JournalLargeIcon != null ? selectedItem.JournalLargeIcon : selectedItem.Icon;
            detailIconImage.enabled = detailIconImage.sprite != null;
        }

        if (detailTitleText != null)
            detailTitleText.text = selectedItem.ItemName;

        if (detailDescriptionText != null)
            detailDescriptionText.text = selectedItem.JournalEntry;
    }

    private void HandleSlotClicked(JournalItemSlotUI slot)
    {
        if (slot == null || slot.ItemData == null || slot.IsPlaceholder)
            return;

        selectedItem = slot.ItemData;
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

            slot.SetSelected(slot.ItemData == selectedItem);
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
    }

    private List<ItemData> GetOrderedItems()
    {
        var sourceItems = itemCatalogAsset != null
            ? GetItemsFromAsset()
            : JournalItemCatalog.GetItems(ShouldIncludeItem);

        sourceItems.Sort((left, right) =>
        {
            var sortOrder = left.JournalSortOrder.CompareTo(right.JournalSortOrder);
            if (sortOrder != 0)
                return sortOrder;

            return string.Compare(left.ItemName, right.ItemName, StringComparison.OrdinalIgnoreCase);
        });

        return sourceItems;
    }

    private List<ItemData> BuildVisibleEntries(List<ItemData> orderedItems)
    {
        var targetCount = Mathf.Max(0, visibleSlotCount);
        if (targetCount == 0)
            return new List<ItemData>();

        var visibleEntries = new List<ItemData>(targetCount);
        if (orderedItems != null)
        {
            for (var i = 0; i < orderedItems.Count && visibleEntries.Count < targetCount; i++)
            {
                var item = orderedItems[i];
                if (item == null)
                    continue;

                visibleEntries.Add(item);
            }
        }

        while (visibleEntries.Count < targetCount)
            visibleEntries.Add(null);

        return visibleEntries;
    }

    private bool ShouldIncludeItem(ItemData item)
    {
        if (item == null)
            return false;

        return item.Type switch
        {
            ItemType.RawResource => includeRawResources,
            ItemType.CraftingResource => includeCraftingResources,
            ItemType.Potion => includePotions,
            _ => false
        };
    }

    private List<ItemData> GetItemsFromAsset()
    {
        var items = new List<ItemData>();
        var seen = new HashSet<ItemData>();

        var source = itemCatalogAsset.Items;
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            if (item == null || !seen.Add(item) || !ShouldIncludeItem(item))
                continue;

            items.Add(item);
        }

        return items;
    }

    private ItemData GetDefaultSelection(List<ItemData> orderedItems)
    {
        if (orderedItems == null || orderedItems.Count == 0)
            return null;

        for (var i = 0; i < orderedItems.Count; i++)
        {
            var item = orderedItems[i];
            if (item != null && IsDiscovered(item))
                return item;
        }

        return orderedItems[0];
    }

    private bool IsDiscovered(ItemData item)
    {
        if (item == null)
            return false;

        EnsureDiscoverySystem();
        return discoverySystem != null && discoverySystem.IsDiscovered(item);
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
}
