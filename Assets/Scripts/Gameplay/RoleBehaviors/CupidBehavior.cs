using Assets.Scripts.Data.Tags;
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class CupidBehavior : RoleBehavior
	{
		[Header("Choose Couple")]
		[SerializeField]
		private GameplayTag _chooseCoupleImage;

		[SerializeField]
		private float _chooseCoupleMaximumDuration = 10.0f;

		[SerializeField]
		private GameplayTag _coupleSelectedGameHistoryEntry;

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
		private GameplayTag _coupleDiedGameHistoryEntry;

		[SerializeField]
		private GameplayTag _coupleDeathImage;

		[SerializeField]
		private float _coupleDeathHighlightHoldDuration = 3.0f;

		private PlayerRef[] _choices;
		private List<PlayerRef[]> _couples = new();
		private IEnumerator _endChooseCoupleAfterTimeCoroutine;
		private IEnumerator _setSelectedCoupleCoroutine;
		private IEnumerator _highlightCoupleCoroutine;
		private IEnumerator _waitToRemoveDeadCoupleHighlightCoroutine;
		private bool _choseCouple;
		private bool _showedCouple;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;
		private VoteManager _voteManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
			_voteManager = VoteManager.Instance;

			_gameManager.PreSelectPlayers += OnPreChoosePlayers;
			_voteManager.VoteStarting += OnVoteStarting;
			_gameManager.PlayerDeathRevealEnded += OnPlayerDeathRevealEnded;
			_gameManager.PostPlayerDisconnected += OnPostPlayerLeft;

			if (PlayerGroups.Count < 2)
			{
				Debug.LogError("Cupid must have two player groups: the first one for cupid and the second one for any couple");
			}

			if (NightPriorities.Count < 2)
			{
				Debug.LogError("Cupid must have two night priorities: the first one to select the couple and the second one to let a couple know each other");
			}
		}

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

		public override GameplayTag[] GetCurrentPlayerGroups()
		{
			return new GameplayTag[1] { PlayerGroups[0] };
		}

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			isWakingUp = false;

			if (!_choseCouple && priorityIndex == NightPriorities[0].index)
			{
				isWakingUp = true;
				return ChooseCouple();
			}
			else if (_choseCouple && !_showedCouple && priorityIndex == NightPriorities[1].index)
			{
				StartCoroutine(ShowCouple());
				return true;
			}

			return false;
		}

		#region Choose Couple
		private bool ChooseCouple()
		{
			List<PlayerRef> choices = _gameManager.GetAlivePlayers();

			_choices = choices.ToArray();

			if (!_gameManager.SelectPlayers(Player,
											choices,
											_chooseCoupleImage.CompactTagId,
											_chooseCoupleMaximumDuration * _gameManager.GameSpeedModifier,
											true,
											2,
											ChoicePurpose.Other,
											OnCoupleSelected))
			{
				if (choices.Count >= 2)
				{
					ChooseRandomCouple();

					PlayerRef[] couple = _couples[^1];

					_gameManager.AddPlayersToNewPlayerGroup(couple, PlayerGroups[1]);
					AddCoupleSelectedGameHistoryEntry(couple);

					_choseCouple = true;
				}

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

			if (players == null || players.Length < 2)
			{
				ChooseRandomCouple();
			}
			else
			{
				_couples.Add(new PlayerRef[2] { players[0], players[1] });
			}

			PlayerRef[] couple = _couples[^1];

			_gameManager.AddPlayersToNewPlayerGroup(couple, PlayerGroups[1]);
			AddCoupleSelectedGameHistoryEntry(couple);

			_choseCouple = true;

			_highlightCoupleCoroutine = HighlightCouple(couple, Player, _choseCoupleHighlightHoldDuration * _gameManager.GameSpeedModifier);
			yield return StartCoroutine(_highlightCoupleCoroutine);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator EndChooseCoupleAfterTime()
		{
			float timeLeft = _chooseCoupleMaximumDuration * _gameManager.GameSpeedModifier;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			_endChooseCoupleAfterTimeCoroutine = null;
			_gameManager.StopSelectingPlayers(Player);

			ChooseRandomCouple();

			PlayerRef[] couple = _couples[^1];

			_gameManager.AddPlayersToNewPlayerGroup(couple, PlayerGroups[1]);
			AddCoupleSelectedGameHistoryEntry(couple);

			_choseCouple = true;

			_highlightCoupleCoroutine = HighlightCouple(couple, Player, _choseCoupleHighlightHoldDuration * _gameManager.GameSpeedModifier);
			yield return StartCoroutine(_highlightCoupleCoroutine);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private void ChooseRandomCouple()
		{
			if (_choices == null || _choices.Length < 2)
			{
				Debug.LogError("Cupid doesn't have enough choices to create a couple");
				return;
			}

			_couples.Add(new PlayerRef[2]);

			int playerCount = 0;

			List<PlayerRef> players = _choices.ToList();

			while (playerCount < 2)
			{
				PlayerRef player = players[UnityEngine.Random.Range(0, players.Count)];
				_couples[^1][playerCount] = player;
				players.Remove(player);
				playerCount++;
			}
		}

		private void AddCoupleSelectedGameHistoryEntry(PlayerRef[] couple)
		{
			_gameHistoryManager.AddEntry(_coupleSelectedGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "CupidPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "FirstCouplePlayer",
												Data = _networkDataManager.PlayerInfos[couple[0]].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "SecondCouplePlayer",
												Data = _networkDataManager.PlayerInfos[couple[1]].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});
		}
		#endregion

		#region Show Couple
		private IEnumerator ShowCouple()
		{
			_gameManager.StartWaitingForPlayersRollCall += OnStartWaitingForPlayersRollCall;

			PlayerRef[] couple = _couples[^1];

			for (int i = 0; i < couple.Length; i++)
			{
				if (!_networkDataManager.PlayerInfos[couple[i]].IsConnected)
				{
					continue;
				}

				StartCoroutine(HighlightCouple(couple, couple[i], _showCoupleHighlightHoldDuration * _gameManager.GameSpeedModifier));
			}

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_DisplayTitle(Player, couple.Contains(Player) ? _inCoupleImage.CompactTagId : _coupleRecognizingEachOtherImage.CompactTagId);
			}

			_showedCouple = true;

			yield return new WaitForSeconds(_showCoupleHighlightHoldDuration * _gameManager.GameSpeedModifier);
			_gameManager.StopWaintingForPlayer(Player);

			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;
		}

		private void OnStartWaitingForPlayersRollCall()
		{
			PlayerRef[] couple = _couples[^1];

			_gameManager.SetPlayerAwake(couple[0], true);
			_gameManager.SetPlayerAwake(couple[1], true);
		}
		#endregion

		private IEnumerator HighlightCouple(PlayerRef[] couple, PlayerRef highlightedFor, float duration)
		{
			_gameManager.RPC_SetPlayersCardHighlightVisible(highlightedFor, couple, true);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(couple, true);
#endif
			yield return new WaitForSeconds(duration);

			if (_networkDataManager.PlayerInfos[highlightedFor].IsConnected)
			{
				_gameManager.RPC_SetPlayersCardHighlightVisible(highlightedFor, couple, false);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(couple, false);
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
				if (_couples[^1].Contains(playerInfo.Key))
				{
					titlesOverride.Add(playerInfo.Key, _inCoupleImage.CompactTagId);
				}
				else
				{
					titlesOverride.Add(playerInfo.Key, _coupleRecognizingEachOtherImage.CompactTagId);
				}
			}
		}

		private void OnPreChoosePlayers(PlayerRef player, ChoicePurpose purpose, List<PlayerRef> immunePlayersForGettingSelected)
		{
			if (purpose != ChoicePurpose.Kill || _couples.Count <= 0)
			{
				return;
			}

			IEnumerable<PlayerRef[]> couples = _couples.Where(x => x.Contains(player));

			if (couples.Count() <= 0)
			{
				return;
			}

			foreach (PlayerRef[] couple in couples)
			{
				PlayerRef otherCouplePlayer = couple[1 - Array.IndexOf(couple, player)];

				if (immunePlayersForGettingSelected.Contains(otherCouplePlayer))
				{
					immunePlayersForGettingSelected.Remove(otherCouplePlayer);
				}
			}
		}

		private void OnVoteStarting(ChoicePurpose purpose)
		{
			if (purpose != ChoicePurpose.Kill || _couples.Count <= 0)
			{
				return;
			}

			foreach (PlayerRef[] couple in _couples)
			{
				_voteManager.AddVoteImmunity(couple[0], couple[1]);
				_voteManager.AddVoteImmunity(couple[1], couple[0]);
			}
		}

		#region Couple Death
		private void OnPlayerDeathRevealEnded(PlayerRef deadPlayer, GameplayTag markForDeath)
		{
			IEnumerable<PlayerRef[]> couples = _couples.Where(x => x.Contains(deadPlayer));

			if (couples.Count() <= 0)
			{
				return;
			}

			HashSet<PlayerRef> otherCouplePlayers = new();

			foreach (PlayerRef[] couple in couples)
			{
				PlayerRef otherCouplePlayer = couple[1 - Array.IndexOf(couple, deadPlayer)];

				if (_gameManager.PlayerGameInfos[otherCouplePlayer].IsAlive)
				{
					otherCouplePlayers.Add(otherCouplePlayer);
				}
			}

			if (otherCouplePlayers.Count <= 0)
			{
				return;
			}

			_gameManager.WaitForPlayer(Player);

			StartCoroutine(WaitToMarkOtherCouplePlayerForDeath(deadPlayer, otherCouplePlayers));
		}

		private IEnumerator WaitToMarkOtherCouplePlayerForDeath(PlayerRef deadCouplePlayer, HashSet<PlayerRef> otherCouplePlayers)
		{
			yield return 0;

			while (_gameManager.PlayersWaitingFor.Count > 1)
			{
				yield return 0;
			}

			foreach (PlayerRef otherCouplePlayer in otherCouplePlayers)
			{
				_gameManager.AddMarkForDeath(otherCouplePlayer, _markForDeathAddedByCoupleDeath, 1);

				_gameHistoryManager.AddEntry(_coupleDiedGameHistoryEntry,
											new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "FirstCouplePlayer",
												Data = _networkDataManager.PlayerInfos[deadCouplePlayer].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "SecondCouplePlayer",
												Data = _networkDataManager.PlayerInfos[otherCouplePlayer].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
											});
			}

			otherCouplePlayers.Add(deadCouplePlayer);

			HighlightDeadPlayers(otherCouplePlayers.ToArray());
		}

		private void HighlightDeadPlayers(PlayerRef[] deadPlayers)
		{
			_gameManager.RPC_DisplayTitle(_coupleDeathImage.CompactTagId);
			_gameManager.RPC_SetPlayersCardHighlightVisible(deadPlayers, true);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(deadPlayers, true);
#endif
			_waitToRemoveDeadCoupleHighlightCoroutine = WaitToRemoveDeadPlayersHighlight(deadPlayers);
			StartCoroutine(_waitToRemoveDeadCoupleHighlightCoroutine);
		}

		private IEnumerator WaitToRemoveDeadPlayersHighlight(PlayerRef[] deadPlayers)
		{
			yield return new WaitForSeconds(_coupleDeathHighlightHoldDuration * _gameManager.GameSpeedModifier);

			_gameManager.RPC_SetPlayersCardHighlightVisible(deadPlayers, false);
			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(deadPlayers, false);
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration);

			_waitToRemoveDeadCoupleHighlightCoroutine = null;
			_gameManager.StopWaintingForPlayer(Player);
		}
		#endregion

		private void OnPostPlayerLeft(PlayerRef deadPlayer)
		{
			if (deadPlayer == Player && _waitToRemoveDeadCoupleHighlightCoroutine != null)
			{
				_gameManager.WaitForPlayer(Player);
			}
		}

		public override void OnPlayerChanged()
		{
			_choseCouple = false;
			_showedCouple = false;
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

			PlayerRef[] couple = _couples[^1];

			_gameManager.AddPlayersToNewPlayerGroup(couple, PlayerGroups[1]);
			AddCoupleSelectedGameHistoryEntry(couple);
		}

		private void OnDestroy()
		{
			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;
			_gameManager.PostPlayerDisconnected -= OnPostPlayerLeft;
			_voteManager.VoteStarting -= OnVoteStarting;
		}
	}
}