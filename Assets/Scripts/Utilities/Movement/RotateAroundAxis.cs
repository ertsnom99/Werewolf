using UnityEngine;

public class RotateAroundAxis : MonoBehaviour
{
	[SerializeField]
	private Vector3 _axis;

	[SerializeField]
	private float _speed = 60.0f;

	private void Update()
	{
		transform.RotateAround(transform.position, _axis, Time.deltaTime * _speed);
	}
}