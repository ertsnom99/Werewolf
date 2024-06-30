using Assets.Scripts.Data.Tags;
using System;
using UnityEngine;
using UnityEngine.Localization;

namespace Werewolf.Data
{
	[Serializable]
	public struct GameHistoryEntryData
	{
		public GameplayTag GameplayTag;
		[field: PreviewSprite]
		public Sprite Image;
		public LocalizedString Text;
	}

	[CreateAssetMenu(fileName = "GameHistoryEntriesData", menuName = "ScriptableObjects/GameHistoryEntriesData")]
	public class GameHistoryEntriesData : ScriptableObject
	{
		[field: SerializeField]
		public GameHistoryEntryData[] Datas { get; private set; }
	}
}