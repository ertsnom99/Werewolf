using Assets.Scripts.Editor.Tags;
using System;
using UnityEngine;

namespace Werewolf.Data
{
    [Serializable]
    public struct RoleSetup
    {
        [ValidateGameplayData]
        public RoleData[] Pool;
        public int UseCount;
    }

    [CreateAssetMenu(fileName = "RolesSetup", menuName = "ScriptableObjects/Roles/RolesSetup")]
    public class RolesSetupData : ScriptableObject
    {
        [field: SerializeField]
        [field: ValidateGameplayData]
        public RoleData DefaultRole { get; private set; }

        [field: SerializeField]
        public RoleSetup[] MandatoryRoles { get; private set; }

        [field: SerializeField]
        public RoleSetup[] AvailableRoles { get; private set; }
    }
}