using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Werewolf.UI
{
	public class LoadingScreen : FadingScreen
	{
		[Header("UI")]
		[SerializeField]
		private Image _image;

		[SerializeField]
		private LocalizeStringEvent _text;

		public void Initialize(LocalizedString text, Sprite image = null)
		{
			_text.StringReference = text;

			if (image)
			{
				_image.sprite = image;
			}
		}
		protected override void OnFadeStarts(float targetOpacity) { }
	}
}