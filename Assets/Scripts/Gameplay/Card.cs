using Fusion;
using System;
using TMPro;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class Card : MonoBehaviour, MouseDetectionListener
	{
		[field: Header("Card")]
		[field: SerializeField]
		public float Thickness { get; private set; }

		public Vector3 OriginalPosition { get; private set; }

		[Header("Selection")]
		[SerializeField]
		private GameObject _notClickableCache;

		[SerializeField]
		private Color _selectedColor = Color.yellow;

		[SerializeField]
		private Color _notSelectedColor = Color.white;

		[Header("Votes")]
		[SerializeField]
		private RectTransform _vote;
		[SerializeField]
		private LineRenderer _voteLine;
		[SerializeField]
		private GameObject _voteSelf;

		[SerializeField]
		private GameObject _skip;

		[Header("Death")]
		[SerializeField]
		private GameObject _deathImage;

		[SerializeField]
		private Color _deathTint;

		[Header("UI")]
		[SerializeField]
		private SpriteRenderer _roleImage;

		[SerializeField]
		private Canvas _groundCanvas;

		[SerializeField]
		private GameObject _highlight;

		[SerializeField]
		private TMP_Text _nicknameText;

		[field: Header("Debug")]
		[field: SerializeField]
		[field: ReadOnly]
		public PlayerRef Player { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public RoleData Role { get; private set; }

		private bool _inSelectionMode;
		private bool _isClickable;
		private bool _isSelected;
		private bool _isHighlightBlocked;

		public event Action<Card> LeftClicked;
		public event Action<Card> RightClicked;
#if UNITY_EDITOR
		private void Awake()
		{
			if (!_roleImage)
			{
				Debug.LogError($"_roleImage of the player {gameObject.name} is null");
			}
		}
#endif
		public void SetOriginalPosition(Vector3 originalPosition)
		{
			OriginalPosition = originalPosition;
		}

		public void SetPlayer(PlayerRef player)
		{
			Player = player;
		}

		public void SetRole(RoleData role)
		{
			Role = role;
			_roleImage.sprite = role != null ? role.Image : null;
		}

		public void SetNickname(string nickname)
		{
			_nicknameText.text = nickname;
		}

		public void DetachGroundCanvas()
		{
			Vector3 tempPosition = _groundCanvas.transform.position;
			_groundCanvas.transform.SetParent(null);
			_groundCanvas.transform.position = tempPosition;
		}

		public void Flip()
		{
			transform.rotation *= Quaternion.AngleAxis(180, transform.forward);
		}

		public void SetHighlightVisible(bool isVisible)
		{
			_highlight.SetActive(isVisible);
		}

		#region Selection mode
		public void SetSelectionMode(bool inSelectionMode, bool isClickable)
		{
			_inSelectionMode = inSelectionMode;
			SetClickable(isClickable);
			_notClickableCache.SetActive(inSelectionMode && !isClickable);
			SetHighlightBlocked(false);
		}

		public void SetClickable(bool isClickable)
		{
			_isClickable = isClickable;
		}

		public void SetSelected(bool isSelected)
		{
			_isSelected = isSelected;
			_nicknameText.color = _isSelected ? _selectedColor : _notSelectedColor;
		}

		public void SetHighlightBlocked(bool isHighlightBlocked)
		{
			_isHighlightBlocked = isHighlightBlocked;
		}

		public void ResetSelectionMode()
		{
			SetSelectionMode(false, false);
			SetSelected(false);
			SetHighlightVisible(false);
		}
		#endregion

		#region Vote display
		public void DisplaySkip(bool display)
		{
			_skip.SetActive(display);
		}

		public void DisplayVote(bool display, Vector3 pointTo = default, bool voteSelf = false)
		{
			_vote.gameObject.SetActive(display && !voteSelf);
			_voteLine.enabled = display && !voteSelf;
			_voteSelf.SetActive(display && voteSelf);

			if (!display || voteSelf)
			{
				return;
			}

			Vector3 lookAt = (pointTo - transform.position).normalized;
			lookAt.y = 0;

			_vote.rotation = Quaternion.LookRotation(Vector3.down, lookAt);

			_voteLine.positionCount = 2;
			_voteLine.SetPosition(0, new Vector3(transform.position.x, .05f, transform.position.z));
			_voteLine.SetPosition(1, new Vector3(pointTo.x, .05f, pointTo.z));
		}
		#endregion

		public void DisplayDead()
		{
			_deathImage.SetActive(true);
			_roleImage.color = _deathTint;
		}

		#region MouseDetectionListener methods
		public void MouseEntered()
		{
			if (!_inSelectionMode || !_isClickable)
			{
				return;
			}

			SetHighlightVisible(true);
		}

		public void MouseOver(Vector3 MousePosition) { }

		public void MouseExited()
		{
			if (!_inSelectionMode || !_isClickable || _isHighlightBlocked)
			{
				return;
			}

			SetHighlightVisible(false);
		}

		public void LeftMouseButtonPressed(Vector3 MousePosition) { }

		public void LeftMouseButtonReleased(Vector3 MousePosition)
		{
			if (!_inSelectionMode || !_isClickable)
			{
				return;
			}

			SetSelected(!_isSelected);
			LeftClicked?.Invoke(this);
		}

		public void RightMouseButtonPressed(Vector3 MousePosition) { }

		public void RightMouseButtonReleased(Vector3 MousePosition)
		{
			RightClicked?.Invoke(this);
		}
		#endregion
	}
}