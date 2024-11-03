using Assets.Scripts.Data;
using UnityEngine;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "ImageData", menuName = "ScriptableObjects/ImageData")]
	public class ImageData : GameplayData
	{
		[field: SerializeField]
		[field: PreviewSprite]
		public Sprite Image { get; private set; }

		[field: SerializeField]
		public string Text { get; private set; }
	}
}
