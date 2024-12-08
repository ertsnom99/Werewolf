using Assets.Scripts.Data;
using UnityEngine;
using UnityEngine.Localization;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "ChoiceScreenData", menuName = "ScriptableObjects/ChoiceScreenData")]
	public class ChoiceScreenData : GameplayData
	{
		[field: SerializeField]
		public LocalizedString ChooseText { get; private set; }

		[field: SerializeField]
		public LocalizedString ChoosedText { get; private set; }

		[field: SerializeField]
		public LocalizedString DidNotChoosedText { get; private set; }
	}
}
