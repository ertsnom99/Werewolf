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

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			// Draw label
			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

			GUI.enabled = false;
			Rect valueRect = position;
			valueRect.width -= PADDING * 2 + GENERATE_BUTTON_WIDTH + COPY_BUTTON_WIDTH;
			SerializedProperty idProperty = property.FindPropertyRelative("Value");
			EditorGUI.PropertyField(valueRect, idProperty, GUIContent.none);
			
			GUI.enabled = true;

			Rect buttonRect = position;
			buttonRect.x += position.width - GENERATE_BUTTON_WIDTH - PADDING - COPY_BUTTON_WIDTH;
			buttonRect.width = GENERATE_BUTTON_WIDTH;
			if (GUI.Button(buttonRect, "Generate"))
			{
				if (string.IsNullOrEmpty(idProperty.stringValue) || EditorUtility.DisplayDialog("Generate a new ID?",
																								"An ID already exist. It is dangerous to generate a new one if this ID is not suppose to change.",
																								"Generate",
																								"Cancel"))
				{
					idProperty.stringValue = Guid.NewGuid().ToString();
				}
			}

			buttonRect.x += GENERATE_BUTTON_WIDTH + PADDING;
			buttonRect.width = COPY_BUTTON_WIDTH;
			if (GUI.Button(buttonRect, "Copy"))
			{
				EditorGUIUtility.systemCopyBuffer = idProperty.stringValue;
			}

			EditorGUI.EndProperty();
		}
	}
}
