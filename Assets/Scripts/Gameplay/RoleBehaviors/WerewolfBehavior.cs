using System.Collections.Generic;
using Werewolf.Data;

namespace Werewolf
{
    public class WerewolfBehavior : RoleBehavior
    {
        public override void Init() { }

        public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

        public override bool OnRoleCall()
        {
            return true;
        }

        public override void OnRoleTimeOut() { }
    }
}