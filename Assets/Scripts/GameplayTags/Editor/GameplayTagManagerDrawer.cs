#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Assets.Scripts.Data.Tags;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Scripts.Editor.Tags
{
    [CustomEditor(typeof(GameplayTagManager))]
    public class GameplayTagManagerDrawer : UnityEditor.Editor
    {
        private SerializedProperty _serializedProperty;
        private ReorderableList _reorderableList;

        private void OnEnable()
        {
            _serializedProperty = serializedObject.FindProperty(GameplayTagManager.GAMEPLAY_TAGS_FIELD_NAME);
            _reorderableList = new ReorderableList(serializedObject, _serializedProperty, true, true, true, true)
            {
                drawHeaderCallback = OnDrawHeader,
                onAddCallback = OnAddCallback,
                onRemoveCallback = OnRemoveCallback,
                drawElementCallback = OnDrawElement
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            _reorderableList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        private void OnDrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, _serializedProperty.displayName);
        }

        private void OnAddCallback(ReorderableList list)
        {
            list.serializedProperty.InsertArrayElementAtIndex(list.serializedProperty.arraySize);
            SerializedProperty tag = list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - 1);
            tag.objectReferenceValue = null;
            list.serializedProperty.serializedObject.ApplyModifiedProperties();
        }

        private void OnRemoveCallback(ReorderableList list)
        {
            int removeIndex = list.index;
            if (removeIndex < 0 || removeIndex >= list.count)
                removeIndex = list.count - 1;

            SerializedProperty property = list.serializedProperty.GetArrayElementAtIndex(removeIndex);
            Object tag = property.objectReferenceValue;

            if (GameplayTagManager.Instance.IsStillUsed(tag.name))
            {
                EditorUtility.DisplayDialog("Warning! Gameplay tag is being used.",
                    $"Cannot remove gameplay tag!\n\n {tag.name} is parent of an other gameplayTag.", "ok");
                return;
            }

            string searchGuid = FindGuidForGameplayTag(tag.name);
            bool parseFilesCancel = ParseFiles(searchGuid, out string fileInUse);
            EditorUtility.ClearProgressBar();

            if (parseFilesCancel)
                return;

            if (fileInUse != string.Empty)
            {
                EditorUtility.DisplayDialog("Warning! Gameplay tag is being used.",
                    $"Please remove tag in those file(s).!\n {fileInUse}", "ok");
                return;
            }

            property.objectReferenceValue = null;
            if (tag == null)
            {
                list.serializedProperty.DeleteArrayElementAtIndex(removeIndex);
                return;
            }
            
            AssetDatabase.RemoveObjectFromAsset(tag);
            DestroyImmediate(tag, true);
            list.serializedProperty.DeleteArrayElementAtIndex(removeIndex);
            property.serializedObject.ApplyModifiedProperties();
            
            SaveObject();
        }

        private void OnDrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            using (EditorGUI.ChangeCheckScope changeScope = new EditorGUI.ChangeCheckScope())
            {
                serializedObject.UpdateIfRequiredOrScript();
                
                SerializedProperty element = _serializedProperty.GetArrayElementAtIndex(index);
                Object refValue = element.objectReferenceValue;
                string oldName = refValue != null ? refValue.name : "";
                string newName = EditorGUI.DelayedTextField(rect, oldName);
            
                if (changeScope.changed && !string.IsNullOrEmpty(newName))
                {
                    if (refValue == null)
                    {
                        refValue = CreateInstance<GameplayTag>();
                        AssetDatabase.AddObjectToAsset(refValue, target);
                        element.objectReferenceValue = refValue;
                        element.serializedObject.ApplyModifiedProperties();
                    }

                    refValue.name = newName;
                    EditorUtility.SetDirty(refValue);

                    SaveObject();
                }
            }
        }

        private string FindGuidForGameplayTag(string tag)
        {
            string[] results = AssetDatabase.FindAssets("t:GameplayTagManager");
            string pathGameplayTagManager = Application.dataPath + AssetDatabase.GUIDToAssetPath(results[0]);
            pathGameplayTagManager = pathGameplayTagManager.Replace("AssetsAssets", "Assets");
            pathGameplayTagManager = pathGameplayTagManager.Replace("GameplayTagManager.asset", string.Empty);

            string[] gameplayTagManagerFile = Directory.GetFiles(pathGameplayTagManager, "*.asset");
            
            string textGameplayTagManager = File.ReadAllText(gameplayTagManagerFile[0]);
            string[] linesGameplayTagManager = textGameplayTagManager.Split('\n');
            string searchGuid = string.Empty;
            int tagLine;
            for (tagLine = 0; tagLine < linesGameplayTagManager.Length; tagLine++)
            {
                string line = linesGameplayTagManager[tagLine];
                if (line.Contains("m_Name:"))
                {
                    string[] split = line.Split();
                    if (split[3] == tag)
                        break;
                }
            }

            --tagLine;
            
            for (; tagLine >=0; tagLine--)
            {
                string line = linesGameplayTagManager[tagLine];
                if (line.Contains("--- !u!114 &"))
                {
                    searchGuid = line.Replace("--- !u!114 &", string.Empty);
                    break;
                }
            }

            return searchGuid;
        }

        private bool ParseFiles(string searchGuid, out string fileInUse)
        {
            fileInUse = string.Empty;

            List<string> allPathToAssetsList = new List<string>();
            string[] allAsset = Directory.GetFiles(Application.dataPath, "*.asset", SearchOption.AllDirectories);
            allPathToAssetsList.AddRange(allAsset);
            string[] allPrefabs = Directory.GetFiles(Application.dataPath, "*.prefab", SearchOption.AllDirectories);
            allPathToAssetsList.AddRange(allPrefabs);
            string[] allUnity = Directory.GetFiles(Application.dataPath, "*.unity", SearchOption.AllDirectories);
            allPathToAssetsList.AddRange(allUnity);

            List<string> filesFound = new List<string>();

            int fileCount = 0;
            foreach (string file in allPathToAssetsList)
            {
                fileCount++;
                if (DisplayCancelableProgressBar(file, fileCount / (float) allPathToAssetsList.Count))
                    return true;

                string text = File.ReadAllText(file);
                string[] lines = text.Split('\n');
                Parallel.ForEach(lines, line =>
                {
                    if (line.Contains("guid:") && line.Contains(searchGuid))
                    {
                        filesFound.Add(file);
                    }
                });
            }

            string[] fileFoundArray = filesFound.ToArray();
            foreach (string file in fileFoundArray)
            {
                string fileName = file.Replace(".asset", string.Empty);
                fileName = fileName.Replace(".prefab", string.Empty);
                fileName = fileName.Replace(".unity", string.Empty);

                int i = fileName.Length - 1;
                for (; i > 0; i--)
                {
                    if (fileName[i - 1] == '\\')
                    {
                        break;
                    }
                }

                fileName = fileName.Substring(i);
                fileInUse += $"\n {fileName}";
            }

            return false;
        }

        private bool DisplayCancelableProgressBar(string file, float progress)
        {
            string fileName = $"Assets{file.Replace(Application.dataPath, string.Empty)}";
            return EditorUtility.DisplayCancelableProgressBar("Finding references, please wait!", $"Parsing {fileName}", progress);
        }

        private void SaveObject()
        {
            EditorUtility.SetDirty(target);
            GameplayTagManager.Instance.UpdateGameplayTagId();
        }
    }
}
#endif