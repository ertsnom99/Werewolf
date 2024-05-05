using System;
using UnityEngine;

namespace Werewolf.Data
{
	[Serializable]
	public struct TitleData
	{
		[field: PreviewSprite]
		public Sprite Image;
		public string Text;
	}

	[CreateAssetMenu(fileName = "TitlesData", menuName = "ScriptableObjects/TitlesData")]
	public class TitlesData : ScriptableObject
	{
		[field: SerializeField]
		public TitleData[] Titles { get; private set; }
	}
}