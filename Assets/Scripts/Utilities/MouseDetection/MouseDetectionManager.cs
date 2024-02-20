using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MouseDetectionManager : MonoSingleton<MouseDetectionManager>
{
	[Header("UI")]
	[SerializeField]
	private GraphicRaycaster _graphicRaycaster;

	[SerializeField]
	private EventSystem _eventSystem;

	private PointerEventData _pointerEventData;

	[Header("Mouse Detection Listener")]
	[SerializeField]
	private LayerMask _mouseDetectionLayerMask;

	private MouseDetectionListener _mouseDetectionListener = null;

	private Camera _camera;

	protected override void Awake()
	{
		base.Awake();

		if (!_eventSystem)
		{
			return;
		}

		_pointerEventData = new(_eventSystem);
	}

	private void Start()
	{
		_camera = Camera.main;
	}

	private void Update()
	{
		MouseDetectionListener currentMouseDetectionListener = null;
		List<RaycastResult> results = new();

		// Raycast for UI
		if (_graphicRaycaster && _eventSystem)
		{
			_pointerEventData.position = Input.mousePosition;
			_graphicRaycaster.Raycast(_pointerEventData, results);
		}

		RaycastHit hit = new();

		if (results.Count == 0)
		{
			// Raycast for the scene
			Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

			if (Physics.Raycast(ray, out hit, float.PositiveInfinity, _mouseDetectionLayerMask))
			{
				currentMouseDetectionListener = hit.transform.gameObject.GetComponent<MouseDetectionListener>();
			}
		}

		// Call MouseDetectionListener methods
		if (_mouseDetectionListener != currentMouseDetectionListener)
		{
			if (_mouseDetectionListener != null)
			{
				_mouseDetectionListener.MouseExited();
			}

			if (currentMouseDetectionListener != null)
			{
				currentMouseDetectionListener.MouseEntered();
			}

			_mouseDetectionListener = currentMouseDetectionListener;
		}

		if (_mouseDetectionListener == null)
		{
			return;
		}

		_mouseDetectionListener.MouseOver(hit.point);

		if (Input.GetMouseButtonDown(0))
		{
			_mouseDetectionListener.MousePressed(hit.point);
		}
		else if (Input.GetMouseButtonUp(0))
		{
			_mouseDetectionListener.MouseReleased(hit.point);
		}
	}
}