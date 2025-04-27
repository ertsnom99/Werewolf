using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
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
		private Light _mainLight;
		[SerializeField]
		private Light[] _lampLights;
#if UNITY_EDITOR
		[Header("Debug")]
		[SerializeField]
		private GameConfig _configForEditor;
#endif
		public Daytime CurrentDaytime { get; private set; }

		private GameConfig _gameConfig;
		private Material _skyboxMaterial;
		private ReflectionProbe _baker;
		private bool _inTitleTransition = false;

		private GameplayDataManager _gameplayDataManager;
		private UIManager _UIManager;

		public void Initialize(GameConfig config)
		{
			_gameConfig = config;
			_skyboxMaterial = RenderSettings.skybox;

			_baker = gameObject.AddComponent<ReflectionProbe>();
			_baker.cullingMask = 0;
			_baker.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
			_baker.mode = ReflectionProbeMode.Realtime;
			_baker.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
			RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

			_gameplayDataManager = GameplayDataManager.Instance;
			_UIManager = UIManager.Instance;

			if (_gameConfig.DaytimeTransitionDuration < (_gameConfig.DaytimeTextFadeInDelay + _gameConfig.UITransitionNormalDuration))
			{
				Debug.LogError($"{nameof(_gameConfig.DaytimeTransitionDuration)} most not be smaller than {_gameConfig.DaytimeTextFadeInDelay + _gameConfig.UITransitionNormalDuration}");
			}
		}

#if UNITY_EDITOR
		public void InitializeForEditor()
		{
			_skyboxMaterial = RenderSettings.skybox;
			_gameConfig = _configForEditor;
		}
#endif
		public void SetDaytime(Daytime daytime)
		{
			CurrentDaytime = daytime;

			switch (daytime)
			{
				case Daytime.Day:
					_skyboxMaterial.SetColor(_gameConfig.SkyHorizonColorParameter, _gameConfig.DaySkyHorizonColor);
					_skyboxMaterial.SetColor(_gameConfig.SkyColorParameter, _gameConfig.DaySkyColor);
					_mainLight.color = _gameConfig.DayColor;
					_mainLight.colorTemperature = _gameConfig.DayTemperature;
					UpdateLampLights(_gameConfig.LampLightsDayIntensity);
					break;
				case Daytime.Night:
					_skyboxMaterial.SetColor(_gameConfig.SkyHorizonColorParameter, _gameConfig.NightSkyHorizonColor);
					_skyboxMaterial.SetColor(_gameConfig.SkyColorParameter, _gameConfig.NightSkyColor);
					_mainLight.color = _gameConfig.NightColor;
					_mainLight.colorTemperature = _gameConfig.NightTemperature;
					UpdateLampLights(_gameConfig.LampLightsNightIntensity);
					break;
			}

			if (_baker)
			{
				StartCoroutine(UpdateEnvironment());
			}
		}

		public void ChangeDaytime(Daytime daytime, bool showTitle = true)
		{
			if (_inTitleTransition || CurrentDaytime == daytime)
			{
				return;
			}

			CurrentDaytime = daytime;

			StartCoroutine(TransitionDaytime());
			
			if (showTitle)
			{
				_inTitleTransition = true;
				StartCoroutine(TransitionTitle(daytime == Daytime.Day ? _gameConfig.DayTransitionTitleScreen.ID.HashCode : _gameConfig.NightTransitionTitleScreen.ID.HashCode));
			}
		}

		private IEnumerator TransitionDaytime()
		{
			Color startingSkyHorizonColor = _skyboxMaterial.GetColor(_gameConfig.SkyHorizonColorParameter);
			Color startingSkyColor = _skyboxMaterial.GetColor(_gameConfig.SkyColorParameter);
			Color startingColor = _mainLight.color;
			float startingTemperature = _mainLight.colorTemperature;
			float startingLampLightsIntensity = _lampLights.Length > 0 ? _lampLights[0].intensity : 0;

			Color targetSkyHorizonColor = CurrentDaytime == Daytime.Day ? _gameConfig.DaySkyHorizonColor : _gameConfig.NightSkyHorizonColor;
			Color targetSkyColor = CurrentDaytime == Daytime.Day ? _gameConfig.DaySkyColor : _gameConfig.NightSkyColor;
			Color targetColor = CurrentDaytime == Daytime.Day ? _gameConfig.DayColor : _gameConfig.NightColor;
			float targetTemperature = CurrentDaytime == Daytime.Day ? _gameConfig.DayTemperature : _gameConfig.NightTemperature;
			float targetLampLightsIntensity = _lampLights.Length > 0 ? (CurrentDaytime == Daytime.Day ? _gameConfig.LampLightsDayIntensity : _gameConfig.LampLightsNightIntensity) : 0;

			float transitionProgress = .0f;

			while (transitionProgress < _gameConfig.DaytimeLightTransitionDuration)
			{
				yield return 0;

				transitionProgress += Time.deltaTime;
				float progressRatio = Mathf.Clamp01(transitionProgress / _gameConfig.DaytimeLightTransitionDuration);

				_skyboxMaterial.SetColor(_gameConfig.SkyHorizonColorParameter, Color.Lerp(startingSkyHorizonColor, targetSkyHorizonColor, progressRatio));
				_skyboxMaterial.SetColor(_gameConfig.SkyColorParameter, Color.Lerp(startingSkyColor, targetSkyColor, progressRatio));
				_mainLight.color = Color.Lerp(startingColor, targetColor, progressRatio);
				_mainLight.colorTemperature = Mathf.Lerp(startingTemperature, targetTemperature, progressRatio);
				UpdateLampLights(Mathf.Lerp(startingLampLightsIntensity, targetLampLightsIntensity, progressRatio));

				StartCoroutine(UpdateEnvironment());
			}
		}

		private void UpdateLampLights(float intensity)
		{
			foreach (Light lampLight in _lampLights)
			{
				lampLight.intensity = intensity;
			}
		}

		private IEnumerator UpdateEnvironment()
		{
			DynamicGI.UpdateEnvironment();
			_baker.RenderProbe();
			yield return new WaitForEndOfFrame();
			RenderSettings.customReflectionTexture = _baker.texture;
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

			_inTitleTransition = false;
		}
	}
}