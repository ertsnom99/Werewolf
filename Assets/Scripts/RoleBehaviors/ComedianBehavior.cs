using System.Collections.Generic;
using UnityEngine;

public class ComedianBehavior : RoleBehavior
{
    private readonly int NEEDED_ROLE_COUNT = 3;

    public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetup> availableRoles)
    {
        List<RoleData> selectedRoles = new List<RoleData>();

        // Try to take the roles from the available roles first
        List<RoleSetup> availableRolesCopy = new List<RoleSetup>(availableRoles);

        while (availableRolesCopy.Count > 0 && selectedRoles.Count < NEEDED_ROLE_COUNT)
        {
            int roleSetupIndex = Random.Range(0, availableRolesCopy.Count);

            if (!CanTakeRolesFromSetup(availableRolesCopy[roleSetupIndex], selectedRoles))
            {
                availableRolesCopy.RemoveAt(roleSetupIndex);
                continue;
            }

            SelectRolesFromRoleSetup(availableRolesCopy[roleSetupIndex], ref selectedRoles);

            availableRoles.Remove(availableRolesCopy[roleSetupIndex]);
            availableRolesCopy.RemoveAt(roleSetupIndex);
        }

        // Take the rest from the roles to distribute
        List<RoleData> rolesToDistributeCopy = new List<RoleData>(rolesToDistribute);

        while (rolesToDistributeCopy.Count > 0 && selectedRoles.Count < NEEDED_ROLE_COUNT)
        {
            int roleIndex = Random.Range(0, rolesToDistributeCopy.Count);

            if (!IsRoleValid(rolesToDistributeCopy[roleIndex], selectedRoles))
            {
                rolesToDistributeCopy.RemoveAt(roleIndex);
                continue;
            }

            selectedRoles.Add(rolesToDistributeCopy[roleIndex]);
            
            rolesToDistribute.Remove(rolesToDistributeCopy[roleIndex]);
            rolesToDistributeCopy.RemoveAt(roleIndex);
        }

        if (selectedRoles.Count < NEEDED_ROLE_COUNT)
        {

            Debug.LogError("The comedian couldn't find enough roles to set aside!!!");
        }

        GameManager.Instance.ReserveRoles(this, selectedRoles.ToArray());
    }

    private bool CanTakeRolesFromSetup(RoleSetup roleSetup, List<RoleData> selectedRoles)
    {
        if (NEEDED_ROLE_COUNT - selectedRoles.Count < roleSetup.UseCount)
        {
            return false;
        }

        int neededValidRolesCount = roleSetup.UseCount;

        foreach (RoleData role in roleSetup.Pool)
        { 
            if (IsRoleValid(role, selectedRoles))
            {
                neededValidRolesCount--;

                if (neededValidRolesCount == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsRoleValid(RoleData role, List<RoleData> selectedRoles)
    {
        return !selectedRoles.Contains(role) && role.Type == RoleData.RoleType.Villager && role.Behavior && !role.Behavior.GetType().Equals(GetType());
    }

    private void SelectRolesFromRoleSetup(RoleSetup roleSetup, ref List<RoleData> selectedRoles)
    {
        int rolesSelectedCount = 0;
        int indexOffset = Random.Range(0, roleSetup.UseCount);

        for (int i = 0; i < roleSetup.Pool.Length; i++)
        {
            int adjustedIndex = (i + indexOffset) % roleSetup.Pool.Length;

            if (IsRoleValid(roleSetup.Pool[adjustedIndex], selectedRoles))
            {
                selectedRoles.Add(roleSetup.Pool[adjustedIndex]);
                rolesSelectedCount++;
            }

            if (rolesSelectedCount >= roleSetup.UseCount)
            {
                return;
            }
        }
    }
}
