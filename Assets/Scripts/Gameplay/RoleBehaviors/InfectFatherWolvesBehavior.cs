using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class InfectFatherWolvesBehavior : WerewolfBehavior
	{
		[Header("Infect")]
		[SerializeField]
		private TitleScreenData _infectTitleScreen;

		[SerializeField]
		private PlayerGroupData _villagersPlayerGroup;

		[SerializeField]
		private RoleData _werewolfRoleData;

		[SerializeField]
		private GameHistoryEntryData _infectedVillagerGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _infectedTitleScreen;

		private PlayerRef _choosenVillager;
		private IEnumerator _endInfectPromptCoroutine;
		private PlayerRef _infected;

		protected override void OnVoteEnded(Dictionary<PlayerRef, int> votes)
		{
			base.OnVoteEnded(votes);

			if (!_infected.IsNone)
			{
				return;
			}

			_choosenVillager = votes.Count == 1 ? votes.Keys.ToArray()[0] : PlayerRef.None;

			if (_choosenVillager.IsNone || votes[_choosenVillager] != _voteManager.Voters.Count)
			{
				return;
			}

			_gameManager.PromptPlayer(Player, _infectTitleScreen.ID.HashCode, _commonWerewolvesData.ChoosenVillagerHighlightDuration, OnInfectVillager);

			_endInfectPromptCoroutine = EndInfectPrompt();
			StartCoroutine(_endInfectPromptCoroutine);
		}

		private IEnumerator EndInfectPrompt()
		{
			yield return new WaitForSeconds(_commonWerewolvesData.ChoosenVillagerHighlightDuration);
			_gameManager.StopPromptingPlayer(Player);
		}

		private void OnInfectVillager(PlayerRef player)
		{
			if (_endInfectPromptCoroutine != null)
			{
				StopCoroutine(_endInfectPromptCoroutine);
				_endInfectPromptCoroutine = null;
			}

			_infected = _choosenVillager;

			_gameManager.RemoveMarkForDeath(_choosenVillager, _commonWerewolvesData.MarkForDeath);
			_gameManager.RemovePlayerFromPlayerGroup(_choosenVillager, _villagersPlayerGroup.ID);
			RoleBehavior behavior = _gameManager.InstanciateRoleBehavior(_werewolfRoleData);
			_gameManager.AddBehavior(_choosenVillager, behavior);

			_gameHistoryManager.AddEntry(_infectedVillagerGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "InfectFatherWolvesPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "InfectedVillager",
												Data = _networkDataManager.PlayerInfos[_choosenVillager].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			_gameManager.StartNightCallChangeDelay += OnStartNightCallChangeDelay;
		}

		private void OnStartNightCallChangeDelay()
		{
			_gameManager.StartNightCallChangeDelay -= OnStartNightCallChangeDelay;
			StartCoroutine(DisplayInfected());
		}

		private IEnumerator DisplayInfected()
		{
			if (!_networkDataManager.PlayerInfos[_choosenVillager].IsConnected)
			{
				yield break;
			}

			_gameManager.RPC_DisplayTitle(_choosenVillager, _infectedTitleScreen.ID.HashCode, fastFade: true);

			Data.GameConfig config = _gameManager.GameConfig;
			yield return new WaitForSeconds(config.NightCallChangeDelay - config.UITransitionFastDuration - config.UITransitionNormalDuration);

			if (_networkDataManager.PlayerInfos[_choosenVillager].IsConnected)
			{
				_gameManager.RPC_HideUI(_choosenVillager);
			}
		}

		public override void OnPlayerChanged()
		{
			_infected = PlayerRef.None;
		}

		public override void OnRoleCallDisconnected()
		{
			if (_endInfectPromptCoroutine != null)
			{
				StopCoroutine(_endInfectPromptCoroutine);
				_endInfectPromptCoroutine = null;
			}
		}
	}
}
