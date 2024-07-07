using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf
{
	public class VoteManager : NetworkBehaviourSingleton<VoteManager>
	{
		private GameConfig _config;
		private Dictionary<PlayerRef, PlayerGameInfo> _players;
		private Dictionary<PlayerRef, Card> _playerCards;

		public List<PlayerRef> Voters { get; private set; }
		private List<PlayerRef> _immune = new();
		private Dictionary<PlayerRef, List<PlayerRef>> _immuneFromPlayers = new();
		private List<PlayerRef> _spectators = new();
		private Dictionary<PlayerRef, Vote> _votes = new();

		public class Vote
		{
			public PlayerRef VotedFor;
			public bool LockedIn;
		}

		private float _maxDuration;
		private bool _allowedToNotVote;
		private bool _failingToVoteGivesPenalty;
		private Dictionary<PlayerRef, int> _modifiers;
		private int _lockedInVoteCount;

		private ChoicePurpose _purpose;

		private Step _step;

		private enum Step
		{
			NotVoting,
			Preparing,
			Voting,
		}

		private Action<PlayerRef[]> _votesCountedCallback;

		private IEnumerator _voteCoroutine;

		private Card _selectedCard;

		private PlayerRef[] _immunePlayers;

		private UIManager _UIManager;
		private NetworkDataManager _networkDataManager;
		private GameHistoryManager _gameHistoryManager;

		public event Action<ChoicePurpose> VoteStarting;
		public event Action<Dictionary<PlayerRef, int>> VoteCompleted;

		protected override void Awake()
		{
			base.Awake();
			Voters = new List<PlayerRef>();
		}

		public void SetPlayerCards(Dictionary<PlayerRef, Card> playerCards)
		{
			_playerCards = playerCards;
		}

		public void SetConfig(GameConfig config)
		{
			_config = config;

			_UIManager = UIManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;

			_UIManager.VoteScreen.SetLockedInDelayDuration(_config.AllLockedInDelayToEndVote);
		}

		public void SetPlayers(Dictionary<PlayerRef, PlayerGameInfo> players)
		{
			_players = players;
		}

		public bool StartVoteForAllPlayers(Action<PlayerRef[]> votesCountedCallback,
											float maxDuration,
											bool allowedToNotVote,
											bool failingToVoteGivesPenalty,
											ChoicePurpose purpose,
											Dictionary<PlayerRef, int> modifiers = null,
											bool canVoteForSelf = false,
											PlayerRef[] ImmunePlayers = null)
		{
			if (_votesCountedCallback != null)
			{
				return false;
			}

			_votesCountedCallback = votesCountedCallback;

			PrepareVote(maxDuration, allowedToNotVote, failingToVoteGivesPenalty, purpose, modifiers);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _players)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					continue;
				}

				if (playerInfo.Value.IsAlive)
				{
					AddVoter(playerInfo.Key);

					if (!canVoteForSelf)
					{
						AddVoteImmunity(playerInfo.Key, playerInfo.Key);
					}
				}
				else
				{
					AddSpectator(playerInfo.Key);
				}

				if (playerInfo.Value.IsAlive && (ImmunePlayers == null || !ImmunePlayers.Contains(playerInfo.Key)))
				{
					continue;
				}

				AddVoteImmunity(playerInfo.Key);
			}

			VoteCompleted += OnAllPlayersVoteEnded;
			StartVote();

			return true;
		}

		public bool PrepareVote(float maxDuration, bool allowedToNotVote, bool failingToVoteGivesPenalty, ChoicePurpose purpose, Dictionary<PlayerRef, int> modifiers = null)
		{
			if (_step != Step.NotVoting)
			{
				return false;
			}

			Voters.Clear();
			_immune.Clear();
			_immuneFromPlayers.Clear();
			_spectators.Clear();
			_votes.Clear();

			_maxDuration = maxDuration;
			_allowedToNotVote = allowedToNotVote;
			_failingToVoteGivesPenalty = failingToVoteGivesPenalty;
			_modifiers = modifiers;
			_lockedInVoteCount = 0;

			_purpose = purpose;
			_step = Step.Preparing;

			return true;
		}

		public void AddVoter(PlayerRef voter)
		{
			if (_step != Step.Preparing || Voters.Contains(voter))
			{
				return;
			}

			Voters.Add(voter);
			_immuneFromPlayers.Add(voter, new());
		}

		public void RemoveVoter(PlayerRef voter)
		{
			if (_step == Step.NotVoting || !Voters.Contains(voter))
			{
				return;
			}

			Voters.Remove(voter);
			_immuneFromPlayers.Remove(voter);
			_votes.Remove(voter);

			if (_step != Step.Voting)
			{
				return;
			}
#if UNITY_SERVER && UNITY_EDITOR
			_playerCards[voter].SetVotingStatusVisible(false);
			UpdateVisualFeedback();
#endif
			foreach (PlayerRef otherVoter in Voters)
			{
				RPC_RemoveClientVoter(otherVoter, voter);
			}

			foreach (PlayerRef spectator in _spectators)
			{
				RPC_RemoveClientVoter(spectator, voter);
			}
		}

		public void AddVoteImmunity(PlayerRef player)
		{
			if (_step != Step.Preparing || _immune.Contains(player))
			{
				return;
			}

			_immune.Add(player);
		}

		public void RemoveVoteImmunity(PlayerRef player)
		{
			if (_step != Step.Preparing || !_immune.Contains(player))
			{
				return;
			}

			_immune.Remove(player);
		}

		public void AddVoteImmunity(PlayerRef immunePlayer, PlayerRef from)
		{
			if (_step != Step.Preparing || !_immuneFromPlayers.ContainsKey(from) || _immuneFromPlayers[from].Contains(immunePlayer))
			{
				return;
			}

			_immuneFromPlayers[from].Add(immunePlayer);
		}

		public void RemoveVoteImmunity(PlayerRef immunePlayer, PlayerRef from)
		{
			if (_step != Step.Preparing || !_immuneFromPlayers.ContainsKey(from) || !_immuneFromPlayers[from].Contains(immunePlayer))
			{
				return;
			}

			_immuneFromPlayers[from].Remove(immunePlayer);
		}

		public void AddSpectator(PlayerRef spectator)
		{
			if (_step != Step.Preparing || _spectators.Contains(spectator) || Voters.Contains(spectator))
			{
				return;
			}

			_spectators.Add(spectator);
		}

		public void RemoveSpectator(PlayerRef spectator)
		{
			if (_step != Step.Preparing || !_spectators.Contains(spectator))
			{
				return;
			}

			_spectators.Remove(spectator);
		}

		public void StartVote()
		{
			if (_step != Step.Preparing || _voteCoroutine != null)
			{
				return;
			}

			VoteStarting?.Invoke(_purpose);

			float voteDuration;

			if (FillImmuneFromPlayers())
			{
				voteDuration = _maxDuration;
			}
			else
			{
				voteDuration = _config.NoVoteDuration;
			}

			foreach (PlayerRef voter in Voters)
			{
				_votes.Add(voter, new());
				RPC_StartVoting(voter,
								Voters.ToArray(),
								_immuneFromPlayers[voter].ToArray(),
								_immuneFromPlayers[voter].Count >= _players.Count, voteDuration,
								_allowedToNotVote);
#if UNITY_SERVER && UNITY_EDITOR
				_playerCards[voter].SetVotingStatusVisible(true);
				_playerCards[voter].UpdateVotingStatus(true);
#endif
			}

			foreach (PlayerRef spectator in _spectators)
			{
				RPC_StartSpectating(spectator,
									Voters.ToArray(),
									voteDuration);
			}

			_voteCoroutine = WaitForVoteEnd(voteDuration);
			StartCoroutine(_voteCoroutine);
#if UNITY_SERVER && UNITY_EDITOR
			_UIManager.FadeIn(_UIManager.VoteScreen, _config.UITransitionNormalDuration);
			_UIManager.VoteScreen.Initialize(false, voteDuration, false);
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
					if (_immuneFromPlayers[voter].Contains(player))
					{
						continue;
					}

					_immuneFromPlayers[voter].Add(player);
				}

				foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _players)
				{
					if (playerInfo.Value.IsAlive || _immuneFromPlayers[voter].Contains(playerInfo.Key))
					{
						continue;
					}

					_immuneFromPlayers[voter].Add(playerInfo.Key);
				}

				if (_immuneFromPlayers[voter].Count < _players.Count)
				{
					canAtLeastOneVoterVote = true;
				}
			}

			return canAtLeastOneVoterVote;
		}

		private void SetCardsClickable(bool areClickable)
		{
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (!playerCard.Value)
				{
					continue;
				}

				if (Array.IndexOf(_immunePlayers, playerCard.Key) >= 0)
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
			float allLockedInElapsedTime = .0f;

			while (elapsedTime < duration && (_lockedInVoteCount < Voters.Count || allLockedInElapsedTime < _config.AllLockedInDelayToEndVote))
			{
				yield return 0;

				if (_lockedInVoteCount < Voters.Count && allLockedInElapsedTime > .0f)
				{
					allLockedInElapsedTime = .0f;
				}
				else if (_lockedInVoteCount >= Voters.Count)
				{
					allLockedInElapsedTime += Time.deltaTime;
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

			_votes[Runner.LocalPlayer].VotedFor = selectedPlayer;
			UpdateVisualFeedback();

			RPC_UpdateServerVote(selectedPlayer, _votes[Runner.LocalPlayer].LockedIn);
		}

		private void OnVoteLockChanged(bool isLocked)
		{
			_votes[Runner.LocalPlayer].LockedIn = isLocked;
			SetCardsClickable(!isLocked);

			UpdateVisualFeedback();

			RPC_UpdateServerVote(_votes[Runner.LocalPlayer].VotedFor, isLocked);
		}

		private void UpdateVisualFeedback()
		{
			foreach (KeyValuePair<PlayerRef, Card> card in _playerCards)
			{
				if (!card.Value)
				{
					continue;
				}

				card.Value.ClearVotes();
			}

			bool allLockedIn = _votes.Count > 0;

			foreach (KeyValuePair<PlayerRef, Vote> vote in _votes)
			{
				if (!vote.Value.LockedIn)
				{
					allLockedIn = false;
				}

				_playerCards[vote.Key].UpdateVotingStatus(!vote.Value.LockedIn);

				if (vote.Value.VotedFor.IsNone)
				{
					continue;
				}

				_playerCards[vote.Value.VotedFor].AddVote(vote.Value.LockedIn);
			}

			_UIManager.VoteScreen.SetLockedInDelayActive(allLockedIn);
		}

		private void EndVote()
		{
			if (_step != Step.Voting || _voteCoroutine == null)
			{
				return;
			}

			foreach (PlayerRef voter in Voters)
			{
				if (!_votes[voter].VotedFor.IsNone)
				{
					_gameHistoryManager.AddEntry(_config.VoteVotedForGameHistoryEntry,
												new GameHistoryManager.GameHistorySaveEntryVariable[] {
													new()
													{
														Name = "Voter",
														Data = _networkDataManager.PlayerInfos[voter].Nickname,
														Type = GameHistoryManager.GameHistorySaveEntryVariableType.Player
													},
													new()
													{
														Name = "Voted",
														Data = _networkDataManager.PlayerInfos[_votes[voter].VotedFor].Nickname,
														Type = GameHistoryManager.GameHistorySaveEntryVariableType.Player
													}
												});

					continue;
				}

				if (_failingToVoteGivesPenalty)
				{
					_votes[voter] = new() { VotedFor = voter, LockedIn = true };
				}

				_gameHistoryManager.AddEntry(_failingToVoteGivesPenalty ? _config.VoteDidNotVoteWithPenalityGameHistoryEntry : _config.VoteDidNotVoteGameHistoryEntry,
											new GameHistoryManager.GameHistorySaveEntryVariable[] {
													new()
													{
														Name = "Player",
														Data = _networkDataManager.PlayerInfos[voter].Nickname,
														Type = GameHistoryManager.GameHistorySaveEntryVariableType.Player
													}
											});
			}

			foreach (PlayerRef voter in Voters)
			{
				RPC_VoteEnded(voter);
			}

			foreach (PlayerRef spectator in _spectators)
			{
				RPC_VoteEnded(spectator);
			}

			Dictionary<PlayerRef, int> totalVotes = new();

			foreach (KeyValuePair<PlayerRef, Vote> vote in _votes)
			{
				if (vote.Value.VotedFor.IsNone)
				{
					continue;
				}

				int voteValue = (_modifiers != null && _modifiers.ContainsKey(vote.Key)) ? _modifiers[vote.Key] : 1;

				if (totalVotes.ContainsKey(vote.Value.VotedFor))
				{
					totalVotes[vote.Value.VotedFor] += voteValue;
					continue;
				}

				totalVotes.Add(vote.Value.VotedFor, voteValue);
			}

			VoteCompleted?.Invoke(totalVotes);

			_voteCoroutine = null;
			VoteCompleted = null;
#if UNITY_SERVER && UNITY_EDITOR
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (!playerCard.Value)
				{
					continue;
				}

				playerCard.Value.SetVotingStatusVisible(false);
				playerCard.Value.ClearVotes();
			}

			_UIManager.SetFade(_UIManager.TitleScreen, .0f);
			_UIManager.VoteScreen.SetLockedInDelayActive(false);
			_UIManager.FadeOut(_UIManager.VoteScreen, _config.UITransitionNormalDuration);
#endif
			_step = Step.NotVoting;
		}

		private void OnAllPlayersVoteEnded(Dictionary<PlayerRef, int> votes)
		{
			VoteCompleted -= OnAllPlayersVoteEnded;

			int mostVoteCount = 0;
			List<PlayerRef> mostVotedPlayers = new();

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

		public bool IsVoting()
		{
			return _step == Step.Voting;
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_StartVoting([RpcTarget] PlayerRef player, PlayerRef[] voters, PlayerRef[] immunePlayers, bool displayWarning, float maxDuration, bool allowedToNotVote)
		{
			if (_playerCards == null || _config == null)
			{
				Debug.LogError("_playerCards and _config must be set!");
				return;
			}

			_votes.Clear();

			foreach (PlayerRef voter in voters)
			{
				_votes.Add(voter, new());

				_playerCards[voter].SetVotingStatusVisible(true);
				_playerCards[voter].UpdateVotingStatus(true);
			}

			_selectedCard = null;
			_immunePlayers = immunePlayers;

			SetCardsClickable(true);

			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				playerCard.Value.LeftClicked += OnCardSelectedChanged;
			}

			_UIManager.FadeIn(_UIManager.VoteScreen, _config.UITransitionNormalDuration);
			_UIManager.VoteScreen.Initialize(displayWarning, maxDuration, true, allowedToNotVote ? null : () => { return _selectedCard != null; });
			_UIManager.VoteScreen.VoteLockChanged += OnVoteLockChanged;
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_StartSpectating([RpcTarget] PlayerRef spectator, PlayerRef[] voters, float maxDuration)
		{
			if (_playerCards == null || _config == null)
			{
				Debug.LogError("_playerCards and _config must be set!");
				return;
			}

			_votes.Clear();

			foreach (PlayerRef voter in voters)
			{
				_votes.Add(voter, new());

				_playerCards[voter].SetVotingStatusVisible(true);
				_playerCards[voter].UpdateVotingStatus(true);
			}

			_UIManager.FadeIn(_UIManager.VoteScreen, _config.UITransitionNormalDuration);
			_UIManager.VoteScreen.Initialize(false, maxDuration, false);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_UpdateServerVote(PlayerRef votedFor, bool islocked, RpcInfo info = default)
		{
			if (_step != Step.Voting)
			{
				return;
			}

			if (_votes[info.Source].LockedIn != islocked)
			{
				if (islocked)
				{
					_lockedInVoteCount++;
				}
				else
				{
					_lockedInVoteCount--;
				}
			}

			_votes[info.Source].VotedFor = votedFor;
			_votes[info.Source].LockedIn = islocked;
#if UNITY_SERVER && UNITY_EDITOR
			UpdateVisualFeedback();
#endif
			foreach (PlayerRef voter in Voters)
			{
				RPC_UpdateClientVote(voter, info.Source, votedFor, islocked);
			}

			foreach (PlayerRef spectator in _spectators)
			{
				RPC_UpdateClientVote(spectator, info.Source, votedFor, islocked);
			}
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_UpdateClientVote([RpcTarget] PlayerRef player, PlayerRef voter, PlayerRef votedFor, bool islocked)
		{
			_votes[voter].VotedFor = votedFor;
			_votes[voter].LockedIn = islocked;
			UpdateVisualFeedback();
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_RemoveClientVoter([RpcTarget] PlayerRef player, PlayerRef voter)
		{
			_votes.Remove(voter);
			_playerCards[voter].SetVotingStatusVisible(false);
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

				playerCard.Value.SetVotingStatusVisible(false);
				playerCard.Value.ResetSelectionMode();
				playerCard.Value.ClearVotes();

				playerCard.Value.LeftClicked -= OnCardSelectedChanged;
			}

			_UIManager.VoteScreen.SetLockedInDelayActive(false);
			_UIManager.VoteScreen.VoteLockChanged -= OnVoteLockChanged;
			_UIManager.FadeOut(_UIManager.VoteScreen, _config.UITransitionNormalDuration);
		}
		#endregion
	}
}