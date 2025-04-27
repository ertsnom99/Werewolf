using UnityEngine;
using UnityEditor;

namespace Werewolf.Managers.Editor
{
	[CustomEditor(typeof(DaytimeManager))]
	public class DaytimeManagerEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			EditorGUILayout.Space();

			if (GUILayout.Button("Set to Day"))
			{
				DaytimeManager manager = (DaytimeManager)target;

				manager.InitializeForEditor();
				manager.SetDaytime(Daytime.Day);
			}

			if (GUILayout.Button("Set to Night"))
			{
				DaytimeManager manager = (DaytimeManager)target;

				manager.InitializeForEditor();
				manager.SetDaytime(Daytime.Night);
			}
		}
	}
}
