using System.Text;
using UnityEditor;
using UnityEngine;

namespace Utilities.GameplayData.Editor
{
	[CustomEditor(typeof(GameplayDataManager))]
	public class GameplayDataManagerEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			EditorGUILayout.Space();

			if (GUILayout.Button("Check GameplayData sanity"))
			{
				GameplayDataManager.SanityIssue[] sanityIssues = (target as GameplayDataManager).CheckGameplayDataSanity();

				if (sanityIssues.Length <= 0)
				{
					EditorUtility.DisplayDialog("No sanity issues found",
												"Everything is OK",
												"OK");
				}
				else
				{
					StringBuilder stringBuilder = new("The following issues were found:\n");

					foreach (GameplayDataManager.SanityIssue sanityIssue in sanityIssues)
					{
						stringBuilder.Append($"-{sanityIssue.gameplayDataName} :: {sanityIssue.issue}\n");
					}

					EditorUtility.DisplayDialog("Sanity issues found",
												stringBuilder.ToString(),
												"OK");
				}
			}
		}
	}
}
