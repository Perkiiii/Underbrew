using System;
using System.Collections.Generic;
using UnityEngine;

public class GameStateFlags : MonoBehaviour
{
    [Serializable]
    private class DefaultFlag
    {
        public string key;
        public bool value;
    }

    public static GameStateFlags Instance { get; private set; }

    [SerializeField] private DefaultFlag[] defaultFlags;

    private readonly Dictionary<string, bool> flags = new();

    public event Action<string, bool> OnFlagChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // If this manager lives under a persistent root, the parent handles persistence.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        ApplyDefaults();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (flags.TryGetValue(key, out var currentValue) && currentValue == value)
            return;

        flags[key] = value;
        OnFlagChanged?.Invoke(key, value);
    }

    public bool GetFlag(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return flags.TryGetValue(key, out var value) && value;
    }

    public void ClearFlag(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (!flags.Remove(key))
            return;

        OnFlagChanged?.Invoke(key, false);
    }

    public void ResetToDefaults()
    {
        flags.Clear();
        ApplyDefaults();
    }

    public List<Underbrew.Core.SaveFlagEntry> CreateSaveSnapshot()
    {
        var snapshot = new List<Underbrew.Core.SaveFlagEntry>(flags.Count);

        foreach (var entry in flags)
        {
            snapshot.Add(new Underbrew.Core.SaveFlagEntry
            {
                key = entry.Key,
                value = entry.Value
            });
        }

        return snapshot;
    }

    public void LoadFromSaveSnapshot(IReadOnlyList<Underbrew.Core.SaveFlagEntry> snapshot)
    {
        flags.Clear();
        ApplyDefaults();

        if (snapshot == null)
            return;

        for (var i = 0; i < snapshot.Count; i++)
        {
            var entry = snapshot[i];
            if (string.IsNullOrWhiteSpace(entry.key))
                continue;

            flags[entry.key] = entry.value;
            OnFlagChanged?.Invoke(entry.key, entry.value);
        }
    }

    private void ApplyDefaults()
    {
        if (defaultFlags == null)
            return;

        for (var i = 0; i < defaultFlags.Length; i++)
        {
            var defaultFlag = defaultFlags[i];
            if (defaultFlag == null || string.IsNullOrWhiteSpace(defaultFlag.key))
                continue;

            flags[defaultFlag.key] = defaultFlag.value;
        }
    }
}
