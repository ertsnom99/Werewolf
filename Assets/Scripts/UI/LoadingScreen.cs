using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
    public class LoadingScreen : FadingScreen
    {
        [Header("UI")]
        [SerializeField]
        private Image _image;

        [SerializeField]
        private TMP_Text _loading;

        public void Config(string text, Sprite image = null)
        {
            _loading.text = text;

            if (image)
            {
                _image.sprite = image;
            }
        }
    }
}