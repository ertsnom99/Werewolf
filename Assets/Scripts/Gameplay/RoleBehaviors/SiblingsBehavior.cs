using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;

namespace Werewolf.Gameplay.Role
{
	public class SiblingsBehavior : RoleBehavior
	{
		[Header("Show Siblings")]
		[SerializeField]
		private float _showSiblingsDuration;

		[SerializeField]
		private TitleScreenData _siblingsTitleScreen;

		private readonly HashSet<PlayerRef> _siblings = new();

		private GameManager _gameManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			base.Initialize();

			_gameManager = GameManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			if (_gameManager.NightCount % 2 == 0)
			{
				return isWakingUp = false;
			}

			_gameManager.StartWaitingForPlayersRollCall += OnStartWaitingForPlayersRollCall;

			return isWakingUp = true;
		}

		private void OnStartWaitingForPlayersRollCall()
		{
			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;

			StartCoroutine(ShowSiblings());
		}

		private IEnumerator ShowSiblings()
		{
			_siblings.Clear();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerGameInfos in _gameManager.PlayerGameInfos)
			{
				if (playerGameInfos.Value.IsAwake)
				{
					_siblings.Add(playerGameInfos.Key);
				}
			}

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_SetPlayersCardHighlightVisible(Player, _siblings.ToArray(), true);
				_gameManager.RPC_DisplayTitle(Player, _siblingsTitleScreen.ID.HashCode);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(_siblings.ToArray(), true);
#endif
			yield return new WaitForSeconds(_showSiblingsDuration * _gameManager.GameSpeedModifier);

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
