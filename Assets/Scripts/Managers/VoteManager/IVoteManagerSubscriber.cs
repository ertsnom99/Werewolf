using Werewolf.Gameplay;

namespace Werewolf.Managers
{
	public interface IVoteManagerSubscriber
	{
		void OnVoteStarting(ChoicePurpose purpose);
	}
}
