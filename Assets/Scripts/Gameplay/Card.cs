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
		[Header("Card")]
		[SerializeField]
		private float _thickness = 0.026f;

		[SerializeField]
		private Transform _card;

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

		[Header("UI")]
		[SerializeField]
		private SpriteRenderer _roleImage;

		[SerializeField]
		private Canvas _nicknameCanvas;

		[SerializeField]
		private TMP_Text _nicknameText;

		[field: Header("Debug")]
		[field: SerializeField]
		[field: ReadOnly]
		public bool IsFaceUp { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public PlayerRef Player { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public RoleData Role { get; private set; }

		private bool _inSelectionMode;
		private bool _isClickable;
		private bool _isSelected;

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

			IsFaceUp = false;
		}
#endif
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

		public void DetachNicknameCanvas()
		{
			Vector3 tempPosition = _nicknameCanvas.transform.position;
			_nicknameCanvas.transform.SetParent(null);
			_nicknameCanvas.transform.position = tempPosition;
		}

		public void Flip()
		{
			Vector3 direction;

			if (IsFaceUp)
			{
				direction = Vector3.up;
			}
			else
			{
				direction = Vector3.down;
			}

			_card.position += direction * _thickness;
			_card.rotation *= Quaternion.AngleAxis(180, Vector3.forward);

			IsFaceUp = !IsFaceUp;
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
			_nicknameText.color = _isSelected ? _selectedColor : _notSelectedColor;
		}

		public void ResetSelectionMode()
		{
			SetSelectionMode(false, false);
			SetSelected(false);
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

		#region MouseDetectionListener methods
		public void MouseEntered() { }

		public void MouseOver(Vector3 MousePosition) { }

		public void MouseExited() { }

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