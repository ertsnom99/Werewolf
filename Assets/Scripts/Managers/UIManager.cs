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
		public QuickActionScreen QuickActionScreen { get; private set; }

		[field: SerializeField]
		public RolesScreen RolesScreen { get; private set; }

		[field: SerializeField]
		public EndGameScreen EndGameScreen { get; private set; }

		[field: Header("Other Screens")]
		[field: SerializeField]
		public EmoteScreen EmoteScreen { get; private set; }

		[field: SerializeField]
		public DisconnectedScreen DisconnectedScreen { get; private set; }

		private readonly HashSet<FadingScreen> _activeFadingScreens = new();
		private readonly List<FadingScreen> _permanentScreens = new();

		protected override void Awake()
		{
			LoadingScreen.FadeFinished += OnFadeFinished;
			TitleScreen.FadeFinished += OnFadeFinished;
			ChoiceScreen.FadeFinished += OnFadeFinished;
			VoteScreen.FadeFinished += OnFadeFinished;
			EndGameScreen.FadeFinished += OnFadeFinished;
		}

		public void AddPermanentScreen(FadingScreen fadingScreen)
		{
			_permanentScreens.Add(fadingScreen);
		}

		public void FadeIn(FadingScreen fadingScreen, float transitionDuration)
		{
			_activeFadingScreens.Add(fadingScreen);
			fadingScreen.FadeIn(transitionDuration);
		}

		public void FadeOut(FadingScreen fadingScreen, float transitionDuration)
		{
			if (_activeFadingScreens.Contains(fadingScreen))
			{
				fadingScreen.FadeOut(transitionDuration);
			}
		}

		public void FadeOutAll(float transitionDuration)
		{
			if (_activeFadingScreens.Count <= 0)
			{
				return;
			}

			foreach (FadingScreen screen in _activeFadingScreens)
			{
				if (!_permanentScreens.Contains(screen))
				{
					screen.FadeOut(transitionDuration);
				}
			}
		}

		private void OnFadeFinished(FadingScreen fadingScreen, float opacity)
		{
			if (opacity <= 0)
			{
				_activeFadingScreens.Remove(fadingScreen);
			}
		}

		public void SetFade(FadingScreen fadingScreen, float fade)
		{
			if (fade > 0)
			{
				_activeFadingScreens.Add(fadingScreen);
			}
			else if (fade <= 0)
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