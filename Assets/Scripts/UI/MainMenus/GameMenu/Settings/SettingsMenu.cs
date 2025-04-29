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
using UnityEngine.UI;

namespace Werewolf.UI
{
	public class SettingsMenu : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private GameObject[] _containerButtons;

		[SerializeField]
		private LocalizeStringEvent _warningsText;

		[Header("Roles")]
		[SerializeField]
		private DraggableRoleSetupsContainer _availableRolesContainer;

		[SerializeField]
		private DraggableRoleSetupsContainer _mandatoryRolesContainer;

		[SerializeField]
		private DraggableRoleSetupsContainer _optionalRolesContainer;

		[SerializeField]
		private DraggableRoleSetup _draggableRoleSetupPrefab;

		[SerializeField]
		private RoleDescriptionPopup _roleDescriptionPopup;

		[Header("Game Speed")]
		[SerializeField]
		private TMP_Dropdown _gameSpeedDropdown;

		[SerializeField]
		private LocalizeDropdown _gameSpeedLocalizeDropdown;

		[SerializeField]
		private LocalizeStringEvent _gameSpeedText;

		[Header("Play Intro")]
		[SerializeField]
		private Toggle _playIntroToggle;

		public GameSpeed GameSpeed => (GameSpeed)_gameSpeedDropdown.value;

		public event Action<SerializableRoleSetups> RolesSetupChanged;
		public event Action<GameSpeed> GameSpeedChanged;
		public event Action<bool> PlayIntroChanged;

		private GameConfig _gameConfig;
		private PlayerRef _localPlayer;
		private bool _isLocalPlayerLeader;
		private readonly List<DraggableRoleSetup> _draggableRoleSetupsPool = new();
		private LocalizedStringListVariable _warningsVariables;
		private bool _draggableRoleSetupsChanged;
		private bool _networkRoleSetupsChanged;

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

			_availableRolesContainer.Initialize(GameConfig.MAX_ROLE_SETUP_COUNT, false);
			_mandatoryRolesContainer.Initialize(GameConfig.MAX_PLAYER_COUNT, true);
			_optionalRolesContainer.Initialize(GameConfig.MAX_ROLE_SETUP_COUNT, true);

			FillRolesContainers();
			UpdateRolesContainers();
			OnPlayerInfosChanged();
			OnGameSpeedChanged();
			OnPlayIntroChanged();

			_availableRolesContainer.DraggableRoleSetupDragChanged += OnDraggableRoleSetupDragChanged;
			_availableRolesContainer.DraggableRoleSetupMiddleClicked += OnDraggableRoleSetupMiddleClicked;
			_mandatoryRolesContainer.DraggableRoleSetupDragChanged += OnDraggableRoleSetupDragChanged;
			_mandatoryRolesContainer.DraggableRoleSetupMiddleClicked += OnDraggableRoleSetupMiddleClicked;
			_optionalRolesContainer.DraggableRoleSetupDragChanged += OnDraggableRoleSetupDragChanged;
			_optionalRolesContainer.DraggableRoleSetupMiddleClicked += OnDraggableRoleSetupMiddleClicked;

