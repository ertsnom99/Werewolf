using Assets.Scripts.Data.Tags;
using Fusion;
using System;
using System.Collections.Generic;

namespace Werewolf
{
	public partial class GameManager
	{
		private readonly List<MarkForDeath> _marksForDeath = new();

		public struct MarkForDeath
		{
			public PlayerRef Player;
			public List<GameplayTag> MarksForDeath;
		}

		public event Action<PlayerRef, GameplayTag> MarkForDeathAdded;

		public void AddMarkForDeath(PlayerRef player, GameplayTag markForDeath)
		{
			_marksForDeath.Add(new() { Player = player, MarksForDeath = new() { markForDeath } });
			MarkForDeathAdded?.Invoke(player, markForDeath);
		}

		public void AddMarkForDeath(PlayerRef player, GameplayTag markForDeath, int index)
		{
			if (_marksForDeath.Count < index)
			{
				_marksForDeath.Add(new() { Player = player, MarksForDeath = new() { markForDeath } });
			}
			else
			{
				_marksForDeath.Insert(index, new() { Player = player, MarksForDeath = new() { markForDeath } });
			}

			MarkForDeathAdded?.Invoke(player, markForDeath);
		}

		public void RemoveMarkForDeath(PlayerRef player, GameplayTag markForDeath)
		{
			for (int i = 0; i < _marksForDeath.Count; i++)
			{
				if (_marksForDeath[i].Player != player)
				{
					continue;
				}

				_marksForDeath[i].MarksForDeath.Remove(markForDeath);

				if (_marksForDeath[i].MarksForDeath.Count <= 0)
				{
					_marksForDeath.RemoveAt(i);
				}

				return;
			}
		}

		public void RemoveAllMarkForDeath(PlayerRef player)
		{
			for (int i = 0; i < _marksForDeath.Count; i++)
			{
				if (_marksForDeath[i].Player != player)
				{
					continue;
				}

				_marksForDeath.RemoveAt(i);
				return;
			}
		}

		public PlayerRef[] GetPlayersWithMarkForDeath(GameplayTag inMarkForDeath)
		{
			List<PlayerRef> players = new();

			foreach (MarkForDeath markForDeath in _marksForDeath)
			{
				if (markForDeath.MarksForDeath.Contains(inMarkForDeath))
				{
					players.Add(markForDeath.Player);
				}
			}

			return players.ToArray();
		}
	}
}