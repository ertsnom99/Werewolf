using TMPro;
using UnityEngine;

namespace Werewolf.UI
{
    public class TitleScreen : FadingScreen
    {
        [Header("UI")]
        [SerializeField]
        private TMP_Text _text;

        public void Initialize(string title)
        {
            _text.text = title;
        }
    }
}