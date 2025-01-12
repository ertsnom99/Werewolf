using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class WitchBehavior : RoleBehavior
	{
		[Header("Player Dead Tonight")]
		[SerializeField]
		private GameplayTag _playerDeadTonightImage;

		[SerializeField]
		private float _playerHighlightHoldDuration;

		[Header("Potion Choice")]
		[SerializeField]
		private GameplayTag _noPotionToUseImage;

		[SerializeField]
		private GameplayTag _choiceScreen;

		[SerializeField]
		private float _choosePotionMaximumDuration = 10.0f;

		[SerializeField]
		private float _choiceSelectedHoldDuration;

		[Header("Life Potion")]
		[SerializeField]
		private GameplayTag _saveImage;

		[SerializeField]
		private GameplayTag _markForDeathRemovedByLifePotion;

		[SerializeField]
		private GameplayTag _usedLifePotionGameHistoryEntry;

		[Header("Death Potion")]
		[SerializeField]
		private GameplayTag _killImage;

		[SerializeField]
		private GameplayTag _choosePlayerImage;

		[SerializeField]
		private float _choosePlayerDuration;

		[SerializeField]
		private GameplayTag _markForDeathAddedByDeathPotion;

		[SerializeField]
		private GameplayTag _usedDeathPotionGameHistoryEntry;

		private bool _hasLifePotion = true;
		private bool _hasDeathPotion = true;

		private PlayerRef _markedForDeathPlayer;
		private int[] _choices;

		private IEnumerator _endChoosePlayerCoroutine;

		private IEnumerator _endRoleCallAfterTimeCoroutine;
		private float _choosePotionTimeLeft;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
		}

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			bool hasPotions = _hasLifePotion || _hasDeathPotion;

			if (_networkDataManager.PlayerInfos[Player].IsConnected && !hasPotions)
			{
				_gameManager.RPC_DisplayTitle(Player, _noPotionToUseImage.CompactTagId);
			}

			if (!_networkDataManager.PlayerInfos[Player].IsConnected || !hasPotions)
			{
				StartCoroutine(WaitToStopWaitingForPlayer());

				isWakingUp = false;
				return true;
			}

			StartCoroutine(ReveilMarkedForDeathPlayer());
			return isWakingUp = true;
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
				_gameManager.RPC_DisplayTitle(Player, _playerDeadTonightImage.CompactTagId);

				yield return new WaitForSeconds(_playerHighlightHoldDuration * _gameManager.GameSpeedModifier);

				_gameManager.RPC_HideUI(Player);

				yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration);

				_gameManager.RPC_SetPlayerCardHighlightVisible(Player, _markedForDeathPlayer, false);
			}

			OnReveilMarkedForDeathPlayerFinished();
		}

		private void OnReveilMarkedForDeathPlayerFinished()
		{
			_choosePotionTimeLeft = _choosePotionMaximumDuration * _gameManager.GameSpeedModifier;

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
					_gameManager.RPC_DisplayTitle(Player, _noPotionToUseImage.CompactTagId);
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
					_gameManager.RPC_DisplayTitle(Player, _noPotionToUseImage.CompactTagId);
				}

				return false;
			}

			if (!canSavePlayer)
			{
				_choices = new int[] { _killImage.CompactTagId };
			}
			else if (!_hasDeathPotion)
			{
				_choices = new int[] { _saveImage.CompactTagId };
			}
			else
			{
				_choices = new int[] { _saveImage.CompactTagId, _killImage.CompactTagId };
			}

			return _gameManager.MakeChoice(Player,
											_choices,
											_choiceScreen.CompactTagId,
											false,
											_choosePotionTimeLeft,
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

			if (choice == _saveImage.CompactTagId)
			{
				_gameManager.RemoveMarkForDeath(_markedForDeathPlayer, _markForDeathRemovedByLifePotion);
				_hasLifePotion = false;

				_gameHistoryManager.AddEntry(_usedLifePotionGameHistoryEntry,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "WitchPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												},
												new()
												{
													Name = "SavedPlayer",
													Data = _networkDataManager.PlayerInfos[_markedForDeathPlayer].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
											});

				StartCoroutine(RefreshPotions(_choiceSelectedHoldDuration * _gameManager.GameSpeedModifier, true));
				return;
			}
			else if (choice == _killImage.CompactTagId)
			{
				StartCoroutine(ChoosePlayerToKill());
				return;
			}

			EndRoleCall();
		}

		private IEnumerator ChoosePlayerToKill()
		{
			yield return new WaitForSeconds(_choiceSelectedHoldDuration * _gameManager.GameSpeedModifier);

			_gameManager.RPC_HideUI(Player);
			yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration);

			List<PlayerRef> choices = _gameManager.GetAlivePlayers();
			choices.Remove(Player);

			if (_gameManager.SelectPlayers(Player,
											choices,
											_choosePlayerImage.CompactTagId,
											_choosePlayerDuration * _gameManager.GameSpeedModifier,
											true,
											1,
											ChoicePurpose.Kill,
											OnChosePlayers))
			{
				_endChoosePlayerCoroutine = EndChoosePlayer(_choosePlayerDuration * _gameManager.GameSpeedModifier);
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

			_gameManager.StopSelectingPlayers(Player);
			OnChosePlayer(PlayerRef.None);
		}

		private void OnChosePlayers(PlayerRef[] players)
		{
			OnChosePlayer((players == null || players.Length <= 0) ? PlayerRef.None : players[0]);
		}

		private void OnChosePlayer(PlayerRef player)
		{
			StopCoroutine(_endChoosePlayerCoroutine);

			_hasDeathPotion = false;

			if (!player.IsNone)
			{
				_gameManager.AddMarkForDeath(player, _markForDeathAddedByDeathPotion);

				_gameHistoryManager.AddEntry(_usedDeathPotionGameHistoryEntry,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "WitchPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												},
												new()
												{
													Name = "KilledPlayer",
													Data = _networkDataManager.PlayerInfos[player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
											});

				StartCoroutine(HighlightPlayerMarkedForDeath(player));
				return;
			}

			StartCoroutine(RefreshPotions(.0f, true));
		}

		private IEnumerator HighlightPlayerMarkedForDeath(PlayerRef player)
		{
			_gameManager.RPC_HideUI(Player);
			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, player, true);
			yield return new WaitForSeconds(_playerHighlightHoldDuration * _gameManager.GameSpeedModifier);
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

		public override void OnPlayerChanged()
		{
			_hasLifePotion = true;
			_hasDeathPotion = true;
		}

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}
	}
}