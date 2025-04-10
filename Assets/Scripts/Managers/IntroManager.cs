using System;
using Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

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

		public event Action IntroFinished;

		public void StartIntro()
		{
			_playableDirector.Play();
		}

		public void SkipIntro()
		{
			OnIntroFinished();
		}

		public void OnIntroFinished()
		{
			foreach(CinemachineVirtualCamera virtualCamera in _virtualCameras)
			{
				virtualCamera.gameObject.SetActive(virtualCamera == _gameVirtualCamera);
			}

			IntroFinished?.Invoke();
		}
	}
}