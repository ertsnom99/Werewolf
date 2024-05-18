using Fusion;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Werewolf.Data;
using Werewolf.UI;

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
		private GridLayoutGroup _voteDotsContainer;

		[SerializeField]
		private VoteDot _voteDotPrefab;

		[Header("Vote status")]
		[SerializeField]
		private Image _votingStatusIcon;

		[SerializeField]
		private Sprite _votingStatusImage;

		[SerializeField]
		private Sprite _doneVotingStatusImage;

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

		private List<VoteDot> _voteDots = new();
		private int _voteAmount;

		public event Action<Card> OnCardClick;
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
			_roleImage.sprite = role?.Image;
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
		public void SetVotingStatusVisible(bool isVisible)
		{
			_votingStatusIcon.gameObject.SetActive(isVisible);
		}

		public void UpdateVotingStatus(bool isVoting)
		{
			_votingStatusIcon.sprite = isVoting ? _votingStatusImage : _doneVotingStatusImage;
		}

		public void AddVote(bool isLockedIn)
		{
			VoteDot voteDot;

			if (_voteAmount >= _voteDots.Count)
			{
				voteDot = Instantiate(_voteDotPrefab, _voteDotsContainer.transform);
				_voteDots.Add(voteDot);
			}
			else
			{
				voteDot = _voteDots[_voteAmount];
				voteDot.gameObject.SetActive(true);
			}

			voteDot.DisplayLockedIn(isLockedIn);
			_voteAmount++;
		}

		public void ClearVotes()
		{
			foreach (VoteDot vote in _voteDots)
			{
				vote.gameObject.SetActive(false);
			}

			_voteAmount = 0;
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

		public void MousePressed(Vector3 MousePosition) { }

		public void MouseReleased(Vector3 MousePosition)
		{
			if (!_inSelectionMode || !_isClickable)
			{
				return;
			}

			SetSelected(!_isSelected);
			OnCardClick?.Invoke(this);
		}
		#endregion
	}
}