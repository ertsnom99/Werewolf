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
            if (!_gameManager.MakePlayerChooseReservedRole(this))
            {
                // TODO: Immediatly end turn
            }
        }

        // TODO: 6. Add a callback that will react to the choice of the player
    }
}