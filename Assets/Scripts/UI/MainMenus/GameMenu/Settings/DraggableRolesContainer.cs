using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class DraggableRolesContainer : MonoBehaviour, IDropHandler
	{
		[field:Header("UI")]
		[field:SerializeField]
		public Transform Grid { get; private set; }

		private int _maxDraggableRoles;

		public event Action DraggableRolesChanged;
		public event Action<DraggableRole> DraggableRoleRightClicked;

		public List<DraggableRole> DraggableRoles { get; private set; }

		public void Initialize(int maxDraggableRoles)
		{
			DraggableRoles = new();
			_maxDraggableRoles = maxDraggableRoles;
		}

		public void OnDrop(PointerEventData eventData)
		{
			if (eventData.button != PointerEventData.InputButton.Left)
			{
				return;
			}

			DraggableRole draggableRole = eventData.pointerDrag.GetComponent<DraggableRole>();

			if (!draggableRole || DraggableRoles.Contains(draggableRole))
			{
				return;
			}

			if (draggableRole.IsInfiniteSource)
			{
				AddRole(draggableRole.DraggableRoleCopy);
			}
			else if (ContainsInfiniteSource(draggableRole.RoleData))
			{
				draggableRole.SetParent(null);
			}
			else
			{
				AddRole(draggableRole);
			}
		}

		public void AddRole(DraggableRole draggableRole, int siblingIndex = -1)
		{
			if (DraggableRoles.Count == _maxDraggableRoles)
			{
				return;
			}

			DraggableRoles.Add(draggableRole);
			draggableRole.SetParent(Grid, siblingIndex > -1 ? siblingIndex : Grid.childCount);
			draggableRole.ParentChanged += OnLeavedContainer;
			draggableRole.RightClicked += OnDraggableRoleRightClicked;
			DraggableRolesChanged?.Invoke();
		}

		private void OnLeavedContainer(DraggableRole draggableRole)
		{
			draggableRole.ParentChanged -= OnLeavedContainer;
			draggableRole.RightClicked -= OnDraggableRoleRightClicked;
			DraggableRoles.Remove(draggableRole);
			DraggableRolesChanged?.Invoke();
		}

		private void OnDraggableRoleRightClicked(DraggableRole draggableRole)
		{
			DraggableRoleRightClicked?.Invoke(draggableRole);
		}

		private bool ContainsInfiniteSource(RoleData roleData)
		{
			foreach (DraggableRole draggableRole in DraggableRoles)
			{
				if (draggableRole.RoleData == roleData && draggableRole.IsInfiniteSource)
				{
					return true;
				}
			}

			return false;
		}

		public void ReturnAllRoles()
		{
			for (int i = DraggableRoles.Count - 1; i >= 0; i--)
			{
				DraggableRoles[i].ReturnToPool();
			}
		}
	}
}
