using System;
using System.Collections.Generic;
using UnityEngine;

public static class JournalItemCatalog
{
    private static readonly List<ItemData> cachedItems = new();
    private static readonly HashSet<ItemData> seenItems = new();
    private static bool isCacheBuilt;

    public static List<ItemData> GetItems(Predicate<ItemData> predicate = null)
    {
        EnsureCache();

        var results = new List<ItemData>();
        for (var i = 0; i < cachedItems.Count; i++)
        {
            var item = cachedItems[i];
            if (item == null)
                continue;

            if (predicate != null && !predicate(item))
                continue;

            results.Add(item);
        }

        return results;
    }

    public static void ClearCache()
    {
        cachedItems.Clear();
        seenItems.Clear();
        isCacheBuilt = false;
    }

    private static void EnsureCache()
    {
        if (isCacheBuilt)
            return;

        cachedItems.Clear();
        seenItems.Clear();

        var loadedItems = Resources.FindObjectsOfTypeAll<ItemData>();
        for (var i = 0; i < loadedItems.Length; i++)
        {
            var item = loadedItems[i];
            if (item == null || !seenItems.Add(item))
                continue;

            cachedItems.Add(item);
        }

        cachedItems.Sort((left, right) =>
        {
            var sortOrder = left.JournalSortOrder.CompareTo(right.JournalSortOrder);
            if (sortOrder != 0)
                return sortOrder;

            return string.Compare(left.ItemName, right.ItemName, StringComparison.OrdinalIgnoreCase);
        });

        isCacheBuilt = true;
    }
}
