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
        private DaytimeConfig _config;

        [Header("Lighting")]
        [SerializeField]
        private Light _light;

        public Daytime CurrentDaytime { get; private set; }
        private bool _inTransition = false;

        private UIManager _UIManager;

        protected override void Awake()
        {
            base.Awake();

            if (!_config)
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

            StartCoroutine(TransitionDaytime());
            StartCoroutine(TransitionTitle(_config.NightTransitionText));
        }

        private IEnumerator TransitionDaytime()
        {
            Color startingColor = _light.color;
            float startingTemperature = _light.colorTemperature;
            Color targetColor = CurrentDaytime == Daytime.Day ? _config.DayColor : _config.NightColor;
            float targetTemperature = CurrentDaytime == Daytime.Day ? _config.DayTemperature : _config.NightTemperature;

            float transitionProgress = .0f;

            while (transitionProgress < _config.DaytimeTransitionDuration)
            {
                transitionProgress += Time.deltaTime;
                float progressRatio = Mathf.Clamp01(transitionProgress / _config.DaytimeTransitionDuration);

                _light.color = Color.Lerp(startingColor, targetColor, progressRatio);
                _light.colorTemperature = Mathf.Lerp(startingTemperature, targetTemperature, progressRatio);

                yield return 0;
            }
        }

        private IEnumerator TransitionTitle(string text)
        {
            _UIManager.TitleScreen.Initialize(text);

            yield return new WaitForSeconds(_config.TextFadeInDelay);

            _UIManager.FadeIn(_UIManager.TitleScreen, _config.TextTransitionDuration);

            yield return new WaitForSeconds(_config.TextHoldDelay);

            _UIManager.FadeOut(_config.TextTransitionDuration);

            _inTransition = false;
        }
    }
}