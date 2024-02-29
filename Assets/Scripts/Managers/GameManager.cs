using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;
using Werewolf.UI;

namespace Werewolf
{
	public class GameManager : NetworkBehaviourSingleton<GameManager>
	{
		#region Server variables
		public List<RoleData> RolesToDistribute { get; private set; }

		private Dictionary<RoleBehavior, RoleData> _unassignedRoleBehaviors = new();

		private Dictionary<PlayerRef, PlayerRole> _playerRoles = new();

		private struct PlayerRole
		{
			public RoleData Data;
			public List<RoleBehavior> Behaviors;
		}

		private Dictionary<RoleBehavior, IndexedReservedRoles> _reservedRolesByBehavior = new();

		private Dictionary<PlayerRef, Action<int>> _chooseReservedRoleCallbacks = new();

		public struct IndexedReservedRoles
		{
			public RoleData[] Roles;
			public RoleBehavior[] Behaviors;
			public int networkIndex;
		}
#if UNITY_SERVER && UNITY_EDITOR
		private Dictionary<RoleBehavior, Card[]> _reservedCardsByBehavior = new();
#endif
		private List<PlayerRef> _playersReady = new();

		private bool _rolesDistributionDone = false;
		private bool _allPlayersReadyToReceiveRole = false;
		private bool _allRolesSent = false;
		private bool _allPlayersReadyToPlay = false;

		private List<NightCall> _nightCalls = new();

		private struct NightCall
		{
			public int PriorityIndex;
			public List<PlayerRef> Players;
		}

		private int _currentNightCallIndex = 0;
		private List<PlayerRef> _playersWaitingFor = new();

		private Dictionary<PlayerRef, Action<PlayerRef>> _choosePlayerCallbacks = new();

		private List<MarkForDeath> _marksForDeath = new();

		public struct MarkForDeath
		{
			public PlayerRef Player;
			public List<string> Marks;
		}

		private Dictionary<PlayerRef, Action> _revealPlayerRoleCallbacks = new();
		#endregion

		#region Networked variables
		[Networked, Capacity(5), SerializeField]
		public NetworkArray<RolesContainer> ReservedRoles { get; }

		[Serializable]
		public struct RolesContainer : INetworkStruct
		{
			public int RoleCount;
			[Networked, Capacity(5)]
			public NetworkArray<int> Roles { get; }
		}
		#endregion

		[field: Header("Config")]
		[field: SerializeField]
		public GameConfig Config { get; private set; }

		[field: Header("Visual")]
		[field: SerializeField]
		private Card _cardPrefab;

		public static event Action OnSpawned;
		public static bool HasSpawned { get; private set; }

		private Dictionary<PlayerRef, Card> _playerCards = new();
		private Card[][] _reservedRolesCards;

		private enum GameplayLoopStep
		{
			NightTransition = 0,
			RoleCall,
			DayTransition,
			DeathReveal,
			Debate,
			Vote,
			Execution,
			Count,
		}

		private GameplayLoopStep _currentGameplayLoopStep;

		private GameDataManager _gameDataManager;
		private GameplayDatabaseManager _gameplayDatabaseManager;
		private UIManager _UIManager;
		private DaytimeManager _daytimeManager;
		private VoteManager _voteManager;

		// Server events
		public event Action OnPreRoleDistribution;
		public event Action OnPostRoleDistribution;
		public event Action OnPreStartGame;
		public event Action<PlayerRef, string> OnMarkForDeathAdded;
		public event Action<PlayerRef> OnMarkForDeathRemoved;

		// Client events
		public event Action OnRoleReceived;

		private readonly Vector3 STARTING_DIRECTION = Vector3.back;

		protected override void Awake()
		{
			base.Awake();

			RolesToDistribute = new();

			if (!Config)
			{
				Debug.LogError("The GameConfig of the GameManager is not defined");
			}
		}

		private void Start()
		{
			_playerCards = new();

			_gameplayDatabaseManager = GameplayDatabaseManager.Instance;
			_UIManager = UIManager.Instance;
			_daytimeManager = DaytimeManager.Instance;
			_voteManager = VoteManager.Instance;

			_voteManager.SetConfig(Config);
			_UIManager.VoteScreen.SetConfig(Config);
		}

		public override void Spawned()
		{
			HasSpawned = true;
			OnSpawned?.Invoke();
		}

		#region Pre Gameplay Loop
		public void PrepareGame(RolesSetup rolesSetup)
		{
			GetGameDataManager();

			SelectRolesToDistribute(rolesSetup);

			OnPreRoleDistribution?.Invoke();
			DistributeRoles();
			OnPostRoleDistribution?.Invoke();

			DetermineNightCalls();
#if UNITY_SERVER && UNITY_EDITOR
			CreatePlayerCardsForServer();
			CreateReservedRoleCardsForServer();
			AdjustCamera();
			LogNightCalls();
#endif
			CheckPreGameplayLoopProgress();
		}

		private void GetGameDataManager()
		{
			_gameDataManager = FindObjectOfType<GameDataManager>();
		}

