using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.SceneManagement;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Gameplay;
using Werewolf.Gameplay.Role;
using Werewolf.Network;
using Werewolf.Network.Configs;
using Werewolf.UI;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Managers
{
	public partial class GameManager : NetworkBehaviourSingleton<GameManager>, INetworkRunnerCallbacks
	{
		[field: SerializeField]
		public GameConfig GameConfig { get; private set; }

		public List<RoleData> RolesToDistribute { get; private set; }

		public float GameSpeedModifier { get; private set; }

		public HashSet<PlayerRef> PlayersWaitingFor { get; private set; }

		public int AlivePlayerCount { get; private set; }

		private bool _allPlayersReadyToBeInitialized = false;
		private bool _startedPlayersInitialization = false;
		private bool _allPlayersReadyToPlay = false;

		public GameplayLoopStep CurrentGameplayLoopStep { get; private set; }

		public enum GameplayLoopStep
		{
			RoleGivenReveal = 0,
			ElectionDebate,
			Election,
			NightTransition,
			NightCall,
			DayTransition,
			DayDeathReveal,
			ExecutionDebate,
			Execution,
			ExecutionDeathReveal,
		}

		private int _nightCount;

		private readonly List<PlayerRef> _captainCandidates = new();

		private readonly Dictionary<string, IVariable> _rolePlayingTextVariables = new()
		{
			{ "Role", null },
			{ "Multiple", new BoolVariable() }
		};

		private bool _isPlayerDeathRevealCompleted;
		private IEnumerator _revealPlayerDeathCoroutine;

		private IEnumerator _startCaptainExecutionCoroutine;

		private GameplayDataManager _gameplayDataManager;
		private GameHistoryManager _gameHistoryManager;
		private UIManager _UIManager;
		private VoteManager _voteManager;
		private EmotesManager _emotesManager;
		private DaytimeManager _daytimeManager;
		private NetworkDataManager _networkDataManager;

		// Server events
		public event Action PreRoleDistribution;
		public event Action PostRoleDistribution;
		public event Action PreStartGame;
		public event Action<GameplayLoopStep> GameplayLoopStepStarts;
		public event Action RollCallBegin;
		public event Action StartWaitingForPlayersRollCall;
		public event Action StartNightCallChangeDelay;
		public event Action DeathRevealEnded;
		public event Action<PlayerRef, MarkForDeathData, float> WaitBeforeDeathRevealStarted;
		public event Action<PlayerRef> WaitBeforeDeathRevealEnded;
		public event Action<PlayerRef, MarkForDeathData> PlayerDeathRevealEnded;

		// Client events
		public event Action PlayerInitialized;

		public static bool HasSpawned { get; private set; }

		public static event Action ManagerSpawned;

		protected override void Awake()
		{
			base.Awake();

			RolesToDistribute = new();
			PlayerGameInfos = new();
			PlayersWaitingFor = new();

			if (!GameConfig)
			{
				Debug.LogError($"The {nameof(Data.GameConfig)} of the {nameof(Managers.GameManager)} is not defined");
			}
		}

		private void Start()
		{
			_gameplayDataManager = GameplayDataManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_UIManager = UIManager.Instance;
			_voteManager = VoteManager.Instance;
			_emotesManager = EmotesManager.Instance;
			_daytimeManager = DaytimeManager.Instance;
		}

		public override void Spawned()
		{
			Runner.AddCallbacks(this);
			HasSpawned = true;

			ManagerSpawned?.Invoke();
		}

		#region Pre Gameplay Loop
		public void PrepareGame()
		{
			_networkDataManager = NetworkDataManager.Instance;
			_networkDataManager.PlayerDisconnected += OnPlayerDisconnected;

			GameSpeedModifier = GameConfig.GameSpeedModifier[(int)_networkDataManager.GameSpeed];

			_gameHistoryManager.ClearEntries();

			UpdatePreGameplayLoopProgress();
		}

		private void UpdatePreGameplayLoopProgress()
		{
			if (!_allPlayersReadyToBeInitialized)
			{
				foreach (KeyValuePair<PlayerRef, NetworkPlayerInfo> playerInfo in _networkDataManager.PlayerInfos)
				{
					if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
					{
						continue;
					}

					WaitForPlayer(playerInfo.Key);
				}
			}
			else if (!_startedPlayersInitialization)
			{
				SelectRolesToDistribute();
				DistributeRoles();

				DeterminePlayerGroups();
				DetermineNightCalls();

				InitializeConfigAndManagers();

				AlivePlayerCount = PlayerGameInfos.Count;
				_emotesManager.SetAsleepCanSee(true);

				CreatePlayersOrder();

				foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerGameInfo in PlayerGameInfos)
				{
					if (!_networkDataManager.PlayerInfos[playerGameInfo.Key].IsConnected)
					{
						continue;
					}

					WaitForPlayer(playerGameInfo.Key);
					RPC_InitializePlayer(playerGameInfo.Key, GameSpeedModifier, _playersOrder, PlayerGameInfos[playerGameInfo.Key].Role.ID.HashCode);
				}

				_startedPlayersInitialization = true;
#if UNITY_SERVER && UNITY_EDITOR
				CreatePlayerCardsForServer();
				CreateReservedRoleCardsForServer();
				LogNightCalls();
				_voteManager.SetPlayerCards(_playerCards);
#endif
			}
			else if (_allPlayersReadyToPlay)
			{
				PreStartGame?.Invoke();
				StartGame();
			}
		}

		#region Roles Selection
		private void SelectRolesToDistribute()
		{
			// Convert RoleIDs to RoleData
			if (!_gameplayDataManager.TryGetGameplayData(GameConfig.DefaultRole.ID.HashCode, out RoleData defaultRole))
			{
				Debug.LogError($"Could not find the role {GameConfig.DefaultRole.ID.HashCode} for the default role");
			}

			List<RoleData> rolesToDistribute = new();
			_networkDataManager.ConvertToRoleSetupDatas(_networkDataManager.MandatoryRoles, out List<RoleSetup> mandatoryRoles);
			_networkDataManager.ConvertToRoleSetupDatas(_networkDataManager.OptionalRoles, out List<RoleSetup> optionalRoles);

			// Add all mandatory roles first
			while (mandatoryRoles.Count > 0)
			{
				RoleData[] addedRoles = SelectRolesFromRoleSetup(mandatoryRoles[0], rolesToDistribute);
				mandatoryRoles.RemoveAt(0);

				if (addedRoles == null)
				{
					continue;
				}

				PrepareRoleBehaviors(addedRoles, rolesToDistribute, mandatoryRoles, optionalRoles);
			}

			List<RoleSetup> excludedRuleSetups = new();
			int attempts = 0;

			// Complete with available roles at random or default role
			while (rolesToDistribute.Count < _networkDataManager.PlayerInfos.Count)
			{
				int startingRoleCount = rolesToDistribute.Count;

				if (optionalRoles.Count <= 0 || attempts >= GameConfig.AvailableRolesMaxAttemptCount)
				{
					rolesToDistribute.Add(defaultRole);
					PrepareRoleBehavior(defaultRole, rolesToDistribute, mandatoryRoles, optionalRoles);

					continue;
				}

				int randomIndex = UnityEngine.Random.Range(0, optionalRoles.Count);
				RoleSetup roleSetup = optionalRoles[randomIndex];

				// Do not use roles setup that would add too many roles 
				if (excludedRuleSetups.Contains(roleSetup))
				{
					attempts++;
					continue;
				}
				else if (roleSetup.UseCount > _networkDataManager.PlayerInfos.Count - rolesToDistribute.Count)
				{
					excludedRuleSetups.Add(roleSetup);
					continue;
				}

				RoleData[] addedRoles = SelectRolesFromRoleSetup(roleSetup, rolesToDistribute);
				optionalRoles.RemoveAt(randomIndex);

				PrepareRoleBehaviors(addedRoles, rolesToDistribute, mandatoryRoles, optionalRoles);

				// Some roles were removed from the list of roles to distribute
				if (startingRoleCount > rolesToDistribute.Count)
				{
					excludedRuleSetups.Clear();
					attempts = 0;
				}
			}

			RolesToDistribute = rolesToDistribute;
		}

		private RoleData[] SelectRolesFromRoleSetup(RoleSetup roleSetup, List<RoleData> rolesToDistribute)
		{
			if (roleSetup.UseCount <= 0 || roleSetup.UseCount > roleSetup.Pool.Length)
			{
				Debug.LogError($"Invalid role setup: UseCount of {roleSetup.UseCount} for a pool of {roleSetup.Pool.Length}");
				return null;
			}

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

		public void PrepareRoleBehaviors(RoleData[] roles, List<RoleData> rolesToDistribute, List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles)
		{
			foreach (RoleData role in roles)
			{
				PrepareRoleBehavior(role, rolesToDistribute, mandatoryRoles, availableRoles);
			}
		}

		public void PrepareRoleBehavior(RoleData role, List<RoleData> rolesToDistribute, List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles)
		{
			if (!role.Behavior)
			{
				return;
			}

			RoleBehavior roleBehavior = InstanciateRoleBehavior(role);

			_unassignedRoleBehaviors.Add(roleBehavior, role);
			roleBehavior.OnSelectedToDistribute(mandatoryRoles, availableRoles, rolesToDistribute);
		}

		public RoleBehavior InstanciateRoleBehavior(RoleData role)
		{
			RoleBehavior roleBehavior = Instantiate(role.Behavior, transform);

			roleBehavior.SetRoleID(role.ID);
			roleBehavior.SetPrimaryRoleType(role.PrimaryType);

			foreach (PlayerGroupData playerGroup in role.PlayerGroups)
			{
				roleBehavior.AddPlayerGroup(playerGroup.ID);
			}

			foreach (Priority nightPriority in role.NightPriorities)
			{
				roleBehavior.AddNightPriority(nightPriority);
			}

			roleBehavior.SetIsPrimaryBehavior(true);
			roleBehavior.Initialize();

			return roleBehavior;
		}
		#endregion

		#region Roles Distribution
		private void DistributeRoles()
		{
			PreRoleDistribution?.Invoke();

			foreach (KeyValuePair<PlayerRef, NetworkPlayerInfo> playerInfo in _networkDataManager.PlayerInfos)
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

				PlayerGameInfos.Add(playerInfo.Key, new() { Role = selectedRole, Behaviors = selectedBehaviors, IsAwake = true, IsAlive = true });

				_gameHistoryManager.AddEntry(GameConfig.PlayerGivenRoleGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "Player",
													Data = playerInfo.Value.Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												},
												new()
												{
													Name = "RoleName",
													Data = selectedRole.ID.HashCode.ToString(),
													Type = GameHistorySaveEntryVariableType.RoleName
												}
											},
											selectedRole.ID);
			}

			PostRoleDistribution?.Invoke();
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
			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (playerInfo.Value.Behaviors.Count <= 0)
				{
					foreach (PlayerGroupData playerGroup in playerInfo.Value.Role.PlayerGroups)
					{
						AddPlayerToPlayerGroup(playerInfo.Key, playerGroup.ID);
					}

					continue;
				}

				foreach (UniqueID playerGroupID in playerInfo.Value.Behaviors[0].GetCurrentPlayerGroupIDs())
				{
					AddPlayerToPlayerGroup(playerInfo.Key, playerGroupID);
				}
			}
		}

		private void DetermineNightCalls()
		{
			// Remove any players that do not have a behavior that needs to be called at night
			List<PlayerRef> players = PlayerGameInfos.Keys.ToList();

			for (int i = players.Count - 1; i >= 0; i--)
			{
				List<RoleBehavior> behaviors = PlayerGameInfos[players[i]].Behaviors;

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
				foreach (Priority priority in PlayerGameInfos[player].Behaviors[0].NightPriorities)
				{
					if (!priorities.Contains(priority.index))
					{
						priorities.Add(priority.index);
					}
				}
			}

			priorities.Sort();

			// Loop threw the priorities and store all players with similare priorities together
			for (int i = 0; i < priorities.Count; i++)
			{
				HashSet<PlayerRef> playersToCall = new();

				foreach (PlayerRef player in players)
				{
					foreach (Priority priority in PlayerGameInfos[player].Behaviors[0].NightPriorities)
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
					roles += $"{PlayerGameInfos[player].Role.NameSingular.GetLocalizedString()} || ";
				}

				Debug.Log(roles);
			}

			Debug.Log("-------------------------------------------------------");
		}
