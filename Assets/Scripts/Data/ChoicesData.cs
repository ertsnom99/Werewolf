using System;
using UnityEngine;

namespace Werewolf.Data
{
	[Serializable]
	public struct ChoiceData
	{
		[field: PreviewSprite]
		public Sprite Image;
		public string Name;
	}

	[CreateAssetMenu(fileName = "ChoicesData", menuName = "ScriptableObjects/ChoicesData")]
	public class ChoicesData : ScriptableObject
	{
		[field: SerializeField]
		public ChoiceData[] Choices { get; private set; }
	}
}