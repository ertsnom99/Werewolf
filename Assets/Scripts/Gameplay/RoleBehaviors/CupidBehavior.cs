using Assets.Scripts.Data.Tags;
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf
{
	public class CupidBehavior : RoleBehavior
	{
		[Header("Choose Couple")]
		[SerializeField]
		private GameplayTag _chooseCoupleImage;

		[SerializeField]
		private float _chooseCoupleMaximumDuration = 10.0f;

		[SerializeField]
		private float _choseCoupleHighlightHoldDuration = 3.0f;

		[Header("Show Couple")]
		[SerializeField]
		private float _showCoupleHighlightHoldDuration = 3.0f;

		[SerializeField]
		private GameplayTag _inCoupleImage;

		[SerializeField]
		private GameplayTag _coupleRecognizingEachOtherImage;

		[Header("Couple Death")]
		[SerializeField]
		private GameplayTag _markForDeathAddedByCoupleDeath;

		[SerializeField]
		private GameplayTag _coupleDeathImage;

		[SerializeField]
		private float _coupleDeathHighlightHoldDuration = 3.0f;

		private PlayerRef[] _couple = new PlayerRef[2];
		private IEnumerator _endChooseCoupleAfterTimeCoroutine;
		private IEnumerator _setSelectedCoupleCoroutine;
		private IEnumerator _highlightCoupleCoroutine;
		private IEnumerator _waitToRemoveDeadCoupleHighlightCoroutine;
		private bool _showedCouple;

		private NetworkDataManager _networkDataManager;
		private GameManager _gameManager;
		private VoteManager _voteManager;

		public override void Init()
		{
			_networkDataManager = NetworkDataManager.Instance;
			_gameManager = GameManager.Instance;
			_voteManager = VoteManager.Instance;

			_gameManager.PreClientChoosesPlayers += OnPreClientChoosesPlayers;
			_gameManager.PlayerDeathRevealEnded += OnPlayerDeathRevealEnded;
			_gameManager.PostPlayerDisconnected += OnPostPlayerLeft;
			_voteManager.VoteStarting += OnVoteStarting;

			if (PlayerGroupIndexes.Count < 2)
			{
				Debug.LogError("Cupid must have two player groups: the first one for cupid and the second one for the couple");
			}

			if (NightPriorities.Count < 2)
			{
				Debug.LogError("Cupid must have two night priorities: the first one to select the couple and the second one to let the couple know each other");
			}
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		public override int[] GetCurrentPlayerGroups()
		{
			return new int[1] { PlayerGroupIndexes[0] };
		}

		public override bool OnRoleCall(int nightCount, int priorityIndex)
		{
			if (!IsCoupleSelected() && priorityIndex == NightPriorities[0].index)
			{
				return ChooseCouple();
			}
			else if (!_showedCouple && priorityIndex == NightPriorities[1].index)
			{
				return ShowCouple();
			}

			return false;
		}

		private bool IsCoupleSelected()
		{
			return !_couple[0].IsNone && !_couple[1].IsNone && _couple[0] != _couple[1];
		}

		#region Choose Couple
		private bool ChooseCouple()
		{
			List<PlayerRef> immunePlayers = _gameManager.GetPlayersDead();

			if (!_gameManager.AskClientToChoosePlayers(Player,
													immunePlayers,
													_chooseCoupleImage.CompactTagId,
													_chooseCoupleMaximumDuration,
													true,
													2,
													ChoicePurpose.Other,
													OnCoupleSelected))
			{
				ChooseRandomCouple();
				AddCouplePlayerGroup();

				StartCoroutine(WaitToStopWaitingForPlayer());

				return true;
			}

			_endChooseCoupleAfterTimeCoroutine = EndChooseCoupleAfterTime();
			StartCoroutine(_endChooseCoupleAfterTimeCoroutine);

			return true;
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnCoupleSelected(PlayerRef[] players)
		{
			_setSelectedCoupleCoroutine = SetSelectedCouple(players);
			StartCoroutine(_setSelectedCoupleCoroutine);
		}

		private IEnumerator SetSelectedCouple(PlayerRef[] players)
		{
			if (_setSelectedCoupleCoroutine != null)
			{
				StopCoroutine(_endChooseCoupleAfterTimeCoroutine);
				_endChooseCoupleAfterTimeCoroutine = null;
			}

			if (players.Length < 2)
			{
				ChooseRandomCouple();
			}
			else
			{
				_couple[0] = players[0];
				_couple[1] = players[1];
			}

			AddCouplePlayerGroup();

			_highlightCoupleCoroutine = HighlightCouple(Player, _choseCoupleHighlightHoldDuration);
			yield return StartCoroutine(_highlightCoupleCoroutine);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator EndChooseCoupleAfterTime()
		{
			float timeLeft = _chooseCoupleMaximumDuration;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			_endChooseCoupleAfterTimeCoroutine = null;
			_gameManager.StopChoosingPlayers(Player);

			ChooseRandomCouple();
			AddCouplePlayerGroup();

			_highlightCoupleCoroutine = HighlightCouple(Player, _choseCoupleHighlightHoldDuration);
			yield return StartCoroutine(_highlightCoupleCoroutine);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private void ChooseRandomCouple()
		{
			int playerCount = 0;

			List<PlayerRef> players = _gameManager.PlayerGameInfos.Keys.ToList();

			while(playerCount < 2)
			{
				PlayerRef player = players[UnityEngine.Random.Range(0, players.Count)];

				if (_gameManager.PlayerGameInfos[player].IsAlive)
				{
					_couple[playerCount] = player;
					playerCount++;
				}

				players.Remove(player);
			}
		}

		private void AddCouplePlayerGroup()
		{
			foreach (PlayerRef player in _couple)
			{
				_gameManager.AddPlayerToPlayerGroup(player, PlayerGroupIndexes[1]);
			}
		}

		#endregion

		#region Show Couple
		private bool ShowCouple()
		{
			for (int i = 0; i < _couple.Length; i++)
			{
				if (!_networkDataManager.PlayerInfos[_couple[i]].IsConnected)
				{
					continue;
				}

				StartCoroutine(HighlightCouple(_couple[i], _showCoupleHighlightHoldDuration));
			}

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_DisplayTitle(Player, _coupleRecognizingEachOtherImage.CompactTagId);
			}

			_showedCouple = true;

			StartCoroutine(EndShowCouple());

			return true;
		}

		private IEnumerator EndShowCouple()
		{
			yield return new WaitForSeconds(_showCoupleHighlightHoldDuration);
			_gameManager.StopWaintingForPlayer(Player);
		}
		#endregion

		private IEnumerator HighlightCouple(PlayerRef highlightedFor, float duration)
		{
			_gameManager.RPC_SetPlayersCardHighlightVisible(highlightedFor, _couple, true);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(_couple, true);
#endif
			yield return new WaitForSeconds(duration);

			if (_networkDataManager.PlayerInfos[highlightedFor].IsConnected)
			{
				_gameManager.RPC_SetPlayersCardHighlightVisible(highlightedFor, _couple, false);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(_couple, false);
#endif
		}

		public override void GetTitlesOverride(int priorityIndex, ref Dictionary<PlayerRef, int> titlesOverride)
		{
			if (priorityIndex != NightPriorities[1].index)
			{
				return;
			}

			titlesOverride.Clear();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (_couple.Contains(playerInfo.Key))
				{
					titlesOverride.Add(playerInfo.Key, _inCoupleImage.CompactTagId);
				}
				else
				{
					titlesOverride.Add(playerInfo.Key, _coupleRecognizingEachOtherImage.CompactTagId);
				}
			}
		}

		private void OnPreClientChoosesPlayers(PlayerRef player, ChoicePurpose purpose)
		{
			if (purpose != ChoicePurpose.Kill || !IsCoupleSelected() || !_couple.Contains(player))
			{
				return;
			}

			PlayerRef otherCouplePlayer = _couple[1 - Array.IndexOf(_couple, player)];

			_gameManager.AddImmunePlayerForGettingChosen(otherCouplePlayer);
		}

		private void OnVoteStarting(ChoicePurpose purpose)
		{
			if (purpose != ChoicePurpose.Kill || !IsCoupleSelected())
			{
				return;
			}

			_voteManager.AddVoteImmunity(_couple[0], _couple[1]);
			_voteManager.AddVoteImmunity(_couple[1], _couple[0]);
		}

		#region Couple Death
		private void OnPlayerDeathRevealEnded(PlayerRef deadPlayer)
		{
			if (_couple[0].IsNone || _couple[1].IsNone || !_couple.Contains(deadPlayer))
			{
				return;
			}

			PlayerRef otherCouplePlayer = _couple[1 - Array.IndexOf(_couple, deadPlayer)];

			if (!_gameManager.PlayerGameInfos[otherCouplePlayer].IsAlive)
			{
				return;
			}

			_gameManager.WaitForPlayer(Player);

			StartCoroutine(WaitToHighlightDeadCouple(otherCouplePlayer));
		}

		private IEnumerator WaitToHighlightDeadCouple(PlayerRef otherCouplePlayer)
		{
			yield return 0;

			while (_gameManager.PlayersWaitingFor.Count > 1)
			{
				yield return 0;
			}

			_gameManager.AddMarkForDeath(otherCouplePlayer, _markForDeathAddedByCoupleDeath, 1);

			_gameManager.RPC_DisplayTitle(_coupleDeathImage.CompactTagId);
			_gameManager.RPC_SetPlayersCardHighlightVisible(_couple, true);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(_couple, true);
#endif
			_waitToRemoveDeadCoupleHighlightCoroutine = WaitToRemoveDeadCoupleHighlight();
			StartCoroutine(_waitToRemoveDeadCoupleHighlightCoroutine);
		}

		private IEnumerator WaitToRemoveDeadCoupleHighlight()
		{
			yield return new WaitForSeconds(_coupleDeathHighlightHoldDuration);

			_gameManager.RPC_SetPlayersCardHighlightVisible(_couple, false);
			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(_couple, false);
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration);

			_waitToRemoveDeadCoupleHighlightCoroutine = null;
			_gameManager.StopWaintingForPlayer(Player);
		}
		#endregion

		private void OnPostPlayerLeft(PlayerRef deadPlayer)
		{
			if (deadPlayer != Player || _waitToRemoveDeadCoupleHighlightCoroutine == null)
			{
				return;
			}

			_gameManager.WaitForPlayer(Player);
		}

		public override void OnRoleCallDisconnected()
		{
			if (_endChooseCoupleAfterTimeCoroutine != null)
			{
				StopCoroutine(_endChooseCoupleAfterTimeCoroutine);
			}

			if (_setSelectedCoupleCoroutine != null)
			{
				StopCoroutine(_setSelectedCoupleCoroutine);
			}

			if (_highlightCoupleCoroutine != null)
			{
				StopCoroutine(_highlightCoupleCoroutine);
			}

			if (_endChooseCoupleAfterTimeCoroutine == null)
			{
				return;
			}

			ChooseRandomCouple();
			AddCouplePlayerGroup();
		}

		private void OnDestroy()
		{
			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;
			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;
			_gameManager.PostPlayerDisconnected -= OnPostPlayerLeft;
			_voteManager.VoteStarting -= OnVoteStarting;
		}
	}
}