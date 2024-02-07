using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
    public class ChoiceScreen : FadingScreen
    {
        [Header("UI")]
        [SerializeField]
        private HorizontalLayoutGroup _roleChoices;

        [SerializeField]
        private Choice _roleChoicePrefab;

        protected override void UpdateFade(float opacity)
        {

        }
    }
}