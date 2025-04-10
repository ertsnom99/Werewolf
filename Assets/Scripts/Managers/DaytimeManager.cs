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

		private Material _skyboxMaterial;
		private GameConfig _gameConfig;
		private bool _inTransition = false;

		private GameplayDataManager _gameplayDataManager;
		private UIManager _UIManager;

		public void Initialize(GameConfig config)
		{
			_skyboxMaterial = RenderSettings.skybox;
			_gameConfig = config;
			_gameplayDataManager = GameplayDataManager.Instance;
			_UIManager = UIManager.Instance;

			CurrentDaytime = Daytime.Night;
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
					_skyboxMaterial.SetColor(_gameConfig.SkyHorizonColorParameter, _gameConfig.DaySkyHorizonColor);
					_skyboxMaterial.SetColor(_gameConfig.SkyColorParameter, _gameConfig.DaySkyColor);
					_light.color = _gameConfig.DayColor;
					_light.colorTemperature = _gameConfig.DayTemperature;
					break;
				case Daytime.Night:
					_skyboxMaterial.SetColor(_gameConfig.SkyHorizonColorParameter, _gameConfig.NightSkyHorizonColor);
					_skyboxMaterial.SetColor(_gameConfig.SkyColorParameter, _gameConfig.NightSkyColor);
					_light.color = _gameConfig.NightColor;
					_light.colorTemperature = _gameConfig.NightTemperature;
					break;
			}
		}

		public void ChangeDaytime(Daytime daytime, bool showTitle = true)
		{
			if (_inTransition || CurrentDaytime == daytime)
			{
				return;
			}

			CurrentDaytime = daytime;
			_inTransition = true;

			StartCoroutine(TransitionDaytime());
			
			if (showTitle)
			{
				StartCoroutine(TransitionTitle(daytime == Daytime.Day ? _gameConfig.DayTransitionTitleScreen.ID.HashCode : _gameConfig.NightTransitionTitleScreen.ID.HashCode));
			}
		}

		private IEnumerator TransitionDaytime()
		{
			Color startingSkyHorizonColor = _skyboxMaterial.GetColor(_gameConfig.SkyHorizonColorParameter);
			Color startingSkyColor = _skyboxMaterial.GetColor(_gameConfig.SkyColorParameter);
			Color startingColor = _light.color;
			float startingTemperature = _light.colorTemperature;

			Color targetSkyHorizonColor = CurrentDaytime == Daytime.Day ? _gameConfig.DaySkyHorizonColor : _gameConfig.NightSkyHorizonColor;
			Color targetSkyColor = CurrentDaytime == Daytime.Day ? _gameConfig.DaySkyColor : _gameConfig.NightSkyColor;
			Color targetColor = CurrentDaytime == Daytime.Day ? _gameConfig.DayColor : _gameConfig.NightColor;
			float targetTemperature = CurrentDaytime == Daytime.Day ? _gameConfig.DayTemperature : _gameConfig.NightTemperature;

			float transitionProgress = .0f;

			while (transitionProgress < _gameConfig.DaytimeLightTransitionDuration)
			{
				yield return 0;

				transitionProgress += Time.deltaTime;
				float progressRatio = Mathf.Clamp01(transitionProgress / _gameConfig.DaytimeLightTransitionDuration);

				_skyboxMaterial.SetColor(_gameConfig.SkyHorizonColorParameter, Color.Lerp(startingSkyHorizonColor, targetSkyHorizonColor, progressRatio));
				_skyboxMaterial.SetColor(_gameConfig.SkyColorParameter, Color.Lerp(startingSkyColor, targetSkyColor, progressRatio));
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