using System;
using UnityEngine;

namespace Utilities.GameplayData
{
	[Serializable]
	public struct UniqueID
	{
		public string Guid;
		public int HashCode;
	}

	public class GameplayData : ScriptableObject
	{
		[field: SerializeField]
		public UniqueID ID { get; protected set; }

		[field: SerializeField]
		public string DebugName { get; protected set; }
	}
}
