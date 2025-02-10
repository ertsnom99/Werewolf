using System;
using UnityEngine;

namespace Utilities.GameplayData
{
	[Serializable]
	public struct UniqueID
	{
		public string Guid;
		public int HashCode;

		public override readonly bool Equals(object obj)
		{
			return obj is UniqueID uniqueID &&
					Guid == uniqueID.Guid &&
					HashCode == uniqueID.HashCode;
		}

		public override readonly int GetHashCode()
		{
			return HashCode;
		}

		public static bool operator ==(UniqueID uniqueID1, UniqueID uniqueID2)
		{
			return uniqueID1.Guid == uniqueID2.Guid && uniqueID1.HashCode == uniqueID2.HashCode;
		}

		public static bool operator !=(UniqueID uniqueID1, UniqueID uniqueID2)
		{
			return uniqueID1.Guid != uniqueID2.Guid || uniqueID1.HashCode != uniqueID2.HashCode;
		}
	}

	public class GameplayData : ScriptableObject
	{
		[field: SerializeField]
		public UniqueID ID { get; protected set; }

		[field: SerializeField]
		public string DebugName { get; protected set; }

		public static UniqueID[] GetIDs(GameplayData[] gameplayDatas)
		{
			UniqueID[] IDs = new UniqueID[gameplayDatas.Length];

			for (int i = 0; i < gameplayDatas.Length; i++)
			{
				IDs[i] = gameplayDatas[i].ID;
			}

			return IDs;
		}
	}
}
