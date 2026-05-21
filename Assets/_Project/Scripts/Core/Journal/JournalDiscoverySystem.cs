using System;
using System.Collections.Generic;
using UnityEngine;

public class JournalDiscoverySystem : MonoBehaviour
{
    public static JournalDiscoverySystem Instance { get; private set; }

    [SerializeField] private List<string> discoveredItemIds = new();

    private readonly HashSet<string> discoveredItemLookup = new(StringComparer.Ordinal);

    public event Action OnDiscoveryChanged;

    public IReadOnlyCollection<string> DiscoveredItemIds => discoveredItemIds;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        RebuildLookupFromSerializedState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool DiscoverItem(ItemData item)
    {
        if (item == null)
            return false;

        return DiscoverItemId(item.SaveId);
    }

    public bool DiscoverItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (!discoveredItemLookup.Add(itemId))
            return false;

        discoveredItemIds.Add(itemId);
        OnDiscoveryChanged?.Invoke();
        return true;
    }

    public bool IsDiscovered(ItemData item)
    {
        if (item == null)
            return false;

        return IsDiscoveredId(item.SaveId);
    }

    public bool IsDiscoveredId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        return discoveredItemLookup.Contains(itemId);
    }

    public List<string> CreateSaveSnapshot()
    {
        return new List<string>(discoveredItemIds);
    }

    public void LoadFromSaveSnapshot(IReadOnlyList<string> snapshot)
    {
        discoveredItemIds.Clear();
        discoveredItemLookup.Clear();

        if (snapshot != null)
        {
            for (var i = 0; i < snapshot.Count; i++)
            {
                var itemId = snapshot[i];
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                if (!discoveredItemLookup.Add(itemId))
                    continue;

                discoveredItemIds.Add(itemId);
            }
        }

        OnDiscoveryChanged?.Invoke();
    }

    public void Clear()
    {
        if (discoveredItemIds.Count == 0)
            return;

        discoveredItemIds.Clear();
        discoveredItemLookup.Clear();
        OnDiscoveryChanged?.Invoke();
    }

    private void RebuildLookupFromSerializedState()
    {
        discoveredItemLookup.Clear();

        if (discoveredItemIds == null)
        {
            discoveredItemIds = new List<string>();
            return;
        }

        for (var i = discoveredItemIds.Count - 1; i >= 0; i--)
        {
            var itemId = discoveredItemIds[i];
            if (string.IsNullOrWhiteSpace(itemId) || !discoveredItemLookup.Add(itemId))
                discoveredItemIds.RemoveAt(i);
        }
    }
}
