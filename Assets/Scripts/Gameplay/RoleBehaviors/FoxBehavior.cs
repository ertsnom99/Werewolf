using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf;
using Werewolf.Data;

public class FoxBehavior : RoleBehavior
{
	[SerializeField]
	private int _lostPowerImageIndex;

	[SerializeField]
	private float _choosePlayerMaximumDuration = 10.0f;

	[SerializeField]
	private int[] _werewolfPlayerGroupIndexes;

	[SerializeField]
	private int _foundWerewolfImageIndex;

	[SerializeField]
	private int _foundNoWerewolfImageIndex;

	[SerializeField]
	private float _foundWerewolfDuration = 3.0f;

	private IEnumerator _endRoleCallAfterTimeCoroutine;

	private bool _hasPower = true;

	private GameManager _gameManager;

	public override void Init()
	{
		_gameManager = GameManager.Instance;
	}

	public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

	public override bool OnRoleCall(int nightCount, int priorityIndex)
	{
		if (!_hasPower)
		{
			StartCoroutine(ShowLostPower());
			return true;
		}

		List<PlayerRef> immunePlayers = _gameManager.GetPlayersDead();

		if (!_gameManager.AskClientToChoosePlayers(Player,
												immunePlayers,
												"Choose the middle player to check",
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

	private IEnumerator ShowLostPower()
	{
		_gameManager.RPC_DisplayTitle(Player, _lostPowerImageIndex);
		yield return 0;
		_gameManager.StopWaintingForPlayer(Player);
	}

	private IEnumerator WaitToStopWaitingForPlayer()
	{
		yield return 0;
		_gameManager.StopWaintingForPlayer(Player);
	}

	private void OnPlayerSelected(PlayerRef[] player)
	{
		StopCoroutine(_endRoleCallAfterTimeCoroutine);

		if (player.Length <= 0 || player[0].IsNone)
		{
			_gameManager.StopWaintingForPlayer(Player);
			return;
		}

		StartCoroutine(CheckForWerewolfs(player[0]));
	}

	private IEnumerator CheckForWerewolfs(PlayerRef middlePlayer)
	{
		List<PlayerRef> playersToCheck = _gameManager.FindSurroundingPlayers(middlePlayer);
		playersToCheck.Add(middlePlayer);

		bool werewolfFound = false;

		foreach(PlayerRef player in playersToCheck)
		{
			if (_gameManager.IsPlayerInPlayerGroups(player, _werewolfPlayerGroupIndexes))
			{
				werewolfFound = true;
			}

			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, player, true);
		}

		if (werewolfFound)
		{
			_gameManager.RPC_DisplayTitle(Player, _foundWerewolfImageIndex);
		}
		else
		{
			_gameManager.RPC_DisplayTitle(Player, _foundNoWerewolfImageIndex);
			_hasPower = false;
		}

		yield return new WaitForSeconds(_foundWerewolfDuration);

		foreach (PlayerRef player in playersToCheck)
		{
			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, player, false);
		}

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

		_gameManager.StopWaintingForPlayer(Player);
	}

	public override void OnRoleCallDisconnected()
	{
		StopAllCoroutines();
	}
}