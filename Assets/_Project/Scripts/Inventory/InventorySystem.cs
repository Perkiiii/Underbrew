using System;
using System.Collections.Generic;
using UnityEngine;
using Underbrew.Core;

public class InventorySystem : MonoBehaviour
{
    [SerializeField] private int maxItemSlots = 12;

    private readonly List<InventorySlotData> slots = new();
    private JournalDiscoverySystem journalDiscoverySystem;

    public IReadOnlyList<InventorySlotData> Slots => slots;
    public int MaxItemSlots => maxItemSlots;

    public event Action<ItemData, int> OnInventoryChanged;

    private void Awake()
    {
        EnsureSlotStorage();
        ResolveJournalDiscoverySystem();
    }

    public bool Add(ItemData item, int quantity = 1, bool recordJournalDiscovery = true)
    {
        EnsureSlotStorage();

        if (item == null || quantity <= 0)
            return false;

        if (!CanAdd(item, quantity))
            return false;

        var existingSlotIndex = FindSlotIndex(item);
        if (existingSlotIndex >= 0)
        {
            var existingSlot = slots[existingSlotIndex];
            existingSlot.Set(item, existingSlot.Quantity + quantity);
            OnInventoryChanged?.Invoke(item, existingSlot.Quantity);
            TryDiscoverJournalItem(item, recordJournalDiscovery);
            return true;
        }

        var emptySlotIndex = FindFirstEmptySlotIndex();
        if (emptySlotIndex < 0)
            return false;

        slots[emptySlotIndex].Set(item, quantity);
        OnInventoryChanged?.Invoke(item, quantity);
        TryDiscoverJournalItem(item, recordJournalDiscovery);
        return true;
    }

    public bool Remove(ItemData item, int quantity = 1)
    {
        EnsureSlotStorage();

        if (item == null || quantity <= 0)
            return false;

        var slotIndex = FindSlotIndex(item);
        if (slotIndex < 0)
            return false;

        var slot = slots[slotIndex];
        var currentAmount = slot.Quantity;
        if (currentAmount < quantity)
            return false;

        currentAmount -= quantity;

        if (currentAmount <= 0)
        {
            slot.Clear();
            OnInventoryChanged?.Invoke(item, 0);
            return true;
        }

        slot.Set(item, currentAmount);
        OnInventoryChanged?.Invoke(item, currentAmount);
        return true;
    }

    public bool HasItem(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0)
            return false;

        var slotIndex = FindSlotIndex(item);
        if (slotIndex < 0)
            return false;

