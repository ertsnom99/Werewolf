using System.Collections.Generic;
using UnityEngine;

public abstract class RoleBehavior : MonoBehaviour
{
    public abstract void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetup> availableRoles);
}
