using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Data.Tags;
using Fusion;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;

namespace Werewolf.Gameplay.Role
{
	public class TwoSisters : RoleBehavior
	{
		[Header("Show Sisters")]
		[SerializeField]
		private float _showSistersDuration;

		[SerializeField]
		private GameplayTag _sistersImage;

		private readonly HashSet<PlayerRef> _sisters = new();

		private GameManager _gameManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
		}

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			if (nightCount % 2 == 0)
			{
				return isWakingUp = false;
			}

			_gameManager.StartWaitingForPlayersRollCall += OnStartWaitingForPlayersRollCall;

			return isWakingUp = true;
		}

		private void OnStartWaitingForPlayersRollCall()
		{
			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;

			StartCoroutine(ShowSisters());
		}

		private IEnumerator ShowSisters()
		{
			_sisters.Clear();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerGameInfos in _gameManager.PlayerGameInfos)
			{
				if (playerGameInfos.Value.IsAwake)
				{
					_sisters.Add(playerGameInfos.Key);
				}
			}

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_SetPlayersCardHighlightVisible(Player, _sisters.ToArray(), true);
				_gameManager.RPC_DisplayTitle(Player, _sistersImage.CompactTagId);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(_sisters.ToArray(), true);
#endif
			yield return new WaitForSeconds(_showSistersDuration * _gameManager.GameSpeedModifier);

			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}

		private void OnDestroy()
		{
			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;
		}
	}
}
