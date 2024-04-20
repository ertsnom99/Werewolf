using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ImageFade : MonoBehaviour
{
	[SerializeField]
	private float _fadeSpeed = 1.0f;
	private float _fadeStart;
	private float _fadeTarget;
	private float _fadeProgress;

	private IEnumerator fadeCoroutine;

	private Image _image;

	public event Action OnFadeInOver;
	public event Action OnFadeOutOver;

	private void Awake()
	{
		_image = GetComponent<Image>();
	}

	public void FadeIn()
	{
		if (fadeCoroutine != null)
		{
			StopCoroutine(fadeCoroutine);
		}

		_fadeStart = 1.0f;
		_fadeTarget = .0f;
		_fadeProgress = .0f;

		fadeCoroutine = Fade();
		StartCoroutine(fadeCoroutine);
	}

	public void FadeOut()
	{
		if (fadeCoroutine != null)
		{
			StopCoroutine(fadeCoroutine);
		}

		_fadeStart = .0f;
		_fadeTarget = 1.0f;
		_fadeProgress = .0f;

		fadeCoroutine = Fade();
		StartCoroutine(fadeCoroutine);
	}

	private IEnumerator Fade()
	{
		while (true)
		{
			yield return 0;

			_fadeProgress += Time.deltaTime * _fadeSpeed;

			float fadeValue = Mathf.Clamp01(Mathf.Lerp(_fadeStart, _fadeTarget, _fadeProgress));

			SetFade(fadeValue);

			if (_fadeProgress >= 1.0f && _fadeTarget <= .0f)
			{
				OnFadeInOver?.Invoke();
				break;
			}
			else if (_fadeProgress >= 1.0f && _fadeTarget >= 1.0f)
			{
				OnFadeOutOver?.Invoke();
				break;
			}
		}
	}

	public void SetFade(float fade)
	{
		_image.color = new() { r = _image.color.r, g = _image.color.g, b = _image.color.b, a = fade };
	}
}