#endif
		#endregion

		private void InitializeConfigAndManagers()
		{
			_daytimeManager.Initialize(GameConfig);
			_voteManager.Initialize(GameConfig);
			_emotesManager.Initialize(GameConfig);
			_UIManager.ChoiceScreen.SetConfig(GameConfig);
			_UIManager.DisconnectedScreen.SetConfig(GameConfig);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_ConfirmPlayerReadyToPlay(RpcInfo info = default)
		{
			StopWaintingForPlayer(info.Source);

			if (PlayersWaitingFor.Count > 0)
			{
				return;
			}

			_allPlayersReadyToPlay = true;

			UpdatePreGameplayLoopProgress();
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_ConfirmPlayerReadyToBeInitialized(RpcInfo info = default)
		{
			StopWaintingForPlayer(info.Source);

			Log.Info($"{info.Source} is ready! Waiting for {PlayersWaitingFor.Count} players");

			if (PlayersWaitingFor.Count > 0)
			{
				return;
			}

			_allPlayersReadyToBeInitialized = true;

			Log.Info("All players are ready to receive their role!");

			UpdatePreGameplayLoopProgress();
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_InitializePlayer([RpcTarget] PlayerRef player, float gameSpeedModifier, PlayerRef[] playersOrder, int roleID)
		{
			GameSpeedModifier = gameSpeedModifier;
			InitializeConfigAndManagers();

			if (!_networkDataManager)
			{
				_networkDataManager = NetworkDataManager.Instance;
			}

			if (!_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
			{
				Debug.LogError($"Could not find the role {roleID}");
			}

			CreatePlayerCards(playersOrder, player, roleData);
			CreateReservedRoleCards();

			_voteManager.SetPlayerCards(_playerCards);
			_emotesManager.SetPlayerCards(_playerCards);
			_UIManager.RolesScreen.SelectRole(roleData, false);

			PlayerInitialized?.Invoke();
		}
		#endregion
		#endregion

		#region Gameplay Loop
		private void StartGame()
		{
			CurrentGameplayLoopStep = GameplayLoopStep.RoleGivenReveal;
			ExecuteGameplayLoopStep();
		}

		#region Gameplay Loop Steps
		private IEnumerator MoveToNextGameplayLoopStep()
		{
			if (CurrentGameplayLoopStep == GameplayLoopStep.ExecutionDeathReveal)
			{
				CurrentGameplayLoopStep = GameplayLoopStep.NightTransition;
			}
			else
			{
				CurrentGameplayLoopStep++;
			}

			yield return new WaitForSeconds(GameConfig.GameplayLoopStepDelay);

			ExecuteGameplayLoopStep();
		}

		private void ExecuteGameplayLoopStep()
		{
			GameplayLoopStepStarts?.Invoke(CurrentGameplayLoopStep);

			switch (CurrentGameplayLoopStep)
			{
				case GameplayLoopStep.RoleGivenReveal:
					StartCoroutine(RevealGivenRole());
					break;
				case GameplayLoopStep.ElectionDebate:
					StartCoroutine(StartElectionDebate());
					break;
				case GameplayLoopStep.Election:
					StartElection();
					break;
				case GameplayLoopStep.NightTransition:
					StartCoroutine(ChangeDaytime(Daytime.Night));
					break;
				case GameplayLoopStep.NightCall:
					_nightCount++;
					StartCoroutine(CallRoles());
					break;
				case GameplayLoopStep.DayTransition:
					StartCoroutine(ChangeDaytime(Daytime.Day));
					break;
				case GameplayLoopStep.DayDeathReveal:
					StartCoroutine(StartDeathReveal(true));
					break;
				case GameplayLoopStep.ExecutionDebate:
					StartCoroutine(StartDebate(GetPlayersExcluding(GetDeadPlayers().ToArray()), GameConfig.ExecutionDebateTitleScreen.ID.HashCode, GameConfig.ExecutionDebateDuration * GameSpeedModifier));
					break;
				case GameplayLoopStep.Execution:
					StartExecution();
					break;
				case GameplayLoopStep.ExecutionDeathReveal:
					StartCoroutine(StartDeathReveal(false));
					break;
			}
		}
		#endregion

		#region RoleGivenReveal
		private IEnumerator RevealGivenRole()
		{
			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					continue;
				}

				if (RevealPlayerRole(playerInfo.Key, playerInfo.Key, false, false, (PlayerRef revealTo) => { StopWaintingForPlayer(revealTo); }))
				{
					WaitForPlayer(playerInfo.Key);
				}
			}

			RPC_ShowGivenRoleTitle();

			while (PlayersWaitingFor.Count > 0)
			{
				yield return 0;
			}

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					continue;
				}

				RPC_HideUI(playerInfo.Key);
			}

			yield return new WaitForSeconds(GameConfig.UITransitionNormalDuration);

			StartCoroutine(MoveToNextGameplayLoopStep());
		}
		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_ShowGivenRoleTitle()
		{
			Dictionary<string, IVariable> variables = new()
			{
				{ "Role", _playerCards[Runner.LocalPlayer].Role.NameSingular }
			};

			DisplayTitle(null, GameConfig.GivenRoleText, variables);
		}
		#endregion
		#endregion

		#region Election
		private IEnumerator StartElectionDebate()
		{
			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				PromptPlayer(playerInfo.Key,
							GameConfig.ElectionPromptTitleScreen.ID.HashCode,
							GameConfig.ElectionPromptDuration * GameSpeedModifier,
							OnPlayerWantsToBeCaptain,
							false);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(GameConfig.ElectionPromptTitleScreen.ID.HashCode, variables: null, countdownDuration: GameConfig.ElectionPromptDuration * GameSpeedModifier);
#endif
			yield return new WaitForSeconds(GameConfig.ElectionPromptDuration * GameSpeedModifier);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				StopPromptingPlayer(playerInfo.Key, false);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_UIManager.FadeOutAll(GameConfig.UITransitionNormalDuration);
#endif
			yield return new WaitForSeconds(GameConfig.UITransitionNormalDuration);

			if (_captainCandidates.Count > 1)
			{
				_gameHistoryManager.AddEntry(GameConfig.ElectionMultipleCandidatesGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "Players",
													Data = ConcatenatePlayersNickname(_captainCandidates.ToArray(), _networkDataManager),
													Type = GameHistorySaveEntryVariableType.Players
												}
											});

				PlayerRef[] captainCandidates = _captainCandidates.ToArray();

				RPC_SetPlayersCardHighlightVisible(captainCandidates, true);