			_networkDataManager.PlayerInfosChanged += OnPlayerInfosChanged;
			_networkDataManager.RoleSetupsChanged += OnRoleSetupsChanged;
			_networkDataManager.GameSpeedChanged += OnGameSpeedChanged;
			_networkDataManager.PlayIntroChanged += OnPlayIntroChanged;
			_networkDataManager.GameSetupReadyChanged += OnGameSetupReadyChanged;
		}

		private void FillRolesContainers()
		{
			List<RoleData> roleDatas = GameplayDataManager.Instance.TryGetGameplayData<RoleData>();
			bool placedDefaultRole = false;

			foreach (RoleData roleData in roleDatas)
			{
				int siblingIndex;

				if (roleData == _gameConfig.DefaultRole)
				{
					siblingIndex = 0;
					placedDefaultRole = true;
				}
				else
				{
					siblingIndex = roleData.CanHaveVariableAmount ? placedDefaultRole ? 1 : 0 : -1;
				}

				AddToAvailable(roleData, siblingIndex);
			}
		}

		private void LateUpdate()
		{
			if (_draggableRoleSetupsChanged)
			{
				RolesSetupChanged?.Invoke(new() {
											MandatoryRoles = ConvertToSerializableRoleSetups(_mandatoryRolesContainer),
											OptionalRoles = ConvertToSerializableRoleSetups(_optionalRolesContainer)
										});
				_draggableRoleSetupsChanged = false;
			}

			if (_networkRoleSetupsChanged)
			{
				UpdateRolesContainers();
				_networkRoleSetupsChanged = false;
			}
		}
		
		private SerializableRoleSetup[] ConvertToSerializableRoleSetups(DraggableRoleSetupsContainer draggableRoleSetupsContainer)
		{
			SerializableRoleSetup[] serializableRoleSetup = new SerializableRoleSetup[draggableRoleSetupsContainer.DraggableRoleSetups.Count];

			for (int i = 0; i < draggableRoleSetupsContainer.DraggableRoleSetups.Count; i++)
			{
				DraggableRoleSetup draggableRoleSetup = draggableRoleSetupsContainer.DraggableRoleSetups[i];

				serializableRoleSetup[i].Pool = new int[draggableRoleSetup.RoleDataPool.Count];

				for (int j = 0; j < draggableRoleSetup.RoleDataPool.Count; j++)
				{
					serializableRoleSetup[i].Pool[j] = draggableRoleSetup.RoleDataPool[j].ID.HashCode;
				}

				serializableRoleSetup[i].UseCount = draggableRoleSetup.UseCount;
			}

			return serializableRoleSetup;
		}

		private void UpdateRolesContainers()
		{
			for (int i = _mandatoryRolesContainer.DraggableRoleSetups.Count - 1; i >= 0; i--)
			{
				ReturnToAvailable(_mandatoryRolesContainer.DraggableRoleSetups[i]);
			}

			for (int i = _optionalRolesContainer.DraggableRoleSetups.Count - 1; i >= 0; i--)
			{
				ReturnToAvailable(_optionalRolesContainer.DraggableRoleSetups[i]);
			}

			foreach (NetworkRoleSetup mandatoryRole in _networkDataManager.MandatoryRoles)
			{
				AddToContainer(mandatoryRole, _mandatoryRolesContainer);
			}

			foreach (NetworkRoleSetup optionalRole in _networkDataManager.OptionalRoles)
			{
				AddToContainer(optionalRole, _optionalRolesContainer);
			}
		}

		private void AddToContainer(NetworkRoleSetup networkRoleSetup, DraggableRoleSetupsContainer draggableRoleSetupsContainer)
		{
			if (networkRoleSetup.UseCount == 0 || networkRoleSetup.Pool.Length == 0)
			{
				return;
			}

			List<RoleData> roleDatas = new();

			foreach (int roleID in networkRoleSetup.Pool)
			{
				if (roleID == 0)
				{
					break;
				}

				if (!_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
				{
					Debug.LogError($"Could not find the role {roleID}");
					return;
				}

				roleDatas.Add(roleData);

				DraggableRoleSetup availableDraggableRoleSetup = _availableRolesContainer.DraggableRoleSetups.Find(x => !x.IsInfiniteSource && x.RoleDataPool[0] == roleData);

				if (availableDraggableRoleSetup)
				{
					ReturnDraggableRoleSetupToPool(availableDraggableRoleSetup);
				}
			}

			DraggableRoleSetup draggableRoleSetup = GetDraggableRoleSetupFromPool();
			draggableRoleSetup.EnableDrag(_isLocalPlayerLeader);
			draggableRoleSetup.EnableUseCountButtons(_isLocalPlayerLeader);
			draggableRoleSetup.Initialize(roleDatas.ToArray(), networkRoleSetup.UseCount, false);
			draggableRoleSetupsContainer.AddRole(draggableRoleSetup);
			draggableRoleSetup.MoveToParent();
		}

		private void AddToAvailable(RoleData roleData, int siblingIndex = -1)
		{
			DraggableRoleSetup draggableRoleSetup = GetDraggableRoleSetupFromPool();
			RoleData[] roleDatas;
			bool isInfiniteSource = roleData.CanHaveVariableAmount;

			if (isInfiniteSource)
			{
				roleDatas = new[] { roleData };
			}
			else
			{
				roleDatas = new RoleData[roleData.MandatoryAmount];

				for (int i = 0; i < roleData.MandatoryAmount; i++)
				{
					roleDatas[i] = roleData;
				}
			}

			draggableRoleSetup.EnableDrag(_isLocalPlayerLeader);
			draggableRoleSetup.EnableUseCountButtons(_isLocalPlayerLeader);
			draggableRoleSetup.Initialize(roleDatas, isInfiniteSource ? 1 : roleData.MandatoryAmount, isInfiniteSource);

			_availableRolesContainer.AddRole(draggableRoleSetup, siblingIndex);
			draggableRoleSetup.MoveToParent();
		}

		private void ReturnToAvailable(DraggableRoleSetup draggableRoleSetup)
		{
			foreach (RoleData roleData in draggableRoleSetup.RoleDataPool)
			{
				if (!_availableRolesContainer.DraggableRoleSetups.Find(x => x.RoleDataPool[0] == roleData))
				{
					AddToAvailable(roleData);
				}
			}

			ReturnDraggableRoleSetupToPool(draggableRoleSetup);
		}

		public void ReturnAllFromContainer(DraggableRoleSetupsContainer draggableRoleSetupsContainer)
		{
			if (!_isLocalPlayerLeader)
			{
				return;
			}

			for (int i = draggableRoleSetupsContainer.DraggableRoleSetups.Count - 1; i >= 0; i--)
			{
				ReturnToAvailable(draggableRoleSetupsContainer.DraggableRoleSetups[i]);
			}
		}

		public void AddAllToContainer(DraggableRoleSetupsContainer draggableRoleSetupsContainer)
		{
			if (!_isLocalPlayerLeader)
			{
				return;
			}

			for (int i = _availableRolesContainer.DraggableRoleSetups.Count - 1; i >= 0; i--)
			{
				DraggableRoleSetup draggableRoleSetup = _availableRolesContainer.DraggableRoleSetups[i];

				if (!draggableRoleSetup.IsInfiniteSource)
				{
					draggableRoleSetupsContainer.AddRole(draggableRoleSetup);
					draggableRoleSetup.MoveToParent();
				}
			}
		}

		private void OnPlayerInfosChanged()
		{
			bool isLocalPlayerLeader = _networkDataManager.PlayerInfos.TryGet(_localPlayer, out NetworkPlayerInfo localPlayerInfo) && localPlayerInfo.IsLeader;

			if (_isLocalPlayerLeader == isLocalPlayerLeader)
			{
				return;
			}

			_isLocalPlayerLeader = isLocalPlayerLeader;

			UpdateVisual(isLocalPlayerLeader);

			if (isLocalPlayerLeader)
			{
				_mandatoryRolesContainer.DraggableRoleSetupsChanged += OnDraggableRoleSetupsChanged;
				_mandatoryRolesContainer.DraggableRoleSetupRightClicked += OnDraggableRoleSetupRightClicked;
				_optionalRolesContainer.DraggableRoleSetupsChanged += OnDraggableRoleSetupsChanged;
				_optionalRolesContainer.DraggableRoleSetupRightClicked += OnDraggableRoleSetupRightClicked;
			}
			else
			{
				_mandatoryRolesContainer.DraggableRoleSetupsChanged -= OnDraggableRoleSetupsChanged;
				_mandatoryRolesContainer.DraggableRoleSetupRightClicked -= OnDraggableRoleSetupRightClicked;
				_optionalRolesContainer.DraggableRoleSetupsChanged -= OnDraggableRoleSetupsChanged;
				_optionalRolesContainer.DraggableRoleSetupRightClicked -= OnDraggableRoleSetupRightClicked;
			}
		}

		private void UpdateVisual(bool isLocalPlayerLeader)
		{
			foreach (GameObject containerButton in _containerButtons)
			{
				containerButton.SetActive(isLocalPlayerLeader);
			}

			_gameSpeedDropdown.gameObject.SetActive(isLocalPlayerLeader);
			_gameSpeedText.gameObject.SetActive(!isLocalPlayerLeader);
			_gameSpeedDropdown.RefreshShownValue();

			_playIntroToggle.interactable = isLocalPlayerLeader;

			EnableDraggableRoleSetups(isLocalPlayerLeader);
		}

		private void EnableDraggableRoleSetups(bool enable)
		{
			foreach (DraggableRoleSetup draggableRoleSetup in _draggableRoleSetupsPool)
			{
				draggableRoleSetup.EnableDrag(enable);
				draggableRoleSetup.EnableUseCountButtons(enable);
			}

			_availableRolesContainer.EnableDrag(enable);
			_availableRolesContainer.EnableUseCountButtons(enable);
			_mandatoryRolesContainer.EnableDrag(enable);
			_mandatoryRolesContainer.EnableUseCountButtons(enable);
			_optionalRolesContainer.EnableDrag(enable);
			_optionalRolesContainer.EnableUseCountButtons(enable);
		}

		private void OnDraggableRoleSetupsChanged()
		{
			_draggableRoleSetupsChanged = true;
		}

		private void OnRoleSetupsChanged()
		{
			if (!_isLocalPlayerLeader)
			{
				_networkRoleSetupsChanged = true;
			}
		}

		#region DraggableRoleSetup Pool
		private DraggableRoleSetup GetDraggableRoleSetupFromPool()
		{
			if (_draggableRoleSetupsPool.Count > 0)
			{
				DraggableRoleSetup draggableRoleSetup = _draggableRoleSetupsPool.Last();
				_draggableRoleSetupsPool.RemoveAt(_draggableRoleSetupsPool.Count - 1);
				draggableRoleSetup.gameObject.SetActive(true);
				return draggableRoleSetup;
			}
			else
			{
				DraggableRoleSetup draggableRoleSetup = Instantiate(_draggableRoleSetupPrefab, transform);
				draggableRoleSetup.SetReturnToPoolDelegate(ReturnDraggableRoleSetupToPool);
				draggableRoleSetup.SetGetFromPoolDelegate(GetDraggableRoleSetupFromPool);
				return draggableRoleSetup;
			}
		}

		private void ReturnDraggableRoleSetupToPool(DraggableRoleSetup draggableRoleSetup)
		{
			draggableRoleSetup.SetParent(null);
			draggableRoleSetup.transform.SetParent(transform);
			draggableRoleSetup.gameObject.SetActive(false);
			_draggableRoleSetupsPool.Add(draggableRoleSetup);
		}
		#endregion

		private void OnDraggableRoleSetupDragChanged(bool isDragging)
		{
			_availableRolesContainer.EnablePointerDown(!isDragging);
			_mandatoryRolesContainer.EnablePointerDown(!isDragging);
			_optionalRolesContainer.EnablePointerDown(!isDragging);
		}

		private void OnDraggableRoleSetupMiddleClicked(RoleData roleData, Vector3 position)
		{
			_roleDescriptionPopup.Display(roleData, position);
		}

		private void OnDraggableRoleSetupRightClicked(DraggableRoleSetup draggableRoleSetup, int RoleDataPoolIndex)
		{
			if (RoleDataPoolIndex == -1)
			{
				ReturnToAvailable(draggableRoleSetup);
			}
			else if (draggableRoleSetup.IsMultiRole && draggableRoleSetup.RoleDataPool.Count > RoleDataPoolIndex)
			{
				RoleData roleData = draggableRoleSetup.RoleDataPool[RoleDataPoolIndex];

				if (!_availableRolesContainer.DraggableRoleSetups.Find(x => x.RoleDataPool[0] == roleData))
				{
					AddToAvailable(roleData);
				}

				draggableRoleSetup.RemoveRoleData(RoleDataPoolIndex);
			}
		}

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

		public void OnChangePlayIntro(bool playIntro)
		{
			PlayIntroChanged?.Invoke(playIntro);
		}

		private void OnPlayIntroChanged()
		{
			_playIntroToggle.isOn = _networkDataManager.PlayIntro;
		}

		private void OnGameSetupReadyChanged()
		{
			_gameSpeedDropdown.interactable = !_networkDataManager.GameSetupReady;
			EnableDraggableRoleSetups(false);
		}

		public void Cleanup()
		{
			if (_isLocalPlayerLeader)
			{
				UpdateVisual(isLocalPlayerLeader: false);

				_mandatoryRolesContainer.DraggableRoleSetupsChanged -= OnDraggableRoleSetupsChanged;
				_mandatoryRolesContainer.DraggableRoleSetupRightClicked -= OnDraggableRoleSetupRightClicked;
				_optionalRolesContainer.DraggableRoleSetupsChanged -= OnDraggableRoleSetupsChanged;
				_optionalRolesContainer.DraggableRoleSetupRightClicked -= OnDraggableRoleSetupRightClicked;
			}

			_availableRolesContainer.DraggableRoleSetupDragChanged -= OnDraggableRoleSetupDragChanged;
			_availableRolesContainer.DraggableRoleSetupMiddleClicked -= OnDraggableRoleSetupMiddleClicked;
			_mandatoryRolesContainer.DraggableRoleSetupDragChanged -= OnDraggableRoleSetupDragChanged;
			_mandatoryRolesContainer.DraggableRoleSetupMiddleClicked -= OnDraggableRoleSetupMiddleClicked;
			_optionalRolesContainer.DraggableRoleSetupDragChanged -= OnDraggableRoleSetupDragChanged;
			_optionalRolesContainer.DraggableRoleSetupMiddleClicked -= OnDraggableRoleSetupMiddleClicked;

			_networkDataManager.PlayerInfosChanged -= OnPlayerInfosChanged;
			_networkDataManager.RoleSetupsChanged -= OnRoleSetupsChanged;
			_networkDataManager.GameSpeedChanged -= OnGameSpeedChanged;
			_networkDataManager.PlayIntroChanged -= OnPlayIntroChanged;
			_networkDataManager.GameSetupReadyChanged -= OnGameSetupReadyChanged;

			_availableRolesContainer.ReturnAllRoles();
			_mandatoryRolesContainer.ReturnAllRoles();
			_optionalRolesContainer.ReturnAllRoles();
		}
	}
}
