using System.Data;
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
		public AnimationCurve CardsOffset { get; private set; }

		[field: SerializeField]
		public float ReservedRolesSpacing { get; private set; }

		[field: SerializeField]
		public AnimationCurve CameraOffset { get; private set; }

		[field: Header("Gameplay Loop")]
		[field: SerializeField]
		public float DaytimeTransitionStepDuration { get; private set; }

		[field: SerializeField]
		public float NightCallMinimumDuration { get; private set; }

		[field: SerializeField]
		public float NightCallMaximumDuration { get; private set; }

		[field: SerializeField]
		public float NightCallChangeDuration { get; private set; }

		[field: Header("Role Reveal")]
		[field: SerializeField]
		public float RevealDistanceToCamera { get; private set; }

		[field: SerializeField]
		public float RevealToCameraDuration { get; private set; }

		[field: SerializeField]
		public float HoldRevealDuration { get; private set; }

		[field: Header("Vote")]
		[field: SerializeField]
		public string VoteCountdownText { get; private set; }

		[field: SerializeField]
		public string LockedInButtonText { get; private set; }

		[field: SerializeField]
		public string LockedOutButtonText { get; private set; }

		[field: SerializeField]
		public float AllLockedInDelayToEndVote { get; private set; }

		public const int MAX_PLAYER_COUNT = 20;

		[field: Header("UI")]
		[field: SerializeField]
		public float UITransitionDuration { get; private set; }

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
	}
}