using System;
using UnityEngine;

namespace Werewolf.UI
{
	[RequireComponent(typeof(CanvasGroup))]
	public class QuitScreen : MonoBehaviour
	{
		private CanvasGroup _canvasGroup;

		public static event Action OnQuit;

		private void Awake()
		{
			_canvasGroup = GetComponent<CanvasGroup>();
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				ToggleScreen();
			}
		}

		public void ToggleScreen()
		{
			_canvasGroup.alpha = 1 - _canvasGroup.alpha;
			_canvasGroup.blocksRaycasts = !_canvasGroup.blocksRaycasts;
		}

		public void Quit()
		{
			OnQuit?.Invoke();
		}
	}
}