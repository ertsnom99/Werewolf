using System;
using UnityEngine;

namespace Werewolf.Data
{
	[Serializable]
	public struct ImageData
	{
		[field: PreviewSprite]
		public Sprite Image;
		public string Text;
	}

	[CreateAssetMenu(fileName = "ImagesData", menuName = "ScriptableObjects/ImagesData")]
	public class ImagesData : ScriptableObject
	{
		[field: SerializeField]
		public ImageData[] Images { get; private set; }
	}
}