using UnityEngine;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "DaytimeConfig", menuName = "ScriptableObjects/Config/DaytimeConfig")]
	public class DaytimeConfig : ScriptableObject
	{
		[field: Header("Day")]
		[field: SerializeField]
		public Color DayColor { get; private set; }

		[field: SerializeField]
		public float DayTemperature { get; private set; }

		[field: SerializeField]
		public string DayTransitionText { get; private set; }

		[field: Header("Night")]
		[field: SerializeField]
		public Color NightColor { get; private set; }

		[field: SerializeField]
		public float NightTemperature { get; private set; }

		[field: SerializeField]
		public string NightTransitionText { get; private set; }

		[field: Header("UI")]
		[field: SerializeField]
		public float DaytimeTransitionDuration { get; private set; }

		[field: SerializeField]
		public float TextFadeInDelay { get; private set; }

		[field: SerializeField]
		public float TextTransitionDuration { get; private set; }

		[field: SerializeField]
		public float TextHoldDelay { get; private set; }
	}
}