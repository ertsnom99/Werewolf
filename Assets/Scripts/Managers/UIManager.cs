using System.Collections.Generic;
using UnityEngine;
using Werewolf.UI;

namespace Werewolf.Managers
{
	public class UIManager : MonoSingleton<UIManager>
	{
		[field: Header("Fading Screens")]
		[field: SerializeField]
		public LoadingScreen LoadingScreen { get; private set; }

		[field: SerializeField]
		public TitleScreen TitleScreen { get; private set; }

		[field: SerializeField]
		public ChoiceScreen ChoiceScreen { get; private set; }

		[field: SerializeField]
		public VoteScreen VoteScreen { get; private set; }

		[field: SerializeField]
		public EndGameScreen EndGameScreen { get; private set; }

		[field: Header("Other Screens")]
		[field: SerializeField]
		public RolesScreen RolesScreen { get; private set; }

		[field: SerializeField]
		public EmoteScreen EmoteScreen { get; private set; }

		[field: SerializeField]
		public DisconnectedScreen DisconnectedScreen { get; private set; }

		private readonly List<FadingScreen> _activeFadingScreens = new();

		protected override void Awake()
		{
			LoadingScreen.FadeFinished += OnFadeFinished;
			TitleScreen.FadeFinished += OnFadeFinished;
			ChoiceScreen.FadeFinished += OnFadeFinished;
			VoteScreen.FadeFinished += OnFadeFinished;
			EndGameScreen.FadeFinished += OnFadeFinished;
		}

		public void FadeIn(FadingScreen fadingScreen, float transitionDuration)
		{
			if (!_activeFadingScreens.Contains(fadingScreen))
			{
				_activeFadingScreens.Add(fadingScreen);
			}

			fadingScreen.FadeIn(transitionDuration);
		}

		public void FadeOut(FadingScreen fadingScreen, float transitionDuration)
		{
			if (!_activeFadingScreens.Contains(fadingScreen))
			{
				return;
			}

			fadingScreen.FadeOut(transitionDuration);
		}

		public void FadeOutAll(float transitionDuration)
		{
			if (_activeFadingScreens.Count <= 0)
			{
				return;
			}

			foreach (FadingScreen screen in _activeFadingScreens)
			{
				screen.FadeOut(transitionDuration);
			}
		}

		private void OnFadeFinished(FadingScreen fadingScreen, float opacity)
		{
			if (!_activeFadingScreens.Contains(fadingScreen) || opacity > 0)
			{
				return;
			}

			_activeFadingScreens.Remove(fadingScreen);
		}

		public void SetFade(FadingScreen fadingScreen, float fade)
		{
			if (fade > 0 && !_activeFadingScreens.Contains(fadingScreen))
			{
				_activeFadingScreens.Add(fadingScreen);
			}
			else if (fade <= 0 && _activeFadingScreens.Contains(fadingScreen))
			{
				_activeFadingScreens.Remove(fadingScreen);
			}

			fadingScreen.SetFade(Mathf.Clamp01(fade));
		}

		private void OnDestroy()
		{
			LoadingScreen.FadeFinished -= OnFadeFinished;
			TitleScreen.FadeFinished -= OnFadeFinished;
			ChoiceScreen.FadeFinished -= OnFadeFinished;
			VoteScreen.FadeFinished -= OnFadeFinished;
			EndGameScreen.FadeFinished -= OnFadeFinished;
		}
	}
}