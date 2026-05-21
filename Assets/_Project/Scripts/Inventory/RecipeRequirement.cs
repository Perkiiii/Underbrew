using UnityEngine;

[System.Serializable]
public struct RecipeRequirement
{
    [SerializeField] private ItemData item;
    [SerializeField] private int quantity;

    public RecipeRequirement(ItemData item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }

    public ItemData Item => item;
    public int Quantity => quantity;
}
