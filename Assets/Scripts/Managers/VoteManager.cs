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
		private Dictionary<PlayerRef, Card> _playerCards;
		private GameConfig _config;

		private Step _step;

		private enum Step
		{
			NotVoting,
			Preparing,
			Voting,
		}

		private List<PlayerRef> _voters = new();
		private List<PlayerRef> _immune = new();
		private Dictionary<PlayerRef, List<PlayerRef>> _immuneFromPlayers = new();
		private Dictionary<PlayerRef, Vote> _votes = new();

		public class Vote
		{
			public PlayerRef VotedFor;
			public bool LockedIn;
		}

		private int _lockedInVoteCount;
		private float _voteMaxDuration;
		private bool _notVotingPenalty;

		private IEnumerator _voteCoroutine;

		private Card _selectedCard;

		private GameManager _gameManager;
		private UIManager _UIManager;

		public event Action<Dictionary<PlayerRef, Vote>> VoteCompletedCallback;

		public void SetPlayerCards(Dictionary<PlayerRef, Card> playerCards)
		{
			_playerCards = playerCards;
		}

		public void SetConfig(GameConfig config)
		{
			_config = config;

			_gameManager = GameManager.Instance;
			_UIManager = UIManager.Instance;
			_UIManager.VoteScreen.SetLockedInDelayDuration(_config.AllLockedInDelayToEndVote);
		}

		public bool PrepareVote(float voteMaxDuration, bool notVotingPenalty)
		{
			if (_step != Step.NotVoting)
			{
				return false;
			}

			_voters.Clear();
			_immune.Clear();
			_immuneFromPlayers.Clear();
			_votes.Clear();

			_lockedInVoteCount = 0;
			_voteMaxDuration = voteMaxDuration;
			_notVotingPenalty = notVotingPenalty;

			_step = Step.Preparing;

			return true;
		}

		public void AddVoter(PlayerRef voter)
		{
			if (_step != Step.Preparing || _voters.Contains(voter))
			{
				return;
			}

			_voters.Add(voter);
		}

		public void RemoveVoter(PlayerRef voter)
		{
			if (_step != Step.Preparing || !_voters.Contains(voter))
			{
				return;
			}

			_voters.Remove(voter);
		}

		public void AddVoteImmunity(PlayerRef player)
		{
			if (_step != Step.Preparing || _immune.Contains(player))
			{
				return;
			}

			_immune.Add(player);
		}

		public void AddVoteImmunity(PlayerRef immunePlayer, PlayerRef from)
		{
			if (_step != Step.Preparing || (_immuneFromPlayers.ContainsKey(from) && _immuneFromPlayers[from].Contains(immunePlayer)))
			{
				return;
			}

			if (_immuneFromPlayers.ContainsKey(from))
			{
				_immuneFromPlayers[from].Add(immunePlayer);
			}
			else
			{
				_immuneFromPlayers.Add(from, new() { immunePlayer });
			}
		}

		public void RemoveVoteImmunity(PlayerRef player)
		{
			if (_step != Step.Preparing || !_immune.Contains(player))
			{
				return;
			}

			_immune.Remove(player);
		}

		public void RemoveVoteImmunity(PlayerRef immunePlayer, PlayerRef from)
		{
			if (_step != Step.Preparing || !_immuneFromPlayers.ContainsKey(from) || !_immuneFromPlayers[from].Contains(immunePlayer))
			{
				return;
			}

			_immuneFromPlayers[from].Remove(immunePlayer);

			if (_immuneFromPlayers[from].Count <= 0)
			{
				_immuneFromPlayers.Remove(from);
			}
		}

		public void StartVote()
		{
			if (_step != Step.Preparing || _voteCoroutine != null)
			{
				return;
			}

			foreach (PlayerRef voter in _voters)
			{
				_votes.Add(voter, new());
#if UNITY_SERVER && UNITY_EDITOR
				_playerCards[voter].SetVotingStatusVisible(true);
				_playerCards[voter].UpdateVotingStatus(true);
#endif
			}

			if (!StartPlayerVotes())
			{
				VoteCompletedCallback?.Invoke(_votes);
				VoteCompletedCallback = null;

				_step = Step.NotVoting;

				return;
			}
#if UNITY_SERVER && UNITY_EDITOR
			_UIManager.FadeIn(_UIManager.VoteScreen, _config.UITransitionDuration);
			_UIManager.VoteScreen.Initialize(_voteMaxDuration);
			_UIManager.VoteScreen.HideLockinButton();
#endif
			_voteCoroutine = WaitForVoteEnd();
			StartCoroutine(_voteCoroutine);

			_step = Step.Voting;
		}

		private bool StartPlayerVotes()
		{
			bool voteStarted = false;

			foreach (PlayerRef voter in _voters)
			{
				List<PlayerRef> immunePlayers = new();
				immunePlayers.AddRange(_immune);

				if (_immuneFromPlayers.ContainsKey(voter))
				{
					foreach (PlayerRef immuneFromPlayer in _immuneFromPlayers[voter])
					{
						if (immunePlayers.Contains(immuneFromPlayer))
						{
							continue;
						}

						immunePlayers.Add(immuneFromPlayer);
					}
				}

				foreach (KeyValuePair<PlayerRef, GameManager.PlayerData> player in _gameManager.Players)
				{
					if (player.Value.IsAlive || immunePlayers.Contains(player.Key))
					{
						continue;
					}

					immunePlayers.Add(player.Key);
				}

				if (immunePlayers.Count >= _gameManager.Players.Count)
				{
					continue;
				}

				voteStarted = true;
				RPC_StartPlayerVote(voter, _voters.ToArray(), immunePlayers.ToArray(), _voteMaxDuration);
			}

			return voteStarted;
		}

		private IEnumerator WaitForVoteEnd()
		{
			float elapsedTime = .0f;
			float allLockedInElapsedTime = .0f;

			while (elapsedTime < _voteMaxDuration && (_lockedInVoteCount < _voters.Count || allLockedInElapsedTime < _config.AllLockedInDelayToEndVote))
			{
				if (_lockedInVoteCount < _voters.Count && allLockedInElapsedTime > .0f)
				{
					allLockedInElapsedTime = .0f;
				}
				else if (_lockedInVoteCount >= _voters.Count)
				{
					allLockedInElapsedTime += Time.deltaTime;
				}

				elapsedTime += Time.deltaTime;
				yield return 0;
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
			UpdateVisualFeedback();

			RPC_UpdateServerVote(_votes[Runner.LocalPlayer].VotedFor, isLocked);
		}

		private void UpdateVisualFeedback()
		{
			foreach (KeyValuePair<PlayerRef, Card> card in _playerCards)
			{
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

				if (vote.Value.VotedFor == PlayerRef.None)
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

			if (_notVotingPenalty)
			{
				foreach (KeyValuePair<PlayerRef, Vote> vote in _votes)
				{
					if (vote.Value.VotedFor == PlayerRef.None)
					{
						_votes[vote.Key] = new() { VotedFor = vote.Key, LockedIn = true };
					}
				}
			}
#if UNITY_SERVER && UNITY_EDITOR
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				playerCard.Value.SetVotingStatusVisible(false);
				playerCard.Value.ClearVotes();
			}

			_UIManager.VoteScreen.StopTimer();
			_UIManager.VoteScreen.SetLockedInDelayActive(false);
#endif
			TellPlayersVoteEnded();

			VoteCompletedCallback?.Invoke(_votes);

			_voteCoroutine = null;
			VoteCompletedCallback = null;

			_step = Step.NotVoting;
		}

		private void TellPlayersVoteEnded()
		{
			foreach (PlayerRef voter in _voters)
			{
				RPC_VoteEnded(voter);
			}
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
		private void RPC_StartPlayerVote([RpcTarget] PlayerRef player, PlayerRef[] voters, PlayerRef[] immunePlayers, float maxDuration)
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

			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				bool isImmune = Array.IndexOf(immunePlayers, playerCard.Key) >= 0;

				if (isImmune)
				{
					playerCard.Value.SetSelectionMode(true, false);
					continue;
				}

				playerCard.Value.SetSelectionMode(true, true);
				playerCard.Value.OnCardClick += OnCardSelectedChanged;
			}

			_UIManager.FadeIn(_UIManager.VoteScreen, _config.UITransitionDuration);
			_UIManager.VoteScreen.Initialize(maxDuration);
			_UIManager.VoteScreen.OnVoteLockChanged += OnVoteLockChanged;
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
			foreach (PlayerRef voter in _voters)
			{
				RPC_UpdateClientVote(voter, info.Source, votedFor, islocked);
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
		private void RPC_VoteEnded([RpcTarget] PlayerRef player)
		{
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				playerCard.Value.SetVotingStatusVisible(false);
				playerCard.Value.ResetSelectionMode();
				playerCard.Value.ClearVotes();

				playerCard.Value.OnCardClick -= OnCardSelectedChanged;
			}

			_UIManager.VoteScreen.StopTimer();
			_UIManager.VoteScreen.SetLockedInDelayActive(false);
			_UIManager.VoteScreen.OnVoteLockChanged -= OnVoteLockChanged;
			_UIManager.FadeOut(_UIManager.VoteScreen, _config.UITransitionDuration);
		}
		#endregion
	}
}