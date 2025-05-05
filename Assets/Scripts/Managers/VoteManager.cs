using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Gameplay;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Managers
{
	public class VoteManager : NetworkBehaviourSingleton<VoteManager>
	{
		public HashSet<PlayerRef> Voters { get; private set; }

		public HashSet<PlayerRef> Spectators { get; private set; }

		public event Action<ChoicePurpose> VoteStarting;
		public event Action<Dictionary<PlayerRef, int>> VoteCompleted;

		private GameConfig _gameConfig;
		private Dictionary<PlayerRef, Card> _playerCards;
		private readonly HashSet<PlayerRef> _immune = new();
		private readonly Dictionary<PlayerRef, HashSet<PlayerRef>> _immuneFromPlayers = new();
		private readonly Dictionary<PlayerRef, PlayerRef> _votes = new();

		private int _voterTitleID;
		private int _spectatorTitleID;
		private float _maxDuration;
		private bool _failingToVoteGivesPenalty;
		private Dictionary<PlayerRef, int> _modifiers;
		private int _voteCount;
		private bool _resetAllVotedElapsedTime;
		private Step _step;
		private ChoicePurpose _purpose;

		private Action<PlayerRef[]> _votesCountedCallback;
		private IEnumerator _voteCoroutine;
		private Card _selectedCard;
		private PlayerRef[] _playersImmuneFromLocalPlayer;

		[Serializable]
		private struct VoteModifier : INetworkStruct
		{
			public PlayerRef Voter;
			public int VoteValue;
		}

		private enum Step
		{
			NotVoting,
			Preparing,
			Voting,
		}

		private GameManager _gameManager;
		private UIManager _UIManager;
		private NetworkDataManager _networkDataManager;
		private GameplayDataManager _gameplayDataManager;
		private GameHistoryManager _gameHistoryManager;

		protected override void Awake()
		{
			base.Awake();
			Voters = new();
			Spectators = new();
		}

		public void SetPlayerCards(Dictionary<PlayerRef, Card> playerCards)
		{
			_playerCards = playerCards;
		}

		public void Initialize(GameConfig config)
		{
			_gameConfig = config;

			_gameManager = GameManager.Instance;
			_UIManager = UIManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
			_gameplayDataManager = GameplayDataManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;

			_UIManager.VoteScreen.SetConfirmVoteDelayDuration(_gameConfig.AllVotedDelayToEndVote * _gameManager.GameSpeedModifier);

			_networkDataManager.PlayerDisconnected += OnPlayerDisconnected;
		}

		public bool StartVoteForAllPlayers(Action<PlayerRef[]> votesCountedCallback,
											int voterTitleID,
											int spectatorTitleID,
											float maxDuration,
											bool failingToVoteGivesPenalty,
											ChoicePurpose purpose,
											PlayerRef[] spectatingPlayers,
											Dictionary<PlayerRef, int> modifiers = null,
											bool canVoteForSelf = false,
											PlayerRef[] immunePlayers = null)
		{
			if (_votesCountedCallback != null)
			{
				return false;
			}

			_votesCountedCallback = votesCountedCallback;

			PrepareVote(voterTitleID, spectatorTitleID, maxDuration, failingToVoteGivesPenalty, purpose, modifiers);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (spectatingPlayers.Contains(playerInfo.Key))
				{
					AddSpectator(playerInfo.Key);
				}
				else
				{
					AddVoter(playerInfo.Key);

					if (!canVoteForSelf)
					{
						AddVoteImmunity(playerInfo.Key, playerInfo.Key);
					}
				}

				if (playerInfo.Value.IsAlive && (immunePlayers == null || !immunePlayers.Contains(playerInfo.Key)))
				{
					continue;
				}

				AddVoteImmunity(playerInfo.Key);
			}

			VoteCompleted += OnAllPlayersVoteEnded;
			StartVote();

			return true;
		}

		public bool PrepareVote(int voterTitleID, int spectatorTitleID, float maxDuration, bool failingToVoteGivesPenalty, ChoicePurpose purpose, Dictionary<PlayerRef, int> modifiers = null)
		{
			if (_step != Step.NotVoting)
			{
				return false;
			}

			_step = Step.Preparing;

			Voters.Clear();
			_immune.Clear();
			_immuneFromPlayers.Clear();
			Spectators.Clear();
			_votes.Clear();

			_voterTitleID = voterTitleID;
			_spectatorTitleID = spectatorTitleID > -1 ? spectatorTitleID : voterTitleID;
			_maxDuration = maxDuration;
			_failingToVoteGivesPenalty = failingToVoteGivesPenalty;
			_modifiers = modifiers;
			_voteCount = 0;

			_purpose = purpose;

			return true;
		}

		public void AddVoter(PlayerRef voter)
		{
			if (_step != Step.Preparing || Voters.Contains(voter))
			{
				return;
			}

			Voters.Add(voter);

			if (!_immuneFromPlayers.ContainsKey(voter))
			{
				_immuneFromPlayers.Add(voter, new());
			}
		}

		public void ClearVoters()
		{
			if (_step == Step.Preparing)
			{
				Voters.Clear();
			}
		}

		public void AddVoteImmunity(PlayerRef player)
		{
			if (_step == Step.Preparing)
			{
				_immune.Add(player);
			}
		}

		public void AddVoteImmunity(PlayerRef immunePlayer, PlayerRef from)
		{
			if (_step == Step.Preparing && _immuneFromPlayers.ContainsKey(from))
			{
				_immuneFromPlayers[from].Add(immunePlayer);
			}
		}

		public void AddSpectator(PlayerRef spectator)
		{
			if (_step == Step.Preparing && !Voters.Contains(spectator))
			{
				Spectators.Add(spectator);
			}
		}

		public void ClearSpectators()
		{
			if (_step == Step.Preparing)
			{
				Spectators.Clear();
			}
		}

		public void StartVote()
		{
			if (_step != Step.Preparing || _voteCoroutine != null)
			{
				return;
			}

			VoteStarting?.Invoke(_purpose);

			VoteModifier[] voteModifiers;

			if (_modifiers != null)
			{
				voteModifiers = new VoteModifier[_modifiers.Count];

				int index = 0;

				foreach (KeyValuePair<PlayerRef, int> modifier in _modifiers)
				{
					voteModifiers[index].Voter = modifier.Key;
					voteModifiers[index].VoteValue = modifier.Value;

					index++;
				}
			}
			else
			{
				voteModifiers = new VoteModifier[0];
			}

			float voteDuration;

			if (FillImmuneFromPlayers())
			{
				voteDuration = _maxDuration;
			}
			else
			{
				voteDuration = _gameConfig.NoVoteDuration * _gameManager.GameSpeedModifier;
			}

			foreach (PlayerRef voter in Voters)
			{
				_votes.Add(voter, new());
				
				if (!_networkDataManager.PlayerInfos[voter].IsConnected)
				{
					continue;
				}

				RPC_StartVoting(voter,
								Voters.ToArray(),
								voteModifiers,
								_immune.ToArray(),
								_immuneFromPlayers[voter].ToArray(),
								_voterTitleID,
								_immuneFromPlayers[voter].Count >= _gameManager.PlayerGameInfos.Count,
								voteDuration);
			}

			foreach (PlayerRef spectator in Spectators)
			{
				if (!_networkDataManager.PlayerInfos[spectator].IsConnected)
				{
					continue;
				}

				RPC_StartSpectating(spectator,
									Voters.ToArray(),
									voteModifiers,
									_immune.ToArray(),
									_spectatorTitleID,
									voteDuration);
			}

			_voteCoroutine = WaitForVoteEnd(voteDuration);
			StartCoroutine(_voteCoroutine);
#if UNITY_SERVER && UNITY_EDITOR
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (!playerCard.Value)
				{
					continue;
				}

				if (!_immune.Contains(playerCard.Key))
				{
					playerCard.Value.DisplayVoteCount(true);
					playerCard.Value.ResetVoteCount();
				}
			}

			_UIManager.FadeIn(_UIManager.VoteScreen, _gameConfig.UITransitionNormalDuration);

			if (!_gameplayDataManager.TryGetGameplayData(_spectatorTitleID, out TitleScreenData titleScreenData))
			{
				Debug.LogError($"Could not find the title {_spectatorTitleID}");
			}

			_UIManager.VoteScreen.Initialize(titleScreenData.Text, false, voteDuration);
#endif
			_step = Step.Voting;
		}

		private bool FillImmuneFromPlayers()
		{
			bool canAtLeastOneVoterVote = false;

			foreach (PlayerRef voter in Voters)
			{
				foreach (PlayerRef player in _immune)
				{
					_immuneFromPlayers[voter].Add(player);
				}

				if (_immuneFromPlayers[voter].Count < _gameManager.PlayerGameInfos.Count)
				{
					canAtLeastOneVoterVote = true;
				}
			}

			return canAtLeastOneVoterVote;
		}

		private void SetModifiers(VoteModifier[] modifiers)
		{
			if (_modifiers == null)
			{
				_modifiers = new();
			}
			else
			{
				_modifiers.Clear();
			}

			foreach (VoteModifier modifier in modifiers)
			{
				_modifiers.Add(modifier.Voter, modifier.VoteValue);
			}
		}

		private void SetCardsClickable(bool areClickable)
		{
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (!playerCard.Value)
				{
					continue;
				}

				if (Array.IndexOf(_playersImmuneFromLocalPlayer, playerCard.Key) >= 0)
				{
					playerCard.Value.SetSelectionMode(true, false);
					continue;
				}

				playerCard.Value.SetSelectionMode(true, areClickable);
			}
		}

		private IEnumerator WaitForVoteEnd(float duration)
		{
			float elapsedTime = .0f;
			float allVotedElapsedTime = .0f;

			while (elapsedTime < duration && (_voteCount < Voters.Count || allVotedElapsedTime < _gameConfig.AllVotedDelayToEndVote * _gameManager.GameSpeedModifier))
			{
				yield return 0;

				if (_resetAllVotedElapsedTime)
				{
					allVotedElapsedTime = .0f;
					_resetAllVotedElapsedTime = false;
				}

				if (_voteCount >= Voters.Count)
				{
					allVotedElapsedTime += Time.deltaTime;
				}

				elapsedTime += Time.deltaTime;
			}

			EndVote();
		}

		private void OnCardSelectedChanged(Card card)
		{
			if (_selectedCard)
			{
				if (_selectedCard == card)
				{
					_selectedCard = null;
				}
				else
				{
					_selectedCard.SetSelected(false);
					_selectedCard = card;
				}
			}
			else
			{
				_selectedCard = card;
			}

			PlayerRef selectedPlayer = _selectedCard ? _selectedCard.Player : PlayerRef.None;

			_votes[Runner.LocalPlayer] = selectedPlayer;
			RPC_UpdateServerVote(selectedPlayer);
		}

		private void UpdateAllClientsVisualFeedback(PlayerRef inVoter, PlayerRef votedFor)
		{
#if UNITY_SERVER && UNITY_EDITOR
			UpdateVisualFeedback();
#endif
			foreach (PlayerRef voter in Voters)
			{
				if (_networkDataManager.PlayerInfos[voter].IsConnected)
				{
					RPC_UpdateClientVote(voter, inVoter, votedFor, _voteCount);
				}
			}

			foreach (PlayerRef spectator in Spectators)
			{
				if (_networkDataManager.PlayerInfos[spectator].IsConnected)
				{
					RPC_UpdateClientVote(spectator, inVoter, votedFor, _voteCount);
				}
			}
		}

		private void UpdateVisualFeedback()
		{
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (playerCard.Value)
				{
					playerCard.Value.ResetVoteCount();
				}
			}

			// Key: the voter | Value: voted for who
			foreach (KeyValuePair<PlayerRef, PlayerRef> vote in _votes)
			{
				if (vote.Value.IsNone)
				{
					_playerCards[vote.Key].DisplayVote(false);
				}
				else
				{
					if (_modifiers != null && _modifiers.TryGetValue(vote.Key, out int modifier))
					{
						_playerCards[vote.Value].IncrementVoteCount(modifier);
					}
					else
					{
						_playerCards[vote.Value].IncrementVoteCount(1);
					}

					_playerCards[vote.Key].DisplayVote(true, _playerCards[vote.Value].transform.position, vote.Key == vote.Value);
				}
			}

			_UIManager.VoteScreen.SetConfirmVoteDelayActive(_voteCount >= _votes.Count);
		}

		private void EndVote()
		{
			if (_step != Step.Voting || _voteCoroutine == null)
			{
				return;
			}

			foreach (PlayerRef voter in Voters)
			{
				if (!_votes[voter].IsNone)
				{
					_gameHistoryManager.AddEntry(_gameConfig.VoteVotedForGameHistoryEntry.ID,
												new GameHistorySaveEntryVariable[] {
													new()
													{
														Name = "Voter",
														Data = _networkDataManager.PlayerInfos[voter].Nickname,
														Type = GameHistorySaveEntryVariableType.Player
													},
													new()
													{
														Name = "Voted",
														Data = _networkDataManager.PlayerInfos[_votes[voter]].Nickname,
														Type = GameHistorySaveEntryVariableType.Player
													}
												});

					continue;
				}

				if (_failingToVoteGivesPenalty)
				{
					_votes[voter] = voter;
				}

				_gameHistoryManager.AddEntry(_failingToVoteGivesPenalty ? _gameConfig.VoteDidNotVoteWithPenalityGameHistoryEntry.ID : _gameConfig.VoteDidNotVoteGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
													new()
													{
														Name = "Player",
														Data = _networkDataManager.PlayerInfos[voter].Nickname,
														Type = GameHistorySaveEntryVariableType.Player
													}
											});
			}

			foreach (PlayerRef voter in Voters)
			{
				if (_networkDataManager.PlayerInfos[voter].IsConnected)
				{
					RPC_VoteEnded(voter);
				}
			}

			foreach (PlayerRef spectator in Spectators)
			{
				if (_networkDataManager.PlayerInfos[spectator].IsConnected)
				{
					RPC_VoteEnded(spectator);
				}
			}

			Dictionary<PlayerRef, int> totalVotes = new();

			foreach (KeyValuePair<PlayerRef, PlayerRef> vote in _votes)
			{
				if (vote.Value.IsNone)
				{
					continue;
				}

				int voteValue = (_modifiers != null && _modifiers.TryGetValue(vote.Key, out int modifier)) ? modifier : 1;

				if (totalVotes.ContainsKey(vote.Value))
				{
					totalVotes[vote.Value] += voteValue;
					continue;
				}

				totalVotes.Add(vote.Value, voteValue);
			}
