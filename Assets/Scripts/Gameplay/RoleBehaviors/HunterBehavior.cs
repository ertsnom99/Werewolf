using Assets.Scripts.Data.Tags;
using Assets.Scripts.Editor.Tags;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf
{
	public class HunterBehavior : RoleBehavior
	{
		[SerializeField]
		private int _choosePlayerImageIndex;

		[SerializeField]
		private float _choosePlayerMaximumDuration = 10.0f;

		[SerializeField]
		[GameplayTagID]
		private GameplayTag _markForDeathAddedByShot;

		[SerializeField]
		private float _selectedPlayerHighlightDuration = 3.0f;

		private IEnumerator _startChoiceTimerCoroutine;

		private NetworkDataManager _networkDataManager;
		private GameManager _gameManager;

		public override void Init()
		{
			_networkDataManager = NetworkDataManager.Instance;
			_gameManager = GameManager.Instance;

			_gameManager.PlayerDeathRevealEnded += OnPlayerDeathRevealEnded;
			_gameManager.PostPlayerDisconnected += OnPostPlayerLeft;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex)
		{
			return false;
		}

		private void OnPlayerDeathRevealEnded(PlayerRef deadPlayer)
		{
			if (Player != deadPlayer || _gameManager.AlivePlayerCount <= 1)
			{
				return;
			}

			_gameManager.WaitForPlayer(Player);

			List<PlayerRef> immunePlayers = _gameManager.GetPlayersDead();
			immunePlayers.Add(Player);

			if (!_gameManager.AskClientToChoosePlayers(Player,
													immunePlayers,
													"Choose a player to kill",
													_choosePlayerMaximumDuration,
													true,
													1,
													ChoicePurpose.Kill,
													OnPlayersSelected))
			{
				SelectRandomPlayer();
				return;
			}

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected || playerInfo.Key == Player)
				{
					continue;
				}

				_gameManager.RPC_DisplayTitle(playerInfo.Key, _choosePlayerImageIndex);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.DisplayTitle(_choosePlayerImageIndex);
#endif
			_startChoiceTimerCoroutine = StartChoiceTimer();
			StartCoroutine(_startChoiceTimerCoroutine);
		}

		private IEnumerator StartChoiceTimer()
		{
			float elapsedTime = .0f;

			while (elapsedTime < _choosePlayerMaximumDuration)
			{
				yield return 0;
				elapsedTime += Time.deltaTime;
			}

			_gameManager.StopChoosingPlayers(Player);
			SelectRandomPlayer();
		}

		private void SelectRandomPlayer()
		{
			int iterationCount = 0;
			PlayerRef[] players = _gameManager.PlayerGameInfos.Keys.ToArray();
			int playerIndex = Random.Range(0, _gameManager.PlayerGameInfos.Count);
			PlayerRef selectedPlayer = PlayerRef.None;

			while (iterationCount < _gameManager.PlayerGameInfos.Count)
			{
				if (players[playerIndex] != Player && _gameManager.PlayerGameInfos[players[playerIndex]].IsAlive)
				{
					selectedPlayer = players[playerIndex];
					break;
				}

				playerIndex++;

				if (playerIndex >= _gameManager.PlayerGameInfos.Count)
				{
					playerIndex = 0;
				}

				iterationCount++;
			}

			if (selectedPlayer.IsNone)
			{
				Debug.LogError("The hunter could not find a player to kill!!!");

				_startChoiceTimerCoroutine = null;

				if (_networkDataManager.PlayerInfos[Player].IsConnected)
				{
					StartCoroutine(HideUIBeforeStopWaintingForPlayer());
				}
				else
				{
					_gameManager.StopWaintingForPlayer(Player);
				}
			}
			else
			{
				OnPlayerSelected(selectedPlayer);
			}
		}

		private void OnPlayersSelected(PlayerRef[] selectedPlayers)
		{
			OnPlayerSelected(selectedPlayers[0]);
		}

		private void OnPlayerSelected(PlayerRef selectedPlayer)
		{
			if (_startChoiceTimerCoroutine != null)
			{
				StopCoroutine(_startChoiceTimerCoroutine);
				_startChoiceTimerCoroutine = null;
			}

			if (!selectedPlayer.IsNone)
			{
				_gameManager.AddMarkForDeath(selectedPlayer, _markForDeathAddedByShot, 1);
				_gameManager.RPC_SetPlayerCardHighlightVisible(selectedPlayer, true);
				_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
				_gameManager.SetPlayerCardHighlightVisible(selectedPlayer, true);
				_gameManager.HideUI();
#endif
				StartCoroutine(WaitToRemeHighlight(selectedPlayer));
				return;
			}

			StartCoroutine(HideUIBeforeStopWaintingForPlayer());
		}

		private IEnumerator HideUIBeforeStopWaintingForPlayer()
		{
			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator WaitToRemeHighlight(PlayerRef selectedPlayer)
		{
			yield return new WaitForSeconds(_selectedPlayerHighlightDuration);

			_gameManager.RPC_SetPlayerCardHighlightVisible(selectedPlayer, false);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(selectedPlayer, false);
#endif
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnPostPlayerLeft(PlayerRef deadPlayer)
		{
			if (deadPlayer != Player || _startChoiceTimerCoroutine == null)
			{
				return;
			}

			_gameManager.WaitForPlayer(Player);
			SelectRandomPlayer();
		}

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}

		private void OnDestroy()
		{
			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;
			_gameManager.PostPlayerDisconnected -= OnPostPlayerLeft;
		}
	}
}