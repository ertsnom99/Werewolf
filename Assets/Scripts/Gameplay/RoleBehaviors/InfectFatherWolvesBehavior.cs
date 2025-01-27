using Assets.Scripts.Data.Tags;
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
		private GameplayTag _infectImage;

		[SerializeField]
		private RoleData _werewolfRoleData;

		[SerializeField]
		private GameplayTag _infectedVillagerGameHistoryEntry;

		[SerializeField]
		private GameplayTag _infectedImage;

		private PlayerRef _choosenVillager;
		private IEnumerator _endInfectPromptCoroutine;
		private PlayerRef _infected;

		public override void Initialize()
		{
			base.Initialize();

			if (PlayerGroups.Count < 2)
			{
				Debug.LogError($"{nameof(CupidBehavior)} must have two player groups: the first one for the werewolves and the second one for the villagers");
			}
		}

		public override GameplayTag[] GetCurrentPlayerGroups()
		{
			return new GameplayTag[1] { PlayerGroups[0] };
		}

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

			_gameManager.PromptPlayer(Player, _infectImage.CompactTagId, _commonWerewolfsData.ChoosenVillagerHighlightDuration, OnInfectVillager);

			_endInfectPromptCoroutine = EndInfectPrompt();
			StartCoroutine(_endInfectPromptCoroutine);
		}

		private IEnumerator EndInfectPrompt()
		{
			yield return new WaitForSeconds(_commonWerewolfsData.ChoosenVillagerHighlightDuration);
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

			_gameManager.RemoveMarkForDeath(_choosenVillager, _commonWerewolfsData.MarkForDeath);
			_gameManager.RemovePlayerFromPlayerGroup(_choosenVillager, PlayerGroups[1]);
			RoleBehavior behavior = _gameManager.InstanciateRoleBehavior(_werewolfRoleData);
			_gameManager.AddBehavior(_choosenVillager, behavior);

			_gameHistoryManager.AddEntry(_infectedVillagerGameHistoryEntry,
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

			_gameManager.RPC_DisplayTitle(_choosenVillager, _infectedImage.CompactTagId, fastFade: true);

			Data.GameConfig config = _gameManager.Config;
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
