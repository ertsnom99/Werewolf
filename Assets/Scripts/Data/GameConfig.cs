using UnityEngine;

namespace Werewolf.Data
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "ScriptableObjects/Config/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [field: Header("Role Distribution")]
        [field: SerializeField]
        public int AvailableRolesMaxAttemptCount { get; private set; }

        [field:Header("Visual")]
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

        [field: Header("Loading Screen")]
        [field: SerializeField]
        public string LoadingScreenText { get; private set; }

        [field: SerializeField]
        public float LoadingScreenTransitionDuration { get; private set; }

        [field: Header("UI")]
        [field: SerializeField]
        public float UITransitionDuration { get; private set; }

        [field: Header("UI Text")]

        [field: SerializeField]
        public string RolePlayingTextSingular { get; private set; }

        [field: SerializeField]
        public string RolePlayingTextPlurial { get; private set; }

        [field: SerializeField]
        public string ChooseRoleText { get; private set; }

        [field: SerializeField]
        public string ChooseRoleTextObligatory { get; private set; }
    }
}