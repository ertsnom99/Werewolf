using Fusion;
using System.Collections.Generic;
using System;
using Werewolf.Data;
using Werewolf.UI;

namespace Werewolf
{
	public partial class GameManager
	{
		private readonly Dictionary<PlayerRef, Action<int>> _makeChoiceCallbacks = new();

		public bool MakeChoice(PlayerRef choosingPlayer, int[] choiceImageIDs, float maximumDuration, string chooseText, string choosedText, string didNotChoosedText, bool mustChoose, Action<int> callback)
		{
			if (!_networkDataManager.PlayerInfos[choosingPlayer].IsConnected || _makeChoiceCallbacks.ContainsKey(choosingPlayer))
			{
				return false;
			}

			_makeChoiceCallbacks.Add(choosingPlayer, callback);
			RPC_MakeChoice(choosingPlayer, choiceImageIDs, maximumDuration, chooseText, choosedText, didNotChoosedText, mustChoose);

			return true;
		}

		private void GiveChoice(int choice)
		{
			_UIManager.ChoiceScreen.ConfirmedChoice -= GiveChoice;
			RPC_GiveChoice(choice);
		}

		public void StopChoosing(PlayerRef player)
		{
			_makeChoiceCallbacks.Remove(player);

			if (!_networkDataManager.PlayerInfos[player].IsConnected)
			{
				return;
			}

			RPC_StopChoosing(player);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_MakeChoice([RpcTarget] PlayerRef player, int[] choiceImageIDs, float maximumDuration, string chooseText, string choosedText, string didNotChoosedText, bool mustChoose)
		{
			List<Choice.ChoiceData> choices = new();

			foreach (int choiceImageID in choiceImageIDs)
			{
				ImageData imageData = _gameplayDatabaseManager.GetGameplayData<ImageData>(choiceImageID);

				if (imageData == null)
				{
					continue;
				}

				choices.Add(new() { Image = imageData.Image, Name = imageData.Text });
			}

			_UIManager.ChoiceScreen.ConfirmedChoice += GiveChoice;

			_UIManager.ChoiceScreen.Initialize(maximumDuration, chooseText, choosedText, didNotChoosedText, choices.ToArray(), mustChoose);
			_UIManager.FadeIn(_UIManager.ChoiceScreen, Config.UITransitionNormalDuration);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_GiveChoice(int choice, RpcInfo info = default)
		{
			if (!_makeChoiceCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_makeChoiceCallbacks[info.Source](choice);
			_makeChoiceCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_StopChoosing([RpcTarget] PlayerRef player)
		{
			_UIManager.ChoiceScreen.StopCountdown();
			_UIManager.ChoiceScreen.DisableConfirmButton();
			_UIManager.ChoiceScreen.ConfirmedChoice -= GiveChoice;
		}
		#endregion
	}
}