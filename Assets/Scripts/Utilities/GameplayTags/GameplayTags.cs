using System;
using System.Collections.Generic;
using Assets.Scripts.Data.Tags;
using UnityEngine;

namespace Data.Tags
{
	[Serializable]
	public class GameplayTags
	{
		[field: SerializeField]
		private List<GameplayTag> Tags { get; set; } = new();
	}
}