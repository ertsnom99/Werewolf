using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class CupidBehavior : RoleBehavior, IVoteManagerSubscriber, IGameManagerSubscriber
	{
		[Header("Choose Couple")]
		[SerializeField]
		private TitleScreenData _chooseCoupleTitleScreen;

		[SerializeField]
		private float _chooseCoupleMaximumDuration;

		[SerializeField]
		private PlayerGroupData _couplePlayerGroup;

		[SerializeField]
		private GameHistoryEntryData _coupleSelectedGameHistoryEntry;

		[SerializeField]
		private float _choseCoupleHighlightHoldDuration;

		[Header("Show Couple")]
		[SerializeField]
		private float _showCoupleHighlightHoldDuration;

		[SerializeField]
		private TitleScreenData _inCoupleTitleScreen;

		[SerializeField]
		private TitleScreenData _coupleRecognizingEachOtherTitleScreen;

		[Header("Couple Death")]
		[SerializeField]
		private MarkForDeathData _markForDeathAddedByCoupleDeath;

		[SerializeField]
		private GameHistoryEntryData _coupleDiedGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _coupleDeathTitleScreen;

		[SerializeField]
		private float _coupleDeathHighlightHoldDuration;

		private PlayerRef[] _choices;
		private readonly List<PlayerRef[]> _couples = new();
		private IEnumerator _endChooseCoupleAfterTimeCoroutine;
		private IEnumerator _setSelectedCoupleCoroutine;
		private IEnumerator _waitToRemoveDeadCoupleHighlightCoroutine;
		private bool _choseCouple;
		private bool _showedCouple;
		private readonly List<PlayerRef[]> _couplesPlayersToHighlight = new();

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

			_gameManager.PreSelectPlayers += OnPreSelectPlayers;
			_voteManager.Subscribe(this);
			_gameManager.PlayerDeathRevealStarted += OnPlayerDeathRevealStarted;
			_gameManager.Subscribe(this, 0);
			_gameManager.PostPlayerDisconnected += OnPostPlayerDisconnected;

			if (NightPriorities.Count < 2)
			{
				Debug.LogError($"{nameof(CupidBehavior)} must have two night priorities: the first one to select the couple and the second one to let a couple know each other");
			}
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override UniqueID[] GetCurrentPlayerGroupIDs()
		{
			return new UniqueID[1] { PlayerGroupIDs[0] };
		}

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			isWakingUp = false;

			if (priorityIndex == NightPriorities[0].index && !_choseCouple)
			{
				isWakingUp = true;
				return ChooseCouple();
			}
			else if (priorityIndex == NightPriorities[1].index && _choseCouple && !_showedCouple)
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
											_chooseCoupleTitleScreen.ID.HashCode,
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

					_gameManager.AddPlayersToNewPlayerGroup(couple, _couplePlayerGroup.ID);
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

			_gameManager.AddPlayersToNewPlayerGroup(couple, _couplePlayerGroup.ID);
			AddCoupleSelectedGameHistoryEntry(couple);

			_choseCouple = true;

			HighlightCouple(couple, Player);
			yield return new WaitForSeconds(_choseCoupleHighlightHoldDuration * _gameManager.GameSpeedModifier);

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

			_gameManager.AddPlayersToNewPlayerGroup(couple, _couplePlayerGroup.ID);
			AddCoupleSelectedGameHistoryEntry(couple);

			_choseCouple = true;

			HighlightCouple(couple, Player);
			yield return new WaitForSeconds(_choseCoupleHighlightHoldDuration * _gameManager.GameSpeedModifier);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private void ChooseRandomCouple()
		{
			if (_choices == null || _choices.Length < 2)
			{
				Debug.LogError($"{nameof(CupidBehavior)} doesn't have enough choices to create a couple");
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
			_gameHistoryManager.AddEntry(_coupleSelectedGameHistoryEntry.ID,
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
				if (_networkDataManager.PlayerInfos[couple[i]].IsConnected)
				{
					HighlightCouple(couple, couple[i]);
				}
			}

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_DisplayTitle(Player, couple.Contains(Player) ? _inCoupleTitleScreen.ID.HashCode : _coupleRecognizingEachOtherTitleScreen.ID.HashCode);
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

		private void HighlightCouple(PlayerRef[] couple, PlayerRef highlightedFor)
		{
			if (_networkDataManager.PlayerInfos[highlightedFor].IsConnected)
			{
				_gameManager.RPC_SetPlayersCardHighlightVisible(highlightedFor, couple, true);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(couple, true);
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
					titlesOverride.Add(playerInfo.Key, _inCoupleTitleScreen.ID.HashCode);
				}
				else
				{
					titlesOverride.Add(playerInfo.Key, _coupleRecognizingEachOtherTitleScreen.ID.HashCode);
				}
			}
		}

		private void OnPreSelectPlayers(PlayerRef player, ChoicePurpose purpose, List<PlayerRef> choices)
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
				choices.Remove(couple[1 - Array.IndexOf(couple, player)]);
			}
		}

		void IVoteManagerSubscriber.OnVoteStarting(ChoicePurpose purpose)
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
		void IGameManagerSubscriber.OnPlayerDied(PlayerRef deadPlayer, MarkForDeathData markForDeath)
		{
			if (_couplesPlayersToHighlight.Any(x => x.Contains(deadPlayer)))
			{
				return;
			}

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

			foreach (PlayerRef otherCouplePlayer in otherCouplePlayers)
			{
				_gameManager.AddMarkForDeath(otherCouplePlayer, _markForDeathAddedByCoupleDeath, 0);
			}

			otherCouplePlayers.Add(deadPlayer);
			_couplesPlayersToHighlight.Add(otherCouplePlayers.ToArray());
		}

		private void OnPlayerDeathRevealStarted(PlayerRef deadPlayer, MarkForDeathData markForDeath)
		{
			if (markForDeath != _markForDeathAddedByCoupleDeath || _couplesPlayersToHighlight.Count <= 0)
			{
				return;
			}

			int couplePlayersToHighlightIndex = _couplesPlayersToHighlight.FindIndex(x => x.Contains(deadPlayer));

			if (couplePlayersToHighlightIndex == -1)
			{
				return;
			}

			PlayerRef[] couplePlayersToHighlight = _couplesPlayersToHighlight[couplePlayersToHighlightIndex];

			for (int i = 0; i < _couplesPlayersToHighlight[couplePlayersToHighlightIndex].Length - 1; i++)
			{
				_gameHistoryManager.AddEntry(_coupleDiedGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "FirstCouplePlayer",
												Data = _networkDataManager.PlayerInfos[couplePlayersToHighlight[couplePlayersToHighlight.Length - 1]].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "SecondCouplePlayer",
												Data = _networkDataManager.PlayerInfos[couplePlayersToHighlight[i]].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
											});
			}

			_gameManager.WaitForPlayer(Player);

			_gameManager.RPC_DisplayTitle(_coupleDeathTitleScreen.ID.HashCode);
			_gameManager.RPC_SetPlayersCardHighlightVisible(couplePlayersToHighlight, true);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(couplePlayersToHighlight, true);
