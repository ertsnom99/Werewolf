using UnityEditor;
using UnityEngine;

namespace Werewolf.Skybox
{
	[ExecuteInEditMode]
	public class MoonControl : MonoBehaviour
	{
		[SerializeField]
		private string _directionParameter;

		private void Update()
		{
			RenderSettings.skybox.SetVector(_directionParameter, transform.forward);
		}
#if UNITY_EDITOR
		private void OnDrawGizmos()
		{
			Handles.color = Color.red;
			Handles.ArrowHandleCap(0, transform.position, Quaternion.LookRotation(transform.forward), 1, EventType.Repaint);
		}
#endif
	}
}