using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
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
		private bool _failToVotePenalty;
		private Dictionary<PlayerRef, int> _modifiers;
		private int _lockedInVoteCount;

		private Step _step;

		private enum Step
		{
			NotVoting,
			Preparing,
			Voting,
		}

		private IEnumerator _voteCoroutine;

		private Card _selectedCard;

		private PlayerRef[] _immunePlayers;

		private UIManager _UIManager;

		public event Action<Dictionary<PlayerRef, int>> VoteCompletedCallback;

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
			_UIManager.VoteScreen.SetLockedInDelayDuration(_config.AllLockedInDelayToEndVote);
		}

		public void SetPlayers(Dictionary<PlayerRef, PlayerGameInfo> players)
		{
			_players = players;
		}

		public bool PrepareVote(float maxDuration, bool allowedToNotVote, bool failToVotePenalty, Dictionary<PlayerRef, int> modifiers = null)
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
			_failToVotePenalty = failToVotePenalty;
			_modifiers = modifiers;
			_lockedInVoteCount = 0;

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

			bool allLockedIn = true;

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

			if (_failToVotePenalty)
			{
				foreach (PlayerRef voter in Voters)
				{
					if (!_votes[voter].VotedFor.IsNone)
					{
						continue;
					}

					_votes[voter] = new() { VotedFor = voter, LockedIn = true };
				}
			}

			foreach (PlayerRef voter in Voters)
			{
				RPC_VoteEnded(voter);
			}

			foreach (PlayerRef spectator in _spectators)
			{
				RPC_VoteEnded(spectator);
			}

			Dictionary<PlayerRef, int> totalVotes = new Dictionary<PlayerRef, int>();

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

			VoteCompletedCallback?.Invoke(totalVotes);

			_voteCoroutine = null;
			VoteCompletedCallback = null;
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
				playerCard.Value.OnCardClick += OnCardSelectedChanged;
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

				playerCard.Value.OnCardClick -= OnCardSelectedChanged;
			}

			_UIManager.VoteScreen.SetLockedInDelayActive(false);
			_UIManager.VoteScreen.VoteLockChanged -= OnVoteLockChanged;
			_UIManager.FadeOut(_UIManager.VoteScreen, _config.UITransitionNormalDuration);
		}
		#endregion
	}
}