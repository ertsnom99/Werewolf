using Assets.Scripts.Data.Tags;
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Werewolf.Data;
using Werewolf.Network;
using Werewolf.Network.Configs;
using Werewolf.UI;

namespace Werewolf
{
	public partial class GameManager : NetworkBehaviourSingleton<GameManager>
	{
		[field: SerializeField]
		public GameConfig Config { get; private set; }

		public List<RoleData> RolesToDistribute { get; private set; }

		public List<PlayerRef> PlayersWaitingFor { get; private set; }

		public int AlivePlayerCount { get; private set; }

		private bool _rolesDistributionDone = false;

		private bool _allPlayersReadyToReceiveRole = false;
		private bool _allRolesSent = false;
		private bool _allPlayersReadyToPlay = false;

		private GameplayLoopStep _currentGameplayLoopStep;

		private enum GameplayLoopStep
		{
			ElectionDebate = 0,
			Election,
			NightTransition,
			RoleCall,
			DayTransition,
			DayDeathReveal,
			ExecutionDebate,
			Execution,
			ExecutionDeathReveal,
		}

		private int _nightCount = 0;

		private List<PlayerRef> _captainCandidates = new();

		private bool _isPlayerDeathRevealCompleted;
		private IEnumerator _revealPlayerDeathCoroutine;
		
		private IEnumerator _startCaptainExecutionCoroutine;

		private GameplayDatabaseManager _gameplayDatabaseManager;
		private UIManager _UIManager;
		private VoteManager _voteManager;
		private DaytimeManager _daytimeManager;
		private NetworkDataManager _networkDataManager;

		// Server events
		public event Action PreRoleDistribution;
		public event Action PostRoleDistribution;
		public event Action PreStartGame;
		public event Action RollCallBegin;
		public event Action StartWaitingForPlayersRollCall;
		public event Action<PlayerRef> PlayerDeathRevealEnded;
		public event Action<PlayerRef, List<GameplayTag>, float> WaitBeforeDeathRevealStarted;
		public event Action<PlayerRef> WaitBeforeDeathRevealEnded;

		// Client events
		public event Action RoleReceived;

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
			_UIManager = UIManager.Instance;
			_voteManager = VoteManager.Instance;
			_daytimeManager = DaytimeManager.Instance;

			Config.ImagesData.Init();

			_voteManager.SetConfig(Config);
			_voteManager.SetPlayers(PlayerGameInfos);
			_daytimeManager.SetConfig(Config);
			_UIManager.TitleScreen.SetConfig(Config);
			_UIManager.ChoiceScreen.SetConfig(Config);
			_UIManager.VoteScreen.SetConfig(Config);
			_UIManager.EndGameScreen.SetConfig(Config);
			_UIManager.DisconnectedScreen.SetConfig(Config);
		}

		public override void Spawned()
		{
			HasSpawned = true;
			ManagerSpawned?.Invoke();
		}

		#region Pre Gameplay Loop
		public void PrepareGame(RolesSetup rolesSetup)
		{
			_networkDataManager = NetworkDataManager.Instance;
			_networkDataManager.PlayerDisconnected += OnPlayerDisconnected;

			SelectRolesToDistribute(rolesSetup);

			PreRoleDistribution?.Invoke();
			DistributeRoles();
			PostRoleDistribution?.Invoke();

			AlivePlayerCount = PlayerGameInfos.Count;

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

		#region Roles Selection
		private void SelectRolesToDistribute(RolesSetup rolesSetup)
		{
			// Convert GameplayTagIDs to RoleData
			RoleData defaultRole = _gameplayDatabaseManager.GetGameplayData<RoleData>(rolesSetup.DefaultRole);
			NetworkDataManager.ConvertToRoleSetupDatas(rolesSetup.MandatoryRoles, out List<RoleSetupData> mandatoryRoles);
			NetworkDataManager.ConvertToRoleSetupDatas(rolesSetup.AvailableRoles, out List<RoleSetupData> availableRoles);

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
			while (rolesToDistribute.Count < _networkDataManager.PlayerInfos.Count)
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
				else if (roleSetup.UseCount > _networkDataManager.PlayerInfos.Count - rolesToDistribute.Count)
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

			roleBehavior.Init();
			roleBehavior.OnSelectedToDistribute(ref rolesToDistribute, ref availableRoles);
		}
		#endregion

		#region Roles Distribution
		private void DistributeRoles()
		{
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

				PlayerGameInfos.Add(playerInfo.Key, new() { Role = selectedRole, Behaviors = selectedBehaviors, IsAlive = true });
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
					roles += $"{PlayerGameInfos[player].Role.Name} || ";
				}

				Debug.Log(roles);
			}

