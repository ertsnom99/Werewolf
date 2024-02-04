using Fusion;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf
{
    public class UIManager : MonoSingleton<UIManager>
    {
        [field:Header("Loading Screen")]
        [field:SerializeField]
        public LoadingScreen LoadingScreen { get; private set; }

        [Header("Middle Text")]
        [SerializeField]
        private GameObject _titleUI;
        [SerializeField]
        private TMP_Text _titleUIText;

        [Header("Middle Sprite Text")]
        [SerializeField]
        private GameObject _titleImageUI;
        [SerializeField]
        private Image _titleImageUIImage;
        [SerializeField]
        private TMP_Text _titleImageUIText;

        private TMP_Text _transitionText;
        private Image _transitionImage;
        private float _opacity;
        private IEnumerator _coroutine;

        private void Start()
        {
            _opacity = .0f;

            _titleUIText.color = new Color { r = _titleUIText.color.r, g = _titleUIText.color.g, b = _titleUIText.color.b, a = _opacity };
            _titleImageUIImage.color = new Color { r = _titleImageUIImage.color.r, g = _titleImageUIImage.color.g, b = _titleImageUIImage.color.b, a = _opacity };
            _titleImageUIText.color = new Color { r = _titleImageUIText.color.r, g = _titleImageUIText.color.g, b = _titleImageUIText.color.b, a = _opacity };
        }

        public void ShowTitleUI(string text, float transitionDuration)
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }

            _titleUI.SetActive(true);
            _titleUIText.text = text;

            _transitionText = _titleUIText;

            _coroutine = TransitionUI(1.0f, transitionDuration);
            StartCoroutine(_coroutine);
        }

        public void HideTitleUI(float transitionDuration)
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }

            _transitionText = _titleUIText;

            _coroutine = TransitionUI(.0f, transitionDuration, _titleUI);
            StartCoroutine(_coroutine);
        }

        public void ShowTitleImageUI(Sprite sprite, string text, float transitionDuration)
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }

            _titleImageUI.SetActive(true);
            _titleImageUIImage.sprite = sprite;
            _titleImageUIText.text = text;

            _transitionText = _titleImageUIText;
            _transitionImage = _titleImageUIImage;

            _coroutine = TransitionUI(1.0f, transitionDuration);
            StartCoroutine(_coroutine);
        }

        public void HideTitleImageUI(float transitionDuration)
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }

            _transitionText = _titleImageUIText;
            _transitionImage = _titleImageUIImage;

            _coroutine = TransitionUI(.0f, transitionDuration, _titleImageUI);
            StartCoroutine(_coroutine);
        }

        private IEnumerator TransitionUI(float targetOpacity, float transitionDuration, GameObject hiddenAfterTransition = null)
        {
            float startingOpacity = _opacity;
            float transitionProgress = .0f;

            while (transitionProgress < transitionDuration)
            {
                transitionProgress += Time.deltaTime;
                float progressRatio = Mathf.Clamp01(transitionProgress / transitionDuration);
                _opacity = Mathf.Lerp(startingOpacity, targetOpacity, progressRatio);

                if (_transitionText)
                {
                    _transitionText.color = new Color {r = _transitionText.color.r, g = _transitionText.color.g, b = _transitionText.color.b, a = _opacity };
                }

                if (_transitionImage)
                {
                    _transitionImage.color = new Color { r = _transitionImage.color.r, g = _transitionImage.color.g, b = _transitionImage.color.b, a = _opacity };
                }

                yield return 0;
            }

            if (hiddenAfterTransition)
            {
                hiddenAfterTransition.SetActive(false);
            }

            _transitionText = null;
            _transitionImage = null;
            _coroutine = null;
        }
    }
}