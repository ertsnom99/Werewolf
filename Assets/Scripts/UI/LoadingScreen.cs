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

        [SerializeField]
        private string _loadingText = "Waiting for server...";

        public event Action OnFadeInOver = delegate { };

        private void Start()
        {
#if UNITY_SERVER
            _imageFade.SetFade(.0f);
            _loading.text = "";
#else
            _imageFade.SetFade(1.0f);
            _loading.text = _loadingText;
#endif
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