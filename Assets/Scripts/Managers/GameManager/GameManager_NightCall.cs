using Fusion;
using System.Collections.Generic;
using UnityEngine;

namespace Werewolf
{
	public partial class GameManager
	{
		private List<NightCall> _nightCalls = new();

		private struct NightCall
		{
			public int PriorityIndex;
			public List<PlayerRef> Players;
		}

		private int _currentNightCallIndex = 0;

		private void AddPlayerToNightCall(int priorityIndex, PlayerRef player)
		{
			NightCall nightCall;

			for (int i = 0; i < _nightCalls.Count; i++)
			{
				if (_nightCalls[i].PriorityIndex == priorityIndex)
				{
					if (_nightCalls[i].Players.Contains(player))
					{
						Debug.LogError("Tried to add duplicated player to a night call");
						return;
					}

					_nightCalls[i].Players.Add(player);
					return;
				}
				else if (_nightCalls[i].PriorityIndex > priorityIndex)
				{
					nightCall = new();
					nightCall.PriorityIndex = priorityIndex;
					nightCall.Players = new() { player };

					_nightCalls.Insert(i, nightCall);

					if (i <= _currentNightCallIndex)
					{
						_currentNightCallIndex++;
					}

					return;
				}
			}

			nightCall = new();
			nightCall.PriorityIndex = priorityIndex;
			nightCall.Players = new() { player };

			_nightCalls.Add(nightCall);
		}

		public void RemovePlayerFromNightCall(int priorityIndex, PlayerRef player)
		{
			for (int i = 0; i < _nightCalls.Count; i++)
			{
				if (_nightCalls[i].PriorityIndex != priorityIndex)
				{
					continue;
				}

				_nightCalls[i].Players.Remove(player);

				if (_nightCalls[i].Players.Count <= 0)
				{
					_nightCalls.RemoveAt(i);

					if (i <= _currentNightCallIndex)
					{
						_currentNightCallIndex--;
					}
				}

				break;
			}
		}
	}
}