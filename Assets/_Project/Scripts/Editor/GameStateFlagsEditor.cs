using System;
using System.Collections.Generic;
using Underbrew.Core;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameStateFlags))]
public class GameStateFlagsEditor : Editor
{
    private SerializedProperty defaultFlagsProp;
    private bool showDefaults = true;
    private bool showKnownFlags = true;
    private bool showOnlyTrueKnownFlags;
    private string knownFlagsFilter = string.Empty;

    private void OnEnable()
    {
        defaultFlagsProp = serializedObject.FindProperty("defaultFlags");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var flags = target as GameStateFlags;
        var runtimeInstance = GameStateFlags.Instance;

        EditorGUILayout.HelpBox(
            "Custom GameStateFlags inspector active. Use Default Flags for planned keys and Known Flags to inspect merged defaults + runtime values.",
            MessageType.Info);

        if (Application.isPlaying)
        {
            if (runtimeInstance == null)
            {
                EditorGUILayout.HelpBox("Play Mode: GameStateFlags.Instance is null.", MessageType.Warning);
            }
            else if (!ReferenceEquals(flags, runtimeInstance))
            {
                EditorGUILayout.HelpBox("Play Mode: You are not inspecting the live runtime GameStateFlags instance. Select the GameStateFlags object under ManagersRoot.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Play Mode: Inspecting LIVE runtime GameStateFlags instance.", MessageType.None);
            }
        }

        DrawDefaultFlagsSection();
        EditorGUILayout.Space(6f);
        DrawKnownFlagsSection(flags);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDefaultFlagsSection()
    {
        showDefaults = EditorGUILayout.BeginFoldoutHeaderGroup(showDefaults, "Default Flags");
        if (showDefaults)
        {
            EditorGUILayout.PropertyField(defaultFlagsProp, includeChildren: true);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawKnownFlagsSection(GameStateFlags flags)
    {
        showKnownFlags = EditorGUILayout.BeginFoldoutHeaderGroup(showKnownFlags, "Known Flags (Defaults + Runtime)");
        if (!showKnownFlags)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        if (flags == null)
        {
            EditorGUILayout.HelpBox("GameStateFlags target is unavailable.", MessageType.Warning);
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        showOnlyTrueKnownFlags = EditorGUILayout.ToggleLeft("Only True", showOnlyTrueKnownFlags, GUILayout.Width(80f));
        knownFlagsFilter = EditorGUILayout.TextField("Key Contains", knownFlagsFilter);
        EditorGUILayout.EndHorizontal();

        var runtimeEntries = Application.isPlaying ? flags.CreateSaveSnapshot() : new List<SaveFlagEntry>();
        var entries = BuildMergedEntries(runtimeEntries);
        entries.Sort(static (a, b) => string.Compare(a.key, b.key, StringComparison.Ordinal));

        var visibleEntries = FilterEntries(entries, knownFlagsFilter, showOnlyTrueKnownFlags);
        EditorGUILayout.LabelField(
            $"Visible: {visibleEntries.Count}  |  Merged: {entries.Count}  |  Runtime: {runtimeEntries.Count}  |  Defaults: {GetDefaultFlagCount()}",
            EditorStyles.miniBoldLabel);
        EditorGUILayout.Space(2f);

        if (visibleEntries.Count == 0)
        {
            EditorGUILayout.HelpBox("No known flags match the current filter.", MessageType.None);
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            for (var i = 0; i < visibleEntries.Count; i++)
            {
                var entry = visibleEntries[i];
                EditorGUILayout.ToggleLeft(entry.key, entry.value);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private List<SaveFlagEntry> BuildMergedEntries(List<SaveFlagEntry> runtimeEntries)
    {
        var merged = new Dictionary<string, bool>(StringComparer.Ordinal);

        if (runtimeEntries != null)
        {
            for (var i = 0; i < runtimeEntries.Count; i++)
            {
                var entry = runtimeEntries[i];
                if (string.IsNullOrWhiteSpace(entry.key))
                    continue;

                merged[entry.key] = entry.value;
            }
        }

        if (defaultFlagsProp != null && defaultFlagsProp.isArray)
        {
            for (var i = 0; i < defaultFlagsProp.arraySize; i++)
            {
                var element = defaultFlagsProp.GetArrayElementAtIndex(i);
                var keyProp = element.FindPropertyRelative("key");
                var valueProp = element.FindPropertyRelative("value");

                if (keyProp == null || valueProp == null)
                    continue;

                var key = keyProp.stringValue;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!merged.ContainsKey(key))
                    merged[key] = valueProp.boolValue;
            }
        }

        var results = new List<SaveFlagEntry>(merged.Count);
        foreach (var pair in merged)
        {
            results.Add(new SaveFlagEntry
            {
                key = pair.Key,
                value = pair.Value
            });
        }

        return results;
    }

    private int GetDefaultFlagCount()
    {
        if (defaultFlagsProp == null || !defaultFlagsProp.isArray)
            return 0;

        var count = 0;
        for (var i = 0; i < defaultFlagsProp.arraySize; i++)
        {
            var element = defaultFlagsProp.GetArrayElementAtIndex(i);
            var keyProp = element.FindPropertyRelative("key");
            if (keyProp == null || string.IsNullOrWhiteSpace(keyProp.stringValue))
                continue;

            count++;
        }

        return count;
    }

    private static List<SaveFlagEntry> FilterEntries(List<SaveFlagEntry> source, string keyFilter, bool onlyTrue)
    {
        var results = new List<SaveFlagEntry>(source.Count);
        var hasFilter = !string.IsNullOrWhiteSpace(keyFilter);

        for (var i = 0; i < source.Count; i++)
        {
            var entry = source[i];

            if (onlyTrue && !entry.value)
                continue;

            if (hasFilter && (entry.key == null || entry.key.IndexOf(keyFilter, StringComparison.OrdinalIgnoreCase) < 0))
                continue;

            results.Add(entry);
        }

        return results;
    }
}