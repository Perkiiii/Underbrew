using UnityEngine;

[CreateAssetMenu(fileName = "ProcessingRecipe", menuName = "Underbrew/Crafting/Processing Recipe")]
public class ProcessingRecipe : ScriptableObject, ICraftingRecipe
{
    [SerializeField] private string recipeName;
    [SerializeField] private ItemData inputItem;
    [SerializeField] private int inputQuantity = 1;
    [SerializeField] private ItemData outputItem;
    [SerializeField] private int outputQuantity = 1;
    [SerializeField] private float processingTime = 1f;
    [SerializeField] private CraftingStationType stationType;

    public string RecipeName => string.IsNullOrWhiteSpace(recipeName)
        ? (outputItem != null ? outputItem.ItemName : name)
        : recipeName;
    public ItemData InputItem => inputItem;
    public int InputQuantity => inputQuantity;
    public ItemData OutputItem => outputItem;
    public int OutputQuantity => outputQuantity;
    public float ProcessingTime => processingTime;
    public float CraftTimeSeconds => processingTime;
    public CraftingStationType StationType => stationType;

    public RecipeRequirement[] Requirements => new[]
    {
        new RecipeRequirement(inputItem, inputQuantity)
    };
}
