using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
	[RequireComponent(typeof(Image))]
	public class GameHistoryButton : MonoBehaviour
	{
		[Header("Text")]
		[SerializeField]
		private TextMeshProUGUI _name;

		[SerializeField]
		private Color _normalNameColor;

		[SerializeField]
		private Color _selectedNameColor;

		[Header("Background")]
		[SerializeField]
		private Color _oddColor;

		[SerializeField]
		private Color _evenColor;

		public string FilePath { get; private set; }

		public event Action<GameHistoryButton> Clicked;

		private Image _background;

		private void Awake()
		{
			_background = GetComponent<Image>();
		}

		public void Initialize(string filePath, bool isOdd)
		{
			FilePath = filePath;
			_name.text = Path.GetFileNameWithoutExtension(filePath);

			_background.color = isOdd ? _oddColor : _evenColor;
		}

		public void OnClicked()
		{
			SetSelected(true);
			Clicked?.Invoke(this);
		}

		public void SetSelected(bool selected)
		{
			_name.color = selected? _selectedNameColor : _normalNameColor;
		}
	}
}