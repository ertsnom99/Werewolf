using Fusion;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
	public class EmoteScreen : MonoBehaviour
	{
		[field: Header("UI")]
		[SerializeField]
		private GameObject _cache;
		[SerializeField]
		private RectTransform _emotes;
		[SerializeField]
		private EmoteButton[] _emoteButtons;

		private PlayerRef _selectedPlayer;

		public event Action<PlayerRef, int> EmoteSelected;

		[Serializable]
		private struct EmoteButton
		{
			public Button Button;
			public Image Image;
		}

		private Camera _camera;

		private void Start()
		{
			_camera = Camera.main;
		}

		public void SetEmotes(Sprite[] emotes)
		{
			for (int i = 0; i < _emoteButtons.Length; i++)
			{
				if (i >= emotes.Length)
				{
					_emoteButtons[i].Button.gameObject.SetActive(false);
					continue;
				}

				_emoteButtons[i].Image.sprite = emotes[i];
			}

			if (_emoteButtons.Length < emotes.Length)
			{
				Debug.LogError($"Need {emotes.Length} buttons for the emotes");
			}
		}

		public void ShowEmoteSelection(PlayerRef selectedPlayer, Vector3 worldPosition)
		{
			_selectedPlayer = selectedPlayer;
			_cache.SetActive(true);

			var screenPoint = _camera.WorldToScreenPoint(worldPosition);
			_emotes.transform.position = new Vector3 (screenPoint.x, screenPoint.y, _emotes.transform.position.z);
		}

		public void SelectEmote(Button button)
		{
			for (int i = 0; i < _emoteButtons.Length; i++)
			{
				if (_emoteButtons[i].Button == button)
				{
					EmoteSelected?.Invoke(_selectedPlayer, i);
				}
			}

			HideEmoteSelection();
		}

		public void HideEmoteSelection()
		{
			StartCoroutine(DelayHideEmoteSelection());
		}

		private IEnumerator DelayHideEmoteSelection()
		{
			yield return 0;

			_cache.SetActive(false);
		}
	}
}