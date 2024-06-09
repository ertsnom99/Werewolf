using UnityEngine;

public interface MouseDetectionListener
{
	void MouseEntered();

	void MouseOver(Vector3 MousePosition);

	void MouseExited();

	void LeftMouseButtonPressed(Vector3 MousePosition);

	void LeftMouseButtonReleased(Vector3 MousePosition);

	void RightMouseButtonPressed(Vector3 MousePosition);

	void RightMouseButtonReleased(Vector3 MousePosition);
}