		#region Roles Selection
		private void SelectRolesToDistribute(RolesSetup rolesSetup)
		{
			// Convert GameplayTagIDs to RoleData
			RoleData defaultRole = _gameplayDatabaseManager.GetGameplayData<RoleData>(rolesSetup.DefaultRole);
			GameDataManager.ConvertToRoleSetupDatas(rolesSetup.MandatoryRoles, out List<RoleSetupData> mandatoryRoles);
			GameDataManager.ConvertToRoleSetupDatas(rolesSetup.AvailableRoles, out List<RoleSetupData> availableRoles);

			List<RoleData> rolesToDistribute = new();

			// Add all mandatory roles first
			foreach (RoleSetupData roleSetup in mandatoryRoles)
			{
				RoleData[] addedRoles = SelectRolesFromRoleSetup(roleSetup, ref rolesToDistribute);
				PrepareRoleBehaviors(addedRoles, ref rolesToDistribute, ref availableRoles);
			}

			List<RoleSetupData> excludedRuleSetups = new();
			int attempts = 0;

			// Complete with available roles at random or default role
			while (rolesToDistribute.Count < _gameDataManager.PlayerInfos.Count)
			{
				int startingRoleCount = rolesToDistribute.Count;

				if (availableRoles.Count <= 0 || attempts >= Config.AvailableRolesMaxAttemptCount)
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
			List<RoleData> rolePool = new(roleSetup.Pool);
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

			roleBehavior.SetPrimaryRoleType(role.PrimaryType);

			foreach (Priority nightPriority in role.NightPriorities)
			{
				roleBehavior.AddNightPriority(nightPriority);
			}

			roleBehavior.SetIsPrimaryBehavior(true);

			_unassignedRoleBehaviors.Add(roleBehavior, role);

			roleBehavior.Init();
			roleBehavior.OnSelectedToDistribute(ref rolesToDistribute, ref availableRoles);
		}
		#endregion

		#region Roles Distribution
		private void DistributeRoles()
		{
			foreach (KeyValuePair<PlayerRef, PlayerInfo> playerInfo in _gameDataManager.PlayerInfos)
			{
				RoleData selectedRole = RolesToDistribute[UnityEngine.Random.Range(0, RolesToDistribute.Count)];
				RolesToDistribute.Remove(selectedRole);

				List<RoleBehavior> selectedBehaviors = new();

				foreach (KeyValuePair<RoleBehavior, RoleData> unassignedRoleBehavior in _unassignedRoleBehaviors)
				{
					if (unassignedRoleBehavior.Value == selectedRole)
					{
						RoleBehavior selectedBehavior = unassignedRoleBehavior.Key;
						selectedBehavior.SetPlayer(playerInfo.Key);
						selectedBehaviors.Add(selectedBehavior);
						_unassignedRoleBehaviors.Remove(unassignedRoleBehavior.Key);
						break;
					}
				}

				_playerRoles.Add(playerInfo.Key, new() { Data = selectedRole, Behaviors = selectedBehaviors });
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
			// Remove any players that do not have a behavior that needs to be called at night
			List<PlayerRef> players = _playerRoles.Keys.ToList();

			for (int i = players.Count - 1; i >= 0; i--)
			{
				List<RoleBehavior> behaviors = _playerRoles[players[i]].Behaviors;

				if (behaviors.Count > 0 && behaviors[0].NightPriorities.Count > 0)
				{
					continue;
				}

				players.RemoveAt(i);
			}

			// Make a list of all different priorities
			List<int> priorities = new();

			foreach (PlayerRef player in players)
			{
				foreach (Priority priority in _playerRoles[player].Behaviors[0].NightPriorities)
				{
					if (priorities.Contains(priority.index))
					{
						continue;
					}

					priorities.Add(priority.index);
				}
			}

			priorities.Sort();

			// Loop threw the priorities and store all players with similare priorities together
			for (int i = 0; i < priorities.Count; i++)
			{
				List<PlayerRef> playersToCall = new();

				foreach (PlayerRef player in players)
				{
					foreach (Priority priority in _playerRoles[player].Behaviors[0].NightPriorities)
					{
						if (priority.index == priorities[i])
						{
							playersToCall.Add(player);
							break;
						}
					}
				}

				_nightCalls.Add(new() { PriorityIndex = priorities[i], Players = playersToCall });
			}
		}
#if UNITY_SERVER && UNITY_EDITOR
		private void LogNightCalls()
		{
			Debug.Log("----------------------Night Calls----------------------");

			foreach (NightCall nightCall in _nightCalls)
			{
				string roles = $"Priority: {nightCall.PriorityIndex} || ";

				foreach (PlayerRef player in nightCall.Players)
				{
					roles += $"{_playerRoles[player].Data.Name} || ";
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
				RPC_GivePlayerRole(playerInfo.Key, _playerRoles[playerInfo.Key].Data.GameplayTag.CompactTagId);
			}

			_allRolesSent = true;
		}

		#region RPC calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_GivePlayerRole([RpcTarget] PlayerRef player, int roleGameplayTagID)
		{
			if (!_gameDataManager)
			{
				GetGameDataManager();
			}

			CreatePlayerCards(player, _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID));
			CreateReservedRoleCards();
			AdjustCamera();

			OnRoleReceived?.Invoke();
		}
		#endregion
		#endregion

		#region Loop Progress
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

		private void CheckPreGameplayLoopProgress()
		{
			if (_rolesDistributionDone && _allPlayersReadyToReceiveRole && !_allRolesSent)
			{
				SendPlayerRoles();
			}
			else if (_allRolesSent && _allPlayersReadyToPlay)
			{
				OnPreStartGame?.Invoke();
				StartGame();
			}
		}

		#region RPC Calls
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
		#endregion
		#endregion

		#region Gameplay Loop
		private void StartGame()
		{
			_currentGameplayLoopStep = GameplayLoopStep.NightTransition;
			StartCoroutine(TransitionToNight());
		}

		#region Gameplay Loop Steps
		private void MoveToNextGameplayLoopStep()
		{
			_currentGameplayLoopStep++;

			if (_currentGameplayLoopStep == GameplayLoopStep.Count)
			{
				_currentGameplayLoopStep = 0;
			}

			switch (_currentGameplayLoopStep)
			{
				case GameplayLoopStep.NightTransition:
					StartCoroutine(TransitionToNight());
					break;
				case GameplayLoopStep.RoleCall:
					StartCoroutine(CallRoles());
					break;
				case GameplayLoopStep.DayTransition:

					break;
				case GameplayLoopStep.DeathReveal:

					break;
				case GameplayLoopStep.Debate:

					break;
				case GameplayLoopStep.Vote:

					break;
				case GameplayLoopStep.Execution:

					break;
			}
		}

		private IEnumerator TransitionToNight()
		{
			RPC_TransitionToNight();
#if UNITY_SERVER && UNITY_EDITOR
			_daytimeManager.ChangeDaytime(Daytime.Night);
#endif
			yield return new WaitForSeconds(Config.DaytimeTransitionStepDuration);

			MoveToNextGameplayLoopStep();
		}

		private IEnumerator CallRoles()
		{
			_currentNightCallIndex = 0;

			while (_currentNightCallIndex < _nightCalls.Count)
			{
				NightCall nightCall = _nightCalls[_currentNightCallIndex];
				int displayRoleGameplayTagID = GetDisplayedRoleGameplayTagID(nightCall);

				Dictionary<PlayerRef, RoleBehavior> actifBehaviors = new();

				// Role call all the roles that must play
				foreach (PlayerRef player in nightCall.Players)
				{
					// TODO: Skip if the player is dead
					bool skipPlayer = false;

					foreach (RoleBehavior behavior in _playerRoles[player].Behaviors)
					{
						int[] nightPrioritiesIndexes = behavior.GetNightPrioritiesIndexes();

						if (nightPrioritiesIndexes.Contains(nightCall.PriorityIndex))
						{
							behavior.SetTimedOut(false);
							skipPlayer = !behavior.OnRoleCall();

							if (!skipPlayer)
							{
								_playersWaitingFor.Add(player);
								actifBehaviors.Add(player, behavior);
							}

							break;
						}
					}

					if (!skipPlayer && !_playersWaitingFor.Contains(player))
					{
						Debug.LogError($"{player} is suppose to play, but he has no behavior with the PriorityIndex {nightCall.PriorityIndex}");
					}
				}

				if (_playersWaitingFor.Count > 0)
				{
					// Tell the players that are not playing, which role is playing
					foreach (KeyValuePair<PlayerRef, PlayerRole> playerRole in _playerRoles)
					{
						if (_playersWaitingFor.Contains(playerRole.Key))
						{
							continue;
						}

						RPC_DisplayRolePlaying(playerRole.Key, displayRoleGameplayTagID);
					}
#if UNITY_SERVER && UNITY_EDITOR
					if (!_voteManager.IsPreparingToVote())
					{
						DisplayRolePlaying(displayRoleGameplayTagID);
					}
#endif
					// Start the vote if it has been prepared
					if (_voteManager.IsPreparingToVote())
					{
						_voteManager.StartVote();
					}

					float elapsedTime = .0f;

					while (!IsNightCallOver(elapsedTime))
					{
						elapsedTime += Time.deltaTime;
						yield return 0;
					}

					// End the turn of all players that are still not done playing
					if (_playersWaitingFor.Count > 0)
					{
						foreach (PlayerRef player in _playersWaitingFor)
						{
							actifBehaviors[player].SetTimedOut(true);
							actifBehaviors[player].OnRoleTimeOut();
						}

						_playersWaitingFor.Clear();
					}

					RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
					HideUI();
#endif
					yield return new WaitForSeconds(Config.NightCallChangeDuration);
				}

				_currentNightCallIndex++;
			}

			MoveToNextGameplayLoopStep();
		}

		private bool IsNightCallOver(float elapsedTime)
		{
			return !_voteManager.IsVoting()
				&& _revealPlayerRoleCallbacks.Count <= 0
				&& ((_playersWaitingFor.Count <= 0 && elapsedTime >= Config.NightCallMinimumDuration) || elapsedTime >= Config.NightCallMaximumDuration);
		}

		public void StopWaintingForPlayer(PlayerRef player)
		{
			if (!_playersWaitingFor.Contains(player))
			{
				return;
			}

			_playersWaitingFor.Remove(player);
		}
		#endregion

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_TransitionToNight()
		{
			_daytimeManager.ChangeDaytime(Daytime.Night);
		}
		#endregion
		#endregion

		#region Role Change
		public void ChangeRole(PlayerRef player, RoleData roleData, RoleBehavior roleBehavior)
		{
			// Change player role
			PlayerRole playerRole = new() { Data = roleData };
			playerRole.Behaviors = _playerRoles[player].Behaviors;
			_playerRoles[player] = playerRole;

#if UNITY_SERVER && UNITY_EDITOR
			_playerCards[player].SetRole(roleData);
#endif
			// Remove the primary behavior
			foreach (RoleBehavior behavior in _playerRoles[player].Behaviors)
			{
				if (!behavior.IsPrimaryBehavior)
				{
					continue;
				}

				RemoveBehavior(player, behavior);
				break;
			}

			if (roleBehavior)
			{
				AddBehavior(player, roleBehavior);
				roleBehavior.SetIsPrimaryBehavior(true);
			}

			RPC_ChangePlayerRole(player, roleData.GameplayTag.CompactTagId);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_ChangePlayerRole([RpcTarget] PlayerRef player, int roleGameplayTagID)
		{
			_playerCards[player].SetRole(_gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID));
		}
		#endregion
		#endregion

		#region Behavior Change
		public void AddBehavior(PlayerRef player, RoleBehavior behavior)
		{
			int[] nightPrioritiesIndexes = behavior.GetNightPrioritiesIndexes();

			// Remove any contradicting behaviors
			List<RoleBehavior> behaviorsToRemove = FindNightCallBehaviors(player, nightPrioritiesIndexes);

			foreach (RoleBehavior behaviorToRemove in behaviorsToRemove)
			{
				RemoveBehavior(player, behaviorToRemove);
			}

			// Add the new behavior
			foreach (int priority in nightPrioritiesIndexes)
			{
				AddPlayerToNightCall(priority, player);
			}

			_playerRoles[player].Behaviors.Add(behavior);

			behavior.SetPlayer(player);
#if UNITY_SERVER && UNITY_EDITOR
			behavior.transform.position = _playerCards[player].transform.position;
#endif
		}

		public void RemoveBehavior(PlayerRef player, RoleBehavior behavior)
		{
			int[] nightPrioritiesIndexes = behavior.GetNightPrioritiesIndexes();

			foreach (int priority in nightPrioritiesIndexes)
			{
				RemovePlayerFromNightCall(priority, player);
			}

			for (int i = _playerRoles[player].Behaviors.Count - 1; i >= 0; i--)
			{
				if (_playerRoles[player].Behaviors[i] != behavior)
				{
					continue;
				}

				_playerRoles[player].Behaviors.RemoveAt(i);
				break;
			}

			Destroy(behavior.gameObject);
		}

		// Returns all the RoleBehavior that are called during a night call and that have at least one of the prioritiesIndex
		private List<RoleBehavior> FindNightCallBehaviors(PlayerRef player, int[] prioritiesIndex)
		{
			List<RoleBehavior> behaviorsToRemove = new();

			foreach (RoleBehavior behavior in _playerRoles[player].Behaviors)
			{
				int[] nightPrioritiesIndexes = behavior.GetNightPrioritiesIndexes();

				foreach (int behaviorNightPriority in nightPrioritiesIndexes)
				{
					if (prioritiesIndex.Contains(behaviorNightPriority) && !behaviorsToRemove.Contains(behavior))
					{
						behaviorsToRemove.Add(behavior);
						break;
					}
				}
			}

			return behaviorsToRemove;
		}
		#endregion

		#region Night Call Change
		private void AddPlayerToNightCall(int priorityIndex, PlayerRef player)
		{
			NightCall nightCall;

			for (int i = 0; i < _nightCalls.Count; i++)
			{
				if (_nightCalls[i].PriorityIndex == priorityIndex)
				{
					if (_nightCalls[i].Players.Contains(player))
					{
						Debug.LogError("Tried to add duplicated player to a night call");
						return;
					}

					_nightCalls[i].Players.Add(player);
					return;
				}
				else if (_nightCalls[i].PriorityIndex > priorityIndex)
				{
					nightCall = new();
					nightCall.PriorityIndex = priorityIndex;
					nightCall.Players = new() { player };

					_nightCalls.Insert(i, nightCall);

					if (i <= _currentNightCallIndex)
					{
						_currentNightCallIndex++;
					}

					return;
				}
			}

			nightCall = new();
			nightCall.PriorityIndex = priorityIndex;
			nightCall.Players = new() { player };

			_nightCalls.Add(nightCall);
		}

		public void RemovePlayerFromNightCall(int priorityIndex, PlayerRef player)
		{
			for (int i = 0; i < _nightCalls.Count; i++)
			{
				if (_nightCalls[i].PriorityIndex != priorityIndex)
				{
					continue;
				}

				_nightCalls[i].Players.Remove(player);

				if (_nightCalls[i].Players.Count <= 0)
				{
					_nightCalls.RemoveAt(i);

					if (i <= _currentNightCallIndex)
					{
						_currentNightCallIndex--;
					}
				}

				break;
			}
		}
		#endregion

		#region Roles Reservation
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
			_reservedRolesByBehavior.Add(roleBehavior, new() { Roles = roles, Behaviors = behaviors, networkIndex = _reservedRolesByBehavior.Count });
		}

		public IndexedReservedRoles GetReservedRoles(RoleBehavior roleBehavior)
		{
			IndexedReservedRoles reservedRoles = new();

			if (_reservedRolesByBehavior.ContainsKey(roleBehavior))
			{
				reservedRoles = _reservedRolesByBehavior[roleBehavior];
			}

			return reservedRoles;
		}

		public void RemoveReservedRoles(RoleBehavior ReservedRoleOwner, int[] specificIndexes)
		{
			if (!_reservedRolesByBehavior.ContainsKey(ReservedRoleOwner))
			{
				return;
			}

			int networkIndex = _reservedRolesByBehavior[ReservedRoleOwner].networkIndex;
			bool mustRemoveEntry = true;

			if (specificIndexes.Length > 0)
			{
				foreach (int specificIndex in specificIndexes)
				{
					RoleBehavior behavior = _reservedRolesByBehavior[ReservedRoleOwner].Behaviors[specificIndex];

					if (behavior && behavior.Player == null)
					{
						Destroy(behavior.gameObject);
					}

					_reservedRolesByBehavior[ReservedRoleOwner].Roles[specificIndex] = null;
					_reservedRolesByBehavior[ReservedRoleOwner].Behaviors[specificIndex] = null;
#if UNITY_SERVER && UNITY_EDITOR
					if (_reservedCardsByBehavior[ReservedRoleOwner][specificIndex])
					{
						Destroy(_reservedCardsByBehavior[ReservedRoleOwner][specificIndex].gameObject);
					}

					_reservedCardsByBehavior[ReservedRoleOwner][specificIndex] = null;
#endif
					// Update networked variable
					// Networked data and server data should ALWAYS be aligned, therefore no need to loop to find the corresponding role
					RolesContainer rolesContainer = new();
					rolesContainer.RoleCount = ReservedRoles[networkIndex].RoleCount;

					for (int i = 0; i < rolesContainer.Roles.Length; i++)
					{
						if (i == specificIndex)
						{
							continue;
						}

						rolesContainer.Roles.Set(i, ReservedRoles[networkIndex].Roles.Get(i));
					}

					ReservedRoles.Set(networkIndex, rolesContainer);

					// Check if the entry is now empty and can be removed
					for (int i = 0; i < _reservedRolesByBehavior[ReservedRoleOwner].Roles.Length; i++)
					{
						if (_reservedRolesByBehavior[ReservedRoleOwner].Roles[i])
						{
							mustRemoveEntry = false;
						}
					}
				}
			}
			else
			{
				// Update server variables
				for (int i = 0; i < _reservedRolesByBehavior[ReservedRoleOwner].Roles.Length; i++)
				{
					RoleBehavior behavior = _reservedRolesByBehavior[ReservedRoleOwner].Behaviors[i];

					if (behavior && behavior.Player == null)
					{
						Destroy(behavior.gameObject);
					}
#if UNITY_SERVER && UNITY_EDITOR
					if (_reservedCardsByBehavior[ReservedRoleOwner][i])
					{
						Destroy(_reservedCardsByBehavior[ReservedRoleOwner][i].gameObject);
					}
#endif
				}

				// Update networked variable
				RolesContainer rolesContainer = new();
				ReservedRoles.Set(networkIndex, rolesContainer);
			}

			// Update server variable entry
			if (mustRemoveEntry)
			{
				_reservedRolesByBehavior.Remove(ReservedRoleOwner);
#if UNITY_SERVER && UNITY_EDITOR
				_reservedCardsByBehavior.Remove(ReservedRoleOwner);
#endif
			}

			// Tell clients to update visual on there side
			RPC_UpdateDisplayedReservedRole(networkIndex);
		}

		// Returns if there is any reserved roles the player can choose from (will be false if the behavior is already waiting for a callback from this method)
		public bool AskClientToChooseReservedRole(RoleBehavior ReservedRoleOwner, bool mustChooseOne, Action<int> callback)
		{
			if (!_reservedRolesByBehavior.ContainsKey(ReservedRoleOwner) || _chooseReservedRoleCallbacks.ContainsKey(ReservedRoleOwner.Player))
			{
				return false;
			}

			RoleData[] roleDatas = _reservedRolesByBehavior[ReservedRoleOwner].Roles;
			RolesContainer rolesContainer = new() { RoleCount = roleDatas.Length };

			for (int i = 0; i < roleDatas.Length; i++)
			{
				rolesContainer.Roles.Set(i, roleDatas[i].GameplayTag.CompactTagId);
			}

			_chooseReservedRoleCallbacks.Add(ReservedRoleOwner.Player, callback);
			RPC_ClientChooseReservedRole(ReservedRoleOwner.Player, rolesContainer, mustChooseOne);

			return true;
		}

		private void GiveReservedRoleChoice(int choice)
		{
			RPC_GiveReservedRoleChoice(choice);
		}

		public void StopChoosingReservedRole(PlayerRef reservedRoleOwner)
		{
			_chooseReservedRoleCallbacks.Remove(reservedRoleOwner);
			RPC_ClientStopChoosingReservedRole(reservedRoleOwner);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_ClientChooseReservedRole([RpcTarget] PlayerRef player, RolesContainer rolesContainer, bool mustChooseOne)
		{
			List<Choice.ChoiceData> choices = new();

			foreach (int roleGameplayTag in rolesContainer.Roles)
			{
				if (roleGameplayTag <= 0)
				{
					break;
				}

				RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTag);
				choices.Add(new() { Image = roleData.Image, Value = roleGameplayTag });
			}

			_UIManager.ChoiceScreen.OnConfirmChoice += GiveReservedRoleChoice;

			_UIManager.ChoiceScreen.Initialize(mustChooseOne ? Config.ChooseRoleObligatoryText : Config.ChooseRoleText, Config.ChoosedRoleText, Config.DidNotChoosedRoleText, choices.ToArray(), mustChooseOne);
			_UIManager.FadeIn(_UIManager.ChoiceScreen, Config.UITransitionDuration);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_GiveReservedRoleChoice(int roleGameplayTagID, RpcInfo info = default)
		{
			if (!_chooseReservedRoleCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_chooseReservedRoleCallbacks[info.Source](roleGameplayTagID);
			_chooseReservedRoleCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_UpdateDisplayedReservedRole(int networkIndex)
		{
			RolesContainer rolesContainer = ReservedRoles[networkIndex];
			for (int i = 0; i < rolesContainer.Roles.Count(); i++)
			{
				if (rolesContainer.Roles[i] != 0 || _reservedRolesCards[networkIndex].Length <= i || !_reservedRolesCards[networkIndex][i])
				{
					continue;
				}

				Destroy(_reservedRolesCards[networkIndex][i].gameObject);
			}
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_ClientStopChoosingReservedRole([RpcTarget] PlayerRef player)
		{
			_UIManager.ChoiceScreen.DisableConfirmButton();
			_UIManager.ChoiceScreen.OnConfirmChoice -= GiveReservedRoleChoice;
		}
		#endregion
		#endregion

		#region Choose a Player
		public bool AskClientToChoosePlayer(PlayerRef choosingPlayer, PlayerRef[] immunePlayers, string displayText, Action<PlayerRef> callback)
		{
			if (_choosePlayerCallbacks.ContainsKey(choosingPlayer))
			{
				return false;
			}

			_choosePlayerCallbacks.Add(choosingPlayer, callback);
			RPC_ClientChoosePlayer(choosingPlayer, immunePlayers, displayText);

			return true;
		}

		private void OnClientChooseNoCard()
		{
			OnClientChooseCard(null);
		}

		private void OnClientChooseCard(Card card)
		{
			StopChoosingPlayer();
			HideUI();

			RPC_GivePlayerChoice(card ? card.Player : PlayerRef.None);
		}

		public void StopChoosingPlayer(PlayerRef player)
		{
			_choosePlayerCallbacks.Remove(player);
			RPC_ClientStopChoosingPlayer(player);
		}

		private void StopChoosingPlayer()
		{
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				playerCard.Value.ResetSelectionMode();
				playerCard.Value.OnCardClick -= OnClientChooseCard;
			}

			_UIManager.TitleScreen.OnConfirm -= OnClientChooseNoCard;
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_ClientChoosePlayer([RpcTarget] PlayerRef player, PlayerRef[] immunePlayers, string displayText)
		{
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (Array.IndexOf(immunePlayers, playerCard.Key) >= 0)
				{
					playerCard.Value.SetSelectionMode(true, false);
					continue;
				}

				playerCard.Value.SetSelectionMode(true, true);
				playerCard.Value.OnCardClick += OnClientChooseCard;
			}

			_UIManager.TitleScreen.Initialize(null, displayText, true, Config.SkipTurnText);
			_UIManager.TitleScreen.OnConfirm += OnClientChooseNoCard;
			_UIManager.FadeIn(_UIManager.TitleScreen, Config.UITransitionDuration);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_GivePlayerChoice(PlayerRef player, RpcInfo info = default)
		{
			if (!_choosePlayerCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_choosePlayerCallbacks[info.Source](player);
			_choosePlayerCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_ClientStopChoosingPlayer([RpcTarget] PlayerRef player)
		{
			StopChoosingPlayer();
		}
		#endregion
		#endregion

		#region Mark For Death
		public void AddMarkForDeath(PlayerRef player, string mark)
		{
			for (int i = 0; i < _marksForDeath.Count; i++)
			{
				if (_marksForDeath[i].Player == player)
				{
					_marksForDeath[i].Marks.Add(mark);
					OnMarkForDeathAdded?.Invoke(player, mark);
					return;
				}
			}

			_marksForDeath.Add(new() { Player = player, Marks = new() { mark } });
			OnMarkForDeathAdded?.Invoke(player, mark);
		}

		public void RemoveMarkForDeath(PlayerRef player)
		{
			for (int i = 0; i < _marksForDeath.Count; i++)
			{
				if (_marksForDeath[i].Player == player)
				{
					_marksForDeath.RemoveAt(i);
					OnMarkForDeathRemoved?.Invoke(player);
					return;
				}
			}
		}
		#endregion

		#region Role Reveal
		public bool RevealPlayerRole(PlayerRef playerRevealed, PlayerRef revealTo, bool flipDuringZoom, Action callback)
		{
			if (_revealPlayerRoleCallbacks.ContainsKey(revealTo))
			{
				return false;
			}

			_revealPlayerRoleCallbacks.Add(revealTo, callback);
			RPC_RevealPlayerRole(revealTo, playerRevealed, _playerRoles[playerRevealed].Data.GameplayTag.CompactTagId, flipDuringZoom);

			return true;
		}

		private IEnumerator AnimatePlayerRoleReveal(Card card, bool waitBeforeReveal, bool returnFacedown)
		{
			Camera mainCamera = Camera.main;

			Vector3 startingPosition = card.transform.position;
			Vector3 targetPosition = mainCamera.transform.position + mainCamera.transform.forward * Config.RevealDistanceToCamera;

			Quaternion currentRotation = card.transform.rotation;
			Quaternion targetRotation;

			// Move the card to the camera
			float elapsedTime = .0f;

			if (waitBeforeReveal)
			{
				targetRotation = Quaternion.LookRotation(mainCamera.transform.up, -mainCamera.transform.forward);
			}
			else
			{
				targetRotation = Quaternion.LookRotation(mainCamera.transform.up, mainCamera.transform.forward);
			}

			while (elapsedTime < Config.MoveToCameraDuration)
			{
				elapsedTime += Time.deltaTime;

				float progress = elapsedTime / Config.MoveToCameraDuration;

				card.transform.position = Vector3.Lerp(startingPosition, targetPosition, progress);
				card.transform.rotation = Quaternion.Lerp(currentRotation, targetRotation, progress);

				yield return 0;
			}

			if (waitBeforeReveal)
			{
				// Wait before flipping the card
				elapsedTime = .0f;

				while (elapsedTime < Config.WaitRevealDuration)
				{
					elapsedTime += Time.deltaTime;
					yield return 0;
				}

				// Flip the card
				elapsedTime = .0f;

				targetRotation = Quaternion.LookRotation(mainCamera.transform.up, mainCamera.transform.forward);

				while (elapsedTime < Config.RevealFlipDuration)
				{
					elapsedTime += Time.deltaTime;

					float progress = elapsedTime / Config.RevealFlipDuration;

					card.transform.rotation = Quaternion.Lerp(currentRotation, targetRotation, progress);

					yield return 0;
				}
			}

			// Hold the card
			elapsedTime = .0f;

			while (elapsedTime < Config.HoldRevealDuration)
			{
				elapsedTime += Time.deltaTime;
				yield return 0;
			}

			// Put the card back down
			elapsedTime = .0f;

			currentRotation = targetRotation;

			if (returnFacedown)
			{
				targetRotation = Quaternion.LookRotation(Vector3.forward, -Vector3.down);
			}
			else
			{
				targetRotation = Quaternion.LookRotation(Vector3.forward, Vector3.down);
			}

			while (elapsedTime < Config.MoveToCameraDuration)
			{
				elapsedTime += Time.deltaTime;

				float progress = elapsedTime / Config.MoveToCameraDuration;

				card.transform.position = Vector3.Lerp(targetPosition, startingPosition, progress);
				card.transform.rotation = Quaternion.Lerp(currentRotation, targetRotation, progress);

				yield return 0;
			}

			if (returnFacedown)
			{
				card.SetRole(null);
			}

			RPC_RevealPlayerRoleFinished();
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_RevealPlayerRole([RpcTarget] PlayerRef player, PlayerRef playerRevealed, int gameplayDataID, bool flipDuringZoom)
		{
			RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(gameplayDataID);

			if (!roleData)
			{
				return;
			}

			Card card = _playerCards[playerRevealed];
			card.SetRole(roleData);

			StartCoroutine(AnimatePlayerRoleReveal(card, false, true));
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_RevealPlayerRoleFinished(RpcInfo info = default)
		{
			if (!_revealPlayerRoleCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_revealPlayerRoleCallbacks[info.Source]();
			_revealPlayerRoleCallbacks.Remove(info.Source);
		}
		#endregion
		#endregion

		#region UI
		private void DisplayRolePlaying(int roleGameplayTagID)
		{
			RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID);
			string text = roleData.CanHaveMultiples ? Config.RolePlayingTextPlurial : Config.RolePlayingTextSingular;

			_UIManager.TitleScreen.Initialize(roleData.Image, string.Format(text, roleData.Name.ToLower()));
			_UIManager.FadeIn(_UIManager.TitleScreen, Config.UITransitionDuration);
		}

		private int GetDisplayedRoleGameplayTagID(NightCall nightCall)
		{
			RoleData alias = null;
			bool aliasFound = false;

			foreach (RoleBehavior behavior in _playerRoles[nightCall.Players[0]].Behaviors)
			{
				foreach (Priority nightPrioritie in behavior.NightPriorities)
				{
					if (nightPrioritie.index != nightCall.PriorityIndex)
					{
						continue;
					}

					alias = nightPrioritie.alias;
					aliasFound = true;

					break;
				}

				if (aliasFound)
				{
					break;
				}
			}

			if (alias)
			{
				return alias.GameplayTag.CompactTagId;
			}
			else
			{
				return _playerRoles[nightCall.Players[0]].Data.GameplayTag.CompactTagId;
			}
		}

		private void HideUI()
		{
			_UIManager.FadeOut(Config.UITransitionDuration);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayRolePlaying([RpcTarget] PlayerRef player, int roleGameplayTagID)
		{
			DisplayRolePlaying(roleGameplayTagID);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_HideUI()
		{
			HideUI();
		}
		#endregion
		#endregion

		#region Visual
#if UNITY_SERVER && UNITY_EDITOR
		private void CreatePlayerCardsForServer()
		{
			float rotationIncrement = 360.0f / _playerRoles.Count;
			Vector3 startingPosition = STARTING_DIRECTION * Config.CardsOffset.Evaluate(_playerRoles.Count);

			int counter = -1;

			foreach (KeyValuePair<PlayerRef, PlayerRole> playerRole in _playerRoles)
			{
				counter++;

				Quaternion rotation = Quaternion.Euler(0, rotationIncrement * counter, 0);

				Card card = Instantiate(_cardPrefab, rotation * startingPosition, Quaternion.identity);
				card.transform.position += Vector3.up * card.Thickness / 2.0f;

				card.SetPlayer(playerRole.Key);
				card.SetRole(playerRole.Value.Data);
				card.SetNickname(_gameDataManager.PlayerInfos[playerRole.Key].Nickname);
				card.DetachNicknameCanvas();
				card.Flip();

				_playerCards.Add(playerRole.Key, card);

				if (playerRole.Value.Behaviors.Count <= 0)
				{
					continue;
				}

				foreach (RoleBehavior behavior in playerRole.Value.Behaviors)
				{
					behavior.transform.position = card.transform.position;
				}
			}

			_voteManager.SetPlayerCards(_playerCards);
		}

		private void CreateReservedRoleCardsForServer()
		{
			int rowCounter = 0;

			foreach (KeyValuePair<RoleBehavior, IndexedReservedRoles> reservedRoleByBehavior in _reservedRolesByBehavior)
			{
				Vector3 rowPosition = (Vector3.back * rowCounter * Config.ReservedRolesSpacing) + (Vector3.forward * (_reservedRolesByBehavior.Count - 1) * Config.ReservedRolesSpacing / 2.0f);
				Card[] cards = new Card[reservedRoleByBehavior.Value.Roles.Length];

				int columnCounter = 0;

				foreach (RoleData role in reservedRoleByBehavior.Value.Roles)
				{
					Vector3 columnPosition = (Vector3.right * columnCounter * Config.ReservedRolesSpacing) + (Vector3.left * (reservedRoleByBehavior.Value.Roles.Length - 1) * Config.ReservedRolesSpacing / 2.0f);

					Card card = Instantiate(_cardPrefab, rowPosition + columnPosition, Quaternion.identity);
					card.transform.position += Vector3.up * card.Thickness / 2.0f;

					card.SetRole(role);
					card.Flip();

					cards[columnCounter] = card;

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
			Vector3 startingPosition = STARTING_DIRECTION * Config.CardsOffset.Evaluate(playerCount);

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

				Card card = Instantiate(_cardPrefab, rotation * startingPosition, Quaternion.identity);
				card.transform.position += Vector3.up * card.Thickness / 2.0f;

				card.SetPlayer(playerInfo.Key);
				card.SetNickname(playerInfo.Value.Nickname);
				card.DetachNicknameCanvas();

				if (playerInfo.Key == bottomPlayer)
				{
					card.SetRole(playerRole);
					card.Flip();
				}

				_playerCards.Add(playerInfo.Key, card);
			}

			// The VoteManager needs to know the player cards
			_voteManager.SetPlayerCards(_playerCards);
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

			if (rowCount <= 0)
			{
				return;
			}

			int rowCounter = 0;

			// Create the reserved cards
			foreach (RolesContainer reservedRole in ReservedRoles)
			{
				_reservedRolesCards[rowCounter] = new Card[reservedRole.RoleCount];

				Vector3 rowPosition = (Vector3.back * rowCounter * Config.ReservedRolesSpacing) + (Vector3.forward * (rowCount - 1) * Config.ReservedRolesSpacing / 2.0f);

				int columnCounter = 0;

				foreach (int roleGameplayTagID in reservedRole.Roles)
				{
					Vector3 columnPosition = (Vector3.right * columnCounter * Config.ReservedRolesSpacing) + (Vector3.left * (reservedRole.RoleCount - 1) * Config.ReservedRolesSpacing / 2.0f);

					Card card = Instantiate(_cardPrefab, rowPosition + columnPosition, Quaternion.identity);
					card.transform.position += Vector3.up * card.Thickness / 2.0f;

					if (roleGameplayTagID > 0)
					{
						RoleData role = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID);
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
			Camera.main.transform.position = Camera.main.transform.position.normalized * Config.CameraOffset.Evaluate(_gameDataManager.PlayerInfos.Count);
		}
		#endregion
	}
}