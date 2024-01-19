using System.Collections.Generic;
using Werewolf.Data;

namespace Werewolf
{
    public class WerewolfBehavior : RoleBehavior
    {
        public override void Init() { }

        public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetup> availableRoles) { }
    }
}