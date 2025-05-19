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

		public void HideUI()
		{
			_UIManager.FadeOutAll(GameConfig.UITransitionNormalDuration);
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
		public void RPC_DisplayTitle(int titleID, int roleID = -1, string playerNickname = "", bool fastFade = false)
		{
			DisplayTitle(titleID, CreateTitleVariables(roleID, playerNickname), fastFade: fastFade);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayTitle([RpcTarget] PlayerRef player, int titleID, int roleID = -1, string playerNickname = "", bool fastFade = false)
		{
			DisplayTitle(titleID, CreateTitleVariables(roleID, playerNickname), fastFade: fastFade);
		}

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
	}
}