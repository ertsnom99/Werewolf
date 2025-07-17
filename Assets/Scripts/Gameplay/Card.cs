using Fusion;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf.Gameplay
{
	public class Card : MonoBehaviour, MouseDetectionListener
	{
		[Header("Card")]
		[SerializeField]
		private MeshRenderer _meshRenderer;

		[field: SerializeField]
		public float Thickness { get; private set; }

		[Header("Role")]
		[SerializeField]
		private float _roleTransitionDuration;

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
		private Color _selectedColor;

		[SerializeField]
		private Color _notSelectedColor;

		[Header("Votes")]
		[SerializeField]
		private GameObject _voteCountContainer;

		[SerializeField]
		private LocalizeStringEvent _voteCountText;

		[SerializeField]
		private RectTransform _vote;

		[SerializeField]
		private LineRenderer _voteLine;

		[SerializeField]
		private GameObject _voteSelf;

		[SerializeField]
		private GameObject _skip;

		[Header("Icons")]
		[SerializeField]
		private Image _icon;

		[SerializeField]
		private CanvasGroup _iconCanvasGroup;

		[SerializeField]
		private float _iconFadeDuration;

		[SerializeField]
		private Sprite _werewolfIcon;

		[SerializeField]
		private Sprite _deathIcon;

		[Header("Group")]
		[SerializeField]
		private Image _groupBackground;

		[SerializeField]
		private TMP_Text _groupText;

		[SerializeField]
		private CanvasGroup _groupCanvasGroup;

		[SerializeField]
		private float _groupFadeDuration;

		[Header("UI")]
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

		public Vector3 OriginalPosition { get; private set; }

		public event Action<Card> LeftClicked;
		public event Action<Card> LeftClickHolded;
		public event Action<Card> RightClicked;

		private Material _material;
		private IEnumerator _waitForLeftClickHoldCoroutine;
		private int _voteCount;
		private IntVariable _voteCountVariable;
		private bool _inSelectionMode;
		private bool _isClickable;
		private bool _isSelected;

		private const string BASE_IMAGE_PROPERTY_REFERENCE = "_BaseImage";
		private const string TARGET_IMAGE_PROPERTY_REFERENCE = "_TargetImage";
		private const string DISSOLVE_AMOUNT_PROPERTY_REFERENCE = "_DissolveAmount";
		private const string DISSOLVE_TO_TARGET_PROPERTY_REFERENCE = "_DissolveToTarget";

		private void Awake()
		{
			if (_meshRenderer)
			{
				_material = _meshRenderer.material;
			}
			else
			{
				Debug.LogError($"{nameof(_meshRenderer)} of the card must be set");
			}

			_voteCountVariable = (IntVariable)_voteCountText.StringReference["Count"];

			if (_voteCountVariable == null)
			{
				Debug.LogError($"{nameof(_voteCountText)} must have a local int variable named Count");
			}
		}

		public void SetOriginalPosition(Vector3 originalPosition)
		{
			OriginalPosition = originalPosition;
		}

		public void SetPlayer(PlayerRef player)
		{
			Player = player;
		}

		public void SetRole(RoleData role, bool useDissolve = false)
		{
			Role = role;
			Texture2D roleTexture = role != null ? role.Image.texture : _lostRole.texture;

			if (useDissolve)
			{
				_material.SetInt(DISSOLVE_TO_TARGET_PROPERTY_REFERENCE, 1);
				_material.SetFloat(DISSOLVE_AMOUNT_PROPERTY_REFERENCE, 0);
				_material.SetTexture(TARGET_IMAGE_PROPERTY_REFERENCE, roleTexture);

				StartCoroutine(DissolveRole(1, _roleTransitionDuration, 0, roleTexture));
			}
			else
			{
				_material.SetTexture(BASE_IMAGE_PROPERTY_REFERENCE, roleTexture);
			}
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

		public void Display(bool display)
		{
			gameObject.SetActive(display);
			_groundCanvas.gameObject.SetActive(display);

			if (!display)
			{
				return;
			}

			_material.SetInt(DISSOLVE_TO_TARGET_PROPERTY_REFERENCE, 0);
			_material.SetFloat(DISSOLVE_AMOUNT_PROPERTY_REFERENCE, 1);

			StartCoroutine(DissolveRole(0, _roleTransitionDuration, 0));
		}

		private IEnumerator DissolveRole(float targetDissolve, float duration, float finalDissolve, Texture2D finalBaseRole = null)
		{
			float initialDissolve = _material.GetFloat(DISSOLVE_AMOUNT_PROPERTY_REFERENCE);
			float elapsedTime = .0f;

			while (elapsedTime < duration)
			{
				elapsedTime += Time.deltaTime;
				_material.SetFloat(DISSOLVE_AMOUNT_PROPERTY_REFERENCE, Mathf.Lerp(initialDissolve, targetDissolve, elapsedTime / duration));

				yield return 0;
			}

			if (finalBaseRole)
			{
				_material.SetTexture(BASE_IMAGE_PROPERTY_REFERENCE, finalBaseRole);
			}

			_material.SetFloat(DISSOLVE_AMOUNT_PROPERTY_REFERENCE, finalDissolve);
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
		}

		public void SetClickable(bool isClickable)
		{
			_isClickable = isClickable;
			_notClickableCache.SetActive(_inSelectionMode && !isClickable);
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
			_voteCountVariable.Value = _voteCount;
		}

		public void IncrementVoteCount(int increment)
		{
			_voteCount += increment;
			_voteCountVariable.Value = _voteCount;
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

		#region Icons
		public void DisplayWerewolfIcon(bool display)
		{
			_icon.sprite = _werewolfIcon;
			_icon.gameObject.SetActive(display);

			StartCoroutine(FadeCanvasGroup(_iconCanvasGroup, display ? 1 : 0, _iconFadeDuration));
		}

		public void DisplayDeadIcon(bool display)
		{
			_icon.sprite = _deathIcon;
			_icon.gameObject.SetActive(display);

			StartCoroutine(FadeCanvasGroup(_iconCanvasGroup, display ? 1 : 0, _iconFadeDuration));
		}
		#endregion

		public void DisplayGroup(string text, Color background)
		{
			_groupText.text = text;
			_groupBackground.color = background;

			_groupBackground.gameObject.SetActive(true);

			StartCoroutine(FadeCanvasGroup(_groupCanvasGroup, 1, _groupFadeDuration));
		}

		private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float targetFade, float duration)
		{
			float initialFade = canvasGroup.alpha;
			float elapsedTime = .0f;

			while (elapsedTime < duration)
			{
				elapsedTime += Time.deltaTime;
				canvasGroup.alpha = Mathf.Lerp(initialFade, targetFade, elapsedTime / duration);

				yield return 0;
			}
		}

		#region MouseDetectionListener methods
		public void MouseEntered() { }

		public void MouseOver(Vector3 MousePosition)
		{
			if (_inSelectionMode && _isClickable)
			{
				SetHighlightVisible(true);
			}
		}

		public void MouseExited()
		{
			if (_waitForLeftClickHoldCoroutine != null)
			{
				StopCoroutine(_waitForLeftClickHoldCoroutine);
				_waitForLeftClickHoldCoroutine = null;
			}

			if (_inSelectionMode && _isClickable)
			{
				SetHighlightVisible(false);
			}
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