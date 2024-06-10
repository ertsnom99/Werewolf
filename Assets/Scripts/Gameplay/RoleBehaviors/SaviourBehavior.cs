using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class SaviourBehavior : RoleBehavior
	{
		[Header("Save Player")]
		[SerializeField]
		private GameplayTag _choosePlayerImage;

		[SerializeField]
		private float _choosePlayerMaximumDuration = 10.0f;

		[SerializeField]
		private float _playerHighlightHoldDuration = 3.0f;

		[SerializeField]
		private GameplayTag _markForDeathRemovedByProtection;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		private int _lastSelectionNightCount;
		private PlayerRef _selectedPlayer;

		private GameManager _gameManager;

		public override void Init()
		{
			_gameManager = GameManager.Instance;

			_gameManager.MarkForDeathAdded += OnMarkForDeathAdded;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex)
		{
			if (_lastSelectionNightCount + 1 < nightCount)
			{
				_selectedPlayer = PlayerRef.None;
			}

			_lastSelectionNightCount = nightCount;

			List<PlayerRef> immunePlayers = _gameManager.GetPlayersDead();

			if (!_selectedPlayer.IsNone)
			{
				immunePlayers.Add(_selectedPlayer);
				_selectedPlayer = PlayerRef.None;
			}

			if (!_gameManager.AskClientToChoosePlayers(Player,
													immunePlayers,
													_choosePlayerImage.CompactTagId,//"Choose a player to protect",
													_choosePlayerMaximumDuration,
													false,
													1,
													ChoicePurpose.Other,
													OnPlayerSelected))
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
			}

			_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
			StartCoroutine(_endRoleCallAfterTimeCoroutine);

			return true;
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_selectedPlayer = PlayerRef.None;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnPlayerSelected(PlayerRef[] player)
		{
			StopCoroutine(_endRoleCallAfterTimeCoroutine);

			if (player.Length <= 0 || player[0].IsNone)
			{
				_selectedPlayer = PlayerRef.None;
				_gameManager.StopWaintingForPlayer(Player);
				return;
			}

			_selectedPlayer = player[0];
			StartCoroutine(HighlightSelectedPlayer());
		}

		private IEnumerator HighlightSelectedPlayer()
		{
			_gameManager.RPC_HideUI(Player);
			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, _selectedPlayer, true);
			yield return new WaitForSeconds(_playerHighlightHoldDuration);
			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, _selectedPlayer, false);
			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator EndRoleCallAfterTime()
		{
			float timeLeft = _choosePlayerMaximumDuration;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			_gameManager.StopChoosingPlayers(Player);
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnMarkForDeathAdded(PlayerRef player, GameplayTag markForDeath)
		{
			if (player != _selectedPlayer || markForDeath != _markForDeathRemovedByProtection)
			{
				return;
			}

			_gameManager.RemoveMarkForDeath(player, _markForDeathRemovedByProtection);
		}

		public override void ReInit()
		{
			_selectedPlayer = PlayerRef.None;
		}

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}

		private void OnDestroy()
		{
			_gameManager.MarkForDeathAdded -= OnMarkForDeathAdded;
		}
	}
}