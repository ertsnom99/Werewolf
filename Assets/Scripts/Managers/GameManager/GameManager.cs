using Assets.Scripts.Data.Tags;
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
		public GameConfig Config { get; private set; }

		public List<RoleData> RolesToDistribute { get; private set; }

		public float GameSpeedModifier { get; private set; }

		public List<PlayerRef> PlayersWaitingFor { get; private set; }

		public int AlivePlayerCount { get; private set; }

		private RolesSetup _rolesSetup;

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

		private int _nightCount = 0;

		private readonly List<PlayerRef> _captainCandidates = new();

		private readonly Dictionary<string, IVariable> _rolePlayingTextVariables = new()
		{
			{ "Role", null },
			{ "Multiple", new BoolVariable() }
		};

		private bool _isPlayerDeathRevealCompleted;
		private IEnumerator _revealPlayerDeathCoroutine;

		private IEnumerator _startCaptainExecutionCoroutine;

		private GameplayDatabaseManager _gameplayDatabaseManager;
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
		public event Action DeathRevealEnded;
		public event Action<PlayerRef, GameplayTag, float> WaitBeforeDeathRevealStarted;
		public event Action<PlayerRef> WaitBeforeDeathRevealEnded;
		public event Action<PlayerRef, GameplayTag> PlayerDeathRevealEnded;

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

			if (!Config)
			{
				Debug.LogError("The GameConfig of the GameManager is not defined");
			}
		}

		private void Start()
		{
			_gameplayDatabaseManager = GameplayDatabaseManager.Instance;
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
		public void PrepareGame(RolesSetup rolesSetup, GameSpeed gameSpeed)
		{
			_networkDataManager = NetworkDataManager.Instance;
			_networkDataManager.PlayerDisconnected += OnPlayerDisconnected;

			_rolesSetup = rolesSetup;
			GameSpeedModifier = Config.GameSpeedModifier[(int)gameSpeed];

			_gameHistoryManager.ClearEntries();

			UpdatePreGameplayLoopProgress();
		}

		private void UpdatePreGameplayLoopProgress()
		{
			if (!_allPlayersReadyToBeInitialized)
			{
				foreach (KeyValuePair<PlayerRef, PlayerNetworkInfo> playerInfo in _networkDataManager.PlayerInfos)
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
				SelectRolesToDistribute(_rolesSetup);
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
					RPC_InitializePlayer(playerGameInfo.Key, GameSpeedModifier, _playersOrder, PlayerGameInfos[playerGameInfo.Key].Role.GameplayTag.CompactTagId);
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
		private void SelectRolesToDistribute(RolesSetup rolesSetup)
		{
			// Convert GameplayTagIDs to RoleData
			RoleData defaultRole = _gameplayDatabaseManager.GetGameplayData<RoleData>(rolesSetup.DefaultRole);
			NetworkDataManager.ConvertToRoleSetupDatas(rolesSetup.MandatoryRoles, out List<RoleSetupData> mandatoryRoles);
			NetworkDataManager.ConvertToRoleSetupDatas(rolesSetup.AvailableRoles, out List<RoleSetupData> availableRoles);

			List<RoleData> rolesToDistribute = new();

			// Add all mandatory roles first
			while (mandatoryRoles.Count > 0)
			{
				RoleData[] addedRoles = SelectRolesFromRoleSetup(mandatoryRoles[0], rolesToDistribute);
				mandatoryRoles.Remove(mandatoryRoles[0]);

				PrepareRoleBehaviors(addedRoles, rolesToDistribute, mandatoryRoles, availableRoles);
			}

			List<RoleSetupData> excludedRuleSetups = new();
			int attempts = 0;

			// Complete with available roles at random or default role
			while (rolesToDistribute.Count < _networkDataManager.PlayerInfos.Count)
			{
				int startingRoleCount = rolesToDistribute.Count;

				if (availableRoles.Count <= 0 || attempts >= Config.AvailableRolesMaxAttemptCount)
				{
					rolesToDistribute.Add(defaultRole);
					PrepareRoleBehavior(defaultRole, rolesToDistribute, mandatoryRoles, availableRoles);

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
				else if (roleSetup.UseCount > _networkDataManager.PlayerInfos.Count - rolesToDistribute.Count)
				{
					excludedRuleSetups.Add(roleSetup);
					continue;
				}

				RoleData[] addedRoles = SelectRolesFromRoleSetup(roleSetup, rolesToDistribute);
				availableRoles.RemoveAt(randomIndex);

				PrepareRoleBehaviors(addedRoles, rolesToDistribute, mandatoryRoles, availableRoles);

				// Some roles were removed from the list of roles to distribute
				if (startingRoleCount > rolesToDistribute.Count)
				{
					excludedRuleSetups.Clear();
					attempts = 0;
				}
			}

			RolesToDistribute = rolesToDistribute;
		}

		private RoleData[] SelectRolesFromRoleSetup(RoleSetupData roleSetup, List<RoleData> rolesToDistribute)
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

		public void PrepareRoleBehaviors(RoleData[] roles, List<RoleData> rolesToDistribute, List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles)
		{
			foreach (RoleData role in roles)
			{
				PrepareRoleBehavior(role, rolesToDistribute, mandatoryRoles, availableRoles);
			}
		}

		public void PrepareRoleBehavior(RoleData role, List<RoleData> rolesToDistribute, List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles)
		{
			if (!role.Behavior)
			{
				return;
			}

			// Temporairy store the behaviors, because they must be attributed to specific players later
			RoleBehavior roleBehavior = Instantiate(role.Behavior, transform);

			roleBehavior.SetRoleGameplayTag(role.GameplayTag);
			roleBehavior.SetPrimaryRoleType(role.PrimaryType);

			foreach (GameplayTag playerGroup in role.PlayerGroups)
			{
				roleBehavior.AddPlayerGroup(playerGroup);
			}

			foreach (Priority nightPriority in role.NightPriorities)
			{
				roleBehavior.AddNightPriority(nightPriority);
			}

			roleBehavior.SetIsPrimaryBehavior(true);

			_unassignedRoleBehaviors.Add(roleBehavior, role);

			roleBehavior.Initialize();
			roleBehavior.OnSelectedToDistribute(mandatoryRoles, availableRoles, rolesToDistribute);
		}
		#endregion

		#region Roles Distribution
		private void DistributeRoles()
		{
			PreRoleDistribution?.Invoke();

			foreach (KeyValuePair<PlayerRef, Network.PlayerNetworkInfo> playerInfo in _networkDataManager.PlayerInfos)
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

				_gameHistoryManager.AddEntry(Config.PlayerGivenRoleGameHistoryEntry,
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
													Data = selectedRole.GameplayTag.name,
													Type = GameHistorySaveEntryVariableType.RoleName
												}
											},
											selectedRole.GameplayTag);
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
					foreach (GameplayTag playerGroup in playerInfo.Value.Role.PlayerGroups)
					{
						AddPlayerToPlayerGroup(playerInfo.Key, playerGroup);
					}

					continue;
				}

				foreach (GameplayTag playerGroup in playerInfo.Value.Behaviors[0].GetCurrentPlayerGroups())
				{
					AddPlayerToPlayerGroup(playerInfo.Key, playerGroup);
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
			_daytimeManager.Initialize(Config);
			_voteManager.Initialize(Config);
			_emotesManager.Initialize(Config);
			_UIManager.ChoiceScreen.SetConfig(Config);
			_UIManager.DisconnectedScreen.SetConfig(Config);
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
		public void RPC_InitializePlayer([RpcTarget] PlayerRef player, float gameSpeedModifier, PlayerRef[] playersOrder, int roleGameplayTagID)
		{
			GameSpeedModifier = gameSpeedModifier;
			InitializeConfigAndManagers();

			if (!_networkDataManager)
			{
				_networkDataManager = NetworkDataManager.Instance;
			}

			RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID);

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

			yield return new WaitForSeconds(Config.GameplayLoopStepDelay);

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
					_nightCount++;
					StartCoroutine(ChangeDaytime(Daytime.Night));
					break;
				case GameplayLoopStep.NightCall:
					StartCoroutine(CallRoles());
					break;
				case GameplayLoopStep.DayTransition:
					StartCoroutine(ChangeDaytime(Daytime.Day));
					break;
				case GameplayLoopStep.DayDeathReveal:
					StartCoroutine(StartDeathReveal(true));
					break;
				case GameplayLoopStep.ExecutionDebate:
					StartCoroutine(StartDebate(GetPlayersExcluding(GetDeadPlayers().ToArray()), Config.ExecutionDebateImage.CompactTagId, Config.ExecutionDebateDuration * GameSpeedModifier));
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

			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

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

			DisplayTitle(null, Config.GivenRoleText, variables);
		}
		#endregion
		#endregion

		#region Election
		private IEnumerator StartElectionDebate()
		{
			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				PromptPlayer(playerInfo.Key,
							Config.ElectionPromptImage.CompactTagId,
							Config.ElectionPromptDuration * GameSpeedModifier,
							OnPlayerWantsToBeCaptain,
							false);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(Config.ElectionPromptImage.CompactTagId, variables: null, countdownDuration: Config.ElectionPromptDuration * GameSpeedModifier);
#endif
			yield return new WaitForSeconds(Config.ElectionPromptDuration * GameSpeedModifier);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				StopPromptingPlayer(playerInfo.Key, false);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_UIManager.FadeOutAll(Config.UITransitionNormalDuration);
#endif
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

			if (_captainCandidates.Count > 1)
			{
				_gameHistoryManager.AddEntry(Config.ElectionMultipleCandidatesGameHistoryEntry,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "Players",
													Data = ConcatenatePlayersNickname(_captainCandidates, _networkDataManager),
													Type = GameHistorySaveEntryVariableType.Players
												}
											});

				PlayerRef[] captainCandidates = _captainCandidates.ToArray();

				RPC_SetPlayersCardHighlightVisible(captainCandidates, true);
#if UNITY_SERVER && UNITY_EDITOR
				SetPlayersCardHighlightVisible(captainCandidates, true);
#endif
				yield return DisplayTitleForAllPlayers(Config.ElectionMultipleCandidatesImage.CompactTagId, Config.ElectionMultipleCandidatesDuration * GameSpeedModifier);
				StartCoroutine(StartDebate(captainCandidates, Config.ElectionDebateImage.CompactTagId, Config.ElectionDebateDuration * GameSpeedModifier));
				yield break;
			}
			else if (_captainCandidates.Count == 1)
			{
				SetCaptain(_captainCandidates[0]);
				yield return ShowCaptain(true);
			}
			else
			{
				_gameHistoryManager.AddEntry(Config.ElectionNoCandidateGameHistoryEntry, null);
				yield return DisplayTitleForAllPlayers(Config.ElectionNoCandidateImage.CompactTagId, Config.ElectionNoCandidateDuration * GameSpeedModifier);
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
												Config.ElectionVoteImage.CompactTagId,
												Config.ElectionVoteDuration * GameSpeedModifier,
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
					_gameHistoryManager.AddEntry(Config.SunRoseGameHistoryEntry, null);
					break;
				case Daytime.Night:
					_gameHistoryManager.AddEntry(Config.SunSetGameHistoryEntry, null);
					break;
			}

			bool isDaytime = daytime == Daytime.Day;
			SetAllPlayersAwake(isDaytime);
			_emotesManager.SetAsleepCanSee(isDaytime);

			yield return new WaitForSeconds(Config.DaytimeTransitionDuration);

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

			while (_currentNightCallIndex < _nightCalls.Count)
			{
				NightCall nightCall = _nightCalls[_currentNightCallIndex];
				Dictionary<PlayerRef, RoleBehavior> actifBehaviors = new();
				Dictionary<PlayerRef, int> titlesOverride = new();

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
								_gameHistoryManager.AddEntry(Config.WokeUpPlayerGameHistoryEntry,
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
																Data = PlayerGameInfos[player].Role.GameplayTag.name,
																Type = GameHistorySaveEntryVariableType.RoleName
															}
															},
															PlayerGameInfos[player].Role.GameplayTag);
							}

							PlayersWaitingFor.Add(player);
							actifBehaviors.Add(player, behavior);
							behavior.GetTitlesOverride(nightCall.PriorityIndex, ref titlesOverride);
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

					int displayRoleGameplayTagID = GetDisplayedRoleGameplayTagID(nightCall);

					foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
					{
						if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected || PlayersWaitingFor.Contains(playerInfo.Key))
						{
							continue;
						}

						if (titlesOverride.Count <= 0 || !titlesOverride.ContainsKey(playerInfo.Key))
						{
							RPC_DisplayRolePlaying(playerInfo.Key, displayRoleGameplayTagID);
						}
						else
						{
							RPC_DisplayTitle(playerInfo.Key, titlesOverride[playerInfo.Key]);
						}
					}
