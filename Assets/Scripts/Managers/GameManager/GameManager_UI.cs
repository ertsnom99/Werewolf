using Fusion;
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
		public void DisplayTitle(int titleID, Dictionary<string, IVariable> variables = null, bool showConfirmButton = false, float countdownDuration = -1, bool fastFade = false)
		{
			if (!_gameplayDataManager.TryGetGameplayData(titleID, out TitleScreenData titleScreenData))
			{
				Debug.LogError($"Could not find the title {titleID}");
				return;
			}

			_UIManager.TitleScreen.Initialize(titleScreenData.Image, titleScreenData.Text, variables, showConfirmButton, titleScreenData.PromptButtonText, countdownDuration);
			_UIManager.FadeIn(_UIManager.TitleScreen, fastFade ? Config.UITransitionFastDuration : Config.UITransitionNormalDuration);
		}

		public void DisplayTitle(Sprite image, LocalizedString title, Dictionary<string, IVariable> variables = null)
		{
			_UIManager.TitleScreen.Initialize(image, title, variables);
			_UIManager.FadeIn(_UIManager.TitleScreen, Config.UITransitionNormalDuration);
		}

		private IEnumerator DisplayTitleForAllPlayers(int titleID, float holdDuration, int roleID = -1, string playerNickname = "")
		{
			if (holdDuration < Config.UITransitionNormalDuration)
			{
				Debug.LogError($"{nameof(holdDuration)} most not be smaller than {Config.UITransitionNormalDuration}");
			}

			RPC_DisplayTitle(titleID, roleID, playerNickname);
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(titleID, CreateTitleVariables(roleID, playerNickname));
#endif
			yield return new WaitForSeconds(holdDuration - Config.UITransitionNormalDuration);
			RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			HideUI();
#endif
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);
		}

		public void HideUI()
		{
			_UIManager.FadeOutAll(Config.UITransitionNormalDuration);
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
	}
}