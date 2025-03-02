using UnityEngine;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "CommonWerewolvesData", menuName = "ScriptableObjects/Roles/CommonWerewolvesData")]
	public class CommonWerewolvesData : ScriptableObject
	{
		[field: SerializeField]
		public TitleScreenData VotePlayerTitleScreen { get; private set; }

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