#if UNITY_SERVER && UNITY_EDITOR
					if (!_voteManager.IsPreparingToVote())
					{
						DisplayRolePlaying(displayRoleGameplayTagID);
					}
#endif
					float elapsedTime = .0f;

					while (PlayersWaitingFor.Count > 0 || elapsedTime < Config.NightCallMinimumDuration * GameSpeedModifier)
					{
						yield return 0;
						elapsedTime += Time.deltaTime;
					}

					SetAllPlayersAwake(false);

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

		private int GetDisplayedRoleGameplayTagID(NightCall nightCall)
		{
			RoleData alias = null;
			RoleBehavior behaviorCalled = null;

			foreach (RoleBehavior behavior in PlayerGameInfos[nightCall.Players[0]].Behaviors)
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
				return alias.GameplayTag.CompactTagId;
			}
			else
			{
				return behaviorCalled.RoleGameplayTag.CompactTagId;
			}
		}

		private void DisplayRolePlaying(int roleGameplayTagID)
		{
			RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID);
			bool multipleRole = roleData.CanHaveMultiples;

			_rolePlayingTextVariables["Role"] = multipleRole ? roleData.NamePlural : roleData.NameSingular;
			((BoolVariable)_rolePlayingTextVariables["Multiple"]).Value = multipleRole;

			DisplayTitle(roleData.Image, Config.RolePlayingText, _rolePlayingTextVariables);
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
		public IEnumerator StartDeathReveal(bool showTitle)
		{
			bool hasAnyPlayerDied = _marksForDeath.Count > 0;

			if (showTitle)
			{
				RPC_DisplayDeathRevealTitle(hasAnyPlayerDied);
#if UNITY_SERVER && UNITY_EDITOR
				DisplayDeathRevealTitle(hasAnyPlayerDied);
#endif
				yield return new WaitForSeconds(Config.UITransitionNormalDuration);
				yield return new WaitForSeconds(Config.DeathRevealHoldDuration * GameSpeedModifier);

				RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
				HideUI();
#endif
				yield return new WaitForSeconds(Config.UITransitionNormalDuration);
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

					yield return new WaitForSeconds(Config.DelayBeforeRevealingDeadPlayer * GameSpeedModifier);

					_gameHistoryManager.AddEntry(Config.PlayerDiedGameHistoryEntry,
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
														Data = PlayerGameInfos[deadPlayer].Role.GameplayTag.name,
														Type = GameHistorySaveEntryVariableType.RoleName
													}
												});

					if (_networkDataManager.PlayerInfos[deadPlayer].IsConnected)
					{
						RPC_DisplayPlayerDiedTitle(deadPlayer, _marksForDeath[0].Mark == Config.ExecutionMarkForDeath);
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
					yield return new WaitForSeconds(Config.UITransitionNormalDuration);

					PlayerDeathRevealEnded?.Invoke(deadPlayer, _marksForDeath[0].Mark);

					while (PlayersWaitingFor.Count > 0)
					{
						yield return 0;
					}

					SetPlayerDead(deadPlayer);
					_marksForDeath.RemoveAt(0);

					if (deadPlayer == _captain)
					{
						_gameHistoryManager.AddEntry(Config.CaptainDiedGameHistoryEntry,
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
			DisplayTitle(hasAnyPlayerDied ? Config.DeathRevealSomeoneDiedImage.CompactTagId : Config.DeathRevealNoOneDiedImage.CompactTagId);
		}

		private void DisplayPlayerDiedTitle(bool wasExecuted)
		{
			DisplayTitle(wasExecuted ? Config.PlayerExecutedImage.CompactTagId : Config.PlayerDiedImage.CompactTagId);
		}

		private IEnumerator RevealPlayerDeath(PlayerRef playerRevealed, PlayerRef[] revealTo, bool waitBeforeReveal, GameplayTag mark, bool returnFaceDown, Action RevealPlayerCompleted)
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
								!waitBeforeReveal ? PlayerGameInfos[playerRevealed].Role.GameplayTag.CompactTagId : -1,
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
				WaitBeforeDeathRevealStarted?.Invoke(playerRevealed, mark, Config.RoleRevealWaitDuration * GameSpeedModifier);

				yield return new WaitForSeconds(Config.RoleRevealWaitDuration * GameSpeedModifier);

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
							PlayerGameInfos[playerRevealed].Role.GameplayTag.CompactTagId);
				}

				while (PlayersWaitingFor.Count > 0)
				{
					yield return 0;
				}
			}

			yield return new WaitForSeconds(Config.RoleRevealHoldDuration * GameSpeedModifier);

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
			if (_playerCards.ContainsKey(deadPlayer) && _playerCards[deadPlayer])
			{
				_playerCards[deadPlayer].DisplayDeadIcon();
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
													Config.ExecutionVoteImage.CompactTagId,
													Config.ExecutionVoteDuration * GameSpeedModifier,
													true,
													ChoicePurpose.Kill,
													GetDeadPlayers().ToArray(),
													GetExecutionVoteModifiers()))
			{
				Debug.LogError("Couldn't start the execution vote");
				return;
			}

			_gameHistoryManager.AddEntry(Config.ExecutionVoteStartedGameHistoryEntry, null);
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
			yield return DisplayTitleForAllPlayers(Config.ExecutionDrawNewVoteImage.CompactTagId, Config.ExecutionHoldDuration * GameSpeedModifier);

			if (!_voteManager.StartVoteForAllPlayers(OnSecondaryExecutionVotesCounted,
													Config.ExecutionVoteImage.CompactTagId,
													Config.ExecutionVoteDuration * GameSpeedModifier,
													false,
													ChoicePurpose.Kill,
													GetDeadPlayers().ToArray(),
													modifiers: GetExecutionVoteModifiers(),
													ImmunePlayers: GetPlayersExcluding(mostVotedPlayers)))
			{
				Debug.LogError("Couldn't start the secondary execution vote");
				yield break;
			}

			_gameHistoryManager.AddEntry(Config.ExecutionDrawNewVoteGameHistoryEntry, null);
		}

		private void OnSecondaryExecutionVotesCounted(PlayerRef[] mostVotedPlayers)
		{
			if (mostVotedPlayers.Length == 1)
			{
				StartCoroutine(ExecutePlayer(mostVotedPlayers[0]));
			}
			else
			{
				_gameHistoryManager.AddEntry(Config.ExecutionDrawAgainGameHistoryEntry, null);
				StartCoroutine(DisplayFailedExecution());
			}
		}

		private IEnumerator StartCaptainExecution(PlayerRef[] mostVotedPlayers)
		{
			List<PlayerRef> choices = mostVotedPlayers.ToList();
			choices.Remove(_captain);

			if (!SelectPlayers(_captain,
								choices,
								Config.ExecutionDrawYouChooseImage.CompactTagId,
								Config.ExecutionCaptainChoiceDuration * GameSpeedModifier,
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
					RPC_DisplayTitle(player.Key, Config.ExecutionDrawCaptainChooseImage.CompactTagId);
				}
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(Config.ExecutionDrawCaptainChooseImage.CompactTagId);
#endif
			yield return new WaitForSeconds(Config.ExecutionCaptainChoiceDuration * GameSpeedModifier);

			_startCaptainExecutionCoroutine = null;

			StopSelectingPlayers(_captain);

			RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			HideUI();
#endif
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

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
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

			if (executedPlayer.IsNone)
			{
				AddExecutionDrawCaptainDidNotChoseGameHistoryEntry();
				StartCoroutine(DisplayFailedExecution());
			}
			else
			{
				_gameHistoryManager.AddEntry(Config.ExecutionDrawCaptainChoseGameHistoryEntry,
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
			_gameHistoryManager.AddEntry(Config.ExecutionDrawCaptainDidnotChoseGameHistoryEntry,
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
			_gameHistoryManager.AddEntry(Config.ExecutionVotedPlayerGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "Player",
												Data = _networkDataManager.PlayerInfos[executedPlayer].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			AddMarkForDeath(executedPlayer, Config.ExecutionMarkForDeath);
			yield return HighlightPlayerToggle(executedPlayer, Config.ExecutionVotedPlayerDuration);

			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private IEnumerator DisplayFailedExecution()
		{
			yield return DisplayTitleForAllPlayers(Config.ExecutionDrawAgainImage.CompactTagId, Config.ExecutionHoldDuration * GameSpeedModifier);
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
				_gameHistoryManager.AddEntry(Config.EndGameNobodyWonGameHistoryEntry, null);

				PrepareEndGameSequence(new() { GameplayTag = null, Priority = -1, Players = new() });
				return true;
			}

			foreach (PlayerGroup playerGroup in _playerGroups)
			{
				if (playerGroup.Players.Count < AlivePlayerCount)
				{
					continue;
				}

				PlayerGroupData playerGroupData = _gameplayDatabaseManager.GetGameplayData<PlayerGroupData>(playerGroup.GameplayTag.CompactTagId);

				_gameHistoryManager.AddEntry(Config.EndGamePlayerGroupWonGameHistoryEntry,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "PlayerGroup",
													Data = playerGroup.GameplayTag.name,
													Type = GameHistorySaveEntryVariableType.PlayerGroupeName
												},
												new()
												{
													Name = "PluralName",
													Data = playerGroupData.HasPluralName.ToString(),
													Type = GameHistorySaveEntryVariableType.Bool
												}
											},
											playerGroupData.GameplayTag);

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

				int role = -1;

				if (playerInfo.Value.Role)
				{
					role = playerInfo.Value.Role.GameplayTag.CompactTagId;
				}

				endGamePlayerInfos.Add(new()
				{
					Player = playerInfo.Key,
					Role = role,
					IsAlive = playerInfo.Value.IsAlive,
					Won = winningPlayerGroup.GameplayTag != null && winningPlayerGroup.Players.Contains(playerInfo.Key)
				});
#if UNITY_SERVER && UNITY_EDITOR
				if (endGamePlayerInfos[^1].Won)
				{
					SetPlayerCardHighlightVisible(playerInfo.Key, true);
				}
#endif
			}

			int winningPlayerGroupID = winningPlayerGroup.GameplayTag != null ? winningPlayerGroup.GameplayTag.CompactTagId : -1;

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
				if (endGamePlayerInfo.Role == -1)
				{
					continue;
				}
#if !UNITY_SERVER
				if (endGamePlayerInfo.IsAlive && endGamePlayerInfo.Player != Runner.LocalPlayer)
				{
					FlipCard(endGamePlayerInfo.Player, endGamePlayerInfo.Role);
				}
#endif
				if (endGamePlayerInfo.Won)
				{
					SetPlayerCardHighlightVisible(endGamePlayerInfo.Player, true);
				}
			}

			if (winningPlayerGroupID > -1)
			{
				PlayerGroupData playerGroupData = _gameplayDatabaseManager.GetGameplayData<PlayerGroupData>(winningPlayerGroupID);

				Dictionary<string, IVariable> variables = new()
				{
					{ "PlayerGroup", playerGroupData.Name },
					{ "PluralName",  new BoolVariable() { Value = playerGroupData.HasPluralName } }
				};

				DisplayTitle(playerGroupData.Image, Config.PlayerGroupWonText, variables);
			}
			else
			{
				DisplayTitle(Config.NoWinnerImage.CompactTagId);
			}

			yield return new WaitForSeconds(Config.EndGameHoldDuration * GameSpeedModifier);
			HideUI();
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

			_UIManager.EndGameScreen.Initialize(endGamePlayerInfos, Config.ReturnToLobbyCountdownDuration * GameSpeedModifier);
			_UIManager.FadeIn(_UIManager.EndGameScreen, Config.UITransitionNormalDuration);

			yield return new WaitForSeconds(Config.ReturnToLobbyCountdownDuration * GameSpeedModifier);

			_UIManager.LoadingScreen.Initialize(null);
			_UIManager.FadeIn(_UIManager.LoadingScreen, Config.LoadingScreenTransitionDuration);
		}

		private IEnumerator ReturnToLobby()
		{
			yield return new WaitForSeconds(Config.EndGameHoldDuration * GameSpeedModifier +
											Config.UITransitionNormalDuration +
											Config.ReturnToLobbyCountdownDuration * GameSpeedModifier +
											Config.LoadingScreenTransitionDuration);

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
			if (!PlayersWaitingFor.Contains(player))
			{
				PlayersWaitingFor.Add(player);
			}
		}

		public void StopWaintingForPlayer(PlayerRef player)
		{
			if (PlayersWaitingFor.Contains(player))
			{
				PlayersWaitingFor.Remove(player);
			}
		}
#if UNITY_SERVER
		private void OnDisable()
		{
			_networkDataManager.PlayerDisconnected -= OnPlayerDisconnected;
		}
#endif
	}
}