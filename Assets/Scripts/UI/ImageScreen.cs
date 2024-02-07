using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
    public class ImageScreen : FadingScreen
    {
        [Header("UI")]
        [SerializeField]
        private Image _image;

        [SerializeField]
        private TMP_Text _text;

        public void Config(Sprite image, string title)
        {
            _image.sprite = image;
            _text.text = title;
        }

        protected override void UpdateFade(float opacity)
        {
            SetImageOpacity(_image, opacity);
            SetTextOpacity(_text, opacity);
        }
    }
}