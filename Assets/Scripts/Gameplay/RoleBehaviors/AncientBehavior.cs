using System.Collections.Generic;
using Fusion;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;
using Werewolf.Managers;
using static Werewolf.Managers.GameHistoryManager;
using System.Collections;
using static Werewolf.Managers.GameManager;

namespace Werewolf.Gameplay.Role
{
	public class AncientBehavior : RoleBehavior, IGameManagerSubscriber
	{
		[Header("Survived Werewolves")]
		[SerializeField]
		private MarkForDeathData[] _marksForSurvivingWerewolves;

		[SerializeField]
		private GameHistoryEntryData _survivedWerewolvesGameHistoryEntry;

		[Header("Villagers Lost Powers")]
		[SerializeField]
		private MarkForDeathData[] _marksForVillagersLosingPowers;

		[SerializeField]
		private RoleData[] _unaffectVillagerRoles;

		[SerializeField]
		private GameHistoryEntryData _villagersLostPowersGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _villagersLostPowersTitleScreen;

		[SerializeField]
		private float _villagersLostPowersTitleDuration;

		private bool _survivedWerewolves;
		private IEnumerator _villagersLostPowersTimerCoroutine;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			base.Initialize();

			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.MarkForDeathAdded += OnMarkForDeathAdded;
			_gameManager.Subscribe(this);
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		private void OnMarkForDeathAdded(PlayerRef player, MarkForDeathData markForDeath)
		{
			if (!CanUsePower || player != Player || _survivedWerewolves || !_marksForSurvivingWerewolves.Contains(markForDeath))
			{
				return;
			}

			_gameManager.RemoveMarkForDeath(Player, markForDeath);

			_gameHistoryManager.AddEntry(_survivedWerewolvesGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "Player",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
										});

			_survivedWerewolves = true;
			_gameManager.MarkForDeathAdded -= OnMarkForDeathAdded;
		}

		void IGameManagerSubscriber.OnPlayerDied(PlayerRef deadPlayer, MarkForDeathData markForDeath)
		{
			if (!CanUsePower || Player != deadPlayer || !_marksForVillagersLosingPowers.Contains(markForDeath))
			{
				return;
			}

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				foreach (RoleBehavior behavior in playerInfo.Value.Behaviors)
				{
					if (behavior.PrimaryRoleType == PrimaryRoleType.Villager && !_unaffectVillagerRoles.Any(role => role.ID.HashCode == behavior.RoleID.HashCode))
					{
						behavior.CanUsePower = false;
					}
				}
			}

			IndexedReservedRoles[] allReservedRoles = _gameManager.GetAllReservedRoles();

			foreach (IndexedReservedRoles reservedRoles in allReservedRoles)
			{
				for (int i = 0; i < reservedRoles.Roles.Length; i++)
				{
					RoleBehavior behavior = reservedRoles.Behaviors[i];

					if (behavior.PrimaryRoleType == PrimaryRoleType.Villager && !_unaffectVillagerRoles.Any(role => role.ID.HashCode == behavior.RoleID.HashCode))
					{
						behavior.CanUsePower = false;
					}
				}
			}

			_gameHistoryManager.AddEntry(_villagersLostPowersGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "Player",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			_gameManager.WaitForPlayer(Player);

			_villagersLostPowersTimerCoroutine = DisplayVillagersLostPowers();
			StartCoroutine(_villagersLostPowersTimerCoroutine);
		}

		private IEnumerator DisplayVillagersLostPowers()
		{
			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected && playerInfo.Key != Player)
				{
					_gameManager.RPC_DisplayTitle(playerInfo.Key, _villagersLostPowersTitleScreen.ID.HashCode);
				}
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.DisplayTitle(_villagersLostPowersTitleScreen.ID.HashCode);
#endif
			yield return new WaitForSeconds(_villagersLostPowersTitleDuration * _gameManager.GameSpeedModifier);

			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(_gameManager.GameConfig.UITransitionNormalDuration);

			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.MarkForDeathAdded -= OnMarkForDeathAdded;
			_gameManager.Unsubscribe(this);
		}
	}
}