using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
    public abstract class RoleBehavior : MonoBehaviour
    {
        [field: SerializeField]
        [field: ReadOnly]
        public PlayerRef Player { get; private set; }

        public void SetPlayer(PlayerRef player)
        {
            Player = player;
        }

        public abstract void Init();

        public abstract void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles);

        public abstract void OnRoleCall();
    }
}