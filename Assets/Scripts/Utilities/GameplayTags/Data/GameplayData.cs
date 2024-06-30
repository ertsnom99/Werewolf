using Assets.Scripts.Data.Tags;
using Assets.Scripts.Editor.Tags;
using UnityEngine;

namespace Assets.Scripts.Data
{
	public class GameplayData : ScriptableObject
	{
		[field: SerializeField]
		[field: GameplayTagID]
		public GameplayTag GameplayTag { get; protected set; }

		[field: SerializeField]
		public string DebugName { get; protected set; }
	}
}