using Assets.Scripts.Editor.Tags;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ValidateGameplayDataAttribute))]
public class ValidateGameplayDataDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GameplayData gameplayData = property.objectReferenceValue as GameplayData;

        if (!gameplayData || !gameplayData.GameplayTag)
        {
            GUI.color = Color.red;
        }

        EditorGUI.PropertyField(position, property, label, true);
    }
}