#if UNITY_SERVER && UNITY_EDITOR
				SetPlayersCardHighlightVisible(captainCandidates, true);
#endif
				yield return DisplayTitleForAllPlayers(GameConfig.ElectionMultipleCandidatesTitleScreen.ID.HashCode, GameConfig.ElectionMultipleCandidatesDuration * GameSpeedModifier);
				StartCoroutine(StartDebate(captainCandidates, GameConfig.ElectionDebateTitleScreen.ID.HashCode, GameConfig.ElectionDebateDuration * GameSpeedModifier));
				yield break;
			}
			else if (_captainCandidates.Count == 1)
			{
				SetCaptain(_captainCandidates[0]);
				yield return ShowCaptain(true);
			}
			else
			{
				_gameHistoryManager.AddEntry(GameConfig.ElectionNoCandidateGameHistoryEntry.ID, null);
				yield return DisplayTitleForAllPlayers(GameConfig.ElectionNoCandidateTitleScreen.ID.HashCode, GameConfig.ElectionNoCandidateDuration * GameSpeedModifier);
			}

			CurrentGameplayLoopStep = GameplayLoopStep.Election;
			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private void OnPlayerWantsToBeCaptain(PlayerRef player)
		{
			if (!_captainCandidates.Contains(player))
			{
				_captainCandidates.Add(player);
			}
		}

		private void StartElection()
		{
			_voteManager.StartVoteForAllPlayers(OnElectionVotesCounted,
												GameConfig.ElectionVoteTitleScreen.ID.HashCode,
												GameConfig.ElectionVoteDuration * GameSpeedModifier,
												false,
												ChoicePurpose.Other,
												GetDeadPlayers().ToArray(),
												canVoteForSelf: true,
												ImmunePlayers: GetPlayersExcluding(_captainCandidates.ToArray()));
		}

		private void OnElectionVotesCounted(PlayerRef[] mostVotedPlayers)
		{
			PlayerRef votedPlayer;

			if (mostVotedPlayers.Length > 1)
			{
				votedPlayer = mostVotedPlayers[UnityEngine.Random.Range(0, mostVotedPlayers.Length)];
			}
			else if (mostVotedPlayers.Length == 1)
			{
				votedPlayer = mostVotedPlayers[0];
			}
			else
			{
				votedPlayer = _captainCandidates[UnityEngine.Random.Range(0, _captainCandidates.Count)];
			}

			SetCaptain(votedPlayer);
			StartCoroutine(ShowElectionResult());
		}

		private IEnumerator ShowElectionResult()
		{
			yield return ShowCaptain(true);
			StartCoroutine(MoveToNextGameplayLoopStep());
		}
		#endregion

		#region Daytime Change
		private IEnumerator ChangeDaytime(Daytime daytime)
		{
			RPC_ChangeDaytime(daytime);
#if UNITY_SERVER && UNITY_EDITOR
			_daytimeManager.ChangeDaytime(daytime);
#endif
			switch (daytime)
			{
				case Daytime.Day:
					_gameHistoryManager.AddEntry(GameConfig.SunRoseGameHistoryEntry.ID, null);
					break;
				case Daytime.Night:
					_gameHistoryManager.AddEntry(GameConfig.SunSetGameHistoryEntry.ID, null);
					break;
			}

			bool isDaytime = daytime == Daytime.Day;
			SetAllPlayersAwake(isDaytime);
			_emotesManager.SetAsleepCanSee(isDaytime);

			yield return new WaitForSeconds(GameConfig.DaytimeTransitionDuration);

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
			RollCallBegin?.Invoke();

			_currentNightCallIndex = 0;

			Dictionary<PlayerRef, RoleBehavior> actifBehaviors = new();
			Dictionary<PlayerRef, int> titlesOverrides = new();

			while (_currentNightCallIndex < _nightCalls.Count)
			{
				NightCall nightCall = _nightCalls[_currentNightCallIndex];
				actifBehaviors.Clear();
				titlesOverrides.Clear();

				// Role call all the roles that must play
				foreach (PlayerRef player in nightCall.Players)
				{
					bool skipPlayer = false;

					foreach (RoleBehavior behavior in PlayerGameInfos[player].Behaviors)
					{
						int[] nightPrioritiesIndexes = behavior.GetNightPrioritiesIndexes();

						if (nightPrioritiesIndexes.Contains(nightCall.PriorityIndex))
						{
							skipPlayer = !behavior.OnRoleCall(_nightCount, nightCall.PriorityIndex, out bool isWakingUp);

							if (skipPlayer)
							{
								break;
							}

							SetPlayerAwake(player, isWakingUp);

							if (isWakingUp)
							{
								_gameHistoryManager.AddEntry(GameConfig.WokeUpPlayerGameHistoryEntry.ID,
															new GameHistorySaveEntryVariable[] {
															new()
															{
																Name = "Player",
																Data = _networkDataManager.PlayerInfos[player].Nickname,
																Type = GameHistorySaveEntryVariableType.Player
															},
															new()
															{
																Name = "RoleName",
																Data = PlayerGameInfos[player].Role.ID.HashCode.ToString(),
																Type = GameHistorySaveEntryVariableType.RoleName
															}
															},
															PlayerGameInfos[player].Role.ID);
							}

							PlayersWaitingFor.Add(player);
							actifBehaviors.Add(player, behavior);
							behavior.GetTitlesOverride(nightCall.PriorityIndex, ref titlesOverrides);
						}
					}

					if (!skipPlayer && !PlayersWaitingFor.Contains(player))
					{
						Debug.LogError($"{player} is suppose to play, but he has no behavior with the PriorityIndex {nightCall.PriorityIndex}");
					}
				}

				if (PlayersWaitingFor.Count > 0)
				{
					StartWaitingForPlayersRollCall?.Invoke();

					int displayedRoleID = GetDisplayedRoleID(nightCall).HashCode;

					foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
					{
						if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected || PlayersWaitingFor.Contains(playerInfo.Key))
						{
							continue;
						}

						if (titlesOverrides.Count <= 0 || !titlesOverrides.TryGetValue(playerInfo.Key, out int titlesOverride))
						{
							RPC_DisplayRolePlaying(playerInfo.Key, displayedRoleID);
						}
						else
						{
							RPC_DisplayTitle(playerInfo.Key, titlesOverride);
						}
					}
#if UNITY_SERVER && UNITY_EDITOR
					if (!_voteManager.IsPreparingToVote())
					{
						DisplayRolePlaying(displayedRoleID);
					}
#endif
					float elapsedTime = .0f;

					while (PlayersWaitingFor.Count > 0 || elapsedTime < GameConfig.NightCallMinimumDuration * GameSpeedModifier)
					{
						yield return 0;
						elapsedTime += Time.deltaTime;
					}

					SetAllPlayersAwake(false);
					RPC_HideUI();
					RPC_SetAllPlayersCardHighlightVisible(false);
#if UNITY_SERVER && UNITY_EDITOR
					HideUI();
					SetAllPlayersCardHighlightVisible(false);
#endif
					yield return new WaitForSeconds(GameConfig.UITransitionNormalDuration);

					StartNightCallChangeDelay?.Invoke();
					yield return new WaitForSeconds(GameConfig.NightCallChangeDelay);
				}

				_currentNightCallIndex++;
			}

			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private UniqueID GetDisplayedRoleID(NightCall nightCall)
		{
			RoleData alias = null;
			RoleBehavior behaviorCalled = null;

			foreach (RoleBehavior behavior in PlayerGameInfos[nightCall.Players.First()].Behaviors)
			{
				foreach (Priority nightPrioritie in behavior.NightPriorities)
				{
					if (nightPrioritie.index != nightCall.PriorityIndex)
					{
						continue;
					}

					alias = nightPrioritie.alias;
					behaviorCalled = behavior;

					break;
				}

				if (behaviorCalled)
				{
					break;
				}
			}

			if (alias)
			{
				return alias.ID;
			}
			else
			{
				return behaviorCalled.RoleID;
			}
		}

		private void DisplayRolePlaying(int roleID)
		{
			if (!_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
			{
				Debug.LogError($"Could not find the role {roleID}");
			}

			bool multipleRole = roleData.CanHaveVariableAmount || roleData.MandatoryAmount > 1;

			_rolePlayingTextVariables["Role"] = multipleRole ? roleData.NamePlural : roleData.NameSingular;
			((BoolVariable)_rolePlayingTextVariables["Multiple"]).Value = multipleRole;

			DisplayTitle(roleData.Image, GameConfig.RolePlayingText, _rolePlayingTextVariables);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayRolePlaying([RpcTarget] PlayerRef player, int roleID)
		{
			DisplayRolePlaying(roleID);
		}
		#endregion
		#endregion

		#region Death Reveal
		public IEnumerator StartDeathReveal(bool showTitle)
		{
			bool hasAnyPlayerDied = _marksForDeath.Count > 0;

			if (showTitle)
			{
				RPC_DisplayDeathRevealTitle(hasAnyPlayerDied);
#if UNITY_SERVER && UNITY_EDITOR
				DisplayDeathRevealTitle(hasAnyPlayerDied);
#endif
				yield return new WaitForSeconds(GameConfig.UITransitionNormalDuration);
				yield return new WaitForSeconds(GameConfig.DeathRevealHoldDuration * GameSpeedModifier);

				RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
				HideUI();
#endif
				yield return new WaitForSeconds(GameConfig.UITransitionNormalDuration);
			}

			if (hasAnyPlayerDied)
			{
				while (_marksForDeath.Count > 0)
				{
					PlayerRef deadPlayer = _marksForDeath[0].Player;

					if (!PlayerGameInfos[deadPlayer].IsAlive)
					{
						_marksForDeath.RemoveAt(0);
						continue;
					}

					yield return new WaitForSeconds(GameConfig.DelayBeforeRevealingDeadPlayer * GameSpeedModifier);

					_gameHistoryManager.AddEntry(GameConfig.PlayerDiedGameHistoryEntry.ID,
												new GameHistorySaveEntryVariable[] {
													new()
													{
														Name = "Player",
														Data = _networkDataManager.PlayerInfos[deadPlayer].Nickname,
														Type = GameHistorySaveEntryVariableType.Player
													},
													new()
													{
														Name = "RoleName",
														Data = PlayerGameInfos[deadPlayer].Role.ID.HashCode.ToString(),
														Type = GameHistorySaveEntryVariableType.RoleName
													}
												});

					if (_networkDataManager.PlayerInfos[deadPlayer].IsConnected)
					{
						RPC_DisplayPlayerDiedTitle(deadPlayer, _marksForDeath[0].Mark == GameConfig.ExecutionMarkForDeath);
					}

					_isPlayerDeathRevealCompleted = false;

					_revealPlayerDeathCoroutine = RevealPlayerDeath(deadPlayer,
																	GetPlayersExcluding(deadPlayer),
																	true,
																	_marksForDeath[0].Mark,
																	false,
																	OnRevealPlayerDeathEnded);
					StartCoroutine(_revealPlayerDeathCoroutine);

					while (!_isPlayerDeathRevealCompleted)
					{
						yield return 0;
					}

					RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
					HideUI();
#endif
					yield return new WaitForSeconds(GameConfig.UITransitionNormalDuration);

					PlayerDeathRevealEnded?.Invoke(deadPlayer, _marksForDeath[0].Mark);

					while (PlayersWaitingFor.Count > 0)
					{
						yield return 0;
					}

					SetPlayerDead(deadPlayer);
					_marksForDeath.RemoveAt(0);

					if (deadPlayer == _captain)
					{
						_gameHistoryManager.AddEntry(GameConfig.CaptainDiedGameHistoryEntry.ID,
													new GameHistorySaveEntryVariable[] {
														new()
														{
															Name = "Player",
															Data = _networkDataManager.PlayerInfos[_captain].Nickname,
															Type = GameHistorySaveEntryVariableType.Player
														}
													});

						_isNextCaptainChoiceCompleted = false;

						_chooseNextCaptainCoroutine = ChooseNextCaptain();
						StartCoroutine(_chooseNextCaptainCoroutine);

						while (!_isNextCaptainChoiceCompleted)
						{
							yield return 0;
						}
					}
				}
			}

			if (CheckForWinners())
			{
				yield break;
			}

			DeathRevealEnded?.Invoke();

			while (PlayersWaitingFor.Count > 0)
			{
				yield return 0;
			}

			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private void DisplayDeathRevealTitle(bool hasAnyPlayerDied)
		{
			DisplayTitle(hasAnyPlayerDied ? GameConfig.DeathRevealSomeoneDiedTitleScreen.ID.HashCode : GameConfig.DeathRevealNoOneDiedTitleScreen.ID.HashCode);
		}

		private void DisplayPlayerDiedTitle(bool wasExecuted)
		{
			DisplayTitle(wasExecuted ? GameConfig.PlayerExecutedTitleScreen.ID.HashCode : GameConfig.PlayerDiedTitleScreen.ID.HashCode);
		}

		private IEnumerator RevealPlayerDeath(PlayerRef playerRevealed, PlayerRef[] revealTo, bool waitBeforeReveal, MarkForDeathData mark, bool returnFaceDown, Action RevealPlayerCompleted)
		{
			foreach (PlayerRef player in revealTo)
			{
				if (!_networkDataManager.PlayerInfos[player].IsConnected)
				{
					continue;
				}

				PlayersWaitingFor.Add(player);
				MoveCardToCamera(player,
								playerRevealed,
								!waitBeforeReveal,
								!waitBeforeReveal ? PlayerGameInfos[playerRevealed].Role.ID.HashCode : -1,
								() => StopWaintingForPlayer(player));
			}
#if UNITY_SERVER && UNITY_EDITOR
			MoveCardToCamera(playerRevealed, waitBeforeReveal);
#endif
			while (PlayersWaitingFor.Count > 0)
			{
				yield return 0;
			}

			if (waitBeforeReveal)
			{
				WaitBeforeDeathRevealStarted?.Invoke(playerRevealed, mark, GameConfig.RoleRevealWaitDuration * GameSpeedModifier);

				yield return new WaitForSeconds(GameConfig.RoleRevealWaitDuration * GameSpeedModifier);

				WaitBeforeDeathRevealEnded?.Invoke(playerRevealed);

				foreach (PlayerRef player in revealTo)
				{
					if (!_networkDataManager.PlayerInfos[player].IsConnected)
					{
						continue;
					}

					PlayersWaitingFor.Add(player);
					FlipCard(player,
							playerRevealed,
							() => StopWaintingForPlayer(player),
							PlayerGameInfos[playerRevealed].Role.ID.HashCode);
				}

				while (PlayersWaitingFor.Count > 0)
				{
					yield return 0;
				}
			}

			yield return new WaitForSeconds(GameConfig.RoleRevealHoldDuration * GameSpeedModifier);

			foreach (PlayerRef player in revealTo)
			{
				if (!_networkDataManager.PlayerInfos[player].IsConnected)
				{
					continue;
				}

				PlayersWaitingFor.Add(player);
				PutCardBackDown(player,
								playerRevealed,
								returnFaceDown,
								() => StopWaintingForPlayer(player));
			}
#if UNITY_SERVER && UNITY_EDITOR
			PutCardBackDown(playerRevealed, returnFaceDown);
#endif
			while (PlayersWaitingFor.Count > 0)
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
			if (_revealPlayerDeathCoroutine == null)
			{
				return;
			}

			StopCoroutine(_revealPlayerDeathCoroutine);
			_revealPlayerDeathCoroutine = null;
		}

		public void SetPlayerDeathRevealCompleted()
		{
			_isPlayerDeathRevealCompleted = true;
		}

		private void SetPlayerDead(PlayerRef deadPlayer)
		{
			SetPlayerAwake(deadPlayer, false);
			PlayerGameInfos[deadPlayer].IsAlive = false;
			AlivePlayerCount--;

			RemovePlayerFromAllPlayerGroups(deadPlayer);

			foreach (RoleBehavior behavior in PlayerGameInfos[deadPlayer].Behaviors)
			{
				foreach (Priority priority in behavior.NightPriorities)
				{
					RemovePlayerFromNightCall(priority.index, deadPlayer);
				}
			}

			RPC_DisplayPlayerDeadIcon(deadPlayer);
#if UNITY_SERVER && UNITY_EDITOR
			if (_playerCards.TryGetValue(deadPlayer, out Card playerCard) && playerCard)
			{
				playerCard.DisplayDeadIcon();
			}
#endif
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayDeathRevealTitle(bool hasAnyPlayerDied)
		{
			DisplayDeathRevealTitle(hasAnyPlayerDied);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayPlayerDiedTitle([RpcTarget] PlayerRef player, bool wasExecuted)
		{
			DisplayPlayerDiedTitle(wasExecuted);
		}
		#endregion
		#endregion

		#region Execution
		private void StartExecution()
		{
			if (!_voteManager.StartVoteForAllPlayers(OnExecutionVotesCounted,
													GameConfig.ExecutionVoteTitleScreen.ID.HashCode,
													GameConfig.ExecutionVoteDuration * GameSpeedModifier,
													true,
													ChoicePurpose.Kill,
													GetDeadPlayers().ToArray(),
													GetExecutionVoteModifiers()))
			{
				Debug.LogError("Couldn't start the execution vote");
				return;
			}

			_gameHistoryManager.AddEntry(GameConfig.ExecutionVoteStartedGameHistoryEntry.ID, null);
		}

		private void OnExecutionVotesCounted(PlayerRef[] mostVotedPlayers)
		{
			if (mostVotedPlayers.Length == 1)
			{
				StartCoroutine(ExecutePlayer(mostVotedPlayers[0]));
			}
			else if (_captain.IsNone)
			{
				StartCoroutine(StartSecondaryExecution(mostVotedPlayers));
			}
			else
			{
				_startCaptainExecutionCoroutine = StartCaptainExecution(mostVotedPlayers);
				StartCoroutine(_startCaptainExecutionCoroutine);
			}
		}

		private IEnumerator StartSecondaryExecution(PlayerRef[] mostVotedPlayers)
		{
			yield return DisplayTitleForAllPlayers(GameConfig.ExecutionDrawNewVoteTitleScreen.ID.HashCode, GameConfig.ExecutionHoldDuration * GameSpeedModifier);

			if (!_voteManager.StartVoteForAllPlayers(OnSecondaryExecutionVotesCounted,
													GameConfig.ExecutionVoteTitleScreen.ID.HashCode,
													GameConfig.ExecutionVoteDuration * GameSpeedModifier,
													false,
													ChoicePurpose.Kill,
													GetDeadPlayers().ToArray(),
													modifiers: GetExecutionVoteModifiers(),
													ImmunePlayers: GetPlayersExcluding(mostVotedPlayers)))
			{
				Debug.LogError("Couldn't start the secondary execution vote");
				yield break;
			}

			_gameHistoryManager.AddEntry(GameConfig.ExecutionDrawNewVoteGameHistoryEntry.ID, null);
		}

		private void OnSecondaryExecutionVotesCounted(PlayerRef[] mostVotedPlayers)
		{
			if (mostVotedPlayers.Length == 1)
			{
				StartCoroutine(ExecutePlayer(mostVotedPlayers[0]));
			}
			else
			{
				_gameHistoryManager.AddEntry(GameConfig.ExecutionDrawAgainGameHistoryEntry.ID, null);
				StartCoroutine(DisplayFailedExecution());
			}
		}

		private IEnumerator StartCaptainExecution(PlayerRef[] mostVotedPlayers)
		{
			List<PlayerRef> choices = mostVotedPlayers.ToList();
			choices.Remove(_captain);

			if (!SelectPlayers(_captain,
								choices,
								GameConfig.ExecutionDrawYouChooseTitleScreen.ID.HashCode,
								GameConfig.ExecutionCaptainChoiceDuration * GameSpeedModifier,
								false,
								1,
								ChoicePurpose.Other,
								OnCaptainChooseExecutedPlayer))
			{
				AddExecutionDrawCaptainDidNotChoseGameHistoryEntry();
				StartCoroutine(DisplayFailedExecution());

				yield break;
			}

			foreach (var player in PlayerGameInfos)
			{
				if (_networkDataManager.PlayerInfos[player.Key].IsConnected && player.Key != _captain)
				{
					RPC_DisplayTitle(player.Key, GameConfig.ExecutionDrawCaptainChooseTitleScreen.ID.HashCode);
				}
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(GameConfig.ExecutionDrawCaptainChooseTitleScreen.ID.HashCode);
#endif
			yield return new WaitForSeconds(GameConfig.ExecutionCaptainChoiceDuration * GameSpeedModifier);

			_startCaptainExecutionCoroutine = null;

			StopSelectingPlayers(_captain);

			RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			HideUI();
#endif
			yield return new WaitForSeconds(GameConfig.UITransitionNormalDuration);

			AddExecutionDrawCaptainDidNotChoseGameHistoryEntry();
			StartCoroutine(DisplayFailedExecution());
		}

		private void OnCaptainChooseExecutedPlayer(PlayerRef[] players)
		{
			if (_startCaptainExecutionCoroutine != null)
			{
				StartCoroutine(EndCaptainExecution((players == null || players.Length <= 0) ? PlayerRef.None : players[0]));
			}
		}

		private IEnumerator EndCaptainExecution(PlayerRef executedPlayer)
		{
			StopCoroutine(_startCaptainExecutionCoroutine);
			_startCaptainExecutionCoroutine = null;

			RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			HideUI();
#endif
			yield return new WaitForSeconds(GameConfig.UITransitionNormalDuration);

			if (executedPlayer.IsNone)
			{
				AddExecutionDrawCaptainDidNotChoseGameHistoryEntry();
				StartCoroutine(DisplayFailedExecution());
			}
			else
			{
				_gameHistoryManager.AddEntry(GameConfig.ExecutionDrawCaptainChoseGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "Player",
												Data = _networkDataManager.PlayerInfos[_captain].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
											});

				StartCoroutine(ExecutePlayer(executedPlayer));
			}
		}

		private void AddExecutionDrawCaptainDidNotChoseGameHistoryEntry()
		{
			_gameHistoryManager.AddEntry(GameConfig.ExecutionDrawCaptainDidnotChoseGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "Player",
												Data = _networkDataManager.PlayerInfos[_captain].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});
		}

		private IEnumerator ExecutePlayer(PlayerRef executedPlayer)
		{
			_gameHistoryManager.AddEntry(GameConfig.ExecutionVotedPlayerGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "Player",
												Data = _networkDataManager.PlayerInfos[executedPlayer].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			AddMarkForDeath(executedPlayer, GameConfig.ExecutionMarkForDeath);
			yield return HighlightPlayerToggle(executedPlayer, GameConfig.ExecutionVotedPlayerDuration);

			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private IEnumerator DisplayFailedExecution()
		{
			yield return DisplayTitleForAllPlayers(GameConfig.ExecutionDrawAgainTitleScreen.ID.HashCode, GameConfig.ExecutionHoldDuration * GameSpeedModifier);
			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private Dictionary<PlayerRef, int> GetExecutionVoteModifiers()
		{
			Dictionary<PlayerRef, int> modifiers = new();

			if (!_captain.IsNone)
			{
				modifiers.Add(_captain, CAPTAIN_VOTE_MODIFIER);
			}

			return modifiers;
		}
		#endregion

		#region End Game
		private bool CheckForWinners()
		{
			if (_playerGroups.Count <= 0)
			{
				_gameHistoryManager.AddEntry(GameConfig.EndGameNobodyWonGameHistoryEntry.ID, null);

				PrepareEndGameSequence(new() { ID = default, Priority = -1, Players = new() });
				return true;
			}

			foreach (PlayerGroup playerGroup in _playerGroups)
			{
				if (playerGroup.Players.Count < AlivePlayerCount || (!playerGroup.Leader.IsNone && !PlayerGameInfos[playerGroup.Leader].IsAlive))
				{
					continue;
				}

				if (!_gameplayDataManager.TryGetGameplayData(playerGroup.ID.HashCode, out PlayerGroupData playerGroupData))
				{
					Debug.LogError($"Could not find the player group {playerGroup.ID.HashCode}");
				}

				_gameHistoryManager.AddEntry(GameConfig.EndGamePlayerGroupWonGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "PlayerGroup",
													Data = playerGroup.ID.HashCode.ToString(),
													Type = GameHistorySaveEntryVariableType.PlayerGroupeName
												},
												new()
												{
													Name = "PluralName",
													Data = playerGroupData.HasPluralName.ToString(),
													Type = GameHistorySaveEntryVariableType.Bool
												}
											},
											playerGroupData.ID);

				PrepareEndGameSequence(playerGroup);
				return true;
			}

			return false;
		}

		private void PrepareEndGameSequence(PlayerGroup winningPlayerGroup)
		{
			List<PlayerEndGameInfo> endGamePlayerInfos = new();
			byte[] gameHistoryData = Encoding.ASCII.GetBytes(_gameHistoryManager.GetGameHistoryJson());

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					Runner.SendReliableDataToPlayer(playerInfo.Key, new ReliableKey(), gameHistoryData);
				}

				int roleID = -1;

				if (playerInfo.Value.Role)
				{
					roleID = playerInfo.Value.Role.ID.HashCode;
				}

				endGamePlayerInfos.Add(new()
				{
					Player = playerInfo.Key,
					RoleID = roleID,
					IsAlive = playerInfo.Value.IsAlive,
					Won = !string.IsNullOrEmpty(winningPlayerGroup.ID.Guid)
						&& ((winningPlayerGroup.Leader.IsNone && winningPlayerGroup.Players.Contains(playerInfo.Key))
						|| (!winningPlayerGroup.Leader.IsNone && winningPlayerGroup.Leader == playerInfo.Key))
				});
#if UNITY_SERVER && UNITY_EDITOR
				if (endGamePlayerInfos[^1].Won)
				{
					SetPlayerCardHighlightVisible(playerInfo.Key, true);
				}
#endif
			}

			int winningPlayerGroupID = !string.IsNullOrEmpty(winningPlayerGroup.ID.Guid) ? winningPlayerGroup.ID.HashCode : -1;

			RPC_StartEndGameSequence(endGamePlayerInfos.ToArray(), winningPlayerGroupID);
#if UNITY_SERVER && UNITY_EDITOR
			StartCoroutine(StartEndGameSequence(endGamePlayerInfos.ToArray(), winningPlayerGroupID));
#endif
			StartCoroutine(ReturnToLobby());
		}

		private IEnumerator StartEndGameSequence(PlayerEndGameInfo[] endGamePlayerInfos, int winningPlayerGroupID)
		{
			foreach (PlayerEndGameInfo endGamePlayerInfo in endGamePlayerInfos)
			{
				if (endGamePlayerInfo.RoleID == -1)
				{
					continue;
				}
#if !UNITY_SERVER
				if (endGamePlayerInfo.IsAlive && endGamePlayerInfo.Player != Runner.LocalPlayer)
				{
					FlipCard(endGamePlayerInfo.Player, endGamePlayerInfo.RoleID);
				}
#endif
				if (endGamePlayerInfo.Won)
				{
					SetPlayerCardHighlightVisible(endGamePlayerInfo.Player, true);
				}
			}

			if (winningPlayerGroupID != -1)
			{
				if (!_gameplayDataManager.TryGetGameplayData(winningPlayerGroupID, out PlayerGroupData playerGroupData))
				{
					Debug.LogError($"Could not find the player group {winningPlayerGroupID}");
				}

				Dictionary<string, IVariable> variables = new()
				{
					{ "PlayerGroup", playerGroupData.Name },
					{ "PluralName",  new BoolVariable() { Value = playerGroupData.HasPluralName } }
				};

				DisplayTitle(playerGroupData.Image, GameConfig.PlayerGroupWonText, variables);
			}
			else
			{
				DisplayTitle(GameConfig.NoWinnerTitleScreen.ID.HashCode);
			}

			yield return new WaitForSeconds(GameConfig.EndGameHoldDuration * GameSpeedModifier);
			HideUI();
			yield return new WaitForSeconds(GameConfig.UITransitionNormalDuration);

			_UIManager.EndGameScreen.Initialize(endGamePlayerInfos, GameConfig.ReturnToLobbyCountdownDuration * GameSpeedModifier);
			_UIManager.FadeIn(_UIManager.EndGameScreen, GameConfig.UITransitionNormalDuration);

			yield return new WaitForSeconds(GameConfig.ReturnToLobbyCountdownDuration * GameSpeedModifier);

			_UIManager.LoadingScreen.Initialize(null);
			_UIManager.FadeIn(_UIManager.LoadingScreen, GameConfig.LoadingScreenTransitionDuration);
		}

		private IEnumerator ReturnToLobby()
		{
			yield return new WaitForSeconds(GameConfig.EndGameHoldDuration * GameSpeedModifier +
											GameConfig.UITransitionNormalDuration +
											GameConfig.ReturnToLobbyCountdownDuration * GameSpeedModifier +
											GameConfig.LoadingScreenTransitionDuration);

			Runner.LoadScene(SceneRef.FromIndex((int)SceneDefs.MENU), LoadSceneMode.Single);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_StartEndGameSequence(PlayerEndGameInfo[] endGamePlayerInfos, int winningPlayerGroupID)
		{
			StartCoroutine(StartEndGameSequence(endGamePlayerInfos.ToArray(), winningPlayerGroupID));
		}
		#endregion
		#endregion
		#endregion

		public void WaitForPlayer(PlayerRef player)
		{
			PlayersWaitingFor.Add(player);
		}

		public void StopWaintingForPlayer(PlayerRef player)
		{
			PlayersWaitingFor.Remove(player);
		}
#if UNITY_SERVER
		private void OnDisable()
		{
			_networkDataManager.PlayerDisconnected -= OnPlayerDisconnected;
		}
#endif
	}
}