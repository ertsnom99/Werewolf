using Utilities.GameplayData;
using UnityEngine;
using UnityEngine.Localization;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "PlayerGroupData", menuName = "ScriptableObjects/PlayerGroupData")]
	public class PlayerGroupData : GameplayData
	{
		[field: SerializeField]
		public LocalizedString Name { get; private set; }

		[field: SerializeField]
		public bool HasPluralName { get; private set; }

		[field: SerializeField]
		public int Priority { get; private set; }

		[field: SerializeField]
		[field: PreviewSprite]
		public Sprite Image { get; private set; }

		[field: SerializeField]
		[field: PreviewSprite]
		public Sprite SmallImage { get; private set; }

		[HideInInspector]
		public GameObject Leader;
	}
}