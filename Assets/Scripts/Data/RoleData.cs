using Assets.Scripts.Data;
using UnityEngine;

namespace Werewolf.Data
{
    [CreateAssetMenu(fileName = "Role", menuName = "ScriptableObjects/Role")]
    public class RoleData : GameplayData
    {
        public enum RoleType
        {
            Villager,
            Werewolf,
            Ambiguous,
            Lonely
        };

        [field: SerializeField]
        public RoleType Type { get; private set; }

        [field: SerializeField]
        [field: TextArea(8, 20)]
        public string Instruction { get; private set; }

        // This allows to have more than once this role in a game
        [field: SerializeField]
        public bool CanHaveMultiples { get; private set; }

        // When CanHaveMultiples is false, there will be this exact number of this role at once in a game 
        [field: SerializeField]
        public int MandatoryCount { get; private set; }

        [field: SerializeField]
        public int[] GroupIndexes { get; private set; }

        [field: SerializeField]
        public int[] NightPriorities { get; private set; }

        [field: SerializeField]
        public int[] DayPriorities { get; private set; }

        [field: SerializeField]
        public RoleBehavior Behavior { get; private set; }
    }
}