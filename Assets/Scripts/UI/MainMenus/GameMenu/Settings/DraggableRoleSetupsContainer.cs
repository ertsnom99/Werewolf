using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class DraggableRoleSetupsContainer : MonoBehaviour, IDropHandler
	{
		[field:Header("UI")]
		[field:SerializeField]
		public Transform Grid { get; private set; }

		private int _maxDraggableRoleSetups;
		private bool _multiRoleAllowed;
		private bool _isDragEnable;

		public event Action DraggableRoleSetupsChanged;
		public event Action<bool> DraggableRoleSetupDragChanged;
		public event Action<RoleData, Vector3> DraggableRoleSetupMiddleClicked;
		public event Action<DraggableRoleSetup, int> DraggableRoleSetupRightClicked;

		public List<DraggableRoleSetup> DraggableRoleSetups { get; private set; }

		public void Initialize(int maxDraggableRoleSetups, bool multiRoleAllowed)
		{
			DraggableRoleSetups = new();
			_maxDraggableRoleSetups = maxDraggableRoleSetups;
			_multiRoleAllowed = multiRoleAllowed;
		}

		public void EnableDrag(bool enable)
		{
			_isDragEnable = enable;

			foreach (DraggableRoleSetup draggableRoleSetup in DraggableRoleSetups)
			{
				draggableRoleSetup.EnableDrag(enable);
			}
		}

		public void EnableUseCountButtons(bool enable)
		{
			foreach (DraggableRoleSetup draggableRoleSetup in DraggableRoleSetups)
			{
				draggableRoleSetup.EnableUseCountButtons(enable);
			}
		}

		public void EnablePointerDown(bool enable)
		{
			foreach (DraggableRoleSetup draggableRoleSetup in DraggableRoleSetups)
			{
				draggableRoleSetup.EnablePointerDown(enable);
			}
		}

		public void OnDrop(PointerEventData eventData)
		{
			if (!_isDragEnable || eventData.button != PointerEventData.InputButton.Left)
			{
				return;
			}

			DraggableRoleSetup draggedRoleSetup = eventData.pointerDrag.GetComponent<DraggableRoleSetup>();

			if (!draggedRoleSetup || !draggedRoleSetup.DraggedRoleSetup)
			{
				return;
			}

			DraggableRoleSetup droppedOnRoleSetup = null;
			
			if (_multiRoleAllowed)
			{
				List<RaycastResult> results = new();
				EventSystem.current.RaycastAll(eventData, results);

				foreach (RaycastResult result in results)
				{
					if (result.gameObject.CompareTag("DraggableRoleSetup"))
					{
						droppedOnRoleSetup = result.gameObject.GetComponent<DraggableRoleSetup>();
						break;
					}
				}
			}

			if (DraggableRoleSetups.Contains(draggedRoleSetup.DraggedRoleSetup) && (!droppedOnRoleSetup || draggedRoleSetup == droppedOnRoleSetup))
			{
				return;
			}

			if (droppedOnRoleSetup)
			{
				if (droppedOnRoleSetup.AddRoleData(draggedRoleSetup.DraggedRoleSetup))
				{
					if (draggedRoleSetup.IsMultiRole)
					{
						draggedRoleSetup.RemoveRoleData(draggedRoleSetup.SelectedMultiRoleDataPoolIndex);
					}

					OnDraggableRoleSetupDragChanged(false);
					draggedRoleSetup.DraggedRoleSetup.SetParent(null);
				}
			}
			else
			{
				if (ContainsInfiniteSource(draggedRoleSetup.DraggedRoleSetup.RoleDataPool[0]))
				{
					if (draggedRoleSetup.IsMultiRole)
					{
						draggedRoleSetup.RemoveRoleData(draggedRoleSetup.SelectedMultiRoleDataPoolIndex);
					}

					OnDraggableRoleSetupDragChanged(false);
					draggedRoleSetup.DraggedRoleSetup.SetParent(null);
				}
				else
				{
					AddRole(draggedRoleSetup.DraggedRoleSetup);
				}
			}
		}

		private bool ContainsInfiniteSource(RoleData roleData)
		{
			foreach (DraggableRoleSetup draggableRoleSetup in DraggableRoleSetups)
			{
				if (draggableRoleSetup.RoleDataPool[0] == roleData && draggableRoleSetup.IsInfiniteSource)
				{
					return true;
				}
			}

			return false;
		}

		public void AddRole(DraggableRoleSetup draggableRoleSetup, int siblingIndex = -1)
		{
			if (GetTotalDraggableRoleUseCount() == _maxDraggableRoleSetups)
			{
				return;
			}

			DraggableRoleSetups.Add(draggableRoleSetup);
			draggableRoleSetup.SetParent(Grid, siblingIndex > -1 ? siblingIndex : Grid.childCount);
			draggableRoleSetup.ParentChanged += OnLeavedContainer;
			draggableRoleSetup.RoleSetupChanged += OnDraggableRoleSetupChanged;
			draggableRoleSetup.DragChanged += OnDraggableRoleSetupDragChanged;
			draggableRoleSetup.MiddleClicked += OnDraggableRoleSetupMiddleClicked;
			draggableRoleSetup.RightClicked += OnDraggableRoleSetupRightClicked;
			DraggableRoleSetupsChanged?.Invoke();
		}

		private int GetTotalDraggableRoleUseCount()
		{
			int amount = 0;

			foreach (DraggableRoleSetup draggableRoleSetup in DraggableRoleSetups)
			{
				amount += draggableRoleSetup.UseCount;
			}

			return amount;
		}

		private void OnLeavedContainer(DraggableRoleSetup draggableRoleSetup)
		{
			draggableRoleSetup.ParentChanged -= OnLeavedContainer;
			draggableRoleSetup.RoleSetupChanged -= OnDraggableRoleSetupChanged;
			draggableRoleSetup.DragChanged -= OnDraggableRoleSetupDragChanged;
			draggableRoleSetup.MiddleClicked -= OnDraggableRoleSetupMiddleClicked;
			draggableRoleSetup.RightClicked -= OnDraggableRoleSetupRightClicked;
			DraggableRoleSetups.Remove(draggableRoleSetup);
			DraggableRoleSetupsChanged?.Invoke();
		}

		private void OnDraggableRoleSetupChanged()
		{
			DraggableRoleSetupsChanged?.Invoke();
		}

		private void OnDraggableRoleSetupDragChanged(bool isDragging)
		{
			DraggableRoleSetupDragChanged?.Invoke(isDragging);
		}

		private void OnDraggableRoleSetupMiddleClicked(RoleData roleData, Vector3 position)
		{
			DraggableRoleSetupMiddleClicked?.Invoke(roleData, position);
		}

		private void OnDraggableRoleSetupRightClicked(DraggableRoleSetup draggableRoleSetup, int RoleDataPoolIndex)
		{
			DraggableRoleSetupRightClicked?.Invoke(draggableRoleSetup, RoleDataPoolIndex);
		}

		public void ReturnAllRoles()
		{
			for (int i = DraggableRoleSetups.Count - 1; i >= 0; i--)
			{
				DraggableRoleSetups[i].ReturnToPool();
			}
		}
	}
}
