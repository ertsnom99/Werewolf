using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf;
using Werewolf.Data;

public class FoxBehavior : RoleBehavior
{
	[Header("Find Werewolfs")]
	[SerializeField]
	private GameplayTag _lostPowerImage;

	[SerializeField]
	private GameplayTag _choosePlayerImage;

	[SerializeField]
	private float _choosePlayerMaximumDuration = 10.0f;

	[SerializeField]
	private GameplayTag[] _werewolfPlayerGroups;

	[SerializeField]
	private GameplayTag _foundWerewolfImage;

	[SerializeField]
	private GameplayTag _foundNoWerewolfImage;

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
												_choosePlayerImage.CompactTagId,//"Choose the middle player to check",
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
		_gameManager.RPC_DisplayTitle(Player, _lostPowerImage.CompactTagId);
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
			if (_gameManager.IsPlayerInPlayerGroups(player, _werewolfPlayerGroups))
			{
				werewolfFound = true;
			}

			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, player, true);
		}

		if (werewolfFound)
		{
			_gameManager.RPC_DisplayTitle(Player, _foundWerewolfImage.CompactTagId);
		}
		else
		{
			_gameManager.RPC_DisplayTitle(Player, _foundNoWerewolfImage.CompactTagId);
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

		_gameManager.StopChoosingPlayers(Player);
		_gameManager.StopWaintingForPlayer(Player);
	}

	public override void OnRoleCallDisconnected()
	{
		StopAllCoroutines();
	}
}