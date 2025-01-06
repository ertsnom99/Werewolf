using System;
using UnityEngine;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf.UI
{
	[RequireComponent(typeof(Image))]
	public class RoleButton : MonoBehaviour
	{
		[SerializeField]
		private GameObject _highlight;

		private Image _image;

		public RoleData RoleData { get; private set; }

		public event Action<RoleButton> Selected;

		private void Awake()
		{
			_image = GetComponent<Image>();
		}

		public void SetRoleData(RoleData roleData)
		{
			RoleData = roleData;
			_image.sprite = roleData.SmallImage;
		}

		public void Select()
		{
			_highlight.SetActive(true);
			Selected?.Invoke(this);
		}

		public void Unselect()
		{
			_highlight.SetActive(false);
		}
	}
}