        return slots[slotIndex].Quantity >= quantity;
    }

    public int GetQuantity(ItemData item)
    {
        EnsureSlotStorage();

        if (item == null)
            return 0;

        var slotIndex = FindSlotIndex(item);
        return slotIndex >= 0 ? slots[slotIndex].Quantity : 0;
    }

    public bool CanAdd(ItemData item, int quantity = 1)
    {
        EnsureSlotStorage();

        if (item == null || quantity <= 0)
            return false;

        if (FindSlotIndex(item) >= 0)
            return true;

        return FindFirstEmptySlotIndex() >= 0;
    }

    public bool CanAddAfterConsuming(ItemData item, RecipeRequirement[] requirements, int quantity = 1)
    {
        EnsureSlotStorage();

        if (item == null || quantity <= 0)
            return false;

        if (FindSlotIndex(item) >= 0)
            return true;

        var projectedOccupiedSlots = GetOccupiedSlotCount();

        if (requirements == null || requirements.Length == 0)
            return projectedOccupiedSlots < Mathf.Max(0, maxItemSlots);

        var quantitiesToConsume = new Dictionary<ItemData, int>();

        for (var i = 0; i < requirements.Length; i++)
        {
            var requirement = requirements[i];
            if (requirement.Item == null || requirement.Quantity <= 0)
                continue;

            if (quantitiesToConsume.ContainsKey(requirement.Item))
                quantitiesToConsume[requirement.Item] += requirement.Quantity;
            else
                quantitiesToConsume[requirement.Item] = requirement.Quantity;
        }

        foreach (var requirementEntry in quantitiesToConsume)
        {
            var slotIndex = FindSlotIndex(requirementEntry.Key);
            if (slotIndex < 0)
                continue;

            if (slots[slotIndex].Quantity <= requirementEntry.Value)
                projectedOccupiedSlots--;
        }

        return projectedOccupiedSlots < Mathf.Max(0, maxItemSlots);
    }

    public bool MoveSlotItem(int fromIndex, int toIndex)
    {
        EnsureSlotStorage();

        if (!IsValidSlotIndex(fromIndex) || !IsValidSlotIndex(toIndex) || fromIndex == toIndex)
            return false;

        var fromSlot = slots[fromIndex];
        var toSlot = slots[toIndex];

        var fromItem = fromSlot.Item;
        var fromQuantity = fromSlot.Quantity;
        var toItem = toSlot.Item;
        var toQuantity = toSlot.Quantity;

        if (fromItem == null || fromQuantity <= 0)
            return false;

        fromSlot.Set(toItem, toQuantity);
        if (fromSlot.IsEmpty)
            fromSlot.Clear();

        toSlot.Set(fromItem, fromQuantity);
        if (toSlot.IsEmpty)
            toSlot.Clear();

        OnInventoryChanged?.Invoke(null, 0);
        AudioManager.Instance?.PlayUi(AudioCueId.UIBackpackMove);
        return true;
    }

    public bool CanFulfillRequirements(RecipeRequirement[] requirements)
    {
        if (requirements == null || requirements.Length == 0)
            return false;

        for (var i = 0; i < requirements.Length; i++)
        {
            var requirement = requirements[i];
            if (!HasItem(requirement.Item, requirement.Quantity))
                return false;
        }

        return true;
    }

    public bool ConsumeRequirements(RecipeRequirement[] requirements)
    {
        if (!CanFulfillRequirements(requirements))
            return false;

        for (var i = 0; i < requirements.Length; i++)
        {
            var requirement = requirements[i];
            Remove(requirement.Item, requirement.Quantity);
        }

        return true;
    }

    public bool CanProcess(ProcessingRecipe recipe)
    {
        if (recipe == null)
            return false;

        if (recipe.OutputItem == null || recipe.OutputQuantity <= 0)
            return false;

        return CanFulfillRequirements(recipe.Requirements)
            && CanAddAfterConsuming(recipe.OutputItem, recipe.Requirements, recipe.OutputQuantity);
    }

    public bool TryProcessInstant(ProcessingRecipe recipe)
    {
        if (!CanProcess(recipe))
            return false;

        if (!ConsumeRequirements(recipe.Requirements))
            return false;

        Add(recipe.OutputItem, recipe.OutputQuantity);
        return true;
    }

    public bool CanBrew(BrewingRecipe recipe)
    {
        if (recipe == null)
            return false;

        if (recipe.OutputItem == null || recipe.OutputQuantity <= 0)
            return false;

        var ingredients = recipe.Ingredients;
        return CanFulfillRequirements(ingredients)
            && CanAddAfterConsuming(recipe.OutputItem, ingredients, recipe.OutputQuantity);
    }

    public bool TryBrewInstant(BrewingRecipe recipe)
    {
        if (!CanBrew(recipe))
            return false;

        if (!ConsumeRequirements(recipe.Ingredients))
            return false;

        Add(recipe.OutputItem, recipe.OutputQuantity);
        return true;
    }

    public List<Underbrew.Core.SaveItemStack> CreateSaveSnapshot()
    {
        EnsureSlotStorage();

        var snapshot = new List<Underbrew.Core.SaveItemStack>();
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty || slot.Item == null)
                continue;

            var itemId = slot.Item.SaveId;
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            snapshot.Add(new Underbrew.Core.SaveItemStack
            {
                itemId = itemId,
                quantity = Mathf.Max(0, slot.Quantity),
                slotIndex = i
            });
        }

        return snapshot;
    }

    public void LoadFromSaveSnapshot(IReadOnlyList<Underbrew.Core.SaveItemStack> snapshot, Func<string, ItemData> itemResolver)
    {
        EnsureSlotStorage();

        for (var i = 0; i < slots.Count; i++)
            slots[i].Clear();

        if (snapshot != null && itemResolver != null)
        {
            var hasExplicitSlots = false;
            for (var i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].slotIndex >= 0)
                {
                    hasExplicitSlots = true;
                    break;
                }
            }

            for (var i = 0; i < snapshot.Count; i++)
            {
                var entry = snapshot[i];
                if (entry.quantity <= 0 || string.IsNullOrWhiteSpace(entry.itemId))
                    continue;

                var item = itemResolver(entry.itemId);
                if (item == null)
                    continue;

                if (hasExplicitSlots && IsValidSlotIndex(entry.slotIndex) && slots[entry.slotIndex].IsEmpty)
                {
                    slots[entry.slotIndex].Set(item, entry.quantity);
                    continue;
                }

                Add(item, entry.quantity, false);
            }
        }

        OnInventoryChanged?.Invoke(null, 0);
    }

    public void Clear()
    {
        EnsureSlotStorage();

        for (var i = 0; i < slots.Count; i++)
            slots[i].Clear();

        OnInventoryChanged?.Invoke(null, 0);
    }

    private void EnsureSlotStorage()
    {
        var targetSlotCount = Mathf.Max(0, maxItemSlots);

        while (slots.Count < targetSlotCount)
            slots.Add(new InventorySlotData());

        while (slots.Count > targetSlotCount)
            slots.RemoveAt(slots.Count - 1);
    }

    private int FindSlotIndex(ItemData item)
    {
        if (item == null)
            return -1;

        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].Item == item && slots[i].Quantity > 0)
                return i;
        }

        return -1;
    }

    private int FindFirstEmptySlotIndex()
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty)
                return i;
        }

        return -1;
    }

    private int GetOccupiedSlotCount()
    {
        var occupiedCount = 0;

        for (var i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
                occupiedCount++;
        }

        return occupiedCount;
    }

    private bool IsValidSlotIndex(int index)
    {
        return index >= 0 && index < slots.Count;
    }

    private void ResolveJournalDiscoverySystem()
    {
        if (journalDiscoverySystem != null)
            return;

        journalDiscoverySystem = JournalDiscoverySystem.Instance ?? FindFirstObjectByType<JournalDiscoverySystem>(FindObjectsInactive.Include);
    }

    private void TryDiscoverJournalItem(ItemData item, bool recordJournalDiscovery)
    {
        if (!recordJournalDiscovery || item == null)
            return;

        ResolveJournalDiscoverySystem();
        if (journalDiscoverySystem == null)
            return;

        journalDiscoverySystem.DiscoverItem(item);
    }
}
