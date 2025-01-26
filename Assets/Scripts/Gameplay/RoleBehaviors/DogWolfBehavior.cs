using System.Collections;
using Assets.Scripts.Data.Tags;
using UnityEngine;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class DogWolfBehavior : WerewolfBehavior
	{
		[Header("Group Choice")]
		[SerializeField]
		private GameplayTag _choiceScreen;

		[SerializeField]
		private GameplayTag _werewolvesImage;

		[SerializeField]
		private GameplayTag _choseWerewolvesGroupGameHistoryEntry;

		[SerializeField]
		private GameplayTag _villagersImage;

		[SerializeField]
		private GameplayTag _choseVillagersGroupGameHistoryEntry;

		[SerializeField]
		private float _chooseGroupMaximumDuration;

		private bool _choseGroup;
		private bool _isWerewolf;

		private int[] _choices;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		public override void Initialize()
		{
			base.Initialize();

			if (PlayerGroups.Count < 2)
			{
				Debug.LogError($"{nameof(DogWolfBehavior)} must have two player groups: the first one for the villagers and the second one for the werewolves");
			}

			if (NightPriorities.Count < 2)
			{
				Debug.LogError($"{nameof(DogWolfBehavior)} must have two night priorities: the first one to choose if he is a werewolf and the second one to vote with the werewolves");
			}
		}

		public override GameplayTag[] GetCurrentPlayerGroups()
		{
			return new GameplayTag[1] { PlayerGroups[_choseGroup && _isWerewolf ? 1 : 0] };
		}

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			if (priorityIndex == NightPriorities[0].index && !_choseGroup)
			{
				isWakingUp = ChooseGroup();
				return true;
			}
			else if (priorityIndex == NightPriorities[1].index && _choseGroup && _isWerewolf)
			{
				VoteForVillager();
				return isWakingUp = true;
			}

			return isWakingUp = false;
		}

		private bool ChooseGroup()
		{
			_choices = new int[] { _werewolvesImage.CompactTagId, _villagersImage.CompactTagId };

			if (!_gameManager.MakeChoice(Player,
										_choices,
										_choiceScreen.CompactTagId,
										true,
										_chooseGroupMaximumDuration * _gameManager.GameSpeedModifier,
										OnGroupSelected))
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
				return false;
			}

			_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
			StartCoroutine(_endRoleCallAfterTimeCoroutine);

			return true;
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			SelectGroup(-1);

			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnGroupSelected(int choiceIndex)
		{
			if (_endRoleCallAfterTimeCoroutine != null)
			{
				StopCoroutine(_endRoleCallAfterTimeCoroutine);
				_endRoleCallAfterTimeCoroutine = null;
			}

			_gameManager.StopChoosing(Player);
			SelectGroup(choiceIndex);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator EndRoleCallAfterTime()
		{
			float timeLeft = _chooseGroupMaximumDuration * _gameManager.GameSpeedModifier;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			SelectGroup(-1);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private void SelectGroup(int choiceIndex)
		{
			_choseGroup = true;

			int choice = _choices[choiceIndex <= -1 ? Random.Range(0, _choices.Length) : choiceIndex];

			if (choice == _werewolvesImage.CompactTagId)
			{
				_isWerewolf = true;

				_gameManager.RemovePlayerFromPlayerGroup(Player, PlayerGroups[0]);
				_gameManager.AddPlayerToPlayerGroup(Player, PlayerGroups[1]);

				_gameHistoryManager.AddEntry(_choseWerewolvesGroupGameHistoryEntry,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "DogWolfPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
											});
			}
			else if (choice == _villagersImage.CompactTagId)
			{
				_gameHistoryManager.AddEntry(_choseVillagersGroupGameHistoryEntry,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "DogWolfPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
											});
			}
		}

		public override void OnPlayerChanged()
		{
			_choseGroup = false;
			_isWerewolf = false;
		}

		public override void OnRoleCallDisconnected()
		{
			if (_endRoleCallAfterTimeCoroutine == null)
			{
				return;
			}

			StopCoroutine(_endRoleCallAfterTimeCoroutine);
			_endRoleCallAfterTimeCoroutine = null;

			OnGroupSelected(-1);
		}
	}
}
