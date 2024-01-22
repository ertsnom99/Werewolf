using Assets.Scripts.Data.Tags;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Tags
{
    [CustomPropertyDrawer(typeof(GameplayTagIDAttribute))]
    public class GameplayTagIDDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true) * 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position.height /= 2;
            EditorGUI.PropertyField(position, property, label, true);

            GameplayTag gameplayTag = property.objectReferenceValue as GameplayTag;
            string IDText = "ID: ";
            GUIStyle style = new GUIStyle(GUI.skin.label);

            if (gameplayTag)
            {
                IDText = "ID: " + gameplayTag.CompactTagId.ToString();
            }
            else
            {
                style.normal.textColor = Color.red;
            }

            position.y += position.height;
            EditorGUI.LabelField(position, " ", IDText, style);
        }
    }
}