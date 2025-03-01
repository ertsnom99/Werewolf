using System.Collections;
using UnityEngine;
using Utilities.GameplayData;
using Werewolf.Data;

namespace Werewolf.Managers
{
	public enum Daytime
	{
		Day,
		Night,
	}

	public class DaytimeManager : MonoSingleton<DaytimeManager>
	{
		[Header("Lighting")]
		[SerializeField]
		private Light _light;

		public Daytime CurrentDaytime { get; private set; }
		private bool _inTransition = false;

		private GameConfig _gameConfig;

		private GameplayDataManager _gameplayDataManager;
		private UIManager _UIManager;

		public void Initialize(GameConfig config)
		{
			_gameConfig = config;
			_gameplayDataManager = GameplayDataManager.Instance;
			_UIManager = UIManager.Instance;

			SetDaytime(CurrentDaytime);

			if (_gameConfig.DaytimeTransitionDuration < (_gameConfig.DaytimeTextFadeInDelay + _gameConfig.UITransitionNormalDuration))
			{
				Debug.LogError($"{nameof(_gameConfig.DaytimeTransitionDuration)} most not be smaller than {_gameConfig.DaytimeTextFadeInDelay + _gameConfig.UITransitionNormalDuration}");
			}
		}

		private void SetDaytime(Daytime daytime)
		{
			CurrentDaytime = daytime;

			switch (daytime)
			{
				case Daytime.Day:
					_light.color = _gameConfig.DayColor;
					_light.colorTemperature = _gameConfig.DayTemperature;
					break;
				case Daytime.Night:
					_light.color = _gameConfig.NightColor;
					_light.colorTemperature = _gameConfig.NightTemperature;
					break;
			}
		}

		public void ChangeDaytime(Daytime daytime)
		{
			if (_inTransition || CurrentDaytime == daytime)
			{
				return;
			}

			CurrentDaytime = daytime;
			_inTransition = true;

			StartCoroutine(TransitionTitle(daytime == Daytime.Day ? _gameConfig.DayTransitionTitleScreen.ID.HashCode : _gameConfig.NightTransitionTitleScreen.ID.HashCode));
			StartCoroutine(TransitionDaytime());
		}

		private IEnumerator TransitionDaytime()
		{
			Color startingColor = _light.color;
			float startingTemperature = _light.colorTemperature;
			Color targetColor = CurrentDaytime == Daytime.Day ? _gameConfig.DayColor : _gameConfig.NightColor;
			float targetTemperature = CurrentDaytime == Daytime.Day ? _gameConfig.DayTemperature : _gameConfig.NightTemperature;

			float transitionProgress = .0f;

			while (transitionProgress < _gameConfig.DaytimeLightTransitionDuration)
			{
				yield return 0;

				transitionProgress += Time.deltaTime;
				float progressRatio = Mathf.Clamp01(transitionProgress / _gameConfig.DaytimeLightTransitionDuration);

				_light.color = Color.Lerp(startingColor, targetColor, progressRatio);
				_light.colorTemperature = Mathf.Lerp(startingTemperature, targetTemperature, progressRatio);
			}
		}

		private IEnumerator TransitionTitle(int titleID)
		{
			if (!_gameplayDataManager.TryGetGameplayData(titleID, out TitleScreenData titleScreenData))
			{
				yield break;
			}

			_UIManager.TitleScreen.Initialize(titleScreenData.Image, titleScreenData.Text);

			yield return new WaitForSeconds(_gameConfig.DaytimeTextFadeInDelay);

			_UIManager.FadeIn(_UIManager.TitleScreen, _gameConfig.UITransitionNormalDuration);

			yield return new WaitForSeconds(_gameConfig.DaytimeTransitionDuration - _gameConfig.DaytimeTextFadeInDelay - _gameConfig.UITransitionNormalDuration);

			_UIManager.FadeOutAll(_gameConfig.UITransitionNormalDuration);

			yield return new WaitForSeconds(_gameConfig.UITransitionNormalDuration);

			_inTransition = false;
		}
	}
}