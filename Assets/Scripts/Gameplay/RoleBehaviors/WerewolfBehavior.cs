using System.Collections.Generic;
using Werewolf.Data;

namespace Werewolf
{
    public class WerewolfBehavior : RoleBehavior
    {
        public override void Init() { }

        public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

        public override void OnRoleCall()
        {
            base.OnRoleCall();
        }

        public override void OnRoleTimeOut()
        {
            base.OnRoleTimeOut();
        }
    }
}