using System.Collections;
using UnityEngine;
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

		private GameConfig _config;

		private GameplayDatabaseManager _gameplayDatabaseManager;
		private UIManager _UIManager;

		public void Initialize(GameConfig config)
		{
			_config = config;
			_gameplayDatabaseManager = GameplayDatabaseManager.Instance;
			_UIManager = UIManager.Instance;

			SetDaytime(CurrentDaytime);

			if (_config.DaytimeTransitionDuration < (_config.DaytimeTextFadeInDelay + _config.UITransitionNormalDuration))
			{
				Debug.LogError($"{nameof(_config.DaytimeTransitionDuration)} most not be smaller than {_config.DaytimeTextFadeInDelay + _config.UITransitionNormalDuration}");
			}
		}

		private void SetDaytime(Daytime daytime)
		{
			CurrentDaytime = daytime;

			switch (daytime)
			{
				case Daytime.Day:
					_light.color = _config.DayColor;
					_light.colorTemperature = _config.DayTemperature;
					break;
				case Daytime.Night:
					_light.color = _config.NightColor;
					_light.colorTemperature = _config.NightTemperature;
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

			StartCoroutine(TransitionTitle(daytime == Daytime.Day ? _config.DayTransitionImage.CompactTagId : _config.NightTransitionImage.CompactTagId));
			StartCoroutine(TransitionDaytime());
		}

		private IEnumerator TransitionDaytime()
		{
			Color startingColor = _light.color;
			float startingTemperature = _light.colorTemperature;
			Color targetColor = CurrentDaytime == Daytime.Day ? _config.DayColor : _config.NightColor;
			float targetTemperature = CurrentDaytime == Daytime.Day ? _config.DayTemperature : _config.NightTemperature;

			float transitionProgress = .0f;

			while (transitionProgress < _config.DaytimeLightTransitionDuration)
			{
				yield return 0;

				transitionProgress += Time.deltaTime;
				float progressRatio = Mathf.Clamp01(transitionProgress / _config.DaytimeLightTransitionDuration);

				_light.color = Color.Lerp(startingColor, targetColor, progressRatio);
				_light.colorTemperature = Mathf.Lerp(startingTemperature, targetTemperature, progressRatio);
			}
		}

		private IEnumerator TransitionTitle(int imageID)
		{
			ImageData imageData = _gameplayDatabaseManager.GetGameplayData<ImageData>(imageID);

			if (imageData == null)
			{
				yield break;
			}

			_UIManager.TitleScreen.Initialize(imageData.Image, imageData.Text);

			yield return new WaitForSeconds(_config.DaytimeTextFadeInDelay);

			_UIManager.FadeIn(_UIManager.TitleScreen, _config.UITransitionNormalDuration);

			yield return new WaitForSeconds(_config.DaytimeTransitionDuration - _config.DaytimeTextFadeInDelay - _config.UITransitionNormalDuration);

			_UIManager.FadeOutAll(_config.UITransitionNormalDuration);

			yield return new WaitForSeconds(_config.UITransitionNormalDuration);

			_inTransition = false;
		}
	}
}