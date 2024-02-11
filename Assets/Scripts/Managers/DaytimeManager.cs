using System.Collections;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
    public enum Daytime
    {
        Day,
        Night,
    }

    public class DaytimeManager : MonoSingleton<DaytimeManager>
    {
        [Header("Config")]
        [SerializeField]
        private GameConfig _gameConfig;

        [Header("Lighting")]
        [SerializeField]
        private Light _light;

        public Daytime CurrentDaytime { get; private set; }
        private bool _inTransition = false;

        private UIManager _UIManager;

        protected override void Awake()
        {
            base.Awake();

            if (!_gameConfig)
            {
                Debug.LogError("The GameConfig of the DayTimeManager is not defined");
            }

            CurrentDaytime = Daytime.Day;
        }

        private void Start()
        {
            _UIManager = UIManager.Instance;

            SetDaytime(CurrentDaytime);
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

            StartCoroutine(TransitionDaytime());
            StartCoroutine(TransitionTitle(_gameConfig.NightTransitionText));
        }

        private IEnumerator TransitionDaytime()
        {
            Color startingColor = _light.color;
            float startingTemperature = _light.colorTemperature;
            Color targetColor = CurrentDaytime == Daytime.Day ? _gameConfig.DayColor : _gameConfig.NightColor;
            float targetTemperature = CurrentDaytime == Daytime.Day ? _gameConfig.DayTemperature : _gameConfig.NightTemperature;

            float transitionProgress = .0f;

            while (transitionProgress < _gameConfig.DaytimeTransitionDuration)
            {
                transitionProgress += Time.deltaTime;
                float progressRatio = Mathf.Clamp01(transitionProgress / _gameConfig.DaytimeTransitionDuration);

                _light.color = Color.Lerp(startingColor, targetColor, progressRatio);
                _light.colorTemperature = Mathf.Lerp(startingTemperature, targetTemperature, progressRatio);

                yield return 0;
            }
        }

        private IEnumerator TransitionTitle(string text)
        {
            _UIManager.TitleScreen.Config(text);
            _UIManager.FadeIn(_UIManager.TitleScreen, _gameConfig.UITransitionDuration);

            yield return new WaitForSeconds(_gameConfig.UITransitionDuration + _gameConfig.DaytimeTransitionDuration);

            _UIManager.FadeOut(_gameConfig.UITransitionDuration);

            _inTransition = false;
        }
    }
}