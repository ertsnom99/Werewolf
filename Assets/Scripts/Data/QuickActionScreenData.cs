using Utilities.GameplayData;
using UnityEngine;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "QuickActionScreenData", menuName = "ScriptableObjects/QuickActionScreenData")]
	public class QuickActionScreenData : GameplayData
	{
		[field: SerializeField]
		[field: PreviewSprite]
		public Sprite Icon { get; private set; }
	}
}
