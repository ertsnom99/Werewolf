using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
    public class GameManager : MonoSingleton<GameManager>
    {
        public List<RoleData> RolesToDistribute { get; private set; }

        public Dictionary<RoleBehavior, Card[]> ReservedRolesByBehavior { get; private set; }

        private Dictionary<RoleBehavior, RoleData> _unassignedRoleBehaviors = new Dictionary<RoleBehavior, RoleData>();

        private Player[] _players;

        [Header("Layout")]
        [SerializeField]
        private Player _playerPrefab;

        [SerializeField]
        private AnimationCurve _playersOffset;

        [SerializeField]
        private Card _cardPrefab;

        [SerializeField]
        private AnimationCurve _cameraOffset;

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField]
        private bool _useDebug;

        [SerializeField]
        private int _debugPlayerCount = 10;

        [SerializeField]
        private GameSetupData _debugGameSetupData;
#endif

        public event Action OnPreRoleDistribution = delegate { };
        public event Action OnPostRoleDistribution = delegate { };

        private readonly int AVAILABLE_ROLES_MAX_ATTEMPT_COUNT = 100;
        private readonly Vector3 STARTING_DIRECTION = Vector3.back;
        private readonly float RESERVED_ROLES_SPACING = 1.5f;

        protected override void Awake()
        {
            base.Awake();

            RolesToDistribute = new List<RoleData>();
            ReservedRolesByBehavior = new Dictionary<RoleBehavior, Card[]>();
        }

        private void Start()
        {
            int playerCount = 0;

            RolesSetup rolesSetup;
            rolesSetup.DefaultRole = null;
            rolesSetup.MandatoryRoles = new RoleSetupData[0];
            rolesSetup.AvailableRoles = new RoleSetupData[0];
            rolesSetup.MinPlayerCount = 0;

#if UNITY_EDITOR
            if (!_playerPrefab)
            {
                Debug.LogError("_playerPrefab of the GameManager is null");
                return;
            }

            if (_useDebug && _debugGameSetupData)
            {
                rolesSetup.DefaultRole = _debugGameSetupData.DefaultRole;
                rolesSetup.MandatoryRoles = _debugGameSetupData.MandatoryRoles;
                rolesSetup.AvailableRoles = _debugGameSetupData.AvailableRoles;
                rolesSetup.MinPlayerCount = _debugGameSetupData.MinPlayerCount;
            }

            playerCount = _debugPlayerCount;
#endif

            SelectRolesToDistribute(rolesSetup, playerCount);

            CreatePlayers(playerCount);
            AdjustCamera(playerCount);

            OnPreRoleDistribution();
            DistributeRoles();
            OnPostRoleDistribution();

            PlaceReservedRolesRows();

            // TODO: Start game loop
        }

        #region Pre Gameplay Loop
