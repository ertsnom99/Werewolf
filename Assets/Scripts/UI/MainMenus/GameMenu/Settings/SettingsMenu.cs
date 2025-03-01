using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;
using Werewolf.Network;
using System;
using System.Collections.Generic;
using Utilities.GameplayData;
using Werewolf.Data;
using System.Linq;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Werewolf.UI
{
	public class SettingsMenu : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private CanvasGroup _canvasGroup;

		[SerializeField]
		private GameObject[] _containerButtons;

		[SerializeField]
		private LocalizeStringEvent _warningsText;

		[Header("Roles")]
		[SerializeField]
		private DraggableRolesContainer _availableRolesContainer;

		[SerializeField]
		private DraggableRolesContainer _mandatoryRolesContainer;

		[SerializeField]
		private DraggableRolesContainer _optionalRolesContainer;

		[SerializeField]
		private DraggableRole _draggableRolePrefab;

		[Header("Game Speed")]
		[SerializeField]
		private TMP_Dropdown _gameSpeedDropdown;

		[SerializeField]
		private LocalizeDropdown _gameSpeedLocalizeDropdown;

		[SerializeField]
		private LocalizeStringEvent _gameSpeedText;

		public GameSpeed GameSpeed => (GameSpeed)_gameSpeedDropdown.value;

		public event Action<int[], int[]> RolesSetupChanged;
		public event Action<GameSpeed> GameSpeedChanged;

		private GameConfig _gameConfig;
		private PlayerRef _localPlayer;
		private bool _isLocalPlayerLeader;
		private LocalizedStringListVariable _warningsVariables;
		private readonly List<DraggableRole> _draggableRolesPool = new();
		private bool _rolesSetupChanged;

		private GameplayDataManager _gameplayDataManager;
		private NetworkDataManager _networkDataManager;

		public void Initialize(NetworkDataManager networkDataManager, GameConfig gameConfig, PlayerRef localPlayer)
		{
			if (!_gameplayDataManager)
			{
				_gameplayDataManager = GameplayDataManager.Instance;
			}

			_networkDataManager = networkDataManager;
			_gameConfig = gameConfig;
			_localPlayer = localPlayer;
			_isLocalPlayerLeader = false;
			_warningsVariables = (LocalizedStringListVariable)_warningsText.StringReference["Warnings"];

			_availableRolesContainer.Initialize(GameConfig.MAX_ROLE_SETUP_COUNT);
			_mandatoryRolesContainer.Initialize(GameConfig.MAX_PLAYER_COUNT);
			_optionalRolesContainer.Initialize(GameConfig.MAX_ROLE_SETUP_COUNT);

			FillRolesContainers();
			UpdateRolesContainers();
			OnPlayerInfosChanged();

			_networkDataManager.PlayerInfosChanged += OnPlayerInfosChanged;
			_networkDataManager.RolesSetupChanged += OnRolesSetupChanged;
			_networkDataManager.GameSpeedChanged += OnGameSpeedChanged;
			_networkDataManager.GameSetupReadyChanged += OnGameSetupReadyChanged;
		}

		private void FillRolesContainers()
		{
			List<RoleData> roles = GameplayDataManager.Instance.TryGetGameplayData<RoleData>();
			bool placedDefaultRole = false;

			foreach (RoleData role in roles)
			{
				DraggableRole draggableRole = GetDraggableRoleFromPool();
				bool isInfiniteSource = role.CanHaveVariableAmount;
				draggableRole.Initialize(role, isInfiniteSource);

				int siblingIndex;
				
				if (role == _gameConfig.DefaultRole)
				{
					siblingIndex = 0;
					placedDefaultRole = true;
				}
				else
				{
					siblingIndex = isInfiniteSource ? placedDefaultRole ? 1 : 0 : -1;
				}

				_availableRolesContainer.AddRole(draggableRole, siblingIndex: siblingIndex);
				draggableRole.MoveToParent();
			}
		}

		private void LateUpdate()
		{
			if (_rolesSetupChanged)
			{
				UpdateRolesContainers();
				_rolesSetupChanged = false;
			}
		}

		private void UpdateRolesContainers()
		{
			for (int i = _mandatoryRolesContainer.DraggableRoles.Count - 1; i >= 0; i--)
			{
				ReturnToAvailable(_mandatoryRolesContainer.DraggableRoles[i]);
			}

			for (int i = _optionalRolesContainer.DraggableRoles.Count - 1; i >= 0; i--)
			{
				ReturnToAvailable(_optionalRolesContainer.DraggableRoles[i]);
			}

			foreach (NetworkRoleSetup mandatoryRole in _networkDataManager.MandatoryRoles)
			{
				AddToContainer(mandatoryRole, _mandatoryRolesContainer);
			}

			foreach (NetworkRoleSetup optionalRole in _networkDataManager.OptionalRoles)
			{
				AddToContainer(optionalRole, _optionalRolesContainer);
			}

			void AddToContainer(NetworkRoleSetup networkRoleSetup, DraggableRolesContainer draggableRolesContainer)
			{
				if (networkRoleSetup.UseCount == 0 || networkRoleSetup.Pool.Length == 0 || !_gameplayDataManager.TryGetGameplayData(networkRoleSetup.Pool[0], out RoleData roleData))
				{
					return;
				}

				if (roleData.CanHaveVariableAmount)
				{
					DraggableRole draggableRole = GetDraggableRoleFromPool();
					draggableRole.Initialize(roleData, false);
					draggableRolesContainer.AddRole(draggableRole);
					draggableRole.MoveToParent();
				}
				else
				{
					DraggableRole draggableRole = _availableRolesContainer.DraggableRoles.Find(x => x.RoleData == roleData);

					if (draggableRole)
					{
						draggableRolesContainer.AddRole(draggableRole);
						draggableRole.MoveToParent();
					}
				}
			}
		}

		public void ReturnAllFromContainer(DraggableRolesContainer draggableRolesContainer)
		{
			if (!_isLocalPlayerLeader)
			{
				return;
			}

			for (int i = draggableRolesContainer.DraggableRoles.Count - 1; i >= 0; i--)
			{
				ReturnToAvailable(draggableRolesContainer.DraggableRoles[i]);
			}
		}

		private void ReturnToAvailable(DraggableRole draggableRole)
		{
			if (draggableRole.RoleData.CanHaveVariableAmount)
			{
				draggableRole.ReturnToPool();
			}
			else
			{
				_availableRolesContainer.AddRole(draggableRole);
				draggableRole.MoveToParent();
			}
		}

		public void AddAllToContainer(DraggableRolesContainer draggableRolesContainer)
		{
			if (!_isLocalPlayerLeader)
			{
				return;
			}

			for (int i = _availableRolesContainer.DraggableRoles.Count - 1; i >= 0; i--)
			{
				DraggableRole draggableRole = _availableRolesContainer.DraggableRoles[i];

				if (!draggableRole.RoleData.CanHaveVariableAmount)
				{
					draggableRolesContainer.AddRole(draggableRole);
					draggableRole.MoveToParent();
				}
			}
		}

		private void OnPlayerInfosChanged()
		{
			bool isLocalPlayerLeader = _networkDataManager.PlayerInfos.TryGet(_localPlayer, out NetworkPlayerInfo localPlayerInfo) && localPlayerInfo.IsLeader;
			_canvasGroup.blocksRaycasts = isLocalPlayerLeader;

			foreach (GameObject containerButton in _containerButtons)
			{
				containerButton.SetActive(isLocalPlayerLeader);
			}

			_gameSpeedDropdown.gameObject.SetActive(isLocalPlayerLeader);
			_gameSpeedText.gameObject.SetActive(!isLocalPlayerLeader);

			if (_isLocalPlayerLeader == isLocalPlayerLeader)
			{
				return;
			}

			_isLocalPlayerLeader = isLocalPlayerLeader;

			if (isLocalPlayerLeader)
			{
				_mandatoryRolesContainer.DraggableRolesChanged += OnDraggableRolesChanged;
				_mandatoryRolesContainer.DraggableRoleRightClicked += ReturnToAvailable;
				_optionalRolesContainer.DraggableRolesChanged += OnDraggableRolesChanged;
				_optionalRolesContainer.DraggableRoleRightClicked += ReturnToAvailable;
			}
			else
			{
				_mandatoryRolesContainer.DraggableRolesChanged -= OnDraggableRolesChanged;
				_mandatoryRolesContainer.DraggableRoleRightClicked -= ReturnToAvailable;
				_optionalRolesContainer.DraggableRolesChanged -= OnDraggableRolesChanged;
				_optionalRolesContainer.DraggableRoleRightClicked -= ReturnToAvailable;
			}
		}

		private void OnDraggableRolesChanged()
		{
			int[] mandatoryRoleIDs = new int[_mandatoryRolesContainer.DraggableRoles.Count];

			for (int i = 0; i < _mandatoryRolesContainer.DraggableRoles.Count; i++)
			{
				mandatoryRoleIDs[i] = _mandatoryRolesContainer.DraggableRoles[i].RoleData.ID.HashCode;
			}

			int[] optionalRoleIDs = new int[_optionalRolesContainer.DraggableRoles.Count];

			for (int i = 0; i < _optionalRolesContainer.DraggableRoles.Count; i++)
			{
				optionalRoleIDs[i] = _optionalRolesContainer.DraggableRoles[i].RoleData.ID.HashCode;
			}

			RolesSetupChanged?.Invoke(mandatoryRoleIDs, optionalRoleIDs);
		}

		private void OnRolesSetupChanged()
		{
			if (!_isLocalPlayerLeader)
			{
				_rolesSetupChanged = true;
			}
		}

		#region DraggableRole Pool
		private DraggableRole GetDraggableRoleFromPool()
		{
			if (_draggableRolesPool.Count > 0)
			{
				DraggableRole draggableRole = _draggableRolesPool.Last();
				_draggableRolesPool.RemoveAt(_draggableRolesPool.Count - 1);
				draggableRole.gameObject.SetActive(true);
				return draggableRole;
			}
			else
			{
				DraggableRole draggableRole = Instantiate(_draggableRolePrefab, transform);
				draggableRole.SetReturnToPoolDelegate(ReturnDraggableRoleToPool);
				draggableRole.SetGetFromPoolDelegate(GetDraggableRoleFromPool);
				return draggableRole;
			}
		}

		private void ReturnDraggableRoleToPool(DraggableRole draggableRole)
		{
			draggableRole.SetParent(null);
			draggableRole.transform.SetParent(transform);
			draggableRole.gameObject.SetActive(false);
			_draggableRolesPool.Add(draggableRole);
		}
		#endregion

		public void UpdateWarnings(List<LocalizedString> warnings)
		{
			_warningsVariables.Values = warnings;
			_warningsText.RefreshString();
		}

		public void OnChangeGameSpeed(int gameSpeed)
		{
			GameSpeedChanged?.Invoke((GameSpeed)gameSpeed);
		}

		private void OnGameSpeedChanged()
		{
			_gameSpeedDropdown.value = (int)_networkDataManager.GameSpeed;
			_gameSpeedText.StringReference = _gameSpeedLocalizeDropdown.GetLocalizedValue();
		}

		private void OnGameSetupReadyChanged()
		{
			_gameSpeedDropdown.interactable = !_networkDataManager.GameSetupReady;
		}

		public void Cleanup()
		{
			if (_isLocalPlayerLeader)
			{
				_mandatoryRolesContainer.DraggableRolesChanged -= OnDraggableRolesChanged;
				_mandatoryRolesContainer.DraggableRoleRightClicked -= ReturnToAvailable;
				_optionalRolesContainer.DraggableRolesChanged -= OnDraggableRolesChanged;
				_optionalRolesContainer.DraggableRoleRightClicked -= ReturnToAvailable;
			}

			_networkDataManager.PlayerInfosChanged -= OnPlayerInfosChanged;
			_networkDataManager.RolesSetupChanged -= OnRolesSetupChanged;
			_networkDataManager.GameSpeedChanged -= OnGameSpeedChanged;
			_networkDataManager.GameSetupReadyChanged -= OnGameSetupReadyChanged;

			_availableRolesContainer.ReturnAllRoles();
			_mandatoryRolesContainer.ReturnAllRoles();
			_optionalRolesContainer.ReturnAllRoles();
		}
	}
}
