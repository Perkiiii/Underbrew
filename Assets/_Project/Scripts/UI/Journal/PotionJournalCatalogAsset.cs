using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PotionJournalCatalog", menuName = "Underbrew/Journal/Potion Catalog")]
public class PotionJournalCatalogAsset : ScriptableObject
{
    [SerializeField] private List<BrewingRecipe> recipes = new();

    public IReadOnlyList<BrewingRecipe> Recipes => recipes;
}
