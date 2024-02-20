#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Data.Tags
{
	public partial class GameplayTagManager
	{
		private static GameplayTagManager s_instance;

		public static GameplayTagManager Instance
		{
			get
			{
				if (s_instance == null)
				{
					Load();
				}

				return s_instance;
			}
		}

		[InitializeOnEnterPlayMode]
		private static void OnEnterPlayModeEditor(EnterPlayModeOptions _)
		{
			s_instance = null;
		}

		private static void Load()
		{
			if (s_instance != null)
				return;

			string[] results = AssetDatabase.FindAssets("t:GameplayTagManager");
			if (results.Length == 0)
			{
				Debug.LogError($"Couldn't find {nameof(GameplayTagManager)} asset in the project, create one to use {nameof(GameplayTag)}");
				return;
			}

			string path = AssetDatabase.GUIDToAssetPath(results[0]);
			if (results.Length > 1)
			{
				Debug.LogError($"Multiple {nameof(GameplayTagManager)} in the project, will use the one at: {path}, others will be ignored.");
			}

			s_instance = AssetDatabase.LoadAssetAtPath<GameplayTagManager>(path);
		}

		private readonly List<GameplayTagInfo> _tagList = new();

		public void UpdateGameplayTagId()
		{
			UpdateGameplayTagInfo();

			for (int i = 0; i < _tagList.Count; i++)
			{
				GameplayTagInfo info = _tagList[i];
				string tagName = info.Name;
				GameplayTag tag = FindGameplayTagByName(tagName);

				if (tag == null)
				{
					tag = CreateInstance<GameplayTag>();
					tag.name = tagName;
					_gameplayTags.Insert(i, tag);
					AssetDatabase.AddObjectToAsset(tag, this);
					EditorUtility.SetDirty(this);
				}

				tag.CompactTagId = info.CompactTagId;
			}
		}

		private void UpdateGameplayTagInfo()
		{
			_gameplayTags.Sort();
			_tagList.Clear();
			int firstStateIndex = 1;

			foreach (GameplayTag tag in _gameplayTags)
			{
				string[] tagPath = tag == null ? new[] { "" } : tag.name.Split('.');
				int parentIndex = -1;

				string tagName = string.Empty;
				for (int j = 0; j < tagPath.Length; ++j)
				{
					tagName += tagPath[j];
					int index = FindTag(tagName);
					if (index == -1)
					{
						GameplayTagInfo info = new() { Name = tagName, Level = j, ParentIndex = parentIndex, ChildIndex = new() };
						if (parentIndex == -1)
						{
							info.LevelId = firstStateIndex;
							info.CompactTagId = firstStateIndex;
							++firstStateIndex;
						}
						else
						{
							info.LevelId = -1;
						}

						index = _tagList.Count;
						_tagList.Add(info);
					}

					if (parentIndex != -1)
					{
						AddUniqueChild(parentIndex, index);
					}

					parentIndex = index;
					tagName += ".";
				}
			}

			foreach (GameplayTagInfo info in _tagList)
			{
				for (int j = 0; j < info.ChildIndex.Count; ++j)
				{
					GameplayTagInfo childInfo = _tagList[info.ChildIndex[j]];
					childInfo.LevelId = j + 1;
					int levelBitMask = childInfo.LevelId << (childInfo.Level * LEVEL_BITS);
					childInfo.CompactTagId |= levelBitMask;

					int parentIndex = childInfo.ParentIndex;
					while (parentIndex != -1)
					{
						GameplayTagInfo parentInfo = _tagList[parentIndex];
						levelBitMask = parentInfo.LevelId << (parentInfo.Level * LEVEL_BITS);
						childInfo.CompactTagId |= levelBitMask;
						parentIndex = parentInfo.ParentIndex;
					}
				}
			}
		}

		private GameplayTag FindGameplayTagByName(string tag) => _gameplayTags.FirstOrDefault(t => t.name == tag);

		public bool IsStillUsed(string tag)
		{
			UpdateGameplayTagInfo();

			int index = FindTag(tag);
			return index != -1 && _tagList[index].ChildIndex.Count != 0;
		}

		private string GetPathTagIdName(string tag)
		{
			string tagPath = "";
			int index = FindTag(tagPath);
			if (index == -1) return tagPath;

			GameplayTagInfo parentInfo = _tagList[index];
			tagPath = tag;
			while (parentInfo.ParentIndex != -1)
			{
				parentInfo = _tagList[parentInfo.ParentIndex];
				tagPath = tagPath.Insert(0, parentInfo.Name + ".");
			}

			return tagPath;
		}

		private int FindTag(string tag)
		{
			for (int i = 0; i < _tagList.Count; i++)
			{
				if (_tagList[i].Name == tag)
					return i;
			}

			return -1;
		}

		private void AddUniqueChild(int parentIndex, int index)
		{
			if (parentIndex == -1) return;

			List<int> childIndex = _tagList[parentIndex].ChildIndex;

			if (childIndex.Any(t => t == index))
			{
				return;
			}

			childIndex.Add(index);
		}
	}
}
#endif
