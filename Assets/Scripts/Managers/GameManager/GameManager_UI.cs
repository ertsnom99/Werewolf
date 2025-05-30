using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Werewolf.Data;

namespace Werewolf.Managers
{
	public partial class GameManager
	{
		private readonly Dictionary<PlayerRef, Action> _quickActionCallbacks = new();

		#region Display Title
		public void DisplayTitle(int titleID, Dictionary<string, IVariable> variables = null, bool showConfirmButton = false, float countdownDuration = -1, bool fastFade = false)
		{
			if (!_gameplayDataManager.TryGetGameplayData(titleID, out TitleScreenData titleScreenData))
			{
				Debug.LogError($"Could not find the title {titleID}");
				return;
			}

			_UIManager.TitleScreen.Initialize(titleScreenData.Image, titleScreenData.Text, variables, showConfirmButton, titleScreenData.PromptButtonText, countdownDuration);
			_UIManager.FadeIn(_UIManager.TitleScreen, fastFade ? GameConfig.UITransitionFastDuration : GameConfig.UITransitionNormalDuration);
		}

		public void DisplayTitle(Sprite image, LocalizedString title, Dictionary<string, IVariable> variables = null)
		{
			_UIManager.TitleScreen.Initialize(image, title, variables);
			_UIManager.FadeIn(_UIManager.TitleScreen, GameConfig.UITransitionNormalDuration);
		}

		private IEnumerator DisplayTitleForAllPlayers(int titleID, float holdDuration, int roleID = -1, string playerNickname = "")
		{
			if (holdDuration < GameConfig.UITransitionNormalDuration)
			{
				Debug.LogError($"{nameof(holdDuration)} most not be smaller than {GameConfig.UITransitionNormalDuration}");
			}

			RPC_DisplayTitle(titleID, roleID, playerNickname);
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(titleID, CreateTitleVariables(roleID, playerNickname));
#endif
			yield return new WaitForSeconds(holdDuration - GameConfig.UITransitionNormalDuration);
			RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			HideUI();
#endif
			yield return new WaitForSeconds(GameConfig.UITransitionNormalDuration);
		}

		private Dictionary<string, IVariable> CreateTitleVariables(int roleID = -1, string playerNickname = "")
		{
			if (roleID == -1 && string.IsNullOrEmpty(playerNickname))
			{
				return null;
			}
			else
			{
				Dictionary<string, IVariable> variables = new();

				if (roleID != -1)
				{
					if (!_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
					{
						Debug.LogError($"Could not find the role {roleID}");
					}

					variables.Add("Role", roleData.NameSingular);
				}

				if (!string.IsNullOrEmpty(playerNickname))
				{
					variables.Add("Player", new StringVariable() { Value = playerNickname });
				}

				return variables;
			}
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayTitle(int titleID, int roleID = -1, string playerNickname = "", bool fastFade = false)
		{
			DisplayTitle(titleID, CreateTitleVariables(roleID, playerNickname), fastFade: fastFade);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayTitle([RpcTarget] PlayerRef player, int titleID, int roleID = -1, string playerNickname = "", bool fastFade = false)
		{
			DisplayTitle(titleID, CreateTitleVariables(roleID, playerNickname), fastFade: fastFade);
		}
		#endregion
		#endregion

		#region Prompt
		public bool Prompt(PlayerRef promptedPlayer, int titleID, float duration, Action<PlayerRef> callback, bool fastFade = true)
		{
			if (!_networkDataManager.PlayerInfos[promptedPlayer].IsConnected || _promptPlayerCallbacks.ContainsKey(promptedPlayer))
			{
				return false;
			}

			_promptPlayerCallbacks.Add(promptedPlayer, callback);
			RPC_Prompt(promptedPlayer, titleID, duration, fastFade);

			return true;
		}

		private void OnPromptAccepted()
		{
			_UIManager.TitleScreen.ConfirmClicked -= OnPromptAccepted;
			RPC_AcceptPrompt();
		}

		public void StopPrompting(PlayerRef player, bool fastFade = true)
		{
			if (!_networkDataManager.PlayerInfos[player].IsConnected)
			{
				return;
			}

			_promptPlayerCallbacks.Remove(player);
			RPC_StopPrompting(player, fastFade);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_Prompt([RpcTarget] PlayerRef player, int titleID, float duration, bool fastFade)
		{
			_UIManager.TitleScreen.ConfirmClicked += OnPromptAccepted;
			DisplayTitle(titleID, showConfirmButton: true, countdownDuration: duration, fastFade: fastFade);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_AcceptPrompt(RpcInfo info = default)
		{
			if (!_promptPlayerCallbacks.TryGetValue(info.Source, out var callback))
			{
				return;
			}

			_promptPlayerCallbacks[info.Source](info.Source);
			_promptPlayerCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_StopPrompting([RpcTarget] PlayerRef player, bool fastFade)
		{
			_UIManager.TitleScreen.ConfirmClicked -= OnPromptAccepted;
			
			if (_UIManager.TitleScreen.IsConfirmButtonActive())
			{
				_UIManager.FadeOut(_UIManager.TitleScreen, fastFade ? GameConfig.UITransitionFastDuration : GameConfig.UITransitionNormalDuration);
			}
		}
		#endregion
		#endregion

		#region Display Quick Action
		public void DisplayQuickAction(PlayerRef player, int quickActionID, Action callback)
		{
			if (!_networkDataManager.PlayerInfos[player].IsConnected || _promptPlayerCallbacks.ContainsKey(player))
			{
				return;
			}

			_quickActionCallbacks.Add(player, callback);
			RPC_DisplayQuickAction(player, quickActionID);
		}

		private void OnQuickActionTriggered()
		{
			_UIManager.QuickActionScreen.QuickActionTriggered -= OnQuickActionTriggered;
			RPC_TriggerQuickAction();
		}

		public void HideQuickAction(PlayerRef player)
		{
			if (!_networkDataManager.PlayerInfos[player].IsConnected)
			{
				return;
			}

			_quickActionCallbacks.Remove(player);
			RPC_HideQuickAction(player);
		}

		#region RPC Calls

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_DisplayQuickAction([RpcTarget] PlayerRef player, int quickActionID)
		{
			if (!_gameplayDataManager.TryGetGameplayData(quickActionID, out QuickActionScreenData quickActionScreenData))
			{
				Debug.LogError($"Could not find the quick action {quickActionID}");
				return;
			}

			_UIManager.QuickActionScreen.Initialize(quickActionScreenData.Icon);
			_UIManager.QuickActionScreen.QuickActionTriggered += OnQuickActionTriggered;

			_UIManager.FadeIn(_UIManager.QuickActionScreen, GameConfig.UITransitionFastDuration);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_TriggerQuickAction(RpcInfo info = default)
		{
			if (!_quickActionCallbacks.TryGetValue(info.Source, out var callback))
			{
				return;
			}

			_quickActionCallbacks[info.Source]();
			_quickActionCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_HideQuickAction([RpcTarget] PlayerRef player)
		{
			_UIManager.QuickActionScreen.QuickActionTriggered -= OnQuickActionTriggered;
			_UIManager.FadeOut(_UIManager.QuickActionScreen, GameConfig.UITransitionNormalDuration);
		}
		#endregion
		#endregion

		#region Hide UI
		public void HideUI()
		{
			_UIManager.FadeOutAll(GameConfig.UITransitionNormalDuration);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_HideUI()
		{
			HideUI();
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_HideUI([RpcTarget] PlayerRef player)
		{
			HideUI();
		}
		#endregion
		#endregion
	}
}