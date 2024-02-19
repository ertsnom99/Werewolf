using UnityEngine;

public interface MouseDetectionListener
{
	void MouseEntered();

	void MouseOver(Vector3 MousePosition);

	void MouseExited();

	void MousePressed(Vector3 MousePosition);

	void MouseReleased(Vector3 MousePosition);
}