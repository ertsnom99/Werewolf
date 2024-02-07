using UnityEngine;
using Werewolf.UI;

namespace Werewolf
{
    public class UIManager : MonoSingleton<UIManager>
    {
        [field:Header("Screens")]
        [field:SerializeField]
        public LoadingScreen LoadingScreen { get; private set; }

        [field: SerializeField]
        public TitleScreen TitleScreen { get; private set; }

        [field: SerializeField]
        public ImageScreen ImageScreen { get; private set; }

        [field: SerializeField]
        public ChoiceScreen ChoiceScreen { get; private set; }
    }
}