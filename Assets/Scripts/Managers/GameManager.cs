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
	public struct PlayerData
	{
		public RoleData Role;
		public List<RoleBehavior> Behaviors;
		public bool IsAlive;
	}

	public class GameManager : NetworkBehaviourSingleton<GameManager>
	{
		#region Server variables
		public List<RoleData> RolesToDistribute { get; private set; }

		private Dictionary<RoleBehavior, RoleData> _unassignedRoleBehaviors = new();

		public Dictionary<PlayerRef, PlayerData> Players { get; private set; }

		private int _alivePlayerCount;

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

		private List<PlayerGroup> _playerGroups = new();

		private struct PlayerGroup
		{
			public int Index;
			public List<PlayerRef> Players;
		}

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

		private bool _isPlayerDeathRevealCompleted;
		private IEnumerator _revealPlayerDeathCoroutine;

		private Dictionary<PlayerRef, Action<PlayerRef>> _revealPlayerRoleCallbacks = new();
		private Dictionary<PlayerRef, Action> _moveCardToCameraCallbacks = new ();
		private Dictionary<PlayerRef, Action> _flipFaceUpCallbacks = new ();
		private Dictionary<PlayerRef, Action> _putCardBackDownCallbacks = new();

		private Dictionary<PlayerRef, Action> _promptPlayerCallbacks = new();
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

		public static event Action ManagerSpawned;

		// Server events
		public event Action PreRoleDistribution;
		public event Action PostRoleDistribution;
		public event Action OnPreStartGame;
		public event Action<PlayerRef, string> OnMarkForDeathAdded;
		public event Action<PlayerRef, float> WaitBeforeDeathRevealStarted;
		public event Action<PlayerRef> WaitBeforeDeathRevealEnded;
		public event Action<PlayerRef> PlayerDeathRevealEnded;

		// Client events
		public event Action OnRoleReceived;

		private readonly Vector3 STARTING_DIRECTION = Vector3.back;

		protected override void Awake()
		{
			base.Awake();

			RolesToDistribute = new();
			Players = new();

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
			_voteManager.SetPlayers(Players);
			_UIManager.TitleScreen.SetConfig(Config);
			_UIManager.ChoiceScreen.SetConfig(Config);
			_UIManager.VoteScreen.SetConfig(Config);
		}

		public override void Spawned()
		{
			HasSpawned = true;
			ManagerSpawned?.Invoke();
		}

		#region Pre Gameplay Loop
		public void PrepareGame(RolesSetup rolesSetup)
		{
			GetGameDataManager();

			SelectRolesToDistribute(rolesSetup);

			PreRoleDistribution?.Invoke();
			DistributeRoles();
			PostRoleDistribution?.Invoke();

			_alivePlayerCount = Players.Count;

			DeterminePlayerGroups();
			DetermineNightCalls();
#if UNITY_SERVER && UNITY_EDITOR
			CreatePlayerCardsForServer();
			CreateReservedRoleCardsForServer();
			AdjustCamera();
			LogNightCalls();
			_voteManager.SetPlayerCards(_playerCards);
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

			foreach(int playerGroupIndex in role.PlayerGroupIndexes)
			{
				roleBehavior.AddPlayerGroupIndex(playerGroupIndex);
			}

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

				Players.Add(playerInfo.Key, new() { Role = selectedRole, Behaviors = selectedBehaviors, IsAlive = true });
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

		private void DeterminePlayerGroups()
		{
			foreach (KeyValuePair<PlayerRef, PlayerData> player in Players)
			{
				if (player.Value.Behaviors.Count <= 0)
				{
					foreach(int playerGroupIndex in player.Value.Role.PlayerGroupIndexes)
					{
						AddPlayerToPlayerGroup(playerGroupIndex, player.Key);
					}

					continue;
				}

				foreach(int playerGroupIndex in player.Value.Behaviors[0].GetCurrentPlayerGroups())
				{
					AddPlayerToPlayerGroup(playerGroupIndex, player.Key);
				}
			}
		}

		private void DetermineNightCalls()
		{
			// Remove any players that do not have a behavior that needs to be called at night
			List<PlayerRef> players = Players.Keys.ToList();

			for (int i = players.Count - 1; i >= 0; i--)
			{
				List<RoleBehavior> behaviors = Players[players[i]].Behaviors;

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
				foreach (Priority priority in Players[player].Behaviors[0].NightPriorities)
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
					foreach (Priority priority in Players[player].Behaviors[0].NightPriorities)
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
					roles += $"{Players[player].Role.Name} || ";
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
				RPC_GivePlayerRole(playerInfo.Key, Players[playerInfo.Key].Role.GameplayTag.CompactTagId);
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

			_voteManager.SetPlayerCards(_playerCards);

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
			_currentGameplayLoopStep = GameplayLoopStep.Execution;
			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		#region Gameplay Loop Steps
		private IEnumerator MoveToNextGameplayLoopStep()
		{
			_currentGameplayLoopStep++;

			if (_currentGameplayLoopStep == GameplayLoopStep.Count)
			{
				_currentGameplayLoopStep = 0;
			}

			yield return new WaitForSeconds(Config.GameplayLoopStepDelay);

			switch (_currentGameplayLoopStep)
			{
				case GameplayLoopStep.NightTransition:
					StartCoroutine(ChangeDaytime(Daytime.Night));
					break;
				case GameplayLoopStep.RoleCall:
					StartCoroutine(CallRoles());
					break;
				case GameplayLoopStep.DayTransition:
					StartCoroutine(ChangeDaytime(Daytime.Day));
					break;
				case GameplayLoopStep.DeathReveal:
					StartCoroutine(StartDeathReveal());
					break;
				case GameplayLoopStep.Debate:
					StartCoroutine(StartDebate());
_currentGameplayLoopStep = GameplayLoopStep.Execution;
					break;
				case GameplayLoopStep.Vote:

					break;
				case GameplayLoopStep.Execution:

					break;
			}
		}

		#region Daytime Change
		private IEnumerator ChangeDaytime(Daytime daytime)
		{
			RPC_ChangeDaytime(daytime);
#if UNITY_SERVER && UNITY_EDITOR
			_daytimeManager.ChangeDaytime(daytime);
#endif
			yield return new WaitForSeconds(Config.DaytimeTransitionStepDuration);

			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_ChangeDaytime(Daytime time)
		{
			_daytimeManager.ChangeDaytime(time);
		}
		#endregion
		#endregion

		#region Night Call
		private IEnumerator CallRoles()
		{
			_currentNightCallIndex = 0;

			while (_currentNightCallIndex < _nightCalls.Count)
			{
				NightCall nightCall = _nightCalls[_currentNightCallIndex];
				Dictionary<PlayerRef, RoleBehavior> actifBehaviors = new();

				// Role call all the roles that must play
				foreach (PlayerRef player in nightCall.Players)
				{
					bool skipPlayer = false;

					foreach (RoleBehavior behavior in Players[player].Behaviors)
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
					int displayRoleGameplayTagID = GetDisplayedRoleGameplayTagID(nightCall);

					foreach (KeyValuePair<PlayerRef, PlayerData> playerRole in Players)
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
					yield return new WaitForSeconds(Config.NightCallChangeDelay);
				}

				_currentNightCallIndex++;
			}

			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private void DisplayRolePlaying(int roleGameplayTagID)
		{
			RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID);
			string text = roleData.CanHaveMultiples ? Config.RolePlayingTextPlurial : Config.RolePlayingTextSingular;

			DisplayTitle(roleData.Image, string.Format(text, roleData.Name.ToLower()));// TODO: Give real image
		}

		private int GetDisplayedRoleGameplayTagID(NightCall nightCall)
		{
			RoleData alias = null;
			bool aliasFound = false;

			foreach (RoleBehavior behavior in Players[nightCall.Players[0]].Behaviors)
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
				return Players[nightCall.Players[0]].Role.GameplayTag.CompactTagId;
			}
		}

		private bool IsNightCallOver(float elapsedTime)
		{
			return !_voteManager.IsVoting()
				&& _revealPlayerRoleCallbacks.Count <= 0
				&& ((_playersWaitingFor.Count <= 0 && elapsedTime >= Config.NightCallMinimumDuration) || elapsedTime >= Config.NightCallMaximumDuration);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayRolePlaying([RpcTarget] PlayerRef player, int roleGameplayTagID)
		{
			DisplayRolePlaying(roleGameplayTagID);
		}
		#endregion
		#endregion

		#region Death Reveal
		public IEnumerator StartDeathReveal()
		{
			bool hasAnyPlayerDied = _marksForDeath.Count > 0;

			RPC_DisplayDeathRevealTitle(hasAnyPlayerDied);
#if UNITY_SERVER && UNITY_EDITOR
			DisplayDeathRevealTitle(hasAnyPlayerDied);
#endif
			yield return new WaitForSeconds(Config.UITransitionDuration);
			yield return new WaitForSeconds(Config.DeathRevealTitleHoldDuration);

			RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			HideUI();
#endif
			yield return new WaitForSeconds(Config.UITransitionDuration);

			if (hasAnyPlayerDied)
			{
				while (_marksForDeath.Count > 0)
				{
					PlayerRef deadPlayer = _marksForDeath[0].Player;

					if (!Players[deadPlayer].IsAlive)
					{
						_marksForDeath.RemoveAt(0);
						continue;
					}

					yield return new WaitForSeconds(Config.DelayBeforeRevealingDeadPlayer);

					List<PlayerRef> revealTo = new List<PlayerRef>();

					foreach (KeyValuePair<PlayerRef, PlayerData> playerRole in Players)
					{
						if (playerRole.Key == deadPlayer)
						{
							RPC_DisplayPlayerDiedTitle(playerRole.Key);
							continue;
						}

						revealTo.Add(playerRole.Key);
					}

					_isPlayerDeathRevealCompleted = false;

					_revealPlayerDeathCoroutine = RevealPlayerDeath(deadPlayer, revealTo.ToArray(), true, false, OnRevealPlayerDeathEnded);
					StartCoroutine(_revealPlayerDeathCoroutine);

					while (!_isPlayerDeathRevealCompleted)
					{
						yield return 0;
					}

					RPC_HideUI();

					yield return new WaitForSeconds(Config.UITransitionDuration);

					PlayerDeathRevealEnded?.Invoke(deadPlayer);

					while (_playersWaitingFor.Count > 0)
					{
						yield return 0;
					}

					SetPlayerDead(deadPlayer);
					_marksForDeath.RemoveAt(0);
				}
			}

			CheckForWinner();
			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private void DisplayDeathRevealTitle(bool hasAnyPlayerDied)
		{
			DisplayTitle(null, hasAnyPlayerDied ? Config.DeathRevealDeathText : Config.DeathRevealNoDeathText);// TODO: Give real image
		}

		private void DisplayPlayerDiedTitle()
		{
			DisplayTitle(null, Config.PlayerDiedText);// TODO: Give real image
		}

		private IEnumerator RevealPlayerDeath(PlayerRef playerRevealed, PlayerRef[] revealTo, bool waitBeforeReveal, bool returnFaceDown, Action RevealPlayerCompleted)
		{
			foreach (PlayerRef player in revealTo)
			{
				_playersWaitingFor.Add(player);
				MoveCardToCamera(player,
								playerRevealed,
								!waitBeforeReveal,
								!waitBeforeReveal ? Players[playerRevealed].Role.GameplayTag.CompactTagId : -1,
								() => StopWaintingForPlayer(player));
			}
#if UNITY_SERVER && UNITY_EDITOR
			MoveCardToCamera(playerRevealed, waitBeforeReveal);
#endif
			while (_playersWaitingFor.Count > 0)
			{
				yield return 0;
			}

			if (waitBeforeReveal)
			{
				WaitBeforeDeathRevealStarted?.Invoke(playerRevealed, Config.WaitRevealDuration);

				yield return new WaitForSeconds(Config.WaitRevealDuration);

				WaitBeforeDeathRevealEnded?.Invoke(playerRevealed);

				foreach (PlayerRef player in revealTo)
				{
					_playersWaitingFor.Add(player);
					FlipFaceUp(player,
							playerRevealed,
							Players[playerRevealed].Role.GameplayTag.CompactTagId,
							() => StopWaintingForPlayer(player));
				}
#if UNITY_SERVER && UNITY_EDITOR
				FlipFaceUp(playerRevealed);
#endif
				while (_playersWaitingFor.Count > 0)
				{
					yield return 0;
				}
			}

			yield return new WaitForSeconds(Config.HoldRevealDuration);

			foreach (PlayerRef player in revealTo)
			{
				_playersWaitingFor.Add(player);
				PutCardBackDown(player,
								playerRevealed,
								returnFaceDown,
								() => StopWaintingForPlayer(player));
			}
#if UNITY_SERVER && UNITY_EDITOR
			PutCardBackDown(playerRevealed, returnFaceDown);
#endif
			while (_playersWaitingFor.Count > 0)
			{
				yield return 0;
			}

			RevealPlayerCompleted?.Invoke();
		}

		private void OnRevealPlayerDeathEnded()
		{
			StopPlayerDeathReveal();
			_isPlayerDeathRevealCompleted = true;
		}

		public void StopPlayerDeathReveal()
		{
			if (_revealPlayerDeathCoroutine != null)
			{
				StopCoroutine(_revealPlayerDeathCoroutine);
			}

			_revealPlayerDeathCoroutine = null;
		}

		public void SetPlayerDeathRevealCompleted()
		{
			_isPlayerDeathRevealCompleted = true;
		}

		private void SetPlayerDead(PlayerRef deadPlayer)
		{
			Players[deadPlayer] = new PlayerData { Role = Players[deadPlayer].Role, Behaviors = Players[deadPlayer].Behaviors, IsAlive = false };
			_alivePlayerCount--;

			RemovePlayerFromAllPlayerGroups(deadPlayer);

			foreach (RoleBehavior behavior in Players[deadPlayer].Behaviors)
			{
				foreach (Priority priority in behavior.NightPriorities)
				{
					RemovePlayerFromNightCall(priority.index, deadPlayer);
				}
			}

			if (!_playerCards[deadPlayer])
			{
				return;
			}

			RPC_DisplayPlayerDead(deadPlayer);
#if UNITY_SERVER && UNITY_EDITOR
			_playerCards[deadPlayer].DisplayDead();
#endif
		}

		private void CheckForWinner()
		{
			foreach(PlayerGroup playerGroup in _playerGroups)
			{
				if (playerGroup.Players.Count >= _alivePlayerCount)
				{
					//TODO: Trigger winner sequence
					return;
				}
			}
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayDeathRevealTitle(bool hasAnyPlayerDied)
		{
			DisplayDeathRevealTitle(hasAnyPlayerDied);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayPlayerDiedTitle([RpcTarget] PlayerRef player)
		{
			DisplayPlayerDiedTitle();
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayPlayerDead(PlayerRef playerDead)
		{
			_playerCards[playerDead].DisplayDead();
		}
		#endregion
		#endregion

		#region Debate
		private IEnumerator StartDebate()
		{
			foreach(KeyValuePair<PlayerRef, PlayerData> player in Players)
			{
				RPC_OnDebateStarted(player.Key, player.Value.IsAlive);

				if (!player.Value.IsAlive)
				{
					continue;
				}

				WaitForPlayer(player.Key);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(null, Config.DebateText, Config.DebateStepDuration, false, Config.SkipText);
#endif
			float elapsedTime = .0f;

			while (_playersWaitingFor.Count > 0 && elapsedTime < Config.DebateStepDuration)
			{
				elapsedTime += Time.deltaTime;
				yield return 0;
			}

			RPC_OnDebateEnded();
			_playersWaitingFor.Clear();

#if UNITY_SERVER && UNITY_EDITOR
			OnDebateEnded();
#endif
			yield return new WaitForSeconds(Config.UITransitionDuration);

			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private void OnPlayerSkipDebate()
		{
			_UIManager.TitleScreen.Confirm -= OnPlayerSkipDebate;

			_playerCards[Runner.LocalPlayer].SetVotingStatusVisible(true);
			_playerCards[Runner.LocalPlayer].UpdateVotingStatus(false);

			RPC_SkipDebate();
		}

		private void OnDebateEnded()
		{
			_UIManager.TitleScreen.Confirm -= OnPlayerSkipDebate;

			foreach (KeyValuePair<PlayerRef, Card> card in _playerCards)
			{
				card.Value.SetVotingStatusVisible(false);
			}

			HideUI();
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_OnDebateStarted([RpcTarget] PlayerRef player, bool showConfirmButton)
		{
			DisplayTitle(null, Config.DebateText, Config.DebateStepDuration, showConfirmButton, Config.SkipText);

			if (!showConfirmButton)
			{
				return;
			}

			_UIManager.TitleScreen.Confirm += OnPlayerSkipDebate;
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_SkipDebate(RpcInfo info = default)
		{
			StopWaintingForPlayer(info.Source);
#if UNITY_SERVER && UNITY_EDITOR
			_playerCards[info.Source].SetVotingStatusVisible(true);
			_playerCards[info.Source].UpdateVotingStatus(false);
#endif
			RPC_PlayerSkippedDebate(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_PlayerSkippedDebate(PlayerRef player)
		{
			_playerCards[player].SetVotingStatusVisible(true);
			_playerCards[player].UpdateVotingStatus(false);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_OnDebateEnded()
		{
			OnDebateEnded();
		}
		#endregion
		#endregion

		public void WaitForPlayer(PlayerRef player)
		{
			if (_playersWaitingFor.Contains(player))
			{
				return;
			}

			_playersWaitingFor.Add(player);
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
		#endregion

		#region Role Change
		public void ChangeRole(PlayerRef player, RoleData roleData, RoleBehavior roleBehavior)
		{
			RemovePrimaryBehavior(player);

			if (!roleBehavior)
			{
				foreach (int playerGroupIndex in roleData.PlayerGroupIndexes)
				{
					AddPlayerToPlayerGroup(playerGroupIndex, player);
				}
			}
			else
			{
				AddBehavior(player, roleBehavior);
				roleBehavior.SetIsPrimaryBehavior(true);
			}

			Players[player] = new() { Role = roleData, Behaviors = Players[player].Behaviors, IsAlive = Players[player].IsAlive };

			RPC_ChangePlayerCardRole(player, roleData.GameplayTag.CompactTagId);
#if UNITY_SERVER && UNITY_EDITOR
			_playerCards[player].SetRole(roleData);
#endif
		}

		public void TransferRole(PlayerRef from, PlayerRef to, bool destroyOldBehavior = true)
		{
			RemovePrimaryBehavior(to, destroyOldBehavior);

			if (Players[from].Behaviors.Count <= 0)
			{
				foreach (int villageGroup in Players[from].Role.PlayerGroupIndexes)
				{
					AddPlayerToPlayerGroup(villageGroup, to);
				}
			}
			else
			{
				foreach (RoleBehavior behavior in Players[from].Behaviors)
				{
					if (behavior.IsPrimaryBehavior)
					{
						RemoveBehavior(from, behavior, false);
						AddBehavior(to, behavior);
						break;
					}
				}
			}

			Players[to] = new() { Role = Players[from].Role, Behaviors = Players[to].Behaviors, IsAlive = Players[to].IsAlive };
			Players[from] = new() { Role = null, Behaviors = Players[from].Behaviors, IsAlive = Players[from].IsAlive };

			RPC_ChangePlayerCardRole(to, Players[to].Role.GameplayTag.CompactTagId);
#if UNITY_SERVER && UNITY_EDITOR
			_playerCards[to].SetRole(Players[to].Role);
#endif
		}

		public void ChangePlayerCardRole(PlayerRef player, RoleData roleData)
		{
			_playerCards[player].SetRole(roleData);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_ChangePlayerCardRole([RpcTarget] PlayerRef player, int roleGameplayTagID)
		{
			ChangePlayerCardRole(player, _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID));
		}
		#endregion
		#endregion

		#region Behavior Change
		private void AddBehavior(PlayerRef player, RoleBehavior behavior)
		{
			int[] nightPrioritiesIndexes = behavior.GetNightPrioritiesIndexes();

			// Remove any contradicting behaviors
			List<RoleBehavior> behaviorsToRemove = FindNightCallBehaviors(player, nightPrioritiesIndexes);

			foreach (RoleBehavior behaviorToRemove in behaviorsToRemove)
			{
				RemoveBehavior(player, behaviorToRemove);
			}

			foreach (int priority in nightPrioritiesIndexes)
			{
				AddPlayerToNightCall(priority, player);
			}

			foreach (int playerGroupIndex in behavior.GetCurrentPlayerGroups())
			{
				AddPlayerToPlayerGroup(playerGroupIndex, player);
			}

			Players[player].Behaviors.Add(behavior);
			behavior.SetPlayer(player);
#if UNITY_SERVER && UNITY_EDITOR
			behavior.transform.position = _playerCards[player].transform.position;
#endif
		}

		private void RemovePrimaryBehavior(PlayerRef player, bool destroyOldBehavior = true)
		{
			if (Players[player].Behaviors.Count <= 0)
			{
				foreach (int playerGroupIndex in Players[player].Role.PlayerGroupIndexes)
				{
					RemovePlayerFromGroup(playerGroupIndex, player);
				}
			}
			else
			{
				foreach (RoleBehavior behavior in Players[player].Behaviors)
				{
					if (behavior.IsPrimaryBehavior)
					{
						RemoveBehavior(player, behavior, destroyOldBehavior);
						break;
					}
				}
			}
		}

		private void RemoveBehavior(PlayerRef player, RoleBehavior behavior, bool destroyBehavior = true)
		{
			int[] nightPrioritiesIndexes = behavior.GetNightPrioritiesIndexes();

			foreach (int priority in nightPrioritiesIndexes)
			{
				RemovePlayerFromNightCall(priority, player);
			}

			for (int i = Players[player].Behaviors.Count - 1; i >= 0; i--)
			{
				if (Players[player].Behaviors[i] != behavior)
				{
					continue;
				}

				Players[player].Behaviors.RemoveAt(i);
				break;
			}

			foreach (int group in behavior.GetCurrentPlayerGroups())
			{
				RemovePlayerFromGroup(group, player);
			}

			if (!destroyBehavior)
			{
				return;
			}
			
			Destroy(behavior.gameObject);
		}

		// Returns all the RoleBehavior that are called during a night call and that have at least one of the prioritiesIndex
		private List<RoleBehavior> FindNightCallBehaviors(PlayerRef player, int[] prioritiesIndex)
		{
			List<RoleBehavior> behaviorsToRemove = new();

			foreach (RoleBehavior behavior in Players[player].Behaviors)
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

		#region Destroy Card
		public void DestroyPlayerCard(PlayerRef cardPlayer)
		{
			Destroy(_playerCards[cardPlayer].gameObject);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DestroyPlayerCard([RpcTarget] PlayerRef player, PlayerRef cardPlayer)
		{
			DestroyPlayerCard(cardPlayer);
		}
		#endregion
		#endregion

		#region Player Group Change
		public void AddPlayerToPlayerGroup(int playerGroupIndex, PlayerRef player)
		{
			PlayerGroup playerGroup;

			for (int i = _playerGroups.Count - 1; i >= 0; i--)
			{
				if (_playerGroups[i].Index == playerGroupIndex)
				{
					if (_playerGroups[i].Players.Contains(player))
					{
						Debug.LogError("Tried to add duplicated player to a player group");
						return;
					}

					_playerGroups[i].Players.Add(player);
					return;
				}
				else if (_playerGroups[i].Index < playerGroupIndex)
				{
					playerGroup = new();
					playerGroup.Index = playerGroupIndex;
					playerGroup.Players = new() { player };

					_playerGroups.Insert(i, playerGroup);
					return;
				}
			}

			playerGroup = new();
			playerGroup.Index = playerGroupIndex;
			playerGroup.Players = new() { player };

			_playerGroups.Add(playerGroup);
		}

		public void RemovePlayerFromGroup(int playerGroupIndex, PlayerRef player)
		{
			for (int i = 0; i < _playerGroups.Count; i++)
			{
				if (_playerGroups[i].Index != playerGroupIndex)
				{
					continue;
				}

				_playerGroups[i].Players.Remove(player);

				if (_playerGroups[i].Players.Count <= 0)
				{
					_playerGroups.RemoveAt(i);
				}

				break;
			}
		}

		public void RemovePlayerFromAllPlayerGroups(PlayerRef player)
		{
			for (int i = _playerGroups.Count - 1; i >= 0; i--)
			{
				_playerGroups[i].Players.Remove(player);

				if (_playerGroups[i].Players.Count <= 0)
				{
					_playerGroups.RemoveAt(i);
				}
			}
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
		public bool AskClientToChooseReservedRole(RoleBehavior ReservedRoleOwner, float maximumDuration, bool mustChooseOne, Action<int> callback)
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
			RPC_ClientChooseReservedRole(ReservedRoleOwner.Player, maximumDuration, rolesContainer, mustChooseOne);

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
		public void RPC_ClientChooseReservedRole([RpcTarget] PlayerRef player, float maximumDuration, RolesContainer rolesContainer, bool mustChooseOne)
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

			_UIManager.ChoiceScreen.ConfirmChoice += GiveReservedRoleChoice;

			_UIManager.ChoiceScreen.Initialize(maximumDuration, mustChooseOne ? Config.ChooseRoleObligatoryText : Config.ChooseRoleText, Config.ChoosedRoleText, Config.DidNotChoosedRoleText, choices.ToArray(), mustChooseOne);
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
			_UIManager.ChoiceScreen.ConfirmChoice -= GiveReservedRoleChoice;
		}
		#endregion
		#endregion

		#region Choose a Player
		public bool AskClientToChoosePlayer(PlayerRef choosingPlayer, PlayerRef[] immunePlayers, float maximumDuration, string displayText, Action<PlayerRef> callback)
		{
			if (_choosePlayerCallbacks.ContainsKey(choosingPlayer))
			{
				return false;
			}

			_choosePlayerCallbacks.Add(choosingPlayer, callback);
			RPC_ClientChoosePlayer(choosingPlayer, immunePlayers, maximumDuration, displayText);

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
				if (!playerCard.Value)
				{
					continue;
				}

				playerCard.Value.ResetSelectionMode();
				playerCard.Value.OnCardClick -= OnClientChooseCard;
			}

			_UIManager.TitleScreen.Confirm -= OnClientChooseNoCard;
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_ClientChoosePlayer([RpcTarget] PlayerRef player, PlayerRef[] immunePlayers, float maximumDuration, string displayText)
		{
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (!playerCard.Value)
				{
					continue;
				}

				if (Array.IndexOf(immunePlayers, playerCard.Key) >= 0)
				{
					playerCard.Value.SetSelectionMode(true, false);
					continue;
				}

				playerCard.Value.SetSelectionMode(true, true);
				playerCard.Value.OnCardClick += OnClientChooseCard;
			}

			DisplayTitle(null, displayText, maximumDuration, true, Config.SkipTurnText);// TODO: Give real image
			_UIManager.TitleScreen.Confirm += OnClientChooseNoCard;
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
			_marksForDeath.Add(new() { Player = player, Marks = new() { mark } });
			OnMarkForDeathAdded?.Invoke(player, mark);
		}

		public void AddMarkForDeath(PlayerRef player, string mark, int index)
		{
			if (_marksForDeath.Count < index)
			{
				_marksForDeath.Add(new() { Player = player, Marks = new() { mark } });
			}
			else
			{
				_marksForDeath.Insert(index, new() { Player = player, Marks = new() { mark } });
			}

			OnMarkForDeathAdded?.Invoke(player, mark);
		}

		public void RemoveMarkForDeath(PlayerRef player)
		{
			for (int i = 0; i < _marksForDeath.Count; i++)
			{
				if (_marksForDeath[i].Player == player)
				{
					_marksForDeath.RemoveAt(i);
					return;
				}
			}
		}
		#endregion

		#region Role Reveal
		public bool RevealPlayerRole(PlayerRef playerRevealed, PlayerRef revealTo, bool waitBeforeReveal, bool returnFaceDown, Action<PlayerRef> callback)
		{
			if (_revealPlayerRoleCallbacks.ContainsKey(revealTo))
			{
				return false;
			}

			_revealPlayerRoleCallbacks.Add(revealTo, callback);
			RPC_RevealPlayerRole(revealTo, playerRevealed, Players[playerRevealed].Role.GameplayTag.CompactTagId, waitBeforeReveal, returnFaceDown);

			return true;
		}

		private IEnumerator RevealPlayerRole(Card card, bool waitBeforeReveal, bool returnFaceDown)
		{
			yield return MoveCardToCamera(card.transform, !waitBeforeReveal, Config.MoveToCameraDuration);

			if (waitBeforeReveal)
			{
				yield return new WaitForSeconds(Config.WaitRevealDuration);
				yield return FlipFaceUp(card.transform, Config.RevealFlipDuration);
			}

			yield return new WaitForSeconds(Config.HoldRevealDuration);
			yield return PutCardBackDown(card, returnFaceDown, Config.MoveToCameraDuration);

			RPC_RevealPlayerRoleFinished();
		}

		public void MoveCardToCamera(PlayerRef cardPlayer, bool showRevealed, Action MovementCompleted = null)
		{
			StartCoroutine(MoveCardToCamera(_playerCards[cardPlayer].transform, showRevealed, Config.MoveToCameraDuration, MovementCompleted));
		}

		private IEnumerator MoveCardToCamera(Transform card, bool showRevealed, float duration, Action MovementCompleted = null)
		{
			Camera mainCamera = Camera.main;

			Vector3 startingPosition = card.position;
			Vector3 targetPosition = mainCamera.transform.position + mainCamera.transform.forward * Config.RevealDistanceToCamera;

			Quaternion startingRotation = card.transform.rotation;
			Quaternion targetRotation;

			float elapsedTime = .0f;

			if (showRevealed)
			{
				targetRotation = Quaternion.LookRotation(mainCamera.transform.up, mainCamera.transform.forward);
			}
			else
			{
				targetRotation = Quaternion.LookRotation(mainCamera.transform.up, -mainCamera.transform.forward);
			}

			while (elapsedTime < duration)
			{
				elapsedTime += Time.deltaTime;

				float progress = elapsedTime / duration;

				card.transform.position = Vector3.Lerp(startingPosition, targetPosition, progress);
				card.transform.rotation = Quaternion.Lerp(startingRotation, targetRotation, progress);

				yield return 0;
			}

			MovementCompleted?.Invoke();
		}

		private bool MoveCardToCamera(PlayerRef movedFor, PlayerRef cardPlayer, bool showRevealed, int gameplayDataID, Action movementCompleted)
		{
			if (_moveCardToCameraCallbacks.ContainsKey(movedFor))
			{
				return false;
			}

			_moveCardToCameraCallbacks.Add(movedFor, movementCompleted);
			RPC_MoveCardToCamera(movedFor, cardPlayer, showRevealed, gameplayDataID);

			return true;
		}

		public void FlipFaceUp(PlayerRef cardPlayer, Action FlipCompleted = null)
		{
			StartCoroutine(FlipFaceUp(_playerCards[cardPlayer].transform, Config.RevealFlipDuration, FlipCompleted));
		}

		private IEnumerator FlipFaceUp(Transform card, float duration, Action FlipCompleted = null)
		{
			Camera mainCamera = Camera.main;

			float elapsedTime = .0f;

			Quaternion startingRotation = card.rotation;
			Quaternion targetRotation = Quaternion.LookRotation(mainCamera.transform.up, mainCamera.transform.forward);

			while (elapsedTime < duration)
			{
				elapsedTime += Time.deltaTime;

				float progress = elapsedTime / duration;

				card.transform.rotation = Quaternion.Lerp(startingRotation, targetRotation, progress);

				yield return 0;
			}

			FlipCompleted?.Invoke();
		}

		private bool FlipFaceUp(PlayerRef flipFor, PlayerRef cardPlayer, int gameplayDataID, Action flipCompleted)
		{
			if (_flipFaceUpCallbacks.ContainsKey(flipFor))
			{
				return false;
			}

			_flipFaceUpCallbacks.Add(flipFor, flipCompleted);
			RPC_FlipFaceUp(flipFor, cardPlayer, gameplayDataID);

			return true;
		}

		public void PutCardBackDown(PlayerRef cardPlayer, bool returnFaceDown, Action PutDownCompleted = null)
		{
			StartCoroutine(PutCardBackDown(_playerCards[cardPlayer], returnFaceDown, Config.MoveToCameraDuration, PutDownCompleted));
		}

		private IEnumerator PutCardBackDown(Card card, bool returnFaceDown, float duration, Action PutDownCompleted = null)
		{
			float elapsedTime = .0f;

			Vector3 startingPosition = card.transform.position;

			Quaternion startingRotation = card.transform.rotation;
			Quaternion targetRotation;

			if (returnFaceDown)
			{
				targetRotation = Quaternion.LookRotation(Vector3.forward, -Vector3.down);
			}
			else
			{
				targetRotation = Quaternion.LookRotation(Vector3.forward, Vector3.down);
			}

			while (elapsedTime < duration)
			{
				elapsedTime += Time.deltaTime;

				float progress = elapsedTime / duration;

				card.transform.position = Vector3.Lerp(startingPosition, card.OriginalPosition, progress);
				card.transform.rotation = Quaternion.Lerp(startingRotation, targetRotation, progress);

				yield return 0;
			}

			if (returnFaceDown)
			{
				card.SetRole(null);
			}

			PutDownCompleted?.Invoke();
		}

		private bool PutCardBackDown(PlayerRef putFor, PlayerRef cardPlayer, bool returnFaceDown, Action putDownCompleted)
		{
			if (_putCardBackDownCallbacks.ContainsKey(putFor))
			{
				return false;
			}

			_putCardBackDownCallbacks.Add(putFor, putDownCompleted);
			RPC_PutCardBackDown(putFor, cardPlayer, returnFaceDown);

			return true;
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_RevealPlayerRole([RpcTarget] PlayerRef player, PlayerRef playerRevealed, int gameplayDataID, bool waitBeforeReveal, bool returnFaceDown)
		{
			Card card = _playerCards[playerRevealed];
			card.SetRole(_gameplayDatabaseManager.GetGameplayData<RoleData>(gameplayDataID));

			StartCoroutine(RevealPlayerRole(card, waitBeforeReveal, returnFaceDown));
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_RevealPlayerRoleFinished(RpcInfo info = default)
		{
			if (!_revealPlayerRoleCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_revealPlayerRoleCallbacks[info.Source](info.Source);
			_revealPlayerRoleCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_MoveCardToCamera([RpcTarget] PlayerRef player, PlayerRef cardPlayer, bool showRevealed, int gameplayDataID = -1)
		{
			if (showRevealed)
			{
				RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(gameplayDataID);
				_playerCards[cardPlayer].SetRole(roleData);
			}

			MoveCardToCamera(cardPlayer, showRevealed, () => RPC_MoveCardToCameraFinished());
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_MoveCardToCameraFinished(RpcInfo info = default)
		{
			if (!_moveCardToCameraCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_moveCardToCameraCallbacks[info.Source]();
			_moveCardToCameraCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_FlipFaceUp([RpcTarget] PlayerRef player, PlayerRef cardPlayer, int gameplayDataID)
		{
			_playerCards[cardPlayer].SetRole(_gameplayDatabaseManager.GetGameplayData<RoleData>(gameplayDataID));
			FlipFaceUp(cardPlayer, () => RPC_FlipFaceUpFinished());
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_FlipFaceUpFinished(RpcInfo info = default)
		{
			if (!_flipFaceUpCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_flipFaceUpCallbacks[info.Source]();
			_flipFaceUpCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_PutCardBackDown([RpcTarget] PlayerRef player, PlayerRef cardPlayer, bool returnFaceDown)
		{
			PutCardBackDown(cardPlayer, returnFaceDown, () =>
			{
				if (returnFaceDown)
				{
					_playerCards[cardPlayer].SetRole(null);
				}

				RPC_PutCardBackDownFinished();
			});
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_PutCardBackDownFinished(RpcInfo info = default)
		{
			if (!_putCardBackDownCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_putCardBackDownCallbacks[info.Source]();
			_putCardBackDownCallbacks.Remove(info.Source);
		}
		#endregion
		#endregion

		#region Prompt Player
		public bool PromptPlayer(PlayerRef promptedPlayer, string prompt, float duration, string confirmButtonText , Action callback)
		{
			if (_promptPlayerCallbacks.ContainsKey(promptedPlayer))
			{
				return false;
			}

			_promptPlayerCallbacks.Add(promptedPlayer, callback);
			RPC_PromptPlayer(promptedPlayer, prompt, duration, confirmButtonText);

			return true;
		}

		private void OnPromptAccepted()
		{
			StopPromptingPlayer();
			RPC_AcceptPrompt();
		}

		public void StopPromptingPlayer(PlayerRef player)
		{
			_promptPlayerCallbacks.Remove(player);
			RPC_StopPromptingPlayer(player);
		}

		private void StopPromptingPlayer()
		{
			_UIManager.TitleScreen.Confirm -= OnPromptAccepted;
			_UIManager.SetFade(_UIManager.TitleScreen, .0f);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_PromptPlayer([RpcTarget] PlayerRef player, string prompt, float duration, string confirmButtonText)
		{
			_UIManager.TitleScreen.Initialize(null, prompt, duration, true, confirmButtonText);// TODO: Give real image
			_UIManager.TitleScreen.Confirm += OnPromptAccepted;
			_UIManager.SetFade(_UIManager.TitleScreen, 1.0f);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_AcceptPrompt(RpcInfo info = default)
		{
			if (!_promptPlayerCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_promptPlayerCallbacks[info.Source]();
			_promptPlayerCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_StopPromptingPlayer([RpcTarget] PlayerRef player)
		{
			StopPromptingPlayer();
		}
		#endregion
		#endregion

		#region UI
		public void DisplayTitle(Sprite image, string title, float countdownDuration = -1, bool showConfirmButton = false, string confirmButtonText = "")
		{
			_UIManager.TitleScreen.Initialize(image, title, countdownDuration, showConfirmButton, confirmButtonText);
			_UIManager.FadeIn(_UIManager.TitleScreen, Config.UITransitionDuration);
		}

		public void HideUI()
		{
			_UIManager.FadeOut(Config.UITransitionDuration);
		}

		public void SetPlayerCardHighlightVisible(PlayerRef player, bool isVisible)
		{
			_playerCards[player].SetHighlightVisible(isVisible);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayTitle([RpcTarget] PlayerRef player, string title)
		{
			DisplayTitle(null, title);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_HideUI()
		{
			HideUI();
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_HideUI([RpcTarget] PlayerRef player)
		{
			HideUI();
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SetPlayerCardHighlightVisible(PlayerRef player, bool isVisible)
		{
			SetPlayerCardHighlightVisible(player, isVisible);
		}
		#endregion
		#endregion

		#region Visual
#if UNITY_SERVER && UNITY_EDITOR
		private void CreatePlayerCardsForServer()
		{
			float rotationIncrement = 360.0f / Players.Count;
			Vector3 startingPosition = STARTING_DIRECTION * Config.CardsOffset.Evaluate(Players.Count);

			int counter = -1;

			foreach (KeyValuePair<PlayerRef, PlayerData> playerRole in Players)
			{
				counter++;

				Quaternion rotation = Quaternion.Euler(0, rotationIncrement * counter, 0);

				Card card = Instantiate(_cardPrefab, rotation * startingPosition, Quaternion.identity);
				card.transform.position += Vector3.up * card.Thickness / 2.0f;

				card.SetOriginalPosition(card.transform.position);
				card.SetPlayer(playerRole.Key);
				card.SetRole(playerRole.Value.Role);
				card.SetNickname(_gameDataManager.PlayerInfos[playerRole.Key].Nickname);
				card.DetachGroundCanvas();
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

				card.SetOriginalPosition(card.transform.position);
				card.SetPlayer(playerInfo.Key);
				card.SetNickname(playerInfo.Value.Nickname);
				card.DetachGroundCanvas();

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