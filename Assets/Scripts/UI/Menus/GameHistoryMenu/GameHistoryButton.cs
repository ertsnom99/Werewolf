using System;
using System.IO;
using TMPro;
using UnityEngine;

namespace Werewolf.UI
{
	public class GameHistoryButton : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _name;

		private string _filePath;

		public event Action<string> Clicked;

		public void Initialize(string filePath)
		{
			_filePath = filePath;
			_name.text = Path.GetFileNameWithoutExtension(filePath);
		}

		public void OnClicked()
		{
			Clicked?.Invoke(_filePath);
		}
	}
}