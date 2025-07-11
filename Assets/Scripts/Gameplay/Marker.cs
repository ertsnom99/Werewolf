using System;
using System.Collections;
using Newtonsoft.Json.Bson;
using UnityEngine;
using UnityEngine.Rendering;
using Werewolf.Data;
using Werewolf.UI;

namespace Werewolf.Gameplay
{
	public class Marker : MonoBehaviour
	{
		[Header("Marker")]
		[SerializeField]
		private MeshRenderer _meshRenderer;

		[SerializeField]
		private float _dissolveDuration;

		public MarkerData MarkerData { get; private set; }

		public event Action<Marker> DissolveFinished;

		private Material _material;

		private const string BASE_IMAGE_PROPERTY_REFERENCE = "_BaseImage";
		private const string DISSOLVE_AMOUNT_PROPERTY_REFERENCE = "_DissolveAmount";
		private const string DISSOLVE_TO_TARGET_PROPERTY_REFERENCE = "_DissolveToTarget";

		private void Awake()
		{
			if (_meshRenderer)
			{
				_material = _meshRenderer.material;
				_material.SetInt(DISSOLVE_TO_TARGET_PROPERTY_REFERENCE, 0);
				_material.SetFloat(DISSOLVE_AMOUNT_PROPERTY_REFERENCE, 1);
			}
			else
			{
				Debug.LogError($"{nameof(_meshRenderer)} of the card must be set");
			}
		}

		public void SetMarkerData(MarkerData markerData)
		{
			MarkerData = markerData;
			_material.SetTexture(BASE_IMAGE_PROPERTY_REFERENCE, markerData.Image.texture);
		}

		public void DissolveIn()
		{
			StartCoroutine(Dissolve(0, _dissolveDuration));
		}

		public void DissolveOut()
		{
			StartCoroutine(Dissolve(1, _dissolveDuration));
		}

		private IEnumerator Dissolve(float targetDissolve, float duration)
		{
			float initialDissolve = _material.GetFloat(DISSOLVE_AMOUNT_PROPERTY_REFERENCE);
			float elapsedTime = .0f;

			while (elapsedTime < duration)
			{
				elapsedTime += Time.deltaTime;
				_material.SetFloat(DISSOLVE_AMOUNT_PROPERTY_REFERENCE, Mathf.Lerp(initialDissolve, targetDissolve, elapsedTime / duration));

				yield return 0;
			}

			DissolveFinished?.Invoke(this);
		}
	}
}