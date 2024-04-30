using Fusion;
using Fusion.Sockets;
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
	public struct PlayerGameInfo
	{
		public RoleData Role;
		public List<RoleBehavior> Behaviors;
		public bool IsAlive;
	}

	[Serializable]
	public struct PlayerEndGameInfo : INetworkStruct
	{
		public PlayerRef Player;
		public int Role;
		public bool IsAlive;
		public bool Won;
	}

	public class GameManager : NetworkBehaviourSingleton<GameManager>, INetworkRunnerCallbacks
	{
		#region Server variables
		public List<RoleData> RolesToDistribute { get; private set; }

		private Dictionary<RoleBehavior, RoleData> _unassignedRoleBehaviors = new();

		public Dictionary<PlayerRef, PlayerGameInfo> PlayerGameInfos { get; private set; }

		private List<PlayerRef> _captainCandidates = new List<PlayerRef>();
		private PlayerRef _captain;
		private GameObject _captainCard;

		public int AlivePlayerCount { get; private set; }

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
		private IEnumerator _chooseNextCaptainCoroutine;

		private bool _isNextCaptainChoiceCompleted;

		private Action<PlayerRef[]> _votesCountedCallback;

		private IEnumerator _startCaptainExecutionCoroutine;

		private Dictionary<PlayerRef, Action<PlayerRef>> _revealPlayerRoleCallbacks = new();
		private Dictionary<PlayerRef, Action> _moveCardToCameraCallbacks = new();
		private Dictionary<PlayerRef, Action> _flipCardCallbacks = new();
		private Dictionary<PlayerRef, Action> _putCardBackDownCallbacks = new();

		private Dictionary<PlayerRef, Action<PlayerRef>> _promptPlayerCallbacks = new();
		#endregion

		#region Networked variables
		[Networked, Capacity(5)]
		public NetworkArray<RolesContainer> ReservedRoles { get; }

		public struct RolesContainer : INetworkStruct
		{
			public int RoleCount;
			[Networked, Capacity(5)]
			public NetworkArray<int> Roles { get; }
		}
		#endregion

		[field: SerializeField]
		public GameConfig Config { get; private set; }

		public static bool HasSpawned { get; private set; }

		private Dictionary<PlayerRef, Card> _playerCards = new();
		private Card[][] _reservedRolesCards;

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

		private GameplayLoopStep _currentGameplayLoopStep;

		private NetworkDataManager _networkDataManager;
		private GameplayDatabaseManager _gameplayDatabaseManager;
		private UIManager _UIManager;
		private VoteManager _voteManager;
		private DaytimeManager _daytimeManager;

		public static event Action ManagerSpawned;

		// Server events
		public event Action PreRoleDistribution;
		public event Action PostRoleDistribution;
		public event Action PreStartGame;
		public event Action StartWaitingForPlayersRollCall;
		public event Action<PlayerRef, string> OnMarkForDeathAdded;
		public event Action<PlayerRef, List<string>, float> WaitBeforeDeathRevealStarted;
		public event Action<PlayerRef> WaitBeforeDeathRevealEnded;
		public event Action<PlayerRef> PlayerDeathRevealEnded;
		public event Action<PlayerRef> OnPostPlayerLeft;

		// Client events
		public event Action OnRoleReceived;

		private readonly Vector3 STARTING_DIRECTION = Vector3.back;
		private readonly int CAPTAIN_VOTE_MODIFIER = 2;

		protected override void Awake()
		{
			base.Awake();

			RolesToDistribute = new();
			PlayerGameInfos = new();

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
			_voteManager = VoteManager.Instance;
			_daytimeManager = DaytimeManager.Instance;

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

			roleBehavior.SetPrimaryRoleType(role.PrimaryType);

			foreach (int playerGroupIndex in role.PlayerGroupIndexes)
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
					foreach (int playerGroupIndex in playerInfo.Value.Role.PlayerGroupIndexes)
					{
						AddPlayerToPlayerGroup(playerGroupIndex, playerInfo.Key);
					}

					continue;
				}

				foreach (int playerGroupIndex in playerInfo.Value.Behaviors[0].GetCurrentPlayerGroups())
				{
					AddPlayerToPlayerGroup(playerGroupIndex, playerInfo.Key);
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

			OnRoleReceived?.Invoke();
		}
		#endregion
		#endregion

		#region Loop Progress
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

			if (_playersWaitingFor.Count > 0)
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

			if (_playersWaitingFor.Count > 0)
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
					StartCoroutine(StartDebate(Config.ExecutionDebateText, Config.ExecutionDebateDuration));
					break;
				case GameplayLoopStep.Execution:
					StartExecution();
					break;
				case GameplayLoopStep.ExecutionDeathReveal:
					StartCoroutine(StartDeathReveal(false));
					break;
			}
		}

		#region Election
		private IEnumerator StartElectionDebate()
		{
			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				PromptPlayer(playerInfo.Key,
							Config.ElectionPromptTitleText,
							Config.ElectionPromptDuration,
							Config.ElectionPromptButtonText,
							OnPlayerWantsToBeCaptain,
							false);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(null, Config.ElectionPromptTitleText, Config.ElectionPromptDuration);
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
				StartCoroutine(HighlightPlayers(_captainCandidates.ToArray()));
				yield return DisplayTitleForAllPlayers(Config.ElectionMultipleCandidateText, Config.ElectionMultipleCandidateDuration);
				StartCoroutine(StartDebate(Config.ElectionDebateText, Config.ElectionDebateDuration));
				yield break;
			}
			else if (_captainCandidates.Count == 1)
			{
				_captain = _captainCandidates[0];
				yield return ShowCaptain();
			}
			else
			{
				yield return DisplayTitleForAllPlayers(Config.ElectionNoCandidateText, Config.ElectionNoCandidateDuration);
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
			StartVoteForAllPlayers(OnElectionVotesCounted, Config.ElectionVoteDuration, false, false, null, true, GetPlayersExcluding(_captainCandidates.ToArray()));
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
			_currentNightCallIndex = 0;

			while (_currentNightCallIndex < _nightCalls.Count)
			{
				NightCall nightCall = _nightCalls[_currentNightCallIndex];
				Dictionary<PlayerRef, RoleBehavior> actifBehaviors = new();

				// Role call all the roles that must play
				foreach (PlayerRef player in nightCall.Players)
				{
					bool skipPlayer = false;

					foreach (RoleBehavior behavior in PlayerGameInfos[player].Behaviors)
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
					StartWaitingForPlayersRollCall?.Invoke();

					int displayRoleGameplayTagID = GetDisplayedRoleGameplayTagID(nightCall);

					foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
					{
						if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected || _playersWaitingFor.Contains(playerInfo.Key))
						{
							continue;
						}

						RPC_DisplayRolePlaying(playerInfo.Key, displayRoleGameplayTagID);
					}
#if UNITY_SERVER && UNITY_EDITOR
					if (!_voteManager.IsPreparingToVote())
					{
						DisplayRolePlaying(displayRoleGameplayTagID);
					}
#endif
					float elapsedTime = .0f;

					while (!IsNightCallOver(elapsedTime))
					{
						yield return 0;
						elapsedTime += Time.deltaTime;
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

		private int GetDisplayedRoleGameplayTagID(NightCall nightCall)
		{
			RoleData alias = null;
			bool aliasFound = false;

			foreach (RoleBehavior behavior in PlayerGameInfos[nightCall.Players[0]].Behaviors)
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
				return PlayerGameInfos[nightCall.Players[0]].Role.GameplayTag.CompactTagId;
			}
		}

		private void DisplayRolePlaying(int roleGameplayTagID)
		{
			RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID);
			string text = roleData.CanHaveMultiples ? Config.RolePlayingTextPlurial : Config.RolePlayingTextSingular;

			DisplayTitle(roleData.Image, string.Format(text, roleData.Name.ToLower()));// TODO: Give real image
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
																	_marksForDeath[0].Marks,
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

					while (_playersWaitingFor.Count > 0)
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
			DisplayTitle(null, hasAnyPlayerDied ? Config.DeathRevealSomeoneDiedText : Config.DeathRevealNooneDiedText);// TODO: Give real image
		}

		private void DisplayPlayerDiedTitle()
		{
			DisplayTitle(null, Config.PlayerDiedText);// TODO: Give real image
		}

		private IEnumerator RevealPlayerDeath(PlayerRef playerRevealed, PlayerRef[] revealTo, bool waitBeforeReveal, List<string> marks, bool returnFaceDown, Action RevealPlayerCompleted)
		{
			foreach (PlayerRef player in revealTo)
			{
				if (!_networkDataManager.PlayerInfos[player].IsConnected)
				{
					continue;
				}

				_playersWaitingFor.Add(player);
				MoveCardToCamera(player,
								playerRevealed,
								!waitBeforeReveal,
								!waitBeforeReveal ? PlayerGameInfos[playerRevealed].Role.GameplayTag.CompactTagId : -1,
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
				WaitBeforeDeathRevealStarted?.Invoke(playerRevealed, marks, Config.RoleRevealWaitDuration);

				yield return new WaitForSeconds(Config.RoleRevealWaitDuration);

				WaitBeforeDeathRevealEnded?.Invoke(playerRevealed);

				foreach (PlayerRef player in revealTo)
				{
					if (!_networkDataManager.PlayerInfos[player].IsConnected)
					{
						continue;
					}

					_playersWaitingFor.Add(player);
					FlipCard(player,
							playerRevealed,
							() => StopWaintingForPlayer(player),
							PlayerGameInfos[playerRevealed].Role.GameplayTag.CompactTagId);
				}

				while (_playersWaitingFor.Count > 0)
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
			PlayerGameInfos[deadPlayer] = new PlayerGameInfo { Role = PlayerGameInfos[deadPlayer].Role, Behaviors = PlayerGameInfos[deadPlayer].Behaviors, IsAlive = false };
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
			StartVoteForAllPlayers(OnExecutionVotesCounted,
									Config.ExecutionVoteDuration,
									false,
									true,
									GetExecutionVoteModifiers());
		}

		private void OnExecutionVotesCounted(PlayerRef[] mostVotedPlayers)
		{
			if (mostVotedPlayers.Length == 1)
			{
				StartCoroutine(ExecutePlayer(mostVotedPlayers[0]));
			}
			else if (_captain == PlayerRef.None)
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
			yield return DisplayTitleForAllPlayers(Config.ExecutionDrawNewVoteText, Config.ExecutionTitleHoldDuration);

			StartVoteForAllPlayers(OnSecondaryExecutionVotesCounted,
									Config.ExecutionVoteDuration,
									false,
									false,
									GetExecutionVoteModifiers(),
									false,
									GetPlayersExcluding(mostVotedPlayers));
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
			yield return DisplayTitleForAllPlayers(Config.ExecutionDrawAgainText, Config.ExecutionTitleHoldDuration);
			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private IEnumerator StartCaptainExecution(PlayerRef[] mostVotedPlayers)
		{
			List<PlayerRef> executionChoices = mostVotedPlayers.ToList();
			executionChoices.Remove(_captain);

			if (!AskClientToChoosePlayer(_captain,
									GetPlayersExcluding(executionChoices.ToArray()),
									Config.ExecutionDrawYouChooseText,
									Config.ExecutionCaptainChoiceDuration,
									false,
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

				RPC_DisplayTitle(player.Key, Config.ExecutionDrawCaptainChooseText);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(null, Config.ExecutionDrawCaptainChooseText);
#endif
			yield return new WaitForSeconds(Config.ExecutionCaptainChoiceDuration);

			_startCaptainExecutionCoroutine = null;

			StopChoosingPlayer(_captain);

			RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			HideUI();
#endif
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

			StartCoroutine(ExecutePlayer(mostVotedPlayers[UnityEngine.Random.Range(0, mostVotedPlayers.Length)]));
		}

		private void OnCaptainChooseExecutedPlayer(PlayerRef executedPlayer)
		{
			if (_startCaptainExecutionCoroutine == null)
			{
				return;
			}

			StartCoroutine(EndCaptainExecution(executedPlayer));
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

			if (_captain != PlayerRef.None)
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
				PrepareEndGameSequence(null);
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

		private void PrepareEndGameSequence(PlayerGroup? winningPlayerGroup)
		{
			List<PlayerEndGameInfo> endGamePlayerInfos = new List<PlayerEndGameInfo>();

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
												Won = winningPlayerGroup != null ? IsPlayerInGroup(playerInfo.Key, (PlayerGroup)winningPlayerGroup) : false });
#if UNITY_SERVER && UNITY_EDITOR
				if (endGamePlayerInfos[endGamePlayerInfos.Count - 1].Won)
				{
					SetPlayerCardHighlightVisible(playerInfo.Key, true);
				}
#endif
			}

			int winningPlayerGroupIndex = winningPlayerGroup != null ? ((PlayerGroup)winningPlayerGroup).Index : -1;

			RPC_StartEndGameSequence(endGamePlayerInfos.ToArray(), winningPlayerGroupIndex);
#if UNITY_SERVER && UNITY_EDITOR
			StartCoroutine(StartEndGameSequence(endGamePlayerInfos.ToArray(), winningPlayerGroupIndex));
#endif
			StartCoroutine(ReturnToLobby());
		}

		private bool IsPlayerInGroup(PlayerRef player, PlayerGroup playerGroup)
		{
			return (PlayerGameInfos[player].Behaviors.Count > 0 && PlayerGameInfos[player].Behaviors[0].PlayerGroupIndexes.Contains(playerGroup.Index))
			|| (PlayerGameInfos[player].Behaviors.Count <= 0 && playerGroup.Index == Config.PlayerGroups.NoBehaviorGroupIndex);
		}

		private IEnumerator StartEndGameSequence(PlayerEndGameInfo[] endGamePlayerInfos, int winningPlayerGroupIndex)
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

			if (winningPlayerGroupIndex > -1)
			{
				PlayerGroupData playerGroupData = Config.PlayerGroups.GetPlayerGroupData(winningPlayerGroupIndex);
				DisplayTitle(playerGroupData.Image, string.Format(Config.WinningPlayerGroupText, playerGroupData.Name));
			}
			else
			{
				DisplayTitle(null, Config.NoWinnerText);
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
		public void RPC_StartEndGameSequence(PlayerEndGameInfo[] endGamePlayerInfos, int winningPlayerGroupIndex)
		{
			StartCoroutine(StartEndGameSequence(endGamePlayerInfos.ToArray(), winningPlayerGroupIndex));
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

		private PlayerRef[] GetPlayersExcluding(PlayerRef playerToExclude)
		{
			List<PlayerRef> players = new List<PlayerRef>();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (playerInfo.Key == playerToExclude)
				{
					continue;
				}

				players.Add(playerInfo.Key);
			}

			return players.ToArray();
		}

		private PlayerRef[] GetPlayersExcluding(PlayerRef[] playersToExclude)
		{
			List<PlayerRef> players = new List<PlayerRef>();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (playersToExclude.Contains(playerInfo.Key))
				{
					continue;
				}

				players.Add(playerInfo.Key);
			}

			return players.ToArray();
		}

		#region Captain
		private IEnumerator ChooseNextCaptain()
		{
			List<PlayerRef> captainChoices = new List<PlayerRef>();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (!playerInfo.Value.IsAlive || playerInfo.Key == _captain)
				{
					continue;
				}

				captainChoices.Add(playerInfo.Key);
			}

			if (captainChoices.Count <= 0)
			{
				RPC_DestroyCaptainCard();
#if UNITY_SERVER && UNITY_EDITOR
				Destroy(_captainCard);
#endif
				_chooseNextCaptainCoroutine = null;
				_isNextCaptainChoiceCompleted = true;
				yield break;
			}

			if (!AskClientToChoosePlayer(_captain,
									GetPlayersExcluding(captainChoices.ToArray()),
									Config.ChooseNextCaptainText,
									Config.CaptainChoiceDuration,
									false,
									OnChoosedNextCaptain))
			{
				StartCoroutine(EndChoosingNextCaptain(captainChoices[UnityEngine.Random.Range(0, captainChoices.Count)]));
				yield break;
			}

			foreach (var player in PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[player.Key].IsConnected || player.Key == _captain)
				{
					continue;
				}

				RPC_DisplayTitle(player.Key, Config.OldCaptainChoosingText);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(null, Config.OldCaptainChoosingText);
#endif
			float elapsedTime = .0f;

			while (_networkDataManager.PlayerInfos[_captain].IsConnected && elapsedTime < Config.CaptainChoiceDuration)
			{
				yield return 0;
				elapsedTime += Time.deltaTime;
			}

			StartCoroutine(EndChoosingNextCaptain(captainChoices[UnityEngine.Random.Range(0, captainChoices.Count)]));
		}

		private void OnChoosedNextCaptain(PlayerRef nextCaptain)
		{
			if (_chooseNextCaptainCoroutine == null)
			{
				return;
			}

			StartCoroutine(EndChoosingNextCaptain(nextCaptain));
		}

		private IEnumerator EndChoosingNextCaptain(PlayerRef nextCaptain)
		{
			StopCoroutine(_chooseNextCaptainCoroutine);
			_chooseNextCaptainCoroutine = null;

			StopChoosingPlayer(_captain);

			RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			HideUI();
#endif
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

			_captain = nextCaptain;
			yield return ShowCaptain();

			_isNextCaptainChoiceCompleted = true;
		}

		private IEnumerator ShowCaptain()
		{
			if (!_captainCard)
			{
				RPC_InstantiateCaptainCard(_captain);
#if UNITY_SERVER && UNITY_EDITOR
				_captainCard = Instantiate(Config.CaptainCardPrefab, _playerCards[_captain].transform.position + Config.CaptainCardOffset, Quaternion.identity);
#endif
			}
			else
			{
				RPC_MoveCaptainCard(_captain);
#if UNITY_SERVER && UNITY_EDITOR
				StartCoroutine(MoveCaptainCard(_playerCards[_captain].transform.position + Config.CaptainCardOffset));
#endif
			}

			StartCoroutine(HighlightPlayerToggle(_captain));
			yield return DisplayTitleForAllPlayers(Config.CaptainRevealText, Config.HighlightDuration);
		}

		private IEnumerator MoveCaptainCard(Vector3 newPosition)
		{
			Vector3 startingPosition = _captainCard.transform.position;
			float elapsedTime = .0f;

			while (elapsedTime < Config.CaptainCardMovementDuration)
			{
				yield return 0;

				elapsedTime += Time.deltaTime;
				float progress = elapsedTime / Config.CaptainCardMovementDuration;

				_captainCard.transform.position = Vector3.Lerp(startingPosition, newPosition, Config.CaptainCardMovementXY.Evaluate(progress))
												+ Vector3.up * Config.CaptainCardMovementYOffset.Evaluate(progress);
			}
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_InstantiateCaptainCard(PlayerRef captain)
		{
			_captainCard = Instantiate(Config.CaptainCardPrefab, _playerCards[captain].transform.position + Config.CaptainCardOffset, Quaternion.identity);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_MoveCaptainCard(PlayerRef captain)
		{
			StartCoroutine(MoveCaptainCard(_playerCards[captain].transform.position + Config.CaptainCardOffset));
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DestroyCaptainCard()
		{
			Destroy(_captainCard); 
		}
		#endregion
		#endregion

		#region Debate
		private IEnumerator StartDebate(string text, float duration)
		{
			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					continue;
				}

				RPC_OnDebateStarted(playerInfo.Key, text, duration, playerInfo.Value.IsAlive);

				if (!playerInfo.Value.IsAlive)
				{
					continue;
				}

				WaitForPlayer(playerInfo.Key);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(null, text, duration, false, Config.SkipText);
#endif
			float elapsedTime = .0f;

			while (_playersWaitingFor.Count > 0 && elapsedTime < duration)
			{
				yield return 0;
				elapsedTime += Time.deltaTime;
			}

			RPC_OnDebateEnded();
			_playersWaitingFor.Clear();

#if UNITY_SERVER && UNITY_EDITOR
			OnDebateEnded();
#endif
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

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
		public void RPC_OnDebateStarted([RpcTarget] PlayerRef player, string debateText, float countdownDuration, bool showConfirmButton)
		{
			DisplayTitle(null, debateText, countdownDuration, showConfirmButton, Config.SkipText);

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

		#region Vote
		private bool StartVoteForAllPlayers(Action<PlayerRef[]> votesCountedCallback,
											float maxDuration,
											bool allowedToNotVote,
											bool failToVotePenalty,
											Dictionary<PlayerRef, int> modifiers = null,
											bool canVoteForSelf = false,
											PlayerRef[] ImmunePlayers = null)
		{
			if (_votesCountedCallback != null)
			{
				return false;
			}

			_votesCountedCallback = votesCountedCallback;

			_voteManager.PrepareVote(maxDuration, allowedToNotVote, failToVotePenalty, modifiers);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					continue;
				}

				if (playerInfo.Value.IsAlive)
				{
					_voteManager.AddVoter(playerInfo.Key);

					if (!canVoteForSelf)
					{
						_voteManager.AddVoteImmunity(playerInfo.Key, playerInfo.Key);
					}
				}
				else
				{
					_voteManager.AddSpectator(playerInfo.Key);
				}

				if (playerInfo.Value.IsAlive && (ImmunePlayers == null || !ImmunePlayers.Contains(playerInfo.Key)))
				{
					continue;
				}

				_voteManager.AddVoteImmunity(playerInfo.Key);
			}

			_voteManager.VoteCompletedCallback += OnVoteEnded;
			_voteManager.StartVote();

			return true;
		}

		private void OnVoteEnded(Dictionary<PlayerRef, int> votes)
		{
			_voteManager.VoteCompletedCallback -= OnVoteEnded;

			int mostVoteCount = 0;
			List<PlayerRef> mostVotedPlayers = new List<PlayerRef>();

			foreach (KeyValuePair<PlayerRef, int> vote in votes)
			{
				if (vote.Value < mostVoteCount)
				{
					continue;
				}

				if (vote.Value > mostVoteCount)
				{
					mostVoteCount = vote.Value;
					mostVotedPlayers.Clear();
				}

				mostVotedPlayers.Add(vote.Key);
			}

			_votesCountedCallback?.Invoke(mostVotedPlayers.ToArray());
			_votesCountedCallback = null;
		}
		#endregion

		#region Highlight Players
		private void HighlightPlayer(PlayerRef player)
		{
			RPC_SetPlayerCardHighlightVisible(player, true);
#if UNITY_SERVER && UNITY_EDITOR
			SetPlayerCardHighlightVisible(player, true);
#endif
		}

		private IEnumerator HighlightPlayerToggle(PlayerRef player)
		{
			RPC_SetPlayerCardHighlightVisible(player, true);
#if UNITY_SERVER && UNITY_EDITOR
			SetPlayerCardHighlightVisible(player, true);
#endif
			yield return new WaitForSeconds(Config.HighlightDuration);

			RPC_SetPlayerCardHighlightVisible(player, false);
#if UNITY_SERVER && UNITY_EDITOR
			SetPlayerCardHighlightVisible(player, false);
#endif
		}

		private IEnumerator HighlightPlayers(PlayerRef[] players)
		{
			foreach(PlayerRef player in players)
			{
				RPC_SetPlayerCardHighlightVisible(player, true);
#if UNITY_SERVER && UNITY_EDITOR
				SetPlayerCardHighlightVisible(player, true);
#endif
			}

			yield return new WaitForSeconds(Config.HighlightDuration);

			foreach (PlayerRef player in players)
			{
				RPC_SetPlayerCardHighlightVisible(player, false);
#if UNITY_SERVER && UNITY_EDITOR
				SetPlayerCardHighlightVisible(player, false);
#endif
			}
		}

		public void SetPlayerCardHighlightVisible(PlayerRef player, bool isVisible)
		{
			_playerCards[player].SetHighlightVisible(isVisible);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SetPlayerCardHighlightVisible(PlayerRef player, bool isVisible)
		{
			SetPlayerCardHighlightVisible(player, isVisible);
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

			PlayerGameInfos[player] = new() { Role = roleData, Behaviors = PlayerGameInfos[player].Behaviors, IsAlive = PlayerGameInfos[player].IsAlive };

			if (_networkDataManager.PlayerInfos[player].IsConnected)
			{
				RPC_ChangePlayerCardRole(player, roleData.GameplayTag.CompactTagId);
			}
#if UNITY_SERVER && UNITY_EDITOR
			ChangePlayerCardRole(player, roleData);
#endif
		}

		public void TransferRole(PlayerRef from, PlayerRef to, bool destroyOldBehavior = true)
		{
			RemovePrimaryBehavior(to, destroyOldBehavior);

			if (PlayerGameInfos[from].Behaviors.Count <= 0)
			{
				foreach (int villageGroup in PlayerGameInfos[from].Role.PlayerGroupIndexes)
				{
					AddPlayerToPlayerGroup(villageGroup, to);
				}
			}
			else
			{
				foreach (RoleBehavior behavior in PlayerGameInfos[from].Behaviors)
				{
					if (behavior.IsPrimaryBehavior)
					{
						RemoveBehavior(from, behavior, false);
						AddBehavior(to, behavior);
						break;
					}
				}
			}

			PlayerGameInfos[to] = new() { Role = PlayerGameInfos[from].Role, Behaviors = PlayerGameInfos[to].Behaviors, IsAlive = PlayerGameInfos[to].IsAlive };
			PlayerGameInfos[from] = new() { Role = null, Behaviors = PlayerGameInfos[from].Behaviors, IsAlive = PlayerGameInfos[from].IsAlive };

			if (_networkDataManager.PlayerInfos[to].IsConnected)
			{
				RPC_ChangePlayerCardRole(to, PlayerGameInfos[to].Role.GameplayTag.CompactTagId);
			}
#if UNITY_SERVER && UNITY_EDITOR
			ChangePlayerCardRole(to, PlayerGameInfos[to].Role);
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

			PlayerGameInfos[player].Behaviors.Add(behavior);
			behavior.SetPlayer(player);
#if UNITY_SERVER && UNITY_EDITOR
			behavior.transform.position = _playerCards[player].transform.position;
#endif
		}

		private void RemovePrimaryBehavior(PlayerRef player, bool destroyOldBehavior = true)
		{
			if (PlayerGameInfos[player].Behaviors.Count <= 0)
			{
				foreach (int playerGroupIndex in PlayerGameInfos[player].Role.PlayerGroupIndexes)
				{
					RemovePlayerFromGroup(playerGroupIndex, player);
				}
			}
			else
			{
				foreach (RoleBehavior behavior in PlayerGameInfos[player].Behaviors)
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

			for (int i = PlayerGameInfos[player].Behaviors.Count - 1; i >= 0; i--)
			{
				if (PlayerGameInfos[player].Behaviors[i] != behavior)
				{
					continue;
				}

				PlayerGameInfos[player].Behaviors.RemoveAt(i);
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

			foreach (RoleBehavior behavior in PlayerGameInfos[player].Behaviors)
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

			for (int i = 0; i < _playerGroups.Count; i++)
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

		#region Role Reservation
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
			if (!_networkDataManager.PlayerInfos[ReservedRoleOwner.Player].IsConnected || !_reservedRolesByBehavior.ContainsKey(ReservedRoleOwner) || _chooseReservedRoleCallbacks.ContainsKey(ReservedRoleOwner.Player))
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

			if (!_networkDataManager.PlayerInfos[reservedRoleOwner].IsConnected)
			{
				return;
			}

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
			_UIManager.FadeIn(_UIManager.ChoiceScreen, Config.UITransitionNormalDuration);
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
		public bool AskClientToChoosePlayer(PlayerRef choosingPlayer, PlayerRef[] immunePlayers, string displayText, float maximumDuration, bool canChooseNobody, Action<PlayerRef> callback)
		{
			if (!_networkDataManager.PlayerInfos[choosingPlayer].IsConnected || _choosePlayerCallbacks.ContainsKey(choosingPlayer))
			{
				return false;
			}

			_choosePlayerCallbacks.Add(choosingPlayer, callback);
			RPC_ClientChoosePlayer(choosingPlayer, immunePlayers, displayText, maximumDuration, canChooseNobody);

			return true;
		}

		private void OnClientChooseNoCard()
		{
			OnClientChooseCard(null);
		}

		private void OnClientChooseCard(Card card)
		{
			StopChoosingPlayer();
			RPC_GivePlayerChoice(card ? card.Player : PlayerRef.None);
		}

		public void StopChoosingPlayer(PlayerRef player)
		{
			_choosePlayerCallbacks.Remove(player);

			if (!_networkDataManager.PlayerInfos[player].IsConnected)
			{
				return;
			}

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
			_UIManager.TitleScreen.SetConfirmButtonInteractable(false);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_ClientChoosePlayer([RpcTarget] PlayerRef player, PlayerRef[] immunePlayers, string displayText, float maximumDuration, bool canChooseNobody)
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

			DisplayTitle(null, displayText, maximumDuration, canChooseNobody, Config.SkipTurnText);// TODO: Give real image
			
			if (!canChooseNobody)
			{
				return;
			}

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
			if (!_networkDataManager.PlayerInfos[revealTo].IsConnected || _revealPlayerRoleCallbacks.ContainsKey(revealTo))
			{
				return false;
			}

			_revealPlayerRoleCallbacks.Add(revealTo, callback);
			RPC_RevealPlayerRole(revealTo, playerRevealed, PlayerGameInfos[playerRevealed].Role.GameplayTag.CompactTagId, waitBeforeReveal, returnFaceDown);

			return true;
		}

		private IEnumerator RevealPlayerRole(Card card, bool waitBeforeReveal, bool returnFaceDown)
		{
			yield return MoveCardToCamera(card.transform, !waitBeforeReveal, Config.MoveToCameraDuration);

			if (waitBeforeReveal)
			{
				yield return new WaitForSeconds(Config.RoleRevealWaitDuration);
				yield return FlipCard(card.transform, Config.RoleRevealFlipDuration);
			}

			yield return new WaitForSeconds(Config.RoleRevealHoldDuration);
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
			Vector3 targetPosition = mainCamera.transform.position + mainCamera.transform.forward * Config.RoleRevealDistanceToCamera;

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
				yield return 0;

				elapsedTime += Time.deltaTime;
				float progress = elapsedTime / duration;

				card.transform.position = Vector3.Lerp(startingPosition, targetPosition, progress);
				card.transform.rotation = Quaternion.Lerp(startingRotation, targetRotation, progress);
			}

			MovementCompleted?.Invoke();
		}

		private bool MoveCardToCamera(PlayerRef movedFor, PlayerRef cardPlayer, bool showRevealed, int gameplayDataID, Action movementCompleted)
		{
			if (!_networkDataManager.PlayerInfos[movedFor].IsConnected || _moveCardToCameraCallbacks.ContainsKey(movedFor))
			{
				return false;
			}

			_moveCardToCameraCallbacks.Add(movedFor, movementCompleted);
			RPC_MoveCardToCamera(movedFor, cardPlayer, showRevealed, gameplayDataID);

			return true;
		}

		public void FlipCard(PlayerRef cardPlayer, int gameplayDataID = -1, Action FlipCompleted = null)
		{
			if (gameplayDataID == -1)
			{
				_playerCards[cardPlayer].SetRole(null);
			}
			else
			{
				_playerCards[cardPlayer].SetRole(_gameplayDatabaseManager.GetGameplayData<RoleData>(gameplayDataID));
			}

			StartCoroutine(FlipCard(_playerCards[cardPlayer].transform, Config.RoleRevealFlipDuration, FlipCompleted));
		}

		private IEnumerator FlipCard(Transform card, float duration, Action FlipCompleted = null)
		{
			float elapsedTime = .0f;

			Quaternion startingRotation = card.rotation;
			Quaternion targetRotation = Quaternion.LookRotation(card.forward, -card.up);

			while (elapsedTime < duration)
			{
				yield return 0;

				elapsedTime += Time.deltaTime;
				float progress = elapsedTime / duration;

				card.transform.rotation = Quaternion.Lerp(startingRotation, targetRotation, progress);
			}

			FlipCompleted?.Invoke();
		}

		private bool FlipCard(PlayerRef flipFor, PlayerRef cardPlayer, Action flipCompleted, int gameplayDataID = -1)
		{
			if (!_networkDataManager.PlayerInfos[flipFor].IsConnected || _flipCardCallbacks.ContainsKey(flipFor))
			{
				return false;
			}

			_flipCardCallbacks.Add(flipFor, flipCompleted);
			RPC_FlipCard(flipFor, cardPlayer, gameplayDataID);

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
				yield return 0;

				elapsedTime += Time.deltaTime;
				float progress = elapsedTime / duration;

				card.transform.position = Vector3.Lerp(startingPosition, card.OriginalPosition, progress);
				card.transform.rotation = Quaternion.Lerp(startingRotation, targetRotation, progress);
			}

			if (returnFaceDown)
			{
				card.SetRole(null);
			}

			PutDownCompleted?.Invoke();
		}

		private bool PutCardBackDown(PlayerRef putFor, PlayerRef cardPlayer, bool returnFaceDown, Action putDownCompleted)
		{
			if (!_networkDataManager.PlayerInfos[putFor].IsConnected || _putCardBackDownCallbacks.ContainsKey(putFor))
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
		public void RPC_FlipCard([RpcTarget] PlayerRef player, PlayerRef cardPlayer, int gameplayDataID = -1)
		{
			FlipCard(cardPlayer, gameplayDataID, () => RPC_FlipCardFinished());
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_FlipCard(PlayerRef cardPlayer, int gameplayDataID = -1)
		{
			FlipCard(cardPlayer, gameplayDataID);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_FlipCardFinished(RpcInfo info = default)
		{
			if (!_flipCardCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_flipCardCallbacks[info.Source]();
			_flipCardCallbacks.Remove(info.Source);
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
		public bool PromptPlayer(PlayerRef promptedPlayer, string prompt, float duration, string confirmButtonText , Action<PlayerRef> callback, bool fastFade = true)
		{
			if (!_networkDataManager.PlayerInfos[promptedPlayer].IsConnected || _promptPlayerCallbacks.ContainsKey(promptedPlayer))
			{
				return false;
			}

			_promptPlayerCallbacks.Add(promptedPlayer, callback);
			RPC_PromptPlayer(promptedPlayer, prompt, duration, confirmButtonText, fastFade);

			return true;
		}

		private void OnPromptAccepted()
		{
			_UIManager.TitleScreen.Confirm -= OnPromptAccepted;
			RPC_AcceptPrompt();
		}

		public void StopPromptingPlayer(PlayerRef player, bool fastFade = true)
		{
			if (!_networkDataManager.PlayerInfos[player].IsConnected)
			{
				return;
			}

			_promptPlayerCallbacks.Remove(player);
			RPC_StopPromptingPlayer(player, fastFade);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_PromptPlayer([RpcTarget] PlayerRef player, string prompt, float duration, string confirmButtonText, bool fastFade)
		{
			_UIManager.TitleScreen.Initialize(null, prompt, duration, true, confirmButtonText);// TODO: Give real image
			_UIManager.TitleScreen.Confirm += OnPromptAccepted;
			_UIManager.FadeIn(_UIManager.TitleScreen, fastFade ? Config.UITransitionFastDuration : Config.UITransitionNormalDuration);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_AcceptPrompt(RpcInfo info = default)
		{
			if (!_promptPlayerCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_promptPlayerCallbacks[info.Source](info.Source);
			_promptPlayerCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_StopPromptingPlayer([RpcTarget] PlayerRef player, bool fastFade)
		{
			_UIManager.TitleScreen.Confirm -= OnPromptAccepted;
			_UIManager.FadeOut(_UIManager.TitleScreen, fastFade ? Config.UITransitionFastDuration : Config.UITransitionNormalDuration);
		}
		#endregion
		#endregion

		#region Player Disconnection
		public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
		{
			if (PlayerGameInfos[player].IsAlive)
			{
				AddMarkForDeath(player, Config.PlayerLeftMarkForDeath);
			}

			if (_playersWaitingFor.Contains(player) && _currentGameplayLoopStep == GameplayLoopStep.RoleCall)
			{
				PlayerGameInfos[player].Behaviors[0].SetTimedOut(true);
				PlayerGameInfos[player].Behaviors[0].OnRoleTimeOut();
			}

			StopWaintingForPlayer(player);

			_voteManager.RemoveVoter(player);

			_chooseReservedRoleCallbacks.Remove(player);
			_choosePlayerCallbacks.Remove(player);
			_revealPlayerRoleCallbacks.Remove(player);
			_moveCardToCameraCallbacks.Remove(player);
			_flipCardCallbacks.Remove(player);
			_putCardBackDownCallbacks.Remove(player);

			OnPostPlayerLeft?.Invoke(player);

			RPC_DisplayPlayerDisconnected(player);
#if UNITY_SERVER && UNITY_EDITOR
			_UIManager.DisconnectedScreen.DisplayDisconnectedPlayer(player);
#endif
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayPlayerDisconnected(PlayerRef player)
		{
			_UIManager.DisconnectedScreen.DisplayDisconnectedPlayer(player);
		}
		#endregion
		#endregion

		#region UI
		public void DisplayTitle(Sprite image, string title, float countdownDuration = -1, bool showConfirmButton = false, string confirmButtonText = "")
		{
			_UIManager.TitleScreen.Initialize(image, title, countdownDuration, showConfirmButton, confirmButtonText);
			_UIManager.FadeIn(_UIManager.TitleScreen, Config.UITransitionNormalDuration);
		}

		private IEnumerator DisplayTitleForAllPlayers(string title, float holdDuration)
		{
			if (holdDuration < Config.UITransitionNormalDuration)
			{
				Debug.LogError("holdDuration most not be smaller than Config.UITransitionNormalDuration");
			}

			RPC_DisplayTitle(title);
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(null, title);
#endif
			yield return new WaitForSeconds(holdDuration - Config.UITransitionNormalDuration);
			RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			HideUI();
#endif
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);
		}

		public void HideUI()
		{
			_UIManager.FadeOut(Config.UITransitionNormalDuration);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayTitle(string title)
		{
			DisplayTitle(null, title);
		}

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
		#endregion
		#endregion

		#region Visual
#if UNITY_SERVER && UNITY_EDITOR
		private void CreatePlayerCardsForServer()
		{
			float rotationIncrement = 360.0f / PlayerGameInfos.Count;
			Vector3 startingPosition = STARTING_DIRECTION * Config.CardsOffset.Evaluate(PlayerGameInfos.Count);

			int counter = -1;

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				counter++;

				Quaternion rotation = Quaternion.Euler(0, rotationIncrement * counter, 0);

				Card card = Instantiate(Config.CardPrefab, rotation * startingPosition, Quaternion.identity);
				card.transform.position += Vector3.up * card.Thickness / 2.0f;

				card.SetOriginalPosition(card.transform.position);
				card.SetPlayer(playerInfo.Key);
				card.SetRole(playerInfo.Value.Role);
				card.SetNickname(_networkDataManager.PlayerInfos[playerInfo.Key].Nickname);
				card.DetachGroundCanvas();
				card.Flip();

				_playerCards.Add(playerInfo.Key, card);

				if (playerInfo.Value.Behaviors.Count <= 0)
				{
					continue;
				}

				foreach (RoleBehavior behavior in playerInfo.Value.Behaviors)
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

					Card card = Instantiate(Config.CardPrefab, rowPosition + columnPosition, Quaternion.identity);
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
			NetworkDictionary<PlayerRef, Network.PlayerNetworkInfo> playerInfos = _networkDataManager.PlayerInfos;
			int playerCount = playerInfos.Count;

			int counter = -1;
			int rotationOffset = -1;

			float rotationIncrement = 360.0f / playerCount;
			Vector3 startingPosition = STARTING_DIRECTION * Config.CardsOffset.Evaluate(playerCount);

			// Offset the rotation to keep bottomPlayer at the bottom
			foreach (KeyValuePair<PlayerRef, Network.PlayerNetworkInfo> playerInfo in playerInfos)
			{
				if (playerInfo.Key == bottomPlayer)
				{
					break;
				}

				rotationOffset--;
			}

			// Create cards
			foreach (KeyValuePair<PlayerRef, Network.PlayerNetworkInfo> playerInfo in playerInfos)
			{
				counter++;
				rotationOffset++;

                Quaternion rotation = Quaternion.Euler(0, rotationIncrement * rotationOffset, 0);

                Card card = Instantiate(Config.CardPrefab, rotation * startingPosition, Quaternion.identity);
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

					Card card = Instantiate(Config.CardPrefab, rowPosition + columnPosition, Quaternion.identity);
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
			Camera.main.transform.position = Camera.main.transform.position.normalized * Config.CameraOffset.Evaluate(_networkDataManager.PlayerInfos.Count);
		}
		#endregion

		#region Unused Callbacks
		public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

		public void OnInput(NetworkRunner runner, NetworkInput input) { }

		public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

		public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

		public void OnConnectedToServer(NetworkRunner runner) { }

		public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

		public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

		public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

		public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

		public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

		public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

		public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

		public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

		public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

		public void OnSceneLoadDone(NetworkRunner runner) { }

		public void OnSceneLoadStart(NetworkRunner runner) { }
		#endregion
	}
}