#if UNITY_SERVER && UNITY_EDITOR
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (!playerCard.Value)
				{
					continue;
				}
				
				playerCard.Value.DisplayVoteCount(false);
				playerCard.Value.DisplayVote(false);
			}

			_UIManager.SetFade(_UIManager.TitleScreen, .0f);
			_UIManager.VoteScreen.SetConfirmVoteDelayActive(false);
			_UIManager.FadeOut(_UIManager.VoteScreen, _gameConfig.UITransitionNormalDuration);
#endif
			_voteCoroutine = null;

			_step = Step.NotVoting;

			VoteCompleted?.Invoke(totalVotes);
			VoteCompleted = null;
		}

		private void OnAllPlayersVoteEnded(Dictionary<PlayerRef, int> votes)
		{
			VoteCompleted -= OnAllPlayersVoteEnded;

			int mostVoteCount = 0;
			HashSet<PlayerRef> mostVotedPlayers = new();

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

		public bool IsPreparingToVote()
		{
			return _step == Step.Preparing;
		}

		private void OnPlayerDisconnected(PlayerRef player)
		{
			if (_step != Step.Voting || _voteCoroutine == null || !_votes.TryGetValue(player, out PlayerRef vote) || !vote.IsNone)
			{
				return;
			}

			_voteCount++;
			_resetAllVotedElapsedTime = true;

			UpdateAllClientsVisualFeedback(player, PlayerRef.None);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_StartVoting([RpcTarget] PlayerRef player, PlayerRef[] voters, VoteModifier[] voteModifiers, PlayerRef[] immunePlayers, PlayerRef[] playersImmuneFromLocalPlayer, int titleID, bool displayWarning, float maxDuration)
		{
			if (_playerCards == null || _gameConfig == null)
			{
				Debug.LogError($"{nameof(_playerCards)} and {nameof(_gameConfig)} must be set!");
				return;
			}

			_votes.Clear();
			_voteCount = 0;

			foreach (PlayerRef voter in voters)
			{
				_votes.Add(voter, new());
			}

			SetModifiers(voteModifiers);

			_selectedCard = null;
			_playersImmuneFromLocalPlayer = playersImmuneFromLocalPlayer;

			SetCardsClickable(true);

			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (!playerCard.Value)
				{
					continue;
				}

				if (!immunePlayers.Contains(playerCard.Key))
				{
					playerCard.Value.DisplayVoteCount(true);
					playerCard.Value.ResetVoteCount();
				}

				playerCard.Value.LeftClicked += OnCardSelectedChanged;
			}

			_UIManager.FadeIn(_UIManager.VoteScreen, _gameConfig.UITransitionNormalDuration);
			
			if (!_gameplayDataManager.TryGetGameplayData(titleID, out TitleScreenData titleScreenData))
			{
				Debug.LogError($"Could not find the title {titleID}");
			}

			_UIManager.VoteScreen.Initialize(titleScreenData.Text, displayWarning, maxDuration);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_StartSpectating([RpcTarget] PlayerRef spectator, PlayerRef[] voters, VoteModifier[] voteModifiers, PlayerRef[] immunePlayers, int titleID, float maxDuration)
		{
			if (_playerCards == null || _gameConfig == null)
			{
				Debug.LogError($"{nameof(_playerCards)} and {nameof(_gameConfig)} must be set!");
				return;
			}

			_votes.Clear();

			foreach (PlayerRef voter in voters)
			{
				_votes.Add(voter, new());
			}

			SetModifiers(voteModifiers);

			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (!playerCard.Value)
				{
					continue;
				}

				if (!immunePlayers.Contains(playerCard.Key))
				{
					playerCard.Value.DisplayVoteCount(true);
					playerCard.Value.ResetVoteCount();
				}
			}

			_UIManager.FadeIn(_UIManager.VoteScreen, _gameConfig.UITransitionNormalDuration);

			if (!_gameplayDataManager.TryGetGameplayData(titleID, out TitleScreenData titleScreenData))
			{
				Debug.LogError($"Could not find the title {titleID}");
			}

			_UIManager.VoteScreen.Initialize(titleScreenData.Text, false, maxDuration);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_UpdateServerVote(PlayerRef votedFor, RpcInfo info = default)
		{
			if (_step != Step.Voting || _immune.Contains(votedFor) || _immuneFromPlayers[info.Source].Contains(votedFor) || _votes[info.Source] == votedFor)
			{
				return;
			}

			if (_votes[info.Source].IsNone && !votedFor.IsNone)
			{
				_voteCount++;
			}
			else if (!_votes[info.Source].IsNone && votedFor.IsNone)
			{
				_voteCount--;
			}

			_resetAllVotedElapsedTime = true;
			_votes[info.Source] = votedFor;

			UpdateAllClientsVisualFeedback(info.Source, votedFor);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_UpdateClientVote([RpcTarget] PlayerRef player, PlayerRef voter, PlayerRef votedFor, int totalVoteCount)
		{
			_votes[voter] = votedFor;
			_voteCount = totalVoteCount;

			UpdateVisualFeedback();
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_VoteEnded([RpcTarget] PlayerRef player)
		{
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (!playerCard.Value)
				{
					continue;
				}

				playerCard.Value.ResetSelectionMode();
				playerCard.Value.DisplayVoteCount(false);
				playerCard.Value.DisplayVote(false);

				playerCard.Value.LeftClicked -= OnCardSelectedChanged;
			}

			_UIManager.VoteScreen.SetConfirmVoteDelayActive(false);
			_UIManager.FadeOut(_UIManager.VoteScreen, _gameConfig.UITransitionNormalDuration);
		}
		#endregion
	}
}