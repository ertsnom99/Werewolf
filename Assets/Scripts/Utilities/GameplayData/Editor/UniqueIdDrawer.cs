// Code taken from (with some modifications): https://www.reddit.com/r/Unity3D/comments/fdc2on/easily_generate_unique_ids_for_your_game_objects/
using System;
using UnityEditor;
using UnityEngine;

namespace Utilities.GameplayData.Editor
{
	[CustomPropertyDrawer(typeof(UniqueID))]
	public class UniqueIdDrawer : PropertyDrawer
	{
		private const float PADDING = 2;
		private const float GENERATE_BUTTON_WIDTH = 62;
		private const float COPY_BUTTON_WIDTH = 40;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return EditorGUI.GetPropertyHeight(property, label, true) * 2;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

			GUI.enabled = false;

			Rect rect = position;
			rect.width -= PADDING * 2 + GENERATE_BUTTON_WIDTH + COPY_BUTTON_WIDTH;
			rect.height /= 2;

			SerializedProperty guidProperty = property.FindPropertyRelative("Guid");
			EditorGUI.PropertyField(rect, guidProperty, GUIContent.none);

			rect.position += Vector2.up * rect.height;

			SerializedProperty hashCodeProperty = property.FindPropertyRelative("HashCode");
			EditorGUI.PropertyField(rect, hashCodeProperty, GUIContent.none);

			GUI.enabled = true;

			rect = position;
			rect.x += position.width - GENERATE_BUTTON_WIDTH - PADDING - COPY_BUTTON_WIDTH;
			rect.width = GENERATE_BUTTON_WIDTH;

			if (GUI.Button(rect, "Generate"))
			{
				if (string.IsNullOrEmpty(guidProperty.stringValue) || EditorUtility.DisplayDialog("Generate a new ID?",
																								"An ID already exist. It is dangerous to generate a new one if this ID is not suppose to change.",
																								"Generate",
																								"Cancel"))
				{
					guidProperty.stringValue = Guid.NewGuid().ToString();
					hashCodeProperty.intValue = guidProperty.stringValue.GetHashCode();
				}
			}

			rect.x += GENERATE_BUTTON_WIDTH + PADDING;
			rect.width = COPY_BUTTON_WIDTH;
			rect.height /= 2;

			if (GUI.Button(rect, "Copy"))
			{
				EditorGUIUtility.systemCopyBuffer = guidProperty.stringValue;
			}

			rect.y += rect.height;

			if (GUI.Button(rect, "Copy"))
			{
				EditorGUIUtility.systemCopyBuffer = hashCodeProperty.intValue.ToString();
			}

			EditorGUI.EndProperty();
		}
	}
}
