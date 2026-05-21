using System;

[Serializable]
public class InventorySlotData
{
    public ItemData Item { get; private set; }
    public int Quantity { get; private set; }
    public bool IsEmpty => Item == null || Quantity <= 0;

    internal void Set(ItemData item, int quantity)
    {
        Item = item;
        Quantity = quantity;
    }

    internal void Clear()
    {
        Item = null;
        Quantity = 0;
    }
}
