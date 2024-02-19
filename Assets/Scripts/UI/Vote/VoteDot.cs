using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
	[RequireComponent(typeof(Image))]
	public class VoteDot : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private Color _lockedOutColor;

		[SerializeField]
		private Color _lockedInColor;

		private Image _image;

		private void Awake()
		{
			_image = GetComponent<Image>();
		}

		public void DisplayLockedIn(bool isLockedIn)
		{
			_image.color = isLockedIn ? _lockedInColor : _lockedOutColor;
		}
	}
}