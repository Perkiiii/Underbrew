using UnityEngine;

[CreateAssetMenu(fileName = "BrewingRecipe", menuName = "Underbrew/Crafting/Brewing Recipe")]
public class BrewingRecipe : ScriptableObject, ICraftingRecipe
{
    [SerializeField] private string recipeSaveId;
    [SerializeField] private string recipeName;
    [SerializeField] private RecipeRequirement[] ingredients;
    [SerializeField] private ItemData outputItem;
    [SerializeField] private int outputQuantity = 1;
    [SerializeField] private float brewingTime = 1f;
    [SerializeField] private CraftingStationType stationType = CraftingStationType.BrewingStand;

    public string RecipeName => string.IsNullOrWhiteSpace(recipeName)
        ? (outputItem != null ? outputItem.ItemName : name)
        : recipeName;
    public string RecipeSaveId => string.IsNullOrWhiteSpace(recipeSaveId) ? RecipeName : recipeSaveId;
    public RecipeRequirement[] Ingredients => ingredients;
    public RecipeRequirement[] Requirements => ingredients;
    public ItemData OutputItem => outputItem;
    public int OutputQuantity => outputQuantity;
    public float BrewingTime => brewingTime;
    public float CraftTimeSeconds => brewingTime;
    public CraftingStationType StationType => stationType;

    private void OnValidate()
    {
        stationType = CraftingStationType.BrewingStand;

        if (string.IsNullOrWhiteSpace(recipeSaveId))
            Debug.LogWarning($"[BrewingRecipe] '{name}' has a blank recipeSaveId and will fall back to recipeName '{RecipeName}'. Assign an explicit recipeSaveId to stabilize persistence.", this);
    }
}
