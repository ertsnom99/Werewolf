using Fusion;
using System;
using System.Collections;
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

		[Header("Role")]
		[SerializeField]
		private Sprite _lostRole;

		[Header("Selection")]
		[SerializeField]
		private float _leftClickHoldDuration;
		[SerializeField]
		private GameObject _notClickableCache;
		[SerializeField]
		private GameObject _selectedFrame;
		[SerializeField]
		private Color _selectedColor = Color.yellow;
		[SerializeField]
		private Color _notSelectedColor = Color.white;

		[Header("Votes")]
		[SerializeField]
		private GameObject _voteCountContainer;
		[SerializeField]
		private TMP_Text _voteCountText;
		[SerializeField]
		private RectTransform _vote;
		[SerializeField]
		private LineRenderer _voteLine;
		[SerializeField]
		private GameObject _voteSelf;
		[SerializeField]
		private GameObject _skip;

		[Header("Werewolf")]
		[SerializeField]
		private GameObject _werewolfIcon;

		[Header("Death")]
		[SerializeField]
		private GameObject _deathIcon;
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

		private IEnumerator _waitForLeftClickHoldCoroutine;

		private int _voteCount;

		private bool _inSelectionMode;
		private bool _isClickable;
		private bool _isSelected;

		public event Action<Card> LeftClicked;
		public event Action<Card> LeftClickHolded;
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
			_roleImage.sprite = role != null ? role.Image : _lostRole;
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
		}

		public void SetClickable(bool isClickable)
		{
			_isClickable = isClickable;
		}

		public void SetSelected(bool isSelected)
		{
			_isSelected = isSelected;
			_selectedFrame.SetActive(isSelected);
			_nicknameText.color = _isSelected ? _selectedColor : _notSelectedColor;
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

		public void DisplayVoteCount(bool display)
		{
			_voteCountContainer.SetActive(display);
		}

		public void ResetVoteCount()
		{
			_voteCount = 0;
			_voteCountText.text = _voteCount.ToString();
		}

		public void IncrementVoteCount()
		{
			_voteCount++;
			_voteCountText.text = _voteCount.ToString();
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

		public void DisplayWerewolfIcon(bool display)
		{
			_werewolfIcon.SetActive(display);
		}

		public void DisplayDeadIcon()
		{
			_deathIcon.SetActive(true);
			_roleImage.color = _deathTint;
		}

		#region MouseDetectionListener methods
		public void MouseEntered() { }

		public void MouseOver(Vector3 MousePosition)
		{
			if (!_inSelectionMode || !_isClickable)
			{
				return;
			}

			SetHighlightVisible(true);
		}

		public void MouseExited()
		{
			if (_waitForLeftClickHoldCoroutine != null)
			{
				StopCoroutine(_waitForLeftClickHoldCoroutine);
				_waitForLeftClickHoldCoroutine = null;
			}

			if (!_inSelectionMode || !_isClickable)
			{
				return;
			}

			SetHighlightVisible(false);
		}

		public void LeftMouseButtonPressed(Vector3 MousePosition)
		{
			_waitForLeftClickHoldCoroutine = WaitForLeftClickHold();
			StartCoroutine(_waitForLeftClickHoldCoroutine);
		}

		private IEnumerator WaitForLeftClickHold()
		{
			yield return new WaitForSeconds(_leftClickHoldDuration);

			_waitForLeftClickHoldCoroutine = null;
			LeftClickHolded?.Invoke(this);
		}

		public void LeftMouseButtonReleased(Vector3 MousePosition)
		{
			if (_waitForLeftClickHoldCoroutine != null)
			{
				StopCoroutine(_waitForLeftClickHoldCoroutine);
				_waitForLeftClickHoldCoroutine = null;
			}

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