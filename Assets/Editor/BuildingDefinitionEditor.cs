using Shikaku.UI.Buildings;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BuildingDefinition))]
public sealed class BuildingDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("buildingId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("chapterPackPath"));
        var style = serializedObject.FindProperty("style");
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Leave Style empty for a stable automatic chapter palette and architecture. Assign a style asset for a custom look. No extra save data is needed.", MessageType.Info);
        EditorGUILayout.PropertyField(style, new GUIContent("Custom Style (optional)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("appearanceSeed"));
        if (GUILayout.Button("Reroll Automatic Appearance"))
            serializedObject.FindProperty("appearanceSeed").intValue++;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overrideArchitecture"));
        if (serializedObject.FindProperty("overrideArchitecture").boolValue)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("architecture"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overrideColors"));
        if (serializedObject.FindProperty("overrideColors").boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wallColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("roofColor"));
        }
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mapOffset"));
        serializedObject.ApplyModifiedProperties();
    }
}
