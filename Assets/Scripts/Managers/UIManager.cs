using UnityEngine;
using Werewolf.UI;

namespace Werewolf
{
	public class UIManager : MonoSingleton<UIManager>
	{
		[field: Header("Screens")]
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

		[field: SerializeField]
		public DisconnectedScreen DisconnectedScreen { get; private set; }

		private FadingScreen _currentFadingScreen;

		public void FadeIn(FadingScreen fadingScreen, float transitionDuration)
		{
			_currentFadingScreen = fadingScreen;
			fadingScreen.FadeIn(transitionDuration);
		}

		public void FadeIn(float transitionDuration)
		{
			if (!_currentFadingScreen)
			{
				return;
			}

			_currentFadingScreen.FadeIn(transitionDuration);
		}

		public void FadeOut(FadingScreen fadingScreen, float transitionDuration)
		{
			_currentFadingScreen = fadingScreen;
			fadingScreen.FadeOut(transitionDuration);
		}

		public void FadeOut(float transitionDuration)
		{
			if (!_currentFadingScreen)
			{
				return;
			}

			_currentFadingScreen.FadeOut(transitionDuration);
		}

		public void SetFade(FadingScreen fadingScreen, float fade)
		{
			_currentFadingScreen = fadingScreen;
			fadingScreen.SetFade(Mathf.Clamp01(fade));
		}
	}
}