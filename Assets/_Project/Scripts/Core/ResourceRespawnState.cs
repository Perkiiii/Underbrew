using System;
using System.Collections.Generic;
using Underbrew.Core;
using UnityEngine;

public class ResourceRespawnState : MonoBehaviour
{
    public static ResourceRespawnState Instance { get; private set; }

    private readonly Dictionary<string, long> nextAvailableUnixSecondsByNodeId = new(StringComparer.Ordinal);

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
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsResourceAvailable(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return true;

        if (!nextAvailableUnixSecondsByNodeId.TryGetValue(nodeId, out var nextAvailableAt))
            return true;

        return GetNowUnixSeconds() >= nextAvailableAt;
    }

    public long GetNextAvailableUnixSeconds(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return 0;

        return nextAvailableUnixSecondsByNodeId.TryGetValue(nodeId, out var nextAvailableAt)
            ? nextAvailableAt
            : 0;
    }

    public void MarkCollected(string nodeId, float cooldownSeconds)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        var clampedCooldown = Mathf.Max(0f, cooldownSeconds);
        var nextAvailableAt = GetNowUnixSeconds() + Mathf.CeilToInt(clampedCooldown);
        nextAvailableUnixSecondsByNodeId[nodeId] = nextAvailableAt;
    }

    public void ClearNodeState(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        nextAvailableUnixSecondsByNodeId.Remove(nodeId);
    }

    public void ResetAll()
    {
        nextAvailableUnixSecondsByNodeId.Clear();
    }

    public List<SaveResourceNodeEntry> CreateSaveSnapshot()
    {
        var snapshot = new List<SaveResourceNodeEntry>(nextAvailableUnixSecondsByNodeId.Count);

        foreach (var pair in nextAvailableUnixSecondsByNodeId)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            snapshot.Add(new SaveResourceNodeEntry
            {
                nodeId = pair.Key,
                nextAvailableUnixSeconds = pair.Value
            });
        }

        return snapshot;
    }

    public void LoadFromSaveSnapshot(IReadOnlyList<SaveResourceNodeEntry> snapshot)
    {
        nextAvailableUnixSecondsByNodeId.Clear();

        if (snapshot == null)
            return;

        for (var i = 0; i < snapshot.Count; i++)
        {
            var entry = snapshot[i];
            if (string.IsNullOrWhiteSpace(entry.nodeId))
                continue;

            nextAvailableUnixSecondsByNodeId[entry.nodeId] = entry.nextAvailableUnixSeconds;
        }
    }

    private static long GetNowUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
