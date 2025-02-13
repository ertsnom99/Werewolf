using UnityEngine;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "MarkForDeath", menuName = "ScriptableObjects/MarkForDeath")]
	public class MarkForDeathData : ScriptableObject
	{
		[field: SerializeField]
		public string DebugName { get; protected set; }
	}
}
