using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using static Werewolf.GameHistoryManager;

namespace Werewolf.UI
{
	[RequireComponent(typeof(Image))]
	public class GameHistoryEntry : MonoBehaviour
	{
		[Header("Image")]
		[SerializeField]
		private Image _image;

		[Header("Text")]
		[SerializeField]
		private LocalizeStringEvent _text;

		[Header("Background")]
		[SerializeField]
		private Color _oddColor;
		[SerializeField]
		private Color _evenColor;

		private Image _background;

		private void Awake()
		{
			_background = GetComponent<Image>();
		}

		public void Initialize(Sprite image, LocalizedString text, bool isOdd)
		{
			_image.sprite = image;
			_text.StringReference = text;
			_background.color = isOdd ? _oddColor : _evenColor;
		}
	}
}