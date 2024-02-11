using System.Collections.Generic;
using System.Data;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
    public class ThiefBehavior : RoleBehavior
    {
        [SerializeField]
        private RoleData[] _rolesToAdd;

        private GameManager.IndexedReservedRoles _reservedRoles;
        private bool _reservedOnlyWerewolfs;

        private GameManager _gameManager;

        public override void Init()
        {
            _gameManager = GameManager.Instance;

            _gameManager.OnPreRoleDistribution += OnPreRoleDistribution;
            _gameManager.OnPostRoleDistribution += OnPostRoleDistribution;
        }

        public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

        private void OnPreRoleDistribution()
        {
            _gameManager.AddRolesToDistribute(_rolesToAdd);
        }

        private void OnPostRoleDistribution()
        {
            List<RoleData> roles = new List<RoleData>();

            for (int i = 0; i < _rolesToAdd.Length; i++)
            {
                if (_gameManager.RolesToDistribute.Count <= 0)
                {
                    Debug.LogError("The thief couldn't find enough roles to set aside!!!");
                    break;
                }

                int randomIndex = Random.Range(0, _gameManager.RolesToDistribute.Count);
                roles.Add(_gameManager.RolesToDistribute[randomIndex]);

                _gameManager.RemoveRoleToDistribute(_gameManager.RolesToDistribute[randomIndex]);
            }

            _gameManager.ReserveRoles(this, roles.ToArray(), false);
        }

        public override void OnRoleCall()
        {
            base.OnRoleCall();

            _reservedRoles = _gameManager.GetReservedRoles(this);

            if (_reservedRoles.Roles == null || _reservedRoles.Roles.Length < 0)
            {
                _gameManager.StopWaintingForPlayer(Player);
                return;
            }

            _reservedOnlyWerewolfs = true;

            foreach (RoleData role in _reservedRoles.Roles)
            {
                if (role.Type != RoleData.RoleType.Werewolf)
                {
                    _reservedOnlyWerewolfs = false;
                    break;
                }
            }

            if (_gameManager.MakePlayerChooseReservedRole(this, _reservedOnlyWerewolfs, OnRoleSelected))
            {
                return;
            }

            _gameManager.StopWaintingForPlayer(Player);
        }

        private void OnRoleSelected(int roleGameplayTagID)
        {
            if (_timedOut)
            {
                return;
            }

            bool validGameplayTagID = false;
            int roleIndex = 0;

            if (roleGameplayTagID > -1)
            {
                foreach (RoleData role in _reservedRoles.Roles)
                {
                    if (role.GameplayTag.CompactTagId != roleGameplayTagID)
                    {
                        roleIndex++;
                        continue;
                    }

                    validGameplayTagID = true;
                }
            }

            if (validGameplayTagID)
            {
                _gameManager.ChangeRole(Player, _reservedRoles.Roles[roleIndex], _reservedRoles.Behaviors[roleIndex]);
            }
            else if (_reservedOnlyWerewolfs)
            {
                ChangeForRandomRole();
            }

            _gameManager.RemoveReservedRoles(this, new int[0]);
            _gameManager.StopWaintingForPlayer(Player);

            if (validGameplayTagID || _reservedOnlyWerewolfs)
            {
                Destroy(gameObject);
            }
        }

        public override void OnRoleTimeOut()
        {
            base.OnRoleTimeOut();

            if (_reservedOnlyWerewolfs)
            {
                ChangeForRandomRole();
            }

            _gameManager.RemoveReservedRoles(this, new int[0]);

            if (_reservedOnlyWerewolfs)
            {
                Destroy(gameObject);
            }
        }

        private void ChangeForRandomRole()
        {
            int randomIndex = Random.Range(0, _reservedRoles.Roles.Length);
            _gameManager.ChangeRole(Player, _reservedRoles.Roles[randomIndex], _reservedRoles.Behaviors[randomIndex]);
        }
    }
}