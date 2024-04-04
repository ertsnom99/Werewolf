using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class HunterBehavior : RoleBehavior
	{
		[SerializeField]
		private float _selectedPlayerHighlightDuration = 3.0f;

		private IEnumerator _startChoiceTimerCoroutine;

		private GameManager _gameManager;

		public override void Init()
		{
			_gameManager = GameManager.Instance;
			_gameManager.PlayerDeathRevealEnded += OnPlayerDeathRevealEnded;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		public override bool OnRoleCall()
		{
			return false;
		}

		public override void OnRoleTimeOut() { }

		private void OnPlayerDeathRevealEnded(PlayerRef deadPlayer)
		{
			if (Player != deadPlayer || _gameManager.AlivePlayerCount <= 1 || !_gameManager.AskClientToChoosePlayer(Player,
																			new[] { Player },
																			"Choose a player to kill",
																			_gameManager.Config.NightCallMaximumDuration,
																			false,
																			OnPlayerSelected))
			{
				return;
			}

			_gameManager.WaitForPlayer(Player);

			foreach (KeyValuePair<PlayerRef, PlayerInfo> player in _gameManager.PlayerInfos)
			{
				if (player.Key == Player)
				{
					continue;
				}

				_gameManager.RPC_MoveCardToCamera(player.Key, Player, true, _gameManager.PlayerInfos[Player].Role.GameplayTag.CompactTagId);
				_gameManager.RPC_DisplayTitle(player.Key, "The hunter is choosing who to kill!");
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.MoveCardToCamera(Player, true);
			_gameManager.DisplayTitle(null, "The hunter is choosing who to kill!");
#endif
			_startChoiceTimerCoroutine = StartChoiceTimer();
			StartCoroutine(_startChoiceTimerCoroutine);
		}

		private IEnumerator StartChoiceTimer()
		{
			float elapsedTime = .0f;

			while (elapsedTime < _gameManager.Config.NightCallMaximumDuration)
			{
				elapsedTime += Time.deltaTime;
				yield return 0;
			}

			_gameManager.StopChoosingPlayer(Player);

			int iterationCount = 0;
			PlayerRef[] players = _gameManager.PlayerInfos.Keys.ToArray();
			int playerIndex = Random.Range(0, _gameManager.PlayerInfos.Count);
			PlayerRef selectedPlayer = PlayerRef.None;

			while (iterationCount < _gameManager.PlayerInfos.Count)
			{
				if (players[playerIndex] != Player && _gameManager.PlayerInfos[players[playerIndex]].IsAlive)
				{
					selectedPlayer = players[playerIndex];
					break;
				}

				playerIndex++;

				if (playerIndex >= _gameManager.PlayerInfos.Count)
				{
					playerIndex = 0;
				}

				iterationCount++;
			}

			if (selectedPlayer == PlayerRef.None)
			{
				Debug.LogError("The hunter could not find a player to kill!!!");

				_startChoiceTimerCoroutine = null;
				StartCoroutine(HideUIBeforeStopWaintingForPlayer());
			}
			else
			{
				OnPlayerSelected(selectedPlayer);
			}
		}

		private void OnPlayerSelected(PlayerRef selectedPlayer)
		{
			if (_startChoiceTimerCoroutine == null)
			{
				return;
			}

			StopCoroutine(_startChoiceTimerCoroutine);
			_startChoiceTimerCoroutine = null;

			foreach (KeyValuePair<PlayerRef, PlayerInfo> player in _gameManager.PlayerInfos)
			{
				if (player.Key == Player)
				{
					continue;
				}

				_gameManager.RPC_PutCardBackDown(player.Key, Player, false);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.PutCardBackDown(Player, false);
#endif
			if (selectedPlayer != PlayerRef.None)
			{
				_gameManager.AddMarkForDeath(selectedPlayer, "Shot", 1);
				_gameManager.RPC_SetPlayerCardHighlightVisible(selectedPlayer, true);
				_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
				_gameManager.SetPlayerCardHighlightVisible(selectedPlayer, true);
				_gameManager.HideUI();
#endif
				StartCoroutine(WaitBeforeRemovingHighlight(selectedPlayer));
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

		private IEnumerator WaitBeforeRemovingHighlight(PlayerRef selectedPlayer)
		{
			yield return new WaitForSeconds(_selectedPlayerHighlightDuration);

			_gameManager.RPC_SetPlayerCardHighlightVisible(selectedPlayer, false);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(selectedPlayer, false);
#endif
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnDestroy()
		{
			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;
		}
	}
}