using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
    public class ThiefBehavior : RoleBehavior
    {
        [SerializeField]
        private RoleData[] _rolesToAdd;

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
            RoleData[] roles = _gameManager.GetReservedRoles(this).Roles;

            if (roles == null || roles.Length < 0)
            {
                _gameManager.StopWaintingForPlayer(Player);
                return;
            }

            bool reservedOnlyWerewolfs = true;

            foreach (RoleData role in roles)
            {
                if (role.Type != RoleData.RoleType.Werewolf)
                {
                    reservedOnlyWerewolfs = false;
                    break;
                }
            }

            if (_gameManager.MakePlayerChooseReservedRole(this, reservedOnlyWerewolfs, OnRoleSelected))
            {
                return;
            }

            _gameManager.StopWaintingForPlayer(Player);
        }

        private void OnRoleSelected(int roleGameplayTagID)
        {
            if (roleGameplayTagID > -1)
            {
                GameManager.IndexedReservedRoles roles = _gameManager.GetReservedRoles(this);
                int roleIndex = 0;

                foreach (RoleData role in roles.Roles)
                {
                    if (role.GameplayTag.CompactTagId != roleGameplayTagID)
                    {
                        roleIndex++;
                        continue;
                    }

                    _gameManager.ChangeRole(Player, GameplayDatabaseManager.Instance.GetGameplayData<RoleData>(roleGameplayTagID), roles.Behaviors[roleIndex]);
                }
            }

            _gameManager.RemoveReservedRoles(this, new int[0]);
            _gameManager.StopWaintingForPlayer(Player);

            Destroy(gameObject);
        }
    }
}