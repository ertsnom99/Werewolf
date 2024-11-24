using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
	public class Emote : MonoBehaviour
	{
		[SerializeField]
		private Image _image;

		public void SetEmote(Sprite emote)
		{
			_image.sprite = emote;
		}

		public void DestroySelf()
		{
			Destroy(gameObject);
		}
	}
}