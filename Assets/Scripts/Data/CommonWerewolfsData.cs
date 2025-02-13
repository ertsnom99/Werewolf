using UnityEngine;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "CommonWerewolfsData", menuName = "ScriptableObjects/Roles/CommonWerewolfsData")]
	public class CommonWerewolfsData : ScriptableObject
	{
		[field: SerializeField]
		public ImageData VotePlayerTitle { get; private set; }

		[field: SerializeField]
		public float VoteMaxDuration { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData VotedPlayerGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public MarkForDeathData MarkForDeath { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData FailedToVotePlayerGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public float ChoosenVillagerHighlightDuration { get; private set; }
	}
}