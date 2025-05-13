using Fusion;
using Werewolf.Data;

namespace Werewolf.Managers
{
	public interface IGameManagerSubscriber
	{
		void OnPlayerDied(PlayerRef deadPlayer, MarkForDeathData markForDeath);
	}
}