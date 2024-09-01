using Assets.Scripts.Data.Tags;
using UnityEngine;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "GameConfig", menuName = "ScriptableObjects/Config/GameConfig")]
	public class GameConfig : ScriptableObject
	{
		public const int MAX_PLAYER_COUNT = 20;

		[field: Header("Loading Screen")]
		[field: SerializeField]
		public string LoadingScreenText { get; private set; }

		[field: SerializeField]
		public float LoadingScreenTransitionDuration { get; private set; }

		[field: Header("Visual")]
		[field: SerializeField]
		public Card CardPrefab { get; private set; }

		[field: SerializeField]
		public AnimationCurve CardsOffset { get; private set; }

		[field: SerializeField]
		public float ReservedRolesSpacing { get; private set; }

		[field: SerializeField]
		public GameObject CaptainCardPrefab { get; private set; }

		[field: SerializeField]
		public Vector3 CaptainCardOffset { get; private set; }

		[field: SerializeField]
		public AnimationCurve CameraOffset { get; private set; }

		[field: Header("Role Distribution")]
		[field: SerializeField]
		public int AvailableRolesMaxAttemptCount { get; private set; }

		[field: SerializeField]
		public GameplayTag PlayerGivenRoleGameHistoryEntry { get; private set; }

		[field: Header("Gameplay Loop")]
		[field: SerializeField]
		public float GameplayLoopStepDelay { get; private set; }

		[field: Header("Given Role Reveal")]
		[field: SerializeField]
		public string RoleGivenRevealText { get; private set; }

		[field: Header("Election")]
		[field: SerializeField]
		public GameplayTag ElectionPromptImage { get; private set; }

		[field: SerializeField]
		public string ElectionPromptButtonText { get; private set; }

		[field: SerializeField]
		public float ElectionPromptDuration { get; private set; }

		[field: SerializeField]
		public GameplayTag ElectionMultipleCandidatesGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag ElectionMultipleCandidatesImage { get; private set; }

		[field: SerializeField]
		public float ElectionMultipleCandidatesDuration { get; private set; }

		[field: SerializeField]
		public GameplayTag ElectionDebateImage { get; private set; }

		[field: SerializeField]
		public float ElectionDebateDuration { get; private set; }

		[field: SerializeField]
		public GameplayTag ElectionNoCandidateGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag ElectionNoCandidateImage { get; private set; }

		[field: SerializeField]
		public float ElectionNoCandidateDuration { get; private set; }

		[field: SerializeField]
		public float ElectionVoteDuration { get; private set; }

		[field: Header("Daytime")]
		[field: SerializeField]
		public GameplayTag SunSetGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag SunRoseGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public float DaytimeTransitionDuration { get; private set; }

		[field: SerializeField]
		public float DaytimeTextFadeInDelay { get; private set; }

		[field: SerializeField]
		public float DaytimeLightTransitionDuration { get; private set; }

		[field: SerializeField]
		public Color DayColor { get; private set; }

		[field: SerializeField]
		public float DayTemperature { get; private set; }

		[field: SerializeField]
		public GameplayTag DayTransitionImage { get; private set; }

		[field: SerializeField]
		public Color NightColor { get; private set; }

		[field: SerializeField]
		public float NightTemperature { get; private set; }

		[field: SerializeField]
		public GameplayTag NightTransitionImage { get; private set; }

		[field: Header("NightCall")]
		[field: SerializeField]
		public GameplayTag WokeUpPlayerGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public string RolePlayingTextSingular { get; private set; }

		[field: SerializeField]
		public string RolePlayingTextPlurial { get; private set; }

		[field: SerializeField]
		public float NightCallMinimumDuration { get; private set; }

		[field: SerializeField]
		public float NightCallChangeDelay { get; private set; }

		[field: Header("Death Reveal")]
		[field: SerializeField]
		public GameplayTag DeathRevealSomeoneDiedImage { get; private set; }

		[field: SerializeField]
		public GameplayTag DeathRevealNoOneDiedImage { get; private set; }

		[field: SerializeField]
		public float DeathRevealTitleHoldDuration { get; private set; }

		[field: SerializeField]
		public float DelayBeforeRevealingDeadPlayer { get; private set; }

		[field: SerializeField]
		public GameplayTag PlayerDiedImage { get; private set; }

		[field: SerializeField]
		public GameplayTag PlayerExecutedImage { get; private set; }

		[field: SerializeField]
		public GameplayTag PlayerDiedGameHistoryEntry { get; private set; }

		[field: Header("Execution")]
		[field: SerializeField]
		public GameplayTag ExecutionDebateImage { get; private set; }

		[field: SerializeField]
		public float ExecutionDebateDuration { get; private set; }

		[field: SerializeField]
		public float ExecutionVoteDuration { get; private set; }

		[field: SerializeField]
		public GameplayTag ExecutionVoteStartedGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag ExecutionDrawNewVoteImage { get; private set; }

		[field: SerializeField]
		public float ExecutionTitleHoldDuration { get; private set; }

		[field: SerializeField]
		public GameplayTag ExecutionDrawNewVoteGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag ExecutionDrawAgainGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag ExecutionDrawAgainImage { get; private set; }

		[field: SerializeField]
		public GameplayTag ExecutionDrawYouChooseImage { get; private set; }

		[field: SerializeField]
		public GameplayTag ExecutionDrawCaptainChooseImage { get; private set; }

		[field: SerializeField]
		public float ExecutionCaptainChoiceDuration { get; private set; }

		[field: SerializeField]
		public GameplayTag ExecutionDrawCaptainChoseGameHistoryEntry { get; private set; }
		
		[field: SerializeField]
		public GameplayTag ExecutionDrawCaptainDidnotChoseGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag ExecutionVotedPlayerGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag ExecutionMarkForDeath { get; private set; }

		[field: Header("End Game")]
		[field: SerializeField]
		public GameplayTag EndGamePlayerGroupWonGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public string WinningPlayerGroupText { get; private set; }

		[field: SerializeField]
		public GameplayTag EndGameNobodyWonGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag NoWinnerImage { get; private set; }

		[field: SerializeField]
		public float EndGameTitleHoldDuration { get; private set; }

		[field: SerializeField]
		public string ReturnToLobbyCountdownText { get; private set; }

		[field: SerializeField]
		public float ReturnToLobbyCountdownDuration { get; private set; }

		[field: Header("Captain")]
		[field: SerializeField]
		public GameplayTag CaptainChangedGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag CaptainRevealImage { get; private set; }

		[field: SerializeField]
		public GameplayTag CaptainDiedGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag ChooseNextCaptainImage { get; private set; }

		[field: SerializeField]
		public float CaptainChoiceDuration { get; private set; }

		[field: SerializeField]
		public GameplayTag OldCaptainChoosingImage { get; private set; }

		[field: SerializeField]
		public float CaptainCardMovementDuration { get; private set; }

		[field: SerializeField]
		public AnimationCurve CaptainCardMovementXY { get; private set; }

		[field: SerializeField]
		public AnimationCurve CaptainCardMovementYOffset { get; private set; }

		[field: Header("Debate")]
		[field: SerializeField]
		public string SkipText { get; private set; }

		[field: Header("Vote")]
		[field: SerializeField]
		public string LockedInButtonText { get; private set; }

		[field: SerializeField]
		public string LockedOutButtonText { get; private set; }

		[field: SerializeField]
		public float AllLockedInDelayToEndVote { get; private set; }

		[field: SerializeField]
		public float NoVoteDuration { get; private set; }

		[field: SerializeField]
		public GameplayTag VoteVotedForGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag VoteDidNotVoteGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameplayTag VoteDidNotVoteWithPenalityGameHistoryEntry { get; private set; }

		[field: Header("Highlight Players")]
		[field: SerializeField]
		public float HighlightDuration { get; private set; }

		[field: Header("Role Reservation")]
		[field: SerializeField]
		public string ChooseRoleText { get; private set; }

		[field: SerializeField]
		public string ChooseRoleObligatoryText { get; private set; }

		[field: SerializeField]
		public string ChoosedRoleText { get; private set; }

		[field: SerializeField]
		public string DidNotChoosedRoleText { get; private set; }

		[field: Header("Role Reveal")]
		[field: SerializeField]
		public float MoveToCameraDuration { get; private set; }

		[field: SerializeField]
		public float RoleRevealDistanceToCamera { get; private set; }

		[field: SerializeField]
		public float RoleRevealWaitDuration { get; private set; }

		[field: SerializeField]
		public float RoleRevealFlipDuration { get; private set; }

		[field: SerializeField]
		public float RoleRevealHoldDuration { get; private set; }

		[field: Header("UI")]
		[field: SerializeField]
		public ImagesData ImagesData { get; private set; }

		[field: SerializeField]
		public float UITransitionNormalDuration { get; private set; }

		[field: SerializeField]
		public float UITransitionFastDuration { get; private set; }

		[field: SerializeField]
		public string CountdownText { get; private set; }

		[field: SerializeField]
		public string ConfirmChoiceText { get; private set; }

		[field: Header("Network")]
		[field: SerializeField]
		public GameplayTag PlayerLeftMarkForDeath { get; private set; }

		[field: SerializeField]
		public GameplayTag PlayerDisconnectedGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public float DisconnectedTextDuration { get; private set; }
	}
}