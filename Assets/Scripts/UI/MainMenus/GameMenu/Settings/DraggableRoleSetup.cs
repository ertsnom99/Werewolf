using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class DraggableRoleSetup : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		[Header("UI")]
		[SerializeField]
		private Image _singleRoleImage;

		[SerializeField]
		private GameObject _multiRoleImagesContainer;

		[SerializeField]
		private Image[] _multiRoleImages;

		[SerializeField]
		private Sprite _multiRoleSpriteBackground;

		[SerializeField]
		private TMP_Text _amount;

		[SerializeField]
		private Button _decrementButton;

		[SerializeField]
		private Button _incrementButton;

		public List<RoleData> RoleDataPool { get; private set; }

		public int UseCount { get; private set; }

		public bool IsMultiRole { get; private set; }

		public int SelectedMultiRoleDataPoolIndex { get; private set; }

		public bool IsInfiniteSource { get; private set; }

		public DraggableRoleSetup DraggedRoleSetup { get; private set; }

		public bool HasParent => _parent != null;

		public delegate void ReturnToPoolDelegate(DraggableRoleSetup draggableRoleSetup);
		public delegate DraggableRoleSetup GetFromPoolDelegate();

		public event Action<DraggableRoleSetup> ParentChanged;
		public event Action RoleSetupChanged;
		public event Action<bool> DragChanged;
		public event Action<RoleData, Vector3> MiddleClicked;
		public event Action<DraggableRoleSetup, int> RightClicked;

		private bool _canBeMultiRole;
		private bool _isDragEnable;
		private bool _isUseCountButtonsEnable;
		private bool _isPointerDownEnable;
		private Vector2 _sizeDelta;
		private Transform _parent;
		private int _siblingIndex;
		private Vector2 _dragOffset;

		private ReturnToPoolDelegate _returnToPoolDelegate;
		private GetFromPoolDelegate _getFromPoolDelegate;

		public void Initialize(RoleData[] roleDatas, int useCount, bool isInfiniteSource)
		{
			RoleDataPool ??= new();
			RoleDataPool.Clear();

			if (roleDatas.Length <= 0)
			{
				Debug.LogError($"A {nameof(DraggableRoleSetup)} must display at least one role");
				return;
			}

			if (roleDatas.Length > 1 && isInfiniteSource)
			{
				Debug.LogError($"A {nameof(DraggableRoleSetup)} that is an infinite source must have exactly one role");
				return;
			}

			if (roleDatas.Length > GameConfig.MAX_ROLE_SETUP_POOL_COUNT)
			{
				Debug.LogError($"A {nameof(DraggableRoleSetup)} can display at most {GameConfig.MAX_ROLE_SETUP_POOL_COUNT} roles");
				return;
			}

			UseCount = useCount;

			RoleData firstRoleData = roleDatas[0];
			_canBeMultiRole = !isInfiniteSource && firstRoleData.MandatoryAmount <= 1;
			IsMultiRole = false;

			foreach (RoleData roleData in roleDatas)
			{
				RoleDataPool.Add(roleData);

				if (!IsMultiRole && _canBeMultiRole && roleData != firstRoleData)
				{
					IsMultiRole = true;
				}
			}

			IsInfiniteSource = isInfiniteSource;
			_isPointerDownEnable = true;
			_sizeDelta = ((RectTransform)transform).sizeDelta;

			UpdateVisual();
		}

		public void SetReturnToPoolDelegate(ReturnToPoolDelegate returnToPoolDelegate)
		{
			_returnToPoolDelegate = returnToPoolDelegate;
		}

		public void SetGetFromPoolDelegate(GetFromPoolDelegate getFromPoolDelegate)
		{
			_getFromPoolDelegate = getFromPoolDelegate;
		}

		public bool AddRoleData(DraggableRoleSetup draggableRoleSetup)
		{
			if (!_canBeMultiRole || draggableRoleSetup.RoleDataPool.Count != 1 || GameConfig.MAX_ROLE_SETUP_POOL_COUNT - RoleDataPool.Count < 1)
			{
				return false;
			}

			RoleDataPool.Add(draggableRoleSetup.RoleDataPool[0]);
			IsMultiRole = true;

			UpdateVisual();

			RoleSetupChanged?.Invoke();

			return true;
		}

		public void RemoveRoleData(int index)
		{
			if (!IsMultiRole || RoleDataPool.Count <= index)
			{
				return;
			}

			RoleDataPool.RemoveAt(index);
			IsMultiRole = RoleDataPool.Count > 1;

			if (UseCount > RoleDataPool.Count)
			{
				UseCount = RoleDataPool.Count;
			}

			UpdateVisual();

			RoleSetupChanged?.Invoke();
		}

		private void UpdateVisual()
		{
			_multiRoleImagesContainer.SetActive(IsMultiRole);
			UpdateUseCountButtons();

			if (IsMultiRole)
			{
				_singleRoleImage.sprite = _multiRoleSpriteBackground;

				for (int i = 0; i < RoleDataPool.Count; i++)
				{
					_multiRoleImages[i].gameObject.SetActive(true);
					_multiRoleImages[i].sprite = RoleDataPool[i].SmallImage;
				}

				for (int i = RoleDataPool.Count; i < _multiRoleImages.Length; i++)
				{
					_multiRoleImages[i].gameObject.SetActive(false);
				}
			}
			else
			{
				_singleRoleImage.sprite = RoleDataPool[0].SmallImage;
			}

			if (IsInfiniteSource)
			{
				_amount.text = "\u221E";
				_amount.gameObject.SetActive(true);
			}
			else if (IsMultiRole || UseCount > 1)
			{
				_amount.text = UseCount.ToString();
				_amount.gameObject.SetActive(true);
			}
			else
			{
				_amount.gameObject.SetActive(false);
			}
		}

		private void UpdateUseCountButtons()
		{
			_decrementButton.gameObject.SetActive(_isUseCountButtonsEnable && IsMultiRole);
			_decrementButton.interactable = IsMultiRole && RoleDataPool != null && _isUseCountButtonsEnable && UseCount > 1;
			_incrementButton.gameObject.SetActive(_isUseCountButtonsEnable && IsMultiRole);
			_incrementButton.interactable = IsMultiRole && RoleDataPool != null && _isUseCountButtonsEnable && UseCount < RoleDataPool.Count;
		}

		public void DecrementUseCount()
		{
			if (UseCount > 1)
			{
				UseCount--;
				UpdateVisual();

				RoleSetupChanged?.Invoke();
			}
		}

		public void IncrementUseCount()
		{
			if (UseCount < RoleDataPool.Count)
			{
				UseCount++;
				UpdateVisual();

				RoleSetupChanged?.Invoke();
			}
		}

		public void EnableDrag(bool enable)
		{
			_isDragEnable = enable;
		}

		public void EnableUseCountButtons(bool enable)
		{
			_isUseCountButtonsEnable = enable;
			UpdateUseCountButtons();
		}

		public void EnablePointerDown(bool enable)
		{
			_isPointerDownEnable = enable;
		}

		public void SetParent(Transform parent, int siblingIndex = -1)
		{
			if (_parent == parent)
			{
				return;
			}

			_parent = parent;
			_siblingIndex = siblingIndex;
			ParentChanged?.Invoke(this);
		}

		public void MoveToRoot()
		{
			transform.SetParent(transform.root);
			transform.SetAsLastSibling();
			_singleRoleImage.raycastTarget = false;
		}

		public void MoveToParent()
		{
			transform.SetParent(_parent);
			transform.SetSiblingIndex(_siblingIndex);
			_singleRoleImage.raycastTarget = true;
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			if (!_isPointerDownEnable)
			{
				return;
			}

			SelectedMultiRoleDataPoolIndex = IsMultiRole ? GetMultiRoleDataPoolIndex(eventData.position - (Vector2)transform.position) : -1;
			_dragOffset = Vector2.zero;

			switch (eventData.button)
			{
				case PointerEventData.InputButton.Left:
					if (!IsMultiRole)
					{
						_dragOffset = eventData.position - (Vector2)transform.position;
					}
					break;
				case PointerEventData.InputButton.Middle:
					if (RoleDataPool.Count > SelectedMultiRoleDataPoolIndex)
					{
						if (IsMultiRole)
						{
							MiddleClicked?.Invoke(RoleDataPool[SelectedMultiRoleDataPoolIndex], _multiRoleImages[SelectedMultiRoleDataPoolIndex].transform.position);
						}
						else
						{
							MiddleClicked?.Invoke(RoleDataPool[0], transform.position);
						}
					}
					break;
				case PointerEventData.InputButton.Right:
					if (RoleDataPool.Count > SelectedMultiRoleDataPoolIndex)
					{
						RightClicked?.Invoke(this, SelectedMultiRoleDataPoolIndex);
					}
					break;
			}
		}

		private int GetMultiRoleDataPoolIndex(Vector2 offset)
		{
			if (offset.x <= 0 && offset.y >= 0)
			{
				return 0;
			}
			else if (offset.x >= 0 && offset.y >= 0)
			{
				return 1;
			}
			else if (offset.x <= 0 && offset.y <= 0)
			{
				return 2;
			}
			else
			{
				return 3;
			}
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
			if (!_isDragEnable || eventData.button != PointerEventData.InputButton.Left)
			{
				return;
			}

			DragChanged?.Invoke(true);

			if (IsMultiRole)
			{
				if (RoleDataPool.Count > SelectedMultiRoleDataPoolIndex)
				{
					CreateNewDraggableRoleSetup(new RoleData[1] { RoleDataPool[SelectedMultiRoleDataPoolIndex] }, 1);
				}
			}
			else if (IsInfiniteSource)
			{
				CreateNewDraggableRoleSetup(RoleDataPool.ToArray(), UseCount);
			}
			else
			{
				DraggedRoleSetup = this;
				_siblingIndex = transform.GetSiblingIndex();
				MoveToRoot();
			}
		}

		private void CreateNewDraggableRoleSetup(RoleData[] roleDatas, int useCount)
		{
			DraggedRoleSetup = _getFromPoolDelegate();
			DraggedRoleSetup.EnableDrag(true);
			DraggedRoleSetup.EnableUseCountButtons(true);
			DraggedRoleSetup.Initialize(roleDatas, useCount, false);
			DraggedRoleSetup.MoveToRoot();
			((RectTransform)DraggedRoleSetup.transform).sizeDelta = _sizeDelta;
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (_isDragEnable && eventData.button == PointerEventData.InputButton.Left && DraggedRoleSetup)
			{
				DraggedRoleSetup.transform.position = eventData.position - _dragOffset;
			}
		}

		public void OnEndDrag(PointerEventData eventData)
		{
			if (!_isDragEnable || eventData.button != PointerEventData.InputButton.Left || !DraggedRoleSetup)
			{
				return;
			}

			if (DraggedRoleSetup.HasParent)
			{
				DraggedRoleSetup.MoveToParent();
				
				if (IsMultiRole)
				{
					RemoveRoleData(SelectedMultiRoleDataPoolIndex);
				}
			}
			else
			{
				_returnToPoolDelegate(DraggedRoleSetup);
			}

			DraggedRoleSetup = null;

			DragChanged?.Invoke(false);
		}

		public void ReturnToPool()
		{
			_returnToPoolDelegate(this);
		}
	}
}
