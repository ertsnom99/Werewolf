using Assets.Scripts.Data.Tags;
using UnityEngine;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "CommonWerewolfsData", menuName = "ScriptableObjects/Roles/CommonWerewolfsData")]
	public class CommonWerewolfsData : ScriptableObject
	{
		[field: SerializeField]
		public GameplayTag VotePlayerImage { get; private set; }

		[field: SerializeField]
		public float VoteMaxDuration { get; private set; }

		[field: SerializeField]
		public GameplayTag VotedPlayerGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag MarkForDeath { get; private set; }

		[field: SerializeField]
		public GameplayTag FailedToVotePlayerGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public float ChoosenVillagerHighlightDuration { get; private set; }
	}
}