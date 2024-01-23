using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
    public abstract class RoleBehavior : MonoBehaviour
    {
        public abstract void Init();

        public abstract void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles);
    }
}