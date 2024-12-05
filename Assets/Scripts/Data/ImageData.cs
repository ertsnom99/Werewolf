using Assets.Scripts.Data;
using UnityEngine;
using UnityEngine.Localization;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "ImageData", menuName = "ScriptableObjects/ImageData")]
	public class ImageData : GameplayData
	{
		[field: SerializeField]
		[field: PreviewSprite]
		public Sprite Image { get; private set; }

		[field: SerializeField]
		public LocalizedString Text { get; private set; }
	}
}
