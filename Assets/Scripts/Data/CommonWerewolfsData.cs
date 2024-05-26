using Assets.Scripts.Data.Tags;
using Assets.Scripts.Editor.Tags;
using UnityEngine;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "CommonWerewolfsData", menuName = "ScriptableObjects/Roles/CommonWerewolfsData")]
	public class CommonWerewolfsData : ScriptableObject
	{
		[field: SerializeField]
		public float VoteMaxDuration { get; private set; }

		[field: SerializeField]
		[field: GameplayTagID]
		public GameplayTag MarkForDeath { get; private set; }
	}
}