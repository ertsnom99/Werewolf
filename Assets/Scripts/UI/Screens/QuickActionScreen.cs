using System;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
	public class QuickActionScreen : FadingScreen
	{
		[Header("UI")]
		[SerializeField]
		private Button _button;

		[SerializeField]
		private Image _icon;

		public event Action QuickActionTriggered;

		public void Initialize(Sprite icon)
		{
			_icon.sprite = icon;
			_button.interactable = true;
		}

		public void OnQuickAction()
		{
			_button.interactable = false;
			QuickActionTriggered?.Invoke();
		}

		protected override void OnFadeStarts(float targetOpacity) { }
	}
}
