using UnityEditor;
using UnityEngine;
using Utilities.GameplayData.Editor.Tags;

namespace Utilities.GameplayData.Editor
{
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

			if (!gameplayData || string.IsNullOrEmpty(gameplayData.ID.Guid))
			{
				GUI.color = Color.red;
			}

			EditorGUI.PropertyField(position, property, label, true);
		}
	}
}
