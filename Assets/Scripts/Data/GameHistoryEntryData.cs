using Utilities.GameplayData;
using UnityEngine;
using UnityEngine.Localization;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "GameHistoryEntryData", menuName = "ScriptableObjects/GameHistoryEntryData")]
	public class GameHistoryEntryData : GameplayData
	{
		[field: SerializeField]
		[field: PreviewSprite]
		public Sprite Image { get; private set; }

		[field: SerializeField]
		public LocalizedString Text { get; private set; }
	}
}
