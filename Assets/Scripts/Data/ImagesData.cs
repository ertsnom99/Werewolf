using Assets.Scripts.Data.Tags;
using Assets.Scripts.Editor.Tags;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Werewolf.Data
{
	[Serializable]
	public struct ImageData
	{
		[GameplayTagID]
		public GameplayTag GameplayTag;
		[PreviewSprite]
		public Sprite Image;
		public string Text;
	}

	[CreateAssetMenu(fileName = "ImagesData", menuName = "ScriptableObjects/ImagesData")]
	public class ImagesData : ScriptableObject
	{
		[SerializeField]
		private ImageData[] _images;

		private Dictionary<int, int> _IDToImage = new();

		public void Init()
		{
			if (_IDToImage.Count > 0)
			{
				return;
			}

			for (int i = 0; i < _images.Length; i++)
			{
				_IDToImage.Add(_images[i].GameplayTag.CompactTagId, i);
			}
		}

		public bool GetImageData(int ID, out ImageData imageData)
		{
			if (!_IDToImage.TryGetValue(ID, out int index))
			{
				Debug.LogError($"No ImageData has the gameplayTag ID {ID}");
				imageData = default;
				return false;
			}

			imageData = _images[index];
			return true;
		}
	}
}