using System;
using Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using Werewolf.Data;

namespace Werewolf.Managers
{
	public class IntroManager : MonoSingleton<IntroManager>
	{
		[Header("Cinematic")]
		[SerializeField]
		private PlayableDirector _playableDirector;

		[Header("Camera")]
		[SerializeField]
		private CinemachineBrain _cameraBrain;

		[SerializeField]
		private CinemachineVirtualCamera[] _virtualCameras;

		[SerializeField]
		private CinemachineVirtualCamera _gameVirtualCamera;

		private GameConfig _gameConfig;

		private GameManager _gameManager;
		private UIManager _UIManager;
		private EmotesManager _emotesManager;

		public event Action IntroFinished;

		public void SetConfig(GameConfig config)
		{
			_gameConfig = config;
		}

		public void StartIntro()
		{
			GetManagers();

			_gameManager.DisplayCards(false);
			_UIManager.SetFade(_UIManager.RolesScreen, .0f);
			_emotesManager.EnableShowEmoteSelection(false);

			_playableDirector.Play();
		}

		public void SkipIntro()
		{
			GetManagers();
			OnIntroFinished();
		}

		private void GetManagers()
		{
			if (!_gameManager)
			{
				_gameManager = GameManager.Instance;
			}

			if (!_UIManager)
			{
				_UIManager = UIManager.Instance;
			}

			if (!_emotesManager)
			{
				_emotesManager = EmotesManager.Instance;
			}
		}

		public void OnCardsAppear()
		{
			_gameManager.DisplayCards(true);
		}

		public void OnIntroFinished()
		{
			_UIManager.FadeIn(_UIManager.RolesScreen, _gameConfig.UITransitionFastDuration);
			_emotesManager.EnableShowEmoteSelection(true);

			foreach(CinemachineVirtualCamera virtualCamera in _virtualCameras)
			{
				virtualCamera.gameObject.SetActive(virtualCamera == _gameVirtualCamera);
			}

			IntroFinished?.Invoke();
		}
	}
}