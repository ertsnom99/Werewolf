using Utilities.GameplayData;
using UnityEngine;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "MarkerData", menuName = "ScriptableObjects/MarkerData")]
	public class MarkerData : GameplayData
	{
		[field: SerializeField]
		[field: PreviewSprite]
		public Sprite Image { get; private set; }
	}
}