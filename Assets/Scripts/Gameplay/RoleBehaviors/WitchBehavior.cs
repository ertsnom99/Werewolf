using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf
{
	public class WitchBehavior : RoleBehavior
	{
		[SerializeField]
		private int _markForDeathImageIndex;

		[SerializeField]
		private int _noPotionToUseImageIndex;

		[SerializeField]
		private float _playerHighlightHoldDuration;

		[SerializeField]
		private float _choosePotionMaximumDuration = 10.0f;

		[SerializeField]
		private float _choiceSelectedHoldDuration;

		[SerializeField]
		private int _saveImageIndex;

		[SerializeField]
		private string _markForDeathRemovedByLifePotion;

		[SerializeField]
		private int _killImageIndex;

		[SerializeField]
		private float _choosePlayerDuration;

		[SerializeField]
		private string _markForDeathAddedByDeathPotion;

		private bool _hasLifePotion = true;
		private bool _hasDeathPotion = true;

		private PlayerRef _markedForDeathPlayer;
		private int[] _choices;

		private IEnumerator _endChoosePlayerCoroutine;

		private IEnumerator _endRoleCallAfterTimeCoroutine;
		private float _choosePotionTimeLeft;

		private NetworkDataManager _networkDataManager;
		private GameManager _gameManager;

		public override void Init()
		{
			_networkDataManager = NetworkDataManager.Instance;
			_gameManager = GameManager.Instance;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		public override bool OnRoleCall(int priorityIndex)
		{
			if (!_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
				return true;
			}

			StartCoroutine(ReveilMarkedForDeathPlayer());
			return true;
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator ReveilMarkedForDeathPlayer()
		{
			PlayerRef[] markedPlayers = _gameManager.GetPlayersWithMarkForDeath(_markForDeathRemovedByLifePotion);
			_markedForDeathPlayer = markedPlayers.Length > 0 ? markedPlayers[0] : PlayerRef.None;

			if (!_markedForDeathPlayer.IsNone)
			{
				_gameManager.RPC_SetPlayerCardHighlightVisible(Player, _markedForDeathPlayer, true);

				_gameManager.RPC_DisplayTitle(Player, _markForDeathImageIndex);
				yield return new WaitForSeconds(_playerHighlightHoldDuration);

				_gameManager.RPC_HideUI(Player);
				yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration);

				_gameManager.RPC_SetPlayerCardHighlightVisible(Player, _markedForDeathPlayer, false);
			}

			OnReveilMarkedForDeathPlayerFinished();
		}

		private void OnReveilMarkedForDeathPlayerFinished()
		{
			_choosePotionTimeLeft = _choosePotionMaximumDuration;

			if (DisplayPotionChoices())
			{
				_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
				StartCoroutine(_endRoleCallAfterTimeCoroutine);
			}
			else
			{
				StartCoroutine(DelayStopWaintingForPlayer());
			}
		}

		private bool DisplayPotionChoices(bool showTitles = true)
		{
			_markedForDeathPlayer = PlayerRef.None;

			if (!_hasLifePotion && !_hasDeathPotion)
			{
				if (showTitles)
				{
					_gameManager.RPC_DisplayTitle(Player, _noPotionToUseImageIndex);
				}

				return false;
			}

			bool canSavePlayer = false;

			if (_hasLifePotion)
			{
				PlayerRef[] markedPlayers = _gameManager.GetPlayersWithMarkForDeath(_markForDeathRemovedByLifePotion);
				_markedForDeathPlayer = markedPlayers.Length > 0 ? markedPlayers[0] : _markedForDeathPlayer;
				canSavePlayer = !_markedForDeathPlayer.IsNone;
			}

			if (!canSavePlayer && !_hasDeathPotion)
			{
				if (showTitles)
				{
					_gameManager.RPC_DisplayTitle(Player, _noPotionToUseImageIndex);
				}

				return false;
			}

			if (!canSavePlayer)
			{
				_choices = new int[] { _killImageIndex };
			}
			else if (!_hasDeathPotion)
			{
				_choices = new int[] { _saveImageIndex };
			}
			else
			{
				_choices = new int[] { _saveImageIndex, _killImageIndex };
			}

			return _gameManager.AskClientToMakeChoice(Player,
													_choices,
													_choosePotionTimeLeft,
													"You can choose a potion to use",
													"You chose this potion",
													"You didn't choose any potion",
													false,
													OnPotionSelected);
		}

		private IEnumerator DelayStopWaintingForPlayer()
		{
			yield return 0;
			EndRoleCall();
		}

		private void OnPotionSelected(int choiceIndex)
		{
			StopCoroutine(_endRoleCallAfterTimeCoroutine);
			_gameManager.StopChoosing(Player);

			int choice = choiceIndex >= 0 ? _choices[choiceIndex] : -1;

			if (choice == _saveImageIndex)
			{
				_gameManager.RemoveMarkForDeath(_markedForDeathPlayer, _markForDeathRemovedByLifePotion);
				_hasLifePotion = false;

				StartCoroutine(RefreshPotions(_choiceSelectedHoldDuration, true));
				return;
			}
			else if (choice == _killImageIndex)
			{
				StartCoroutine(ChoosePlayerToKill());
				return;
			}

			EndRoleCall();
		}

		private IEnumerator ChoosePlayerToKill()
		{
			yield return new WaitForSeconds(_choiceSelectedHoldDuration);

			_gameManager.RPC_HideUI(Player);
			yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration);

			List<PlayerRef> immunePlayers = _gameManager.GetPlayersDeadList();
			immunePlayers.Add(Player);

			if (_gameManager.AskClientToChoosePlayers(Player,
													immunePlayers,
													"Choose a player to kill",
													_choosePlayerDuration,
													true,
													1,
													ChoicePurpose.Kill,
													OnChosePlayers))
			{
				_endChoosePlayerCoroutine = EndChoosePlayer(_choosePlayerDuration);
				StartCoroutine(_endChoosePlayerCoroutine);
			}
			else
			{
				_hasDeathPotion = false;
				StartCoroutine(RefreshPotions(.0f, false));
			}
		}

		private IEnumerator EndChoosePlayer(float duration)
		{
			yield return new WaitForSeconds(duration);

			_gameManager.StopChoosingPlayers(Player);
			OnChosePlayer(PlayerRef.None);
		}

		private void OnChosePlayers(PlayerRef[] players)
		{
			OnChosePlayer(players[0]);
		}

		private void OnChosePlayer(PlayerRef player)
		{
			StopCoroutine(_endChoosePlayerCoroutine);

			_hasDeathPotion = false;

			if (!player.IsNone)
			{
				_gameManager.AddMarkForDeath(player, _markForDeathAddedByDeathPotion);
				StartCoroutine(HighlightPlayerMarkedForDeath(player));
				return;
			}

			StartCoroutine(RefreshPotions(.0f, true));
		}

		private IEnumerator HighlightPlayerMarkedForDeath(PlayerRef player)
		{
			_gameManager.RPC_HideUI(Player);
			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, player, true);
			yield return new WaitForSeconds(_playerHighlightHoldDuration);
			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, player, false);

			StartCoroutine(RefreshPotions(.0f, false));
		}

		private IEnumerator RefreshPotions(float delay, bool hideUI)
		{
			yield return new WaitForSeconds(delay);

			if (hideUI)
			{
				_gameManager.RPC_HideUI(Player);
				yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration);
			}

			if (DisplayPotionChoices(false))
			{
				_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
				StartCoroutine(_endRoleCallAfterTimeCoroutine);

				yield break;
			}

			EndRoleCall();
		}

		private IEnumerator EndRoleCallAfterTime()
		{
			while (_choosePotionTimeLeft > 0)
			{
				yield return 0;
				_choosePotionTimeLeft -= Time.deltaTime;
			}

			_gameManager.StopChoosing(Player);

			EndRoleCall();
		}

		private void EndRoleCall()
		{
			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}
	}
}