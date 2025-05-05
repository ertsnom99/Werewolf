using UnityEngine;
using UnityEngine.Localization;
using Werewolf.Gameplay;
using Werewolf.UI;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "GameConfig", menuName = "ScriptableObjects/GameConfig")]
	public class GameConfig : ScriptableObject
	{
		[field: Header("Players")]
		[field: SerializeField]
		public int MinNicknameCharacterCount { get; private set; }

		[field: Header("Game Setup")]
		[field: SerializeField]
		public int MinPlayerCount { get; private set; }

		[field: SerializeField]
		public RoleData DefaultRole { get; private set; }

		[field: SerializeField]
		public LocalizedString TooManyMandatoryRolesWarning { get; private set; }

		[field: SerializeField]
		public LocalizedString NeedOneWerewolfWarning { get; private set; }

		[field: Header("Loading Screen")]
		[field: SerializeField]
		public LocalizedString WaitingForServerText { get; private set; }

		[field: SerializeField]
		public float LoadingScreenTransitionDuration { get; private set; }

		[field: Header("Intro")]
		[field: SerializeField]
		public float PostIntroDelay { get; private set; }

		[field: Header("Cards")]
		[field: SerializeField]
		public Card CardPrefab { get; private set; }

		[field: SerializeField]
		public float ReservedRolesSpacing { get; private set; }

		[field: SerializeField]
		public GameObject CaptainCardPrefab { get; private set; }

		[field: SerializeField]
		public Vector3 CaptainCardOffset { get; private set; }

		[field: Header("Role Distribution")]
		[field: SerializeField]
		public int AvailableRolesMaxAttemptCount { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData PlayerGivenRoleGameHistoryEntry { get; private set; }

		[field: Header("Game Speed")]
		[field: SerializeField]
		public float[] GameSpeedModifier { get; private set; }

		[field: Header("Gameplay Loop")]
		[field: SerializeField]
		public float GameplayLoopStepDelay { get; private set; }

		[field: Header("Given Role Reveal")]
		[field: SerializeField]
		public LocalizedString GivenRoleText { get; private set; }

		[field: Header("Election")]
		[field: SerializeField]
		public TitleScreenData ElectionPromptTitleScreen { get; private set; }

		[field: SerializeField]
		public float ElectionPromptDuration { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData ElectionMultipleCandidatesGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public TitleScreenData ElectionMultipleCandidatesTitleScreen { get; private set; }

		[field: SerializeField]
		public float ElectionMultipleCandidatesDuration { get; private set; }

		[field: SerializeField]
		public TitleScreenData ElectionDebateTitleScreen { get; private set; }

		[field: SerializeField]
		public float ElectionDebateDuration { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData ElectionNoCandidateGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public TitleScreenData ElectionNoCandidateTitleScreen { get; private set; }

		[field: SerializeField]
		public float ElectionNoCandidateDuration { get; private set; }

		[field: SerializeField]
		public TitleScreenData ElectionVoteTitleScreen { get; private set; }

		[field: SerializeField]
		public float ElectionVoteDuration { get; private set; }

		[field: Header("Daytime")]
		[field: SerializeField]
		public GameHistoryEntryData SunSetGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData SunRoseGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public float DaytimeTransitionDuration { get; private set; }

		[field: SerializeField]
		public float DaytimeTextFadeInDelay { get; private set; }

		[field: SerializeField]
		public float DaytimeLightTransitionDuration { get; private set; }

		[field: SerializeField]
		public string SkyHorizonColorParameter { get; private set; }

		[field: SerializeField]
		public string SkyColorParameter { get; private set; }

		[field: SerializeField]
		public Color DaySkyHorizonColor { get; private set; }

		[field: SerializeField]
		public Color DaySkyColor { get; private set; }

		[field: SerializeField]
		public Color DayColor { get; private set; }

		[field: SerializeField]
		public float DayTemperature { get; private set; }

		[field: SerializeField]
		public TitleScreenData DayTransitionTitleScreen { get; private set; }

		[field: SerializeField]
		public Color NightSkyHorizonColor { get; private set; }

		[field: SerializeField]
		public Color NightSkyColor { get; private set; }

		[field: SerializeField]
		public Color NightColor { get; private set; }

		[field: SerializeField]
		public float NightTemperature { get; private set; }

		[field: SerializeField]
		public float LampLightsDayIntensity { get; private set; }

		[field: SerializeField]
		public float LampLightsNightIntensity { get; private set; }

		[field: SerializeField]
		public TitleScreenData NightTransitionTitleScreen { get; private set; }

		[field: Header("NightCall")]
		[field: SerializeField]
		public GameHistoryEntryData WokeUpPlayerGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public LocalizedString RolePlayingText { get; private set; }

		[field: SerializeField]
		public float NightCallMinimumDuration { get; private set; }

		[field: SerializeField]
		public float NightCallChangeDelay { get; private set; }

		[field: Header("Death Reveal")]
		[field: SerializeField]
		public TitleScreenData DeathRevealSomeoneDiedTitleScreen { get; private set; }

		[field: SerializeField]
		public TitleScreenData DeathRevealNoOneDiedTitleScreen { get; private set; }

		[field: SerializeField]
		public float DeathRevealHoldDuration { get; private set; }

		[field: SerializeField]
		public float DelayBeforeRevealingDeadPlayer { get; private set; }

		[field: SerializeField]
		public TitleScreenData PlayerDiedTitleScreen { get; private set; }

		[field: SerializeField]
		public TitleScreenData PlayerExecutedTitleScreen { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData PlayerDiedGameHistoryEntry { get; private set; }

		[field: Header("Execution")]
		[field: SerializeField]
		public TitleScreenData ExecutionDebateTitleScreen { get; private set; }

		[field: SerializeField]
		public float ExecutionDebateDuration { get; private set; }

		[field: SerializeField]
		public TitleScreenData ExecutionVoterTitleScreen { get; private set; }

		[field: SerializeField]
		public TitleScreenData ExecutionSpectatorTitleScreen { get; private set; }

		[field: SerializeField]
		public float ExecutionVoteDuration { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData ExecutionVoteStartedGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public TitleScreenData ExecutionDrawNewVoteTitleScreen { get; private set; }

		[field: SerializeField]
		public float ExecutionHoldDuration { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData ExecutionDrawNewVoteGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData ExecutionDrawAgainGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public TitleScreenData ExecutionDrawAgainTitleScreen { get; private set; }

		[field: SerializeField]
		public TitleScreenData ExecutionDrawYouChooseTitleScreen { get; private set; }

		[field: SerializeField]
		public TitleScreenData ExecutionDrawCaptainChooseTitleScreen { get; private set; }

		[field: SerializeField]
		public float ExecutionCaptainChoiceDuration { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData ExecutionDrawCaptainChoseGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData ExecutionDrawCaptainDidnotChoseGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData ExecutionVotedPlayerGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public MarkForDeathData ExecutionMarkForDeath { get; private set; }

		[field: SerializeField]
		public float ExecutionVotedPlayerDuration { get; private set; }

		[field: Header("End Game")]
		[field: SerializeField]
		public GameHistoryEntryData EndGamePlayerGroupWonGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public LocalizedString PlayerGroupWonText { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData EndGameNobodyWonGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public TitleScreenData NoWinnerTitleScreen { get; private set; }

		[field: SerializeField]
		public float EndGameHoldDuration { get; private set; }

		[field: SerializeField]
		public float ReturnToLobbyCountdownDuration { get; private set; }

		[field: Header("Captain")]
		[field: SerializeField]
		public GameHistoryEntryData CaptainChangedGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public float CaptainRevealDuration { get; private set; }

		[field: SerializeField]
		public TitleScreenData CaptainRevealTitleScreen { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData CaptainDiedGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public TitleScreenData ChooseNextCaptainTitleScreen { get; private set; }

		[field: SerializeField]
		public float CaptainChoiceDuration { get; private set; }

		[field: SerializeField]
		public TitleScreenData OldCaptainChoosingTitleScreen { get; private set; }

		[field: SerializeField]
		public float CaptainCardMovementDuration { get; private set; }

		[field: SerializeField]
		public AnimationCurve CaptainCardMovementXY { get; private set; }

		[field: SerializeField]
		public AnimationCurve CaptainCardMovementYOffset { get; private set; }

		[field: Header("Vote")]
		[field: SerializeField]
		public float AllVotedDelayToEndVote { get; private set; }

		[field: SerializeField]
		public float NoVoteDuration { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData VoteVotedForGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData VoteDidNotVoteGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData VoteDidNotVoteWithPenalityGameHistoryEntry { get; private set; }

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

		[field: Header("Emotes")]
		[field: SerializeField]
		public Sprite[] Emotes { get; private set; }

		[field: SerializeField]
		public Emote EmotePrefab { get; private set; }

		[field: SerializeField]
		public float EmoteMaxDistance { get; private set; }

		[field: SerializeField]
		public Vector3 EmoteGlobalOffset { get; private set; }

		[field: SerializeField]
		public float EmoteDelay { get; private set; }

		[field: SerializeField]
		public int EmoteLimit { get; private set; }

		[field: Header("UI")]
		[field: SerializeField]
		public float UITransitionNormalDuration { get; private set; }

		[field: SerializeField]
		public float UITransitionFastDuration { get; private set; }

		[field: SerializeField]
		public LocalizedString SkipChoiceText { get; private set; }

		[field: SerializeField]
		public LocalizedString MustChooseText { get; private set; }

		[field: SerializeField]
		public LocalizedString ConfirmChoiceText { get; private set; }

		[field: Header("Network")]
		[field: SerializeField]
		public MarkForDeathData PlayerLeftMarkForDeath { get; private set; }

		[field: SerializeField]
		public GameHistoryEntryData PlayerDisconnectedGameHistoryEntry { get; private set; }

		[field: SerializeField]
		public float DisconnectedTextDuration { get; private set; }

		public const int MAX_PLAYER_COUNT = 24;
		public const int MAX_NICKNAME_CHARACTER_COUNT = 30;
		public const int MAX_ROLE_SETUP_COUNT = 50;
		public const int MAX_ROLE_SETUP_POOL_COUNT = 4;
	}
}