#endif
			_waitToRemoveDeadCoupleHighlightCoroutine = WaitToRemoveDeadPlayersHighlight(couplePlayersToHighlight, couplePlayersToHighlightIndex);
			StartCoroutine(_waitToRemoveDeadCoupleHighlightCoroutine);
		}

		private IEnumerator WaitToRemoveDeadPlayersHighlight(PlayerRef[] deadPlayers, int couplePlayersToHighlightIndex)
		{
			yield return new WaitForSeconds(_coupleDeathHighlightHoldDuration * _gameManager.GameSpeedModifier);

			_gameManager.RPC_SetPlayersCardHighlightVisible(deadPlayers, false);
			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(deadPlayers, false);
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(_gameManager.GameConfig.UITransitionNormalDuration);

			_waitToRemoveDeadCoupleHighlightCoroutine = null;
			_couplesPlayersToHighlight.RemoveAt(couplePlayersToHighlightIndex);

			_gameManager.StopWaintingForPlayer(Player);
		}
		#endregion

		private void OnPostPlayerDisconnected(PlayerRef deadPlayer)
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

			if (_endChooseCoupleAfterTimeCoroutine == null)
			{
				return;
			}

			ChooseRandomCouple();

			PlayerRef[] couple = _couples[^1];

			_gameManager.AddPlayersToNewPlayerGroup(couple, _couplePlayerGroup.ID);
			AddCoupleSelectedGameHistoryEntry(couple);
		}

		private void OnDestroy()
		{
			_gameManager.PreSelectPlayers -= OnPreSelectPlayers;
			_voteManager.Unsubscribe(this);
			_gameManager.PlayerDeathRevealStarted -= OnPlayerDeathRevealStarted;
			_gameManager.Unsubscribe(this);
			_gameManager.PostPlayerDisconnected -= OnPostPlayerDisconnected;
		}
	}
}