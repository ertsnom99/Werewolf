using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class DraggableRole : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		[Header("UI")]
		[SerializeField]
		private Image _image;

		[SerializeField]
		private TMP_Text _amount;

		public RoleData RoleData { get; private set; }

		public bool IsInfiniteSource { get; private set; }

		public DraggableRole DraggableRoleCopy { get; private set; }

		public bool HasParent => _parent != null;

		public delegate void ReturnToPoolDelegate(DraggableRole draggableRole);
		public delegate DraggableRole GetFromPoolDelegate();

		public event Action<DraggableRole> ParentChanged;
		public event Action<DraggableRole> RightClicked;

		private Vector2 sizeDelta;
		private Transform _parent;
		private int _siblingIndex;
		private Vector2 _dragOffset;

		private ReturnToPoolDelegate _returnToPoolDelegate;
		private GetFromPoolDelegate _getFromPoolDelegate;

		public void Initialize(RoleData roleData, bool isInfiniteSource)
		{
			RoleData = roleData;
			IsInfiniteSource = isInfiniteSource;
			_image.sprite = roleData.SmallImage;

			if (isInfiniteSource)
			{
				sizeDelta = ((RectTransform)transform).sizeDelta;
				_amount.text = "\u221E";
			}
			else if (roleData.MandatoryAmount > 1)
			{
				_amount.text = roleData.MandatoryAmount.ToString();
			}
			else
			{
				_amount.text = "";
			}
		}

		public void SetReturnToPoolDelegate(ReturnToPoolDelegate returnToPoolDelegate)
		{
			_returnToPoolDelegate = returnToPoolDelegate;
		}

		public void SetGetFromPoolDelegate(GetFromPoolDelegate getFromPoolDelegate)
		{
			_getFromPoolDelegate = getFromPoolDelegate;
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
			_image.raycastTarget = false;
		}

		public void MoveToParent()
		{
			transform.SetParent(_parent);
			transform.SetSiblingIndex(_siblingIndex);
			_image.raycastTarget = true;
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			switch(eventData.button)
			{
				case PointerEventData.InputButton.Left:
					_dragOffset = (Vector2)transform.position - eventData.position;
					break;
				case PointerEventData.InputButton.Right:
					RightClicked?.Invoke(this);
					break;
			}
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
			if (eventData.button != PointerEventData.InputButton.Left)
			{
				return;
			}

			if (IsInfiniteSource)
			{
				DraggableRoleCopy = _getFromPoolDelegate();
				DraggableRoleCopy.Initialize(RoleData, false);
				DraggableRoleCopy.MoveToRoot();
				((RectTransform)DraggableRoleCopy.transform).sizeDelta = sizeDelta;
			}
			else
			{
				_siblingIndex = transform.GetSiblingIndex();
				MoveToRoot();
			}
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (eventData.button != PointerEventData.InputButton.Left)
			{
				return;
			}

			if (IsInfiniteSource)
			{
				DraggableRoleCopy.transform.position = eventData.position + _dragOffset;
			}
			else
			{
				transform.position = eventData.position + _dragOffset;
			}
		}

		public void OnEndDrag(PointerEventData eventData)
		{
			if (eventData.button != PointerEventData.InputButton.Left)
			{
				return;
			}

			if (IsInfiniteSource)
			{
				if (DraggableRoleCopy.HasParent)
				{
					DraggableRoleCopy.MoveToParent();
				}
				else
				{
					_returnToPoolDelegate(DraggableRoleCopy);
				}

				DraggableRoleCopy = null;
			}
			else if (_parent)
			{
				MoveToParent();
			}
			else
			{
				_returnToPoolDelegate(this);
			}
		}

		public void ReturnToPool()
		{
			_returnToPoolDelegate(this);
		}
	}
}
