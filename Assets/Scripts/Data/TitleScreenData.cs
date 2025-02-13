using Utilities.GameplayData;
using UnityEngine;
using UnityEngine.Localization;

namespace Werewolf.Data
{
	[CreateAssetMenu(fileName = "TitleScreenData", menuName = "ScriptableObjects/TitleScreenData")]
	public class TitleScreenData : GameplayData
	{
		[field: SerializeField]
		[field: PreviewSprite]
		public Sprite Image { get; private set; }

		[field: SerializeField]
		public LocalizedString Text { get; private set; }

		[field: SerializeField]
		public LocalizedString PromptButtonText { get; private set; }
	}
}
