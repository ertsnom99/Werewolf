using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf
{
    public class GameManager : NetworkBehaviourSingleton<GameManager>
    {
        #region Server variables
        public List<RoleData> RolesToDistribute { get; private set; }

        private Dictionary<RoleBehavior, RoleData> _unassignedRoleBehaviors = new Dictionary<RoleBehavior, RoleData>();

        private Dictionary<PlayerRef, PlayerRole> _playerRoles = new Dictionary<PlayerRef, PlayerRole>();

        private struct PlayerRole
        {
            public RoleData Data;
            public RoleBehavior Behavior;
        }

        private Dictionary<RoleBehavior, IndexedReservedRoles> _reservedRolesByBehavior = new Dictionary<RoleBehavior, IndexedReservedRoles>();

        private struct IndexedReservedRoles
        {
            public RoleData[] Roles;
            public RoleBehavior[] Behaviors;
            public int networkIndex;
        }
#if UNITY_SERVER && UNITY_EDITOR
        private Dictionary<RoleBehavior, Card[]> _reservedCardsByBehavior = new Dictionary<RoleBehavior, Card[]>();
#endif
        private List<PlayerRef> _playersReady = new List<PlayerRef>();

        private bool _rolesDistributionDone = false;
        private bool _allPlayersReadyToReceiveRole = false;
        private bool _allRolesSent = false;
        private bool _allPlayersReadyToPlay = false;
        #endregion

        #region Networked variables
        [Networked, Capacity(5)]
        public NetworkArray<RolesContainer> ReservedRoles { get; }

        [Serializable]
        public struct RolesContainer : INetworkStruct
        {
            public int RoleCount;
            [Networked, Capacity(5)]
            public NetworkArray<int> Roles { get; }
        }

        private List<List<PlayerRef>> _nightCalls = new List<List<PlayerRef>>();
        #endregion

        [Header("Config")]
        [SerializeField]
        private GameConfig _gameConfig;

        [field: Header("Visual")]
        [field: SerializeField]
        private Card _cardPrefab;

        private Dictionary<PlayerRef, Card> _playerCards = new Dictionary<PlayerRef, Card>();
        private Card[][] _reservedRolesCards;

        private GameDataManager _gameDataManager;
        private DaytimeManager _daytimeManager;

        public static event Action OnSpawned = delegate { };
        public static bool HasSpawned { get; private set; }

        // Server events
        public event Action OnPreRoleDistribution = delegate { };
        public event Action OnPostRoleDistribution = delegate { };
        public event Action OnPreStartGame = delegate { };

        // Client events
        public event Action OnRoleReceived = delegate { };

        private readonly int AVAILABLE_ROLES_MAX_ATTEMPT_COUNT = 100;
        private readonly Vector3 STARTING_DIRECTION = Vector3.back;
        private readonly float RESERVED_ROLES_SPACING = 1.5f;

        protected override void Awake()
        {
            base.Awake();

            RolesToDistribute = new List<RoleData>();

            if (!_gameConfig)
            {
                Debug.LogError("The GameConfig of the GameManager is not defined");
            }
        }

        private void Start()
        {
            _daytimeManager = DaytimeManager.Instance;
        }

        public override void Spawned()
        {
            HasSpawned = true;
            OnSpawned();
        }

        public void PrepareGame(RolesSetup rolesSetup)
        {
            GetGameDataManager();

            SelectRolesToDistribute(rolesSetup);

            OnPreRoleDistribution();
            DistributeRoles();
            OnPostRoleDistribution();

            DetermineNightCalls();
#if UNITY_SERVER && UNITY_EDITOR
            CreatePlayerCardsForServer();
            CreateReservedRoleCardsForServer();
            AdjustCamera();
            LogNightCalls();
#endif
            CheckPreGameplayLoopProgress();
        }

        #region Pre Gameplay Loop
        private void GetGameDataManager()
        {
            _gameDataManager = FindObjectOfType<GameDataManager>();
        }

        #region Roles selection
        private void SelectRolesToDistribute(RolesSetup rolesSetup)
        {
            // Convert GameplayTagIDs to RoleData
            RoleData defaultRole = GameplayDatabaseManager.Instance.GetGameplayData<RoleData>(rolesSetup.DefaultRole);
            GameDataManager.ConvertToRoleSetupDatas(rolesSetup.MandatoryRoles, out List<RoleSetupData> mandatoryRoles);
            GameDataManager.ConvertToRoleSetupDatas(rolesSetup.AvailableRoles, out List<RoleSetupData> availableRoles);

            List<RoleData> rolesToDistribute = new List<RoleData>();

            // Add all mandatory roles first
            foreach (RoleSetupData roleSetup in mandatoryRoles)
            {
                RoleData[] addedRoles = SelectRolesFromRoleSetup(roleSetup, ref rolesToDistribute);
                PrepareRoleBehaviors(addedRoles, ref rolesToDistribute, ref availableRoles);
            }

            List<RoleSetupData> excludedRuleSetups = new List<RoleSetupData>();
            int attempts = 0;

            // Complete with available roles at random or default role
            while (rolesToDistribute.Count < _gameDataManager.PlayerInfos.Count)
            {
                int startingRoleCount = rolesToDistribute.Count;

                if (availableRoles.Count <= 0 || attempts >= AVAILABLE_ROLES_MAX_ATTEMPT_COUNT)
                {
                    rolesToDistribute.Add(defaultRole);
                    PrepareRoleBehavior(defaultRole, ref rolesToDistribute, ref availableRoles);

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
                else if (roleSetup.UseCount > _gameDataManager.PlayerInfos.Count - rolesToDistribute.Count)
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

        #region Roles distribution
        private void DistributeRoles()
        {
            foreach (KeyValuePair<PlayerRef, PlayerInfo> playerInfo in _gameDataManager.PlayerInfos)
            {
                RoleData selectedRole = RolesToDistribute[UnityEngine.Random.Range(0, RolesToDistribute.Count)];
                RolesToDistribute.Remove(selectedRole);

                RoleBehavior selectedBehavior = null;

                foreach (KeyValuePair<RoleBehavior, RoleData> unassignedRoleBehavior in _unassignedRoleBehaviors)
                {
                    if (unassignedRoleBehavior.Value == selectedRole)
                    {
                        selectedBehavior = unassignedRoleBehavior.Key;
                        selectedBehavior.SetPlayer(playerInfo.Key);
                        _unassignedRoleBehaviors.Remove(unassignedRoleBehavior.Key);
                        break;
                    }
                }

                _playerRoles.Add(playerInfo.Key, new PlayerRole { Data = selectedRole, Behavior = selectedBehavior });
            }

            _rolesDistributionDone = true;
        }

        public void AddRolesToDistribute(RoleData[] roles)
        {
            RolesToDistribute.AddRange(roles);
        }

        public void RemoveRoleToDistribute(RoleData role)
        {
            RolesToDistribute.Remove(role);
        }

        private void DetermineNightCalls()
        {
            // Remove any players that do not need to be called at night
            List<PlayerRef> players = _playerRoles.Keys.ToList();

            for (int i = players.Count - 1; i >= 0; i--)
            {
                if (_playerRoles[players[i]].Data.NightPriorities.Length > 0)
                {
                    continue;
                }

                players.RemoveAt(i);
            }

            // Make a list of all different priorities
            List<int> priorities = new List<int>();

            foreach (PlayerRef player in players)
            {
                foreach (int priority in _playerRoles[player].Data.NightPriorities)
                {
                    if (priorities.Contains(priority))
                    {
                        continue;
                    }

                    priorities.Add(priority);
                }
            }

            priorities.Sort();

            // Loop threw the priorities and store all players with similare priorities together
            for (int i = 0; i < priorities.Count; i++)
            {
                List<PlayerRef> playersToCall = new List<PlayerRef>();

                foreach (PlayerRef player in players)
                {
                    foreach (int priority in _playerRoles[player].Data.NightPriorities)
                    {
                        if (priority == priorities[i])
                        {
                            playersToCall.Add(player);
                            break;
                        }
                    }
                }

                _nightCalls.Add(playersToCall);
            }
        }
#if UNITY_SERVER && UNITY_EDITOR
        private void LogNightCalls()
        {
            Debug.Log("----------------------Night Calls----------------------");
             
            foreach (List<PlayerRef> nightCall in _nightCalls)
            {
                string roles = "";

                foreach (PlayerRef player in nightCall)
                {
                    roles += _playerRoles[player].Data.Name + " || ";
                }

                Debug.Log(roles);
            }

            Debug.Log("-------------------------------------------------------");
        }
#endif
        private void SendPlayerRoles()
        {
            foreach (KeyValuePair<PlayerRef, PlayerInfo> playerInfo in _gameDataManager.PlayerInfos)
            {
                RPC_TellPlayerRole(playerInfo.Key, _playerRoles[playerInfo.Key].Data.GameplayTag.CompactTagId);
            }

            _allRolesSent = true;
        }
        #endregion

        #region Roles reservation
        public void ReserveRoles(RoleBehavior roleBehavior, RoleData[] roles, bool AreFaceUp)
        {
            RolesContainer rolesContainer = new();
            RoleBehavior[] behaviors = new RoleBehavior[roles.Length];

            rolesContainer.RoleCount = roles.Length;

            for (int i = 0; i < roles.Length; i++)
            {
                rolesContainer.Roles.Set(i, AreFaceUp ? roles[i].GameplayTag.CompactTagId : -1);

                foreach (KeyValuePair<RoleBehavior, RoleData> unassignedRoleBehavior in _unassignedRoleBehaviors)
                {
                    if (unassignedRoleBehavior.Value == roles[i])
                    {
                        behaviors[i] = unassignedRoleBehavior.Key;
                        _unassignedRoleBehaviors.Remove(unassignedRoleBehavior.Key);
                        break;
                    }
                }
            }

            ReservedRoles.Set(_reservedRolesByBehavior.Count, rolesContainer);
            _reservedRolesByBehavior.Add(roleBehavior, new IndexedReservedRoles { Roles = roles, Behaviors = behaviors, networkIndex = _reservedRolesByBehavior.Count });
        }
        #endregion

        #region RPC calls
        [Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        public void RPC_ConfirmPlayerReadyToReceiveRole(RpcInfo info = default)
        {
            if (!AddPlayerReady(info.Source))
            {
                return;
            }

            _allPlayersReadyToReceiveRole = true;
            _playersReady.Clear();

            Log.Info("All players are ready!");

            CheckPreGameplayLoopProgress();
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
        public void RPC_TellPlayerRole([RpcTarget] PlayerRef player, int role)
        {
            if (!_gameDataManager)
            {
                GetGameDataManager();
            }

            CreatePlayerCards(player, GameplayDatabaseManager.Instance.GetGameplayData<RoleData>(role));
            CreateReservedRoleCards();
            AdjustCamera();

            OnRoleReceived();
        }

        [Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        public void RPC_ConfirmPlayerReadyToPlay(RpcInfo info = default)
        {
            if (!AddPlayerReady(info.Source))
            {
                return;
            }

            _allPlayersReadyToPlay = true;

            CheckPreGameplayLoopProgress();
        }
        #endregion

        private void CheckPreGameplayLoopProgress()
        {
            if (_rolesDistributionDone && _allPlayersReadyToReceiveRole && !_allRolesSent)
            {
                SendPlayerRoles();
            }
            else if (_allRolesSent && _allPlayersReadyToPlay)
            {
                OnPreStartGame();
                StartGame();
            }
        }
        #endregion

        #region Gameplay Loop
        private void StartGame()
        {
            // TODO
            RPC_ChangeDayTime(Daytime.Night);
#if UNITY_SERVER && UNITY_EDITOR
            _daytimeManager.ChangeDaytime(Daytime.Night);
#endif
        }

        #region RPC calls
        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
        public void RPC_ChangeDayTime(Daytime dayTime)
        {
            _daytimeManager.ChangeDaytime(dayTime);
        }
        #endregion
        #endregion

        private bool AddPlayerReady(PlayerRef player)
        {
            if (_playersReady.Contains(player))
            {
                return false;
            }

            _playersReady.Add(player);

            Log.Info($"{player} is ready!");

            if (!_gameDataManager)
            {
                GetGameDataManager();
            }

            if (_playersReady.Count < _gameDataManager.PlayerInfos.Count)
            {
                return false;
            }

            return true;
        }

        #region Visual
#if UNITY_SERVER && UNITY_EDITOR
        private void CreatePlayerCardsForServer()
        {
            float rotationIncrement = 360.0f / _playerRoles.Count;
            Vector3 startingPosition = STARTING_DIRECTION * _gameConfig.CardsOffset.Evaluate(_playerRoles.Count);

            int counter = -1;

            foreach (KeyValuePair<PlayerRef, PlayerRole> playerRole in _playerRoles)
            {
                counter++;

                Quaternion rotation = Quaternion.Euler(0, rotationIncrement * counter, 0);

                Card card = Instantiate(_cardPrefab, rotation * startingPosition, rotation);

                card.SetPlayer(playerRole.Key);
                card.SetRole(playerRole.Value.Data);
                card.SetNickname(_gameDataManager.PlayerInfos[playerRole.Key].Nickname);
                card.Flip();

                if (!playerRole.Value.Behavior)
                {
                    continue;
                }

                Transform roleBehaviorTransform = playerRole.Value.Behavior.transform;
                roleBehaviorTransform.parent = card.transform;
                roleBehaviorTransform.localPosition = Vector3.zero;
            }
        }

        private void CreateReservedRoleCardsForServer()
        {
            int rowCounter = 0;

            foreach (KeyValuePair<RoleBehavior, IndexedReservedRoles> reservedRoleByBehavior in _reservedRolesByBehavior)
            {
                Vector3 rowPosition = (Vector3.back * rowCounter * RESERVED_ROLES_SPACING) + (Vector3.forward * (_reservedRolesByBehavior.Count - 1) * RESERVED_ROLES_SPACING / 2.0f);
                Card[] cards = new Card[reservedRoleByBehavior.Value.Roles.Length];

                int columnCounter = 0;

                foreach (RoleData role in reservedRoleByBehavior.Value.Roles)
                {
                    Vector3 columnPosition = (Vector3.right * columnCounter * RESERVED_ROLES_SPACING) + (Vector3.left * (reservedRoleByBehavior.Value.Roles.Length - 1) * RESERVED_ROLES_SPACING / 2.0f);

                    Card card = Instantiate(_cardPrefab, rowPosition + columnPosition, Quaternion.identity);

                    card.SetRole(role);
                    card.Flip();

                    columnCounter++;
                }

                _reservedCardsByBehavior.Add(reservedRoleByBehavior.Key, cards);
                rowCounter++;
            }
        }
#endif
        private void CreatePlayerCards(PlayerRef bottomPlayer, RoleData playerRole)
        {
            NetworkDictionary<PlayerRef, PlayerInfo> playerInfos = _gameDataManager.PlayerInfos;
            int playerCount = playerInfos.Count;
            
            int counter = -1;
            int rotationOffset = -1;

            float rotationIncrement = 360.0f / playerCount;
            Vector3 startingPosition = STARTING_DIRECTION * _gameConfig.CardsOffset.Evaluate(playerCount);

            // Offset the rotation to keep bottomPlayer at the bottom
            foreach (KeyValuePair<PlayerRef, PlayerInfo> playerInfo in playerInfos)
            {
                if (playerInfo.Key == bottomPlayer)
                {
                    break;
                }

                rotationOffset--;
            }

            // Create cards
            foreach (KeyValuePair<PlayerRef, PlayerInfo> playerInfo in playerInfos)
            {
                counter++;
                rotationOffset++;

                Quaternion rotation = Quaternion.Euler(0, rotationIncrement * rotationOffset, 0);

                Card card = Instantiate(_cardPrefab, rotation * startingPosition, rotation);

                card.SetPlayer(playerInfo.Key);
                card.SetNickname(playerInfo.Value.Nickname);

                if (playerInfo.Key == bottomPlayer)
                {
                    card.SetRole(playerRole);
                    card.Flip();
                }

                _playerCards.Add(playerInfo.Key, card);
            }
        }

        private void CreateReservedRoleCards()
        {
            // Must figure out how many actual row are in the networked data
            int rowCount = 0;

            foreach (RolesContainer rolesContainer in ReservedRoles)
            {
                if (rolesContainer.RoleCount <= 0)
                {
                    break;
                }

                rowCount++;
            }

            _reservedRolesCards = new Card[rowCount][];

            int rowCounter = 0;

            // Create the reserved cards
            foreach (RolesContainer reservedRole in ReservedRoles)
            {
                _reservedRolesCards[rowCounter] = new Card[reservedRole.RoleCount];

                Vector3 rowPosition = (Vector3.back * rowCounter * RESERVED_ROLES_SPACING) + (Vector3.forward * (rowCount - 1) * RESERVED_ROLES_SPACING / 2.0f);
                
                int columnCounter = 0;

                foreach (int roleGameplayTagID in reservedRole.Roles)
                {
                    Vector3 columnPosition = (Vector3.right * columnCounter * RESERVED_ROLES_SPACING) + (Vector3.left * (reservedRole.RoleCount - 1) * RESERVED_ROLES_SPACING / 2.0f);

                    Card card = Instantiate(_cardPrefab, rowPosition + columnPosition, Quaternion.identity);

                    if (roleGameplayTagID > 0)
                    {
                        RoleData role = GameplayDatabaseManager.Instance.GetGameplayData<RoleData>(roleGameplayTagID);
                        card.SetRole(role);
                        card.Flip();
                    }

                    _reservedRolesCards[rowCounter][columnCounter] = card;

                    columnCounter++;

                    if (columnCounter >= reservedRole.RoleCount)
                    {
                        break;
                    }
                }

                rowCounter++;

                if (rowCounter >= rowCount)
                {
                    break;
                }
            }
        }

        private void AdjustCamera()
        {
            Camera.main.transform.position = Camera.main.transform.position.normalized * _gameConfig.CameraOffset.Evaluate(_gameDataManager.PlayerInfos.Count);
        }
        #endregion
    }
}