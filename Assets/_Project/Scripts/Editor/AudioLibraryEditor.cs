using Underbrew.Core;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(AudioLibrary))]
public class AudioLibraryEditor : Editor
{
    private ReorderableList uiCuesList;
    private ReorderableList gameplayCuesList;
    private ReorderableList footstepCuesList;
    private ReorderableList ambienceCuesList;
    private ReorderableList musicCuesList;
    private ReorderableList sceneAmbienceList;
    private ReorderableList sceneMusicList;

    private void OnEnable()
    {
        uiCuesList = CreateAudioCueList("uiCues", "UI Cues");
        gameplayCuesList = CreateAudioCueList("gameplayCues", "Gameplay Cues");
        footstepCuesList = CreateAudioCueList("footstepCues", "Footstep Cues");
        ambienceCuesList = CreateAudioCueList("ambienceCues", "Ambience Cues");
        musicCuesList = CreateAudioCueList("musicCues", "Music Cues");
        sceneAmbienceList = CreateSceneRoutingList("sceneAmbience", "Scene Ambience", "sceneName", "ambienceCueId");
        sceneMusicList = CreateSceneRoutingList("sceneMusic", "Scene Music", "sceneName", "musicCueId");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Author audio here by category. Each row is labeled by cue or scene so you can scan the asset without opening every element.",
            MessageType.Info);

        DrawList(uiCuesList);
        DrawList(gameplayCuesList);
        DrawList(footstepCuesList);
        DrawList(ambienceCuesList);
        DrawList(musicCuesList);
        DrawList(sceneAmbienceList);
        DrawList(sceneMusicList);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawList(ReorderableList list)
    {
        if (list == null)
            return;

        EditorGUILayout.Space(4f);
        list.DoLayoutList();
    }

    private ReorderableList CreateAudioCueList(string propertyName, string header)
    {
        var property = serializedObject.FindProperty(propertyName);
        var list = new ReorderableList(serializedObject, property, draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);

        list.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, header, EditorStyles.boldLabel);
        };

        list.elementHeightCallback = index =>
        {
            var element = property.GetArrayElementAtIndex(index);
            if (!element.isExpanded)
                return EditorGUIUtility.singleLineHeight + 6f;

            var clipsProp = element.FindPropertyRelative("clips");
            var clipsHeight = EditorGUI.GetPropertyHeight(clipsProp, includeChildren: true);
            var lineHeight = EditorGUIUtility.singleLineHeight;
            return lineHeight + 2f + lineHeight + 2f + clipsHeight + 2f + lineHeight + 2f + lineHeight + 2f + lineHeight + 2f + lineHeight + 8f;
        };

        list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = property.GetArrayElementAtIndex(index);
            var cueIdProp = element.FindPropertyRelative("cueId");
            var clipsProp = element.FindPropertyRelative("clips");
            var volumeProp = element.FindPropertyRelative("volume");
            var pitchMinProp = element.FindPropertyRelative("pitchMin");
            var pitchMaxProp = element.FindPropertyRelative("pitchMax");
            var cooldownProp = element.FindPropertyRelative("cooldownSeconds");

            rect.y += 2f;

            var label = BuildAudioCueLabel(element, index);
            element.isExpanded = EditorGUI.Foldout(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                element.isExpanded,
                label,
                true);

            if (!element.isExpanded)
                return;

            var y = rect.y + EditorGUIUtility.singleLineHeight + 2f;
            var lineHeight = EditorGUIUtility.singleLineHeight;

            var cueRect = new Rect(rect.x, y, rect.width, lineHeight);
            EditorGUI.PropertyField(cueRect, cueIdProp);
            y += lineHeight + 2f;

            var clipsHeight = EditorGUI.GetPropertyHeight(clipsProp, includeChildren: true);
            var clipsRect = new Rect(rect.x, y, rect.width, clipsHeight);
            EditorGUI.PropertyField(clipsRect, clipsProp, includeChildren: true);
            y += clipsHeight + 2f;

            var volumeRect = new Rect(rect.x, y, rect.width, lineHeight);
            EditorGUI.Slider(volumeRect, volumeProp, 0f, 1f);
            y += lineHeight + 2f;

            var pitchMinRect = new Rect(rect.x, y, rect.width, lineHeight);
            EditorGUI.PropertyField(pitchMinRect, pitchMinProp);
            y += lineHeight + 2f;

            var pitchMaxRect = new Rect(rect.x, y, rect.width, lineHeight);
            EditorGUI.PropertyField(pitchMaxRect, pitchMaxProp);
            y += lineHeight + 2f;

            var cooldownRect = new Rect(rect.x, y, rect.width, lineHeight);
            EditorGUI.PropertyField(cooldownRect, cooldownProp);
        };

        return list;
    }

    private ReorderableList CreateSceneRoutingList(string propertyName, string header, string sceneNamePropertyName, string cuePropertyName)
    {
        var property = serializedObject.FindProperty(propertyName);
        var list = new ReorderableList(serializedObject, property, draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);

        list.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, header, EditorStyles.boldLabel);
        };

        list.elementHeight = (EditorGUIUtility.singleLineHeight * 2f) + 10f;

        list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = property.GetArrayElementAtIndex(index);
            var sceneNameProp = element.FindPropertyRelative(sceneNamePropertyName);
            var cueProp = element.FindPropertyRelative(cuePropertyName);

            rect.y += 2f;

            var labelRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, BuildSceneRoutingLabel(sceneNameProp, cueProp, index), EditorStyles.boldLabel);

            var sceneRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight + 2f, rect.width * 0.52f, EditorGUIUtility.singleLineHeight);
            var cueRect = new Rect(rect.x + rect.width * 0.55f, rect.y + EditorGUIUtility.singleLineHeight + 2f, rect.width * 0.45f, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(sceneRect, sceneNameProp, GUIContent.none);
            EditorGUI.PropertyField(cueRect, cueProp, GUIContent.none);
        };

        return list;
    }

    private static string BuildAudioCueLabel(SerializedProperty element, int index)
    {
        var cueIdProp = element.FindPropertyRelative("cueId");
        var clipsProp = element.FindPropertyRelative("clips");

        var cueName = cueIdProp != null && cueIdProp.enumValueIndex >= 0
            ? cueIdProp.enumDisplayNames[cueIdProp.enumValueIndex]
            : "Unassigned Cue";

        var clipCount = clipsProp != null && clipsProp.isArray ? clipsProp.arraySize : 0;
        return $"{index + 1}. {cueName}  ({clipCount} clip{(clipCount == 1 ? string.Empty : "s")})";
    }

    private static string BuildSceneRoutingLabel(SerializedProperty sceneNameProp, SerializedProperty cueProp, int index)
    {
        var sceneName = sceneNameProp != null && !string.IsNullOrWhiteSpace(sceneNameProp.stringValue)
            ? sceneNameProp.stringValue
            : "<scene>";

        var cueName = cueProp != null && cueProp.enumValueIndex >= 0
            ? cueProp.enumDisplayNames[cueProp.enumValueIndex]
            : "None";

        return $"{index + 1}. {sceneName} -> {cueName}";
    }
}
