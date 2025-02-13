using Fusion;
using System.Collections.Generic;
using System;
using Werewolf.Data;
using Werewolf.UI;
using UnityEngine;

namespace Werewolf.Managers
{
	public partial class GameManager
	{
		private readonly Dictionary<PlayerRef, Action<int>> _makeChoiceCallbacks = new();

		public bool MakeChoice(PlayerRef choosingPlayer, int[] choiceTitleIDs, int choiceScreenID, bool mustChoose, float maximumDuration, Action<int> callback)
		{
			if (!_networkDataManager.PlayerInfos[choosingPlayer].IsConnected || _makeChoiceCallbacks.ContainsKey(choosingPlayer))
			{
				return false;
			}

			_makeChoiceCallbacks.Add(choosingPlayer, callback);
			RPC_MakeChoice(choosingPlayer, choiceTitleIDs, choiceScreenID, mustChoose, maximumDuration);

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

			if (_networkDataManager.PlayerInfos[player].IsConnected)
			{
				RPC_StopChoosing(player);
			}
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_MakeChoice([RpcTarget] PlayerRef player, int[] choiceTitleIDs, int choiceScreenID, bool mustChoose, float maximumDuration)
		{
			List<Choice.ChoiceData> choices = new();

			foreach (int choiceTitleID in choiceTitleIDs)
			{
				if (!_gameplayDataManager.TryGetGameplayData(choiceTitleID, out TitleScreenData titleScreenData))
				{
					Debug.LogError($"Could not find the title {choiceTitleID}");
				}

				if (titleScreenData != null)
				{
					choices.Add(new() { Image = titleScreenData.Image, Text = titleScreenData.Text });
				}
			}

			if (!_gameplayDataManager.TryGetGameplayData(choiceScreenID, out ChoiceScreenData choiceScreen))
			{
				Debug.LogError($"Could not find the choice screen {choiceScreenID}");
				return;
			}

			_UIManager.ChoiceScreen.ConfirmedChoice += GiveChoice;

			_UIManager.ChoiceScreen.Initialize(choices.ToArray(), choiceScreen.ChooseText, choiceScreen.ChoosedText, choiceScreen.DidNotChoosedText, mustChoose, maximumDuration);
			_UIManager.FadeIn(_UIManager.ChoiceScreen, Config.UITransitionNormalDuration);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_GiveChoice(int choice, RpcInfo info = default)
		{
			if (!_makeChoiceCallbacks.TryGetValue(info.Source, out Action<int> callback))
			{
				return;
			}

			callback(choice);
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