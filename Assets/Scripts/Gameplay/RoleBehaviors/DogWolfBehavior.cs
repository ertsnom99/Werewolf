using System.Collections;
using UnityEngine;
using Utilities.GameplayData;
using Werewolf.Data;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class DogWolfBehavior : WerewolfBehavior
	{
		[Header("Group Choice")]
		[SerializeField]
		private ChoiceScreenData _choiceScreen;

		[SerializeField]
		private TitleScreenData _werewolvesTitleScreen;

		[SerializeField]
		private GameHistoryEntryData _choseWerewolvesGroupGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _villagersTitleScreen;

		[SerializeField]
		private GameHistoryEntryData _choseVillagersGroupGameHistoryEntry;

		[SerializeField]
		private float _chooseGroupMaximumDuration;

		private bool _choseGroup;
		private bool _isWerewolf;
		private int[] _choiceTitleIDs;
		private IEnumerator _endRoleCallAfterTimeCoroutine;

		public override void Initialize()
		{
			base.Initialize();

			if (PlayerGroupIDs.Count < 2)
			{
				Debug.LogError($"{nameof(DogWolfBehavior)} must have two player groups: the first one for the villagers and the second one for the werewolves");
			}

			if (NightPriorities.Count < 2)
			{
				Debug.LogError($"{nameof(DogWolfBehavior)} must have two night priorities: the first one to choose if he is a werewolf and the second one to vote with the werewolves");
			}
		}

		public override UniqueID[] GetCurrentPlayerGroupIDs()
		{
			return new UniqueID[1] { PlayerGroupIDs[_choseGroup && _isWerewolf ? 1 : 0] };
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
			_choiceTitleIDs = new int[] { _werewolvesTitleScreen.ID.HashCode, _villagersTitleScreen.ID.HashCode };

			if (!_gameManager.MakeChoice(Player,
										_choiceTitleIDs,
										_choiceScreen.ID.HashCode,
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

			int choiceTitleID = _choiceTitleIDs[choiceIndex <= -1 ? Random.Range(0, _choiceTitleIDs.Length) : choiceIndex];

			if (choiceTitleID == _werewolvesTitleScreen.ID.HashCode)
			{
				_isWerewolf = true;

				_gameManager.RemovePlayerFromPlayerGroup(Player, PlayerGroupIDs[0]);
				_gameManager.AddPlayerToPlayerGroup(Player, PlayerGroupIDs[1]);

				_gameHistoryManager.AddEntry(_choseWerewolvesGroupGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "DogWolfPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
											});
			}
			else if (choiceTitleID == _villagersTitleScreen.ID.HashCode)
			{
				_gameHistoryManager.AddEntry(_choseVillagersGroupGameHistoryEntry.ID,
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
