using UnityEngine;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "GameConfig", menuName = "ScriptableObjects/Config/GameConfig")]
	public class GameConfig : ScriptableObject
	{
		[field: Header("Role Distribution")]
		[field: SerializeField]
		public int AvailableRolesMaxAttemptCount { get; private set; }

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

		[field: Header("Gameplay Loop")]
		[field: SerializeField]
		public float GameplayLoopStepDelay { get; private set; }

		[field: SerializeField]
		public float DaytimeTransitionStepDuration { get; private set; }

		[field: SerializeField]
		public float NightCallMinimumDuration { get; private set; }

		[field: SerializeField]
		public float NightCallMaximumDuration { get; private set; }

		[field: SerializeField]
		public float NightCallChangeDelay { get; private set; }

		[field: Header("Role Reveal")]
		[field: SerializeField]
		public float RevealDistanceToCamera { get; private set; }

		[field: SerializeField]
		public float MoveToCameraDuration { get; private set; }

		[field: SerializeField]
		public float WaitRevealDuration { get; private set; }

		[field: SerializeField]
		public float RevealFlipDuration { get; private set; }

		[field: SerializeField]
		public float HoldRevealDuration { get; private set; }

		[field: Header("Death Reveal")]
		[field: SerializeField]
		public string DeathRevealNoDeathText { get; private set; }

		[field: SerializeField]
		public string DeathRevealDeathText { get; private set; }

		[field: SerializeField]
		public float DeathRevealTitleHoldDuration { get; private set; }

		[field: SerializeField]
		public string PlayerDiedText { get; private set; }

		[field: SerializeField]
		public float DelayBeforeRevealingDeadPlayer { get; private set; }

		[field: Header("Election")]
		[field: SerializeField]
		public string ElectionPromptTitleText { get; private set; }

		[field: SerializeField]
		public string ElectionPromptButtonText { get; private set; }

		[field: SerializeField]
		public float ElectionPromptDuration { get; private set; }

		[field: SerializeField]
		public string ElectionMultipleCandidateText { get; private set; }

		[field: SerializeField]
		public string ElectionDebateText { get; private set; }

		[field: SerializeField]
		public float ElectionVoteDuration { get; private set; }

		[field: SerializeField]
		public string ElectionNoCandidateText { get; private set; }

		[field: SerializeField]
		public float ElectionNoCandidateDuration { get; private set; }

		[field: Header("Execution")]
		[field: SerializeField]
		public string ExecutionDebateText { get; private set; }

		[field: SerializeField]
		public float ExecutionVoteDuration { get; private set; }

		[field: SerializeField]
		public string ExecutionDrawNewVoteText { get; private set; }

		[field: SerializeField]
		public string ExecutionDrawAgainText { get; private set; }

		[field: SerializeField]
		public float ExecutionTitleHoldDuration { get; private set; }

		[field: SerializeField]
		public string ExecutionDrawYouChooseText { get; private set; }

		[field: SerializeField]
		public string ExecutionDrawCaptainChooseText { get; private set; }

		[field: SerializeField]
		public float ExecutionCaptainChoiceDuration { get; private set; }

		[field: SerializeField]
		public string ExecutionMarkForDeath { get; private set; }

		[field: Header("Debate")]
		[field: SerializeField]
		public string SkipText { get; private set; }

		[field: SerializeField]
		public float DebateStepDuration { get; private set; }

		[field: Header("Captain")]
		[field: SerializeField]
		public string CaptainRevealText { get; private set; }

		[field: SerializeField]
		public string ChooseNextCaptainText { get; private set; }

		[field: SerializeField]
		public float CaptainChoiceDuration { get; private set; }

		[field: SerializeField]
		public string OldCaptainChoosingText { get; private set; }

		[field: SerializeField]
		public float CaptainCardMovementDuration { get; private set; }

		[field: SerializeField]
		public AnimationCurve CaptainCardMovementXY { get; private set; }

		[field: SerializeField]
		public AnimationCurve CaptainCardMovementYOffset { get; private set; }

		[field: Header("Highlight")]
		[field: SerializeField]
		public float HighlightDuration { get; private set; }

		[field: Header("Vote")]
		[field: SerializeField]
		public string LockedInButtonText { get; private set; }

		[field: SerializeField]
		public string LockedOutButtonText { get; private set; }

		[field: SerializeField]
		public float AllLockedInDelayToEndVote { get; private set; }

		[field: SerializeField]
		public float NoVoteDuration { get; private set; }

		[field: Header("UI")]
		[field: SerializeField]
		public string CountdownText { get; private set; }

		[field: SerializeField]
		public float UITransitionNormalDuration { get; private set; }

		[field: SerializeField]
		public float UITransitionFastDuration { get; private set; }

		[field: Header("Loading Screen")]
		[field: SerializeField]
		public string LoadingScreenText { get; private set; }

		[field: SerializeField]
		public float LoadingScreenTransitionDuration { get; private set; }

		[field: Header("UI Text")]

		[field: SerializeField]
		public string RolePlayingTextSingular { get; private set; }

		[field: SerializeField]
		public string RolePlayingTextPlurial { get; private set; }

		[field: SerializeField]
		public string SkipTurnText { get; private set; }

		[field: SerializeField]
		public string ChooseRoleText { get; private set; }

		[field: SerializeField]
		public string ChooseRoleObligatoryText { get; private set; }

		[field: SerializeField]
		public string ChoosedRoleText { get; private set; }

		[field: SerializeField]
		public string DidNotChoosedRoleText { get; private set; }

		public const int MAX_PLAYER_COUNT = 20;
	}
}