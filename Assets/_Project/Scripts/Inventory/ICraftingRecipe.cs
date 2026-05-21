public interface ICraftingRecipe
{
    string RecipeName { get; }
    RecipeRequirement[] Requirements { get; }
    ItemData OutputItem { get; }
    int OutputQuantity { get; }
    float CraftTimeSeconds { get; }
    CraftingStationType StationType { get; }
}