			Debug.Log("-------------------------------------------------------");
		}
#endif
		#region RPC calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_GivePlayerRole([RpcTarget] PlayerRef player, int roleGameplayTagID)
		{
			if (!_networkDataManager)
			{
				_networkDataManager = NetworkDataManager.Instance;
			}

			CreatePlayerCards(player, _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID));
			CreateReservedRoleCards();
			AdjustCamera();

			_voteManager.SetPlayerCards(_playerCards);

			RoleReceived?.Invoke();
		}
		#endregion
		#endregion

		#region Pre Gameplay Loop Progress
		private void CheckPreGameplayLoopProgress()
		{
			if (_rolesDistributionDone && !_allPlayersReadyToReceiveRole)
			{
				foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerGameInfo in PlayerGameInfos)
				{
					if (!_networkDataManager.PlayerInfos[playerGameInfo.Key].IsConnected)
					{
						continue;
					}

					WaitForPlayer(playerGameInfo.Key);
				}
			}
			else if (_rolesDistributionDone && _allPlayersReadyToReceiveRole && !_allRolesSent)
			{
				foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerGameInfo in PlayerGameInfos)
				{
					if (!_networkDataManager.PlayerInfos[playerGameInfo.Key].IsConnected)
					{
						continue;
					}

					WaitForPlayer(playerGameInfo.Key);
					RPC_GivePlayerRole(playerGameInfo.Key, PlayerGameInfos[playerGameInfo.Key].Role.GameplayTag.CompactTagId);
				}

				_allRolesSent = true;
			}
			else if (_allRolesSent && _allPlayersReadyToPlay)
			{
				PreStartGame?.Invoke();
				StartGame();
			}
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_ConfirmPlayerReadyToReceiveRole(RpcInfo info = default)
		{
			StopWaintingForPlayer(info.Source);

			Log.Info($"{info.Source} is ready!");

			if (PlayersWaitingFor.Count > 0)
			{
				return;
			}

			_allPlayersReadyToReceiveRole = true;

			Log.Info("All players are ready to receive their role!");

			CheckPreGameplayLoopProgress();
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_ConfirmPlayerReadyToPlay(RpcInfo info = default)
		{
			StopWaintingForPlayer(info.Source);

			if (PlayersWaitingFor.Count > 0)
			{
				return;
			}

			_allPlayersReadyToPlay = true;

			CheckPreGameplayLoopProgress();
		}
		#endregion
		#endregion

		private void AdjustCamera()
		{
			Camera.main.transform.position = Camera.main.transform.position.normalized * Config.CameraOffset.Evaluate(_networkDataManager.PlayerInfos.Count);
		}
		#endregion

		#region Gameplay Loop
		private void StartGame()
		{
			_currentGameplayLoopStep = GameplayLoopStep.ElectionDebate;
			ExecuteGameplayLoopStep();
		}

		#region Gameplay Loop Steps
		private IEnumerator MoveToNextGameplayLoopStep()
		{
			if (_currentGameplayLoopStep == GameplayLoopStep.ExecutionDeathReveal)
			{
				_currentGameplayLoopStep = GameplayLoopStep.Election;
			}

			_currentGameplayLoopStep++;

			yield return new WaitForSeconds(Config.GameplayLoopStepDelay);

			ExecuteGameplayLoopStep();
		}

		private void ExecuteGameplayLoopStep()
		{
			switch (_currentGameplayLoopStep)
			{
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
				case GameplayLoopStep.RoleCall:
					StartCoroutine(CallRoles());
					break;
				case GameplayLoopStep.DayTransition:
					StartCoroutine(ChangeDaytime(Daytime.Day));
					break;
				case GameplayLoopStep.DayDeathReveal:
					StartCoroutine(StartDeathReveal(true));
					break;
				case GameplayLoopStep.ExecutionDebate:
					StartCoroutine(StartDebate(Config.ExecutionDebateImage.CompactTagId, Config.ExecutionDebateDuration));
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

		#region Election
		private IEnumerator StartElectionDebate()
		{
			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				PromptPlayer(playerInfo.Key,
							Config.ElectionPromptTitleImage.CompactTagId,
							Config.ElectionPromptDuration,
							Config.ElectionPromptButtonText,
							OnPlayerWantsToBeCaptain,
							false);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(Config.ElectionPromptTitleImage.CompactTagId, Config.ElectionPromptDuration);
#endif
			yield return new WaitForSeconds(Config.ElectionPromptDuration);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				StopPromptingPlayer(playerInfo.Key, false);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_UIManager.FadeOut(Config.UITransitionNormalDuration);
#endif
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

			if (_captainCandidates.Count > 1)
			{
				StartCoroutine(HighlightPlayersToggle(_captainCandidates.ToArray()));
				yield return DisplayTitleForAllPlayers(Config.ElectionMultipleCandidateImage.CompactTagId, Config.ElectionMultipleCandidateDuration);
				StartCoroutine(StartDebate(Config.ElectionDebateImage.CompactTagId, Config.ElectionDebateDuration));
				yield break;
			}
			else if (_captainCandidates.Count == 1)
			{
				_captain = _captainCandidates[0];
				yield return ShowCaptain();
			}
			else
			{
				yield return DisplayTitleForAllPlayers(Config.ElectionNoCandidateImage.CompactTagId, Config.ElectionNoCandidateDuration);
			}

			_currentGameplayLoopStep = GameplayLoopStep.Election;
			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private void OnPlayerWantsToBeCaptain(PlayerRef player)
		{
			if (_captainCandidates.Contains(player))
			{
				return;
			}

			_captainCandidates.Add(player);
		}

		private void StartElection()
		{
			_voteManager.StartVoteForAllPlayers(OnElectionVotesCounted,
												Config.ElectionVoteDuration,
												false,
												false,
												ChoicePurpose.Other,
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

			_captain = votedPlayer;
			StartCoroutine(ShowElectionResult());
		}

		private IEnumerator ShowElectionResult()
		{
			yield return ShowCaptain();
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
							skipPlayer = !behavior.OnRoleCall(_nightCount, nightCall.PriorityIndex);

							if (!skipPlayer)
							{
								PlayersWaitingFor.Add(player);
								actifBehaviors.Add(player, behavior);
								behavior.GetTitlesOverride(nightCall.PriorityIndex, ref titlesOverride);
							}

							break;
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

					while (PlayersWaitingFor.Count > 0 || elapsedTime < Config.NightCallMinimumDuration)
					{
						yield return 0;
						elapsedTime += Time.deltaTime;
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
			string text = roleData.CanHaveMultiples ? Config.RolePlayingTextPlurial : Config.RolePlayingTextSingular;

			DisplayTitle(roleData.Image, string.Format(text, roleData.Name.ToLower()));
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
				yield return new WaitForSeconds(Config.DeathRevealTitleHoldDuration);

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

					yield return new WaitForSeconds(Config.DelayBeforeRevealingDeadPlayer);

					if (_networkDataManager.PlayerInfos[deadPlayer].IsConnected)
					{
						RPC_DisplayPlayerDiedTitle(deadPlayer);
					}

					_isPlayerDeathRevealCompleted = false;

					_revealPlayerDeathCoroutine = RevealPlayerDeath(deadPlayer,
																	GetPlayersExcluding(deadPlayer),
																	true,
																	_marksForDeath[0].MarksForDeath,
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

					PlayerDeathRevealEnded?.Invoke(deadPlayer);

					while (PlayersWaitingFor.Count > 0)
					{
						yield return 0;
					}

					SetPlayerDead(deadPlayer);
					_marksForDeath.RemoveAt(0);

					if (deadPlayer == _captain)
					{
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

			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private void DisplayDeathRevealTitle(bool hasAnyPlayerDied)
		{
			DisplayTitle(hasAnyPlayerDied ? Config.DeathRevealSomeoneDiedImage.CompactTagId : Config.DeathRevealNoOneDiedImage.CompactTagId);
		}

		private void DisplayPlayerDiedTitle()
		{
			DisplayTitle(Config.PlayerDiedImage.CompactTagId);
		}

		private IEnumerator RevealPlayerDeath(PlayerRef playerRevealed, PlayerRef[] revealTo, bool waitBeforeReveal, List<GameplayTag> marks, bool returnFaceDown, Action RevealPlayerCompleted)
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
				WaitBeforeDeathRevealStarted?.Invoke(playerRevealed, marks, Config.RoleRevealWaitDuration);

				yield return new WaitForSeconds(Config.RoleRevealWaitDuration);

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

			yield return new WaitForSeconds(Config.RoleRevealHoldDuration);

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
			PlayerGameInfos[deadPlayer] = new() { Role = PlayerGameInfos[deadPlayer].Role, Behaviors = PlayerGameInfos[deadPlayer].Behaviors, IsAlive = false };
			AlivePlayerCount--;

			RemovePlayerFromAllPlayerGroups(deadPlayer);

			foreach (RoleBehavior behavior in PlayerGameInfos[deadPlayer].Behaviors)
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

		#region Execution
		private void StartExecution()
		{
			_voteManager.StartVoteForAllPlayers(OnExecutionVotesCounted,
												Config.ExecutionVoteDuration,
												false,
												true,
												ChoicePurpose.Kill,
												GetExecutionVoteModifiers());
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
			yield return DisplayTitleForAllPlayers(Config.ExecutionDrawNewVoteImage.CompactTagId, Config.ExecutionTitleHoldDuration);

			_voteManager.StartVoteForAllPlayers(OnSecondaryExecutionVotesCounted,
												Config.ExecutionVoteDuration,
												false,
												false,
												ChoicePurpose.Kill,
												modifiers: GetExecutionVoteModifiers(),
												ImmunePlayers: GetPlayersExcluding(mostVotedPlayers));
		}

		private void OnSecondaryExecutionVotesCounted(PlayerRef[] mostVotedPlayers)
		{
			if (mostVotedPlayers.Length == 1)
			{
				StartCoroutine(ExecutePlayer(mostVotedPlayers[0]));
			}
			else
			{
				StartCoroutine(DisplayFailedExecution());
			}
		}

		private IEnumerator DisplayFailedExecution()
		{
			yield return DisplayTitleForAllPlayers(Config.ExecutionDrawAgainImage.CompactTagId, Config.ExecutionTitleHoldDuration);
			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private IEnumerator StartCaptainExecution(PlayerRef[] mostVotedPlayers)
		{
			List<PlayerRef> executionChoices = mostVotedPlayers.ToList();
			executionChoices.Remove(_captain);

			if (!AskClientToChoosePlayers(_captain,
										GetPlayersExcluding(executionChoices.ToArray()).ToList(),
										Config.ExecutionDrawYouChooseImage.CompactTagId,
										Config.ExecutionCaptainChoiceDuration,
										true,
										1,
										ChoicePurpose.Other,
										OnCaptainChooseExecutedPlayer))
			{
				StartCoroutine(ExecutePlayer(mostVotedPlayers[UnityEngine.Random.Range(0, mostVotedPlayers.Length)]));
				yield break;
			}

			foreach (var player in PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[player.Key].IsConnected || player.Key == _captain)
				{
					continue;
				}

				RPC_DisplayTitle(player.Key, Config.ExecutionDrawCaptainChooseImage.CompactTagId);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(Config.ExecutionDrawCaptainChooseImage.CompactTagId);
#endif
			yield return new WaitForSeconds(Config.ExecutionCaptainChoiceDuration);

			_startCaptainExecutionCoroutine = null;

			StopChoosingPlayers(_captain);

			RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			HideUI();
#endif
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

			StartCoroutine(ExecutePlayer(mostVotedPlayers[UnityEngine.Random.Range(0, mostVotedPlayers.Length)]));
		}

		private void OnCaptainChooseExecutedPlayer(PlayerRef[] executedPlayer)
		{
			if (_startCaptainExecutionCoroutine == null)
			{
				return;
			}

			StartCoroutine(EndCaptainExecution(executedPlayer[0]));
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

			StartCoroutine(ExecutePlayer(executedPlayer));
		}

		private IEnumerator ExecutePlayer(PlayerRef executedPlayer)
		{
			AddMarkForDeath(executedPlayer, Config.ExecutionMarkForDeath);
			yield return HighlightPlayerToggle(executedPlayer);

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
				PrepareEndGameSequence(new() { GameplayTag = null, Priority = -1, Players = new() });
				return true;
			}

			foreach (PlayerGroup playerGroup in _playerGroups)
			{
				if (playerGroup.Players.Count < AlivePlayerCount)
				{
					continue;
				}

				PrepareEndGameSequence(playerGroup);
				return true;
			}

			return false;
		}

		private void PrepareEndGameSequence(PlayerGroup winningPlayerGroup)
		{
			List<PlayerEndGameInfo> endGamePlayerInfos = new();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				int role = -1;

				if (playerInfo.Value.Role)
				{
					role = playerInfo.Value.Role.GameplayTag.CompactTagId;
				}

				endGamePlayerInfos.Add(new() { Player = playerInfo.Key,
												Role = role,
												IsAlive = playerInfo.Value.IsAlive,
												Won = winningPlayerGroup.GameplayTag != null ? winningPlayerGroup.Players.Contains(playerInfo.Key) : false });
#if UNITY_SERVER && UNITY_EDITOR
				if (endGamePlayerInfos[endGamePlayerInfos.Count - 1].Won)
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
				if (!endGamePlayerInfo.Won)
				{
					continue;
				}

				SetPlayerCardHighlightVisible(endGamePlayerInfo.Player, true);
			}

			if (winningPlayerGroupID > -1)
			{
				PlayerGroupData playerGroup = Config.PlayerGroups.GetPlayerGroup(winningPlayerGroupID);
				DisplayTitle(playerGroup.Image, string.Format(Config.WinningPlayerGroupText, playerGroup.Name));
			}
			else
			{
				DisplayTitle(Config.NoWinnerImage.CompactTagId);
			}

			yield return new WaitForSeconds(Config.EndGameTitleHoldDuration);
			HideUI();
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

			_UIManager.EndGameScreen.Initialize(endGamePlayerInfos, Config.ReturnToLobbyCountdownDuration);
			_UIManager.FadeIn(_UIManager.EndGameScreen, Config.UITransitionNormalDuration);

			yield return new WaitForSeconds(Config.ReturnToLobbyCountdownDuration);

			_UIManager.LoadingScreen.Initialize("");
			_UIManager.FadeIn(_UIManager.LoadingScreen, Config.LoadingScreenTransitionDuration);
		}

		private IEnumerator ReturnToLobby()
		{
			yield return new WaitForSeconds(Config.EndGameTitleHoldDuration +
											Config.UITransitionNormalDuration +
											Config.ReturnToLobbyCountdownDuration +
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
			if (PlayersWaitingFor.Contains(player))
			{
				return;
			}

			PlayersWaitingFor.Add(player);
		}

		public void StopWaintingForPlayer(PlayerRef player)
		{
			if (!PlayersWaitingFor.Contains(player))
			{
				return;
			}

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