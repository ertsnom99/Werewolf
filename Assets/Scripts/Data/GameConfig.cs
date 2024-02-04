using UnityEngine;

namespace Werewolf.Data
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "ScriptableObjects/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [field: Header("Daytime")]
        [field:SerializeField]
        public float DaytimeTransitionDuration { get; private set; }
        [field: SerializeField]
        public Color DayColor { get; private set; }
        [field: SerializeField]
        public float DayTemperature { get; private set; }
        [field: SerializeField]
        public Color NightColor { get; private set; }
        [field: SerializeField]
        public float NightTemperature { get; private set; }

        [field:Header("Visual")]
        [field: SerializeField]
        public AnimationCurve CardsOffset { get; private set; }

        [field: SerializeField]
        public AnimationCurve CameraOffset { get; private set; }
    }
}