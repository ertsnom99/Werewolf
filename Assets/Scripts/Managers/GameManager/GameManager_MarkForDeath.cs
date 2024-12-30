using Assets.Scripts.Data.Tags;
using Fusion;
using System;
using System.Collections.Generic;

namespace Werewolf.Managers
{
	public partial class GameManager
	{
		private readonly List<MarkForDeath> _marksForDeath = new();

		public struct MarkForDeath
		{
			public PlayerRef Player;
			public GameplayTag Mark;
		}

		public event Action<PlayerRef, GameplayTag> MarkForDeathAdded;

		public void AddMarkForDeath(PlayerRef player, GameplayTag markForDeath)
		{
			_marksForDeath.Add(new() { Player = player, Mark = markForDeath });
			MarkForDeathAdded?.Invoke(player, markForDeath);
		}

		public void AddMarkForDeath(PlayerRef player, GameplayTag markForDeath, int index)
		{
			if (_marksForDeath.Count < index)
			{
				_marksForDeath.Add(new() { Player = player, Mark = markForDeath });
			}
			else
			{
				_marksForDeath.Insert(index, new() { Player = player, Mark = markForDeath });
			}

			MarkForDeathAdded?.Invoke(player, markForDeath);
		}

		public void RemoveMarkForDeath(PlayerRef player, GameplayTag markForDeath)
		{
			for (int i = 0; i < _marksForDeath.Count; i++)
			{
				if (_marksForDeath[i].Player == player && _marksForDeath[i].Mark == markForDeath)
				{
					_marksForDeath.RemoveAt(i);
				}
			}
		}

		public void MoveMarksForDeathToLast(PlayerRef player)
		{
			int index = 0;

			foreach (var marksForDeath in _marksForDeath)
			{
				if (marksForDeath.Player == player)
				{
					MarkForDeath tempMarkForDeath = _marksForDeath[index];
					_marksForDeath.RemoveAt(index);
					_marksForDeath.Add(tempMarkForDeath);

					return;
				}

				index++;
			}
		}

		public PlayerRef[] GetPlayersWithMarkForDeath(GameplayTag inMarkForDeath)
		{
			List<PlayerRef> players = new();

			foreach (MarkForDeath markForDeath in _marksForDeath)
			{
				if (markForDeath.Mark == inMarkForDeath)
				{
					players.Add(markForDeath.Player);
				}
			}

			return players.ToArray();
		}
	}
}