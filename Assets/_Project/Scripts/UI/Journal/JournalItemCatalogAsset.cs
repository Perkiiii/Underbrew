using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "JournalItemCatalog", menuName = "Underbrew/Journal/Item Catalog")]
public class JournalItemCatalogAsset : ScriptableObject
{
    [SerializeField] private List<ItemData> items = new();

    public IReadOnlyList<ItemData> Items => items;
}
