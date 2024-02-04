using System;
using TMPro;
using UnityEngine;

namespace Werewolf
{
    public class LoadingScreen : MonoBehaviour
    {
        [SerializeField]
        private ImageFade _imageFade;

        [SerializeField]
        private TMP_Text _loading;

        public event Action OnFadeInOver = delegate { };

        private void Start()
        {
#if UNITY_SERVER
            _imageFade.SetFade(.0f);
#else
            _imageFade.SetFade(1.0f);
#endif
            _loading.text = "";
        }

        public void SetText(string text)
        {
            _loading.text = text;
        }

        public void FadeIn()
        {
            _imageFade.OnFadeInOver += FadeInOver;
            _imageFade.FadeIn();
            _loading.text = "";
        }

        private void FadeInOver()
        {
            _imageFade.OnFadeInOver -= FadeInOver;
            OnFadeInOver();
        }
    }
}