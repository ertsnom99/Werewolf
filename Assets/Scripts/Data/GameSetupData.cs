using Assets.Scripts.Editor.Tags;
using System;
using UnityEngine;

namespace Werewolf.Data
{
    [Serializable]
    public struct RoleSetupData
    {
        [ValidateGameplayData]
        public RoleData[] Pool;
        public int UseCount;
    }

    [CreateAssetMenu(fileName = "GameSetup", menuName = "ScriptableObjects/GameSetup")]
    public class GameSetupData : ScriptableObject
    {
        [field: SerializeField]
        [field: ValidateGameplayData]
        public RoleData DefaultRole { get; private set; }

        [field: SerializeField]
        public RoleSetupData[] MandatoryRoles { get; private set; }

        [field: SerializeField]
        public RoleSetupData[] AvailableRoles { get; private set; }

        [field: SerializeField]
        public int MinPlayerCount { get; private set; }
    }
}