[Serializable]
public struct RolesSetup
{
    public RoleData DefaultRole;
    public RoleSetupData[] MandatoryRoles;
    public RoleSetupData[] AvailableRoles;
    public int MinPlayerCount;
}
        #region Roles selection
        private void SelectRolesToDistribute(RolesSetup rolesSetup, int playerCount)
        {
            List<RoleData> rolesToDistribute = new List<RoleData>();
            List<RoleSetupData> availableRoles = new List<RoleSetupData>(rolesSetup.AvailableRoles);

            // Add all mandatory roles first
            foreach (RoleSetupData roleSetup in rolesSetup.MandatoryRoles)
            {
                RoleData[] addedRoles = SelectRolesFromRoleSetup(roleSetup, ref rolesToDistribute);
                PrepareRoleBehaviors(addedRoles, ref rolesToDistribute, ref availableRoles);
            }

            List<RoleSetupData> excludedRuleSetups = new List<RoleSetupData>();
            int attempts = 0;

            // Complete with available roles at random or default role
            while (rolesToDistribute.Count < playerCount)
            {
                int startingRoleCount = rolesToDistribute.Count;

                if (availableRoles.Count <= 0 || attempts >= AVAILABLE_ROLES_MAX_ATTEMPT_COUNT)
                {
                    rolesToDistribute.Add(rolesSetup.DefaultRole);
                    PrepareRoleBehavior(rolesSetup.DefaultRole, ref rolesToDistribute, ref availableRoles);

                    continue;
                }

                int randomIndex = UnityEngine.Random.Range(0, availableRoles.Count);
                RoleSetupData roleSetup = availableRoles[randomIndex];

                // Do not use roles setup that would add too many roles 
                if (excludedRuleSetups.Contains(roleSetup))
                {
                    attempts++;
                    continue;
                }
                else if (roleSetup.UseCount > playerCount - rolesToDistribute.Count)
                {
                    excludedRuleSetups.Add(roleSetup);
                    continue;
                }

                RoleData[] addedRoles = SelectRolesFromRoleSetup(roleSetup, ref rolesToDistribute);
                availableRoles.RemoveAt(randomIndex);

                PrepareRoleBehaviors(addedRoles, ref rolesToDistribute, ref availableRoles);

                // Some roles were removed from the list of roles to distribute
                if (startingRoleCount > rolesToDistribute.Count)
                {
                    excludedRuleSetups.Clear();
                    attempts = 0;
                }
            }

            RolesToDistribute = rolesToDistribute;
        }

        private RoleData[] SelectRolesFromRoleSetup(RoleSetupData roleSetup, ref List<RoleData> rolesToDistribute)
        {
            List<RoleData> rolePool = new List<RoleData>(roleSetup.Pool);
            RoleData[] addedRoles = new RoleData[roleSetup.UseCount];

            for (int i = 0; i < roleSetup.UseCount; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, rolePool.Count);
                rolesToDistribute.Add(rolePool[randomIndex]);
                addedRoles[i] = rolePool[randomIndex];
                rolePool.RemoveAt(randomIndex);
            }

            return addedRoles;
        }

        public void PrepareRoleBehaviors(RoleData[] roles, ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles)
        {
            foreach (RoleData role in roles)
            {
                PrepareRoleBehavior(role, ref rolesToDistribute, ref availableRoles);
            }
        }

        public void PrepareRoleBehavior(RoleData role, ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles)
        {
            if (!role.Behavior)
            {
                return;
            }

            // Temporairy store the behaviors, because they must be attributed to specific players later
            RoleBehavior roleBehavior = Instantiate(role.Behavior, transform);
            _unassignedRoleBehaviors.Add(roleBehavior, role);

            roleBehavior.Init();
            roleBehavior.OnSelectedToDistribute(ref rolesToDistribute, ref availableRoles);
        }
        #endregion

        private void CreatePlayers(int playerCount)
        {
            _players = new Player[playerCount];

            float rotationIncrement = 360.0f / playerCount;
            Vector3 startingPosition = STARTING_DIRECTION * _playersOffset.Evaluate(playerCount);

            for (int i = 0; i < playerCount; i++)
            {
                Quaternion rotation = Quaternion.Euler(0, rotationIncrement * i, 0);
                _players[i] = Instantiate(_playerPrefab, rotation * startingPosition, rotation);
            }
        }

        private void AdjustCamera(int playerCount)
        {
            Camera.main.transform.position = Camera.main.transform.position.normalized * _cameraOffset.Evaluate(playerCount);
        }

        #region Roles distribution
        private void DistributeRoles()
        {
            foreach (Player player in _players)
            {
                RoleData selectedRole = RolesToDistribute[UnityEngine.Random.Range(0, RolesToDistribute.Count)];
                player.Card.SetRole(selectedRole);
                RolesToDistribute.Remove(selectedRole);

                // Attach behavior prefab
                foreach (KeyValuePair<RoleBehavior, RoleData> roleBehavior in _unassignedRoleBehaviors)
                {
                    if (roleBehavior.Value == selectedRole)
                    {
                        roleBehavior.Key.transform.parent = player.Card.transform;
                        roleBehavior.Key.transform.localPosition = Vector3.zero;

                        player.Card.SetBehavior(roleBehavior.Key);
                        _unassignedRoleBehaviors.Remove(roleBehavior.Key);
                        break;
                    }
                }
            }
        }

        public void AddRolesToDistribute(RoleData[] roles)
        {
            RolesToDistribute.AddRange(roles);
        }

        public void RemoveRoleToDistribute(RoleData role)
        {
            RolesToDistribute.Remove(role);
        }
        #endregion

        #region Roles reservation
        public void ReserveRoles(RoleBehavior roleBehavior, RoleData[] roles, bool AreFaceUp)
        {
            Card[] cards = new Card[roles.Length];

            for (int i = 0; i < roles.Length; i++)
            {
                Card card = Instantiate(_cardPrefab);
                card.SetRole(roles[i]);

                // Attach behavior prefab
                foreach (KeyValuePair<RoleBehavior, RoleData> unassignedRoleBehavior in _unassignedRoleBehaviors)
                {
                    if (unassignedRoleBehavior.Value == card.Role)
                    {
                        unassignedRoleBehavior.Key.transform.parent = card.transform;
                        unassignedRoleBehavior.Key.transform.localPosition = Vector3.zero;

                        card.SetBehavior(unassignedRoleBehavior.Key);
                        _unassignedRoleBehaviors.Remove(unassignedRoleBehavior.Key);
                        break;
                    }
                }

                // Spread the cards horizontally
                card.transform.position += (Vector3.right * i * RESERVED_ROLES_SPACING) + (Vector3.left * (roles.Length - 1) * RESERVED_ROLES_SPACING / 2.0f);

                if (AreFaceUp && !card.IsFaceUp)
                {
                    card.Flip();
                }

                cards[i] = card;
            }

            ReservedRolesByBehavior.Add(roleBehavior, cards);
        }

        private void PlaceReservedRolesRows()
        {
            int row = 0;

            // Spread the rows vertically
            foreach (KeyValuePair<RoleBehavior, Card[]> reservedRoles in ReservedRolesByBehavior)
            {
                foreach (Card card in reservedRoles.Value)
                {
                    card.transform.position += (Vector3.back * row * RESERVED_ROLES_SPACING) + (Vector3.forward * (ReservedRolesByBehavior.Count - 1) * RESERVED_ROLES_SPACING / 2.0f);
                }

                row++;
            }
        }
        #endregion

        #endregion
    }
}