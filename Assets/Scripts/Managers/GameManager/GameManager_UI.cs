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
		public void DisplayTitle(int imageID, Dictionary<string, IVariable> variables = null, bool showConfirmButton = false, float countdownDuration = -1, bool fastFade = false)
		{
			ImageData imageData = _gameplayDatabaseManager.GetGameplayData<ImageData>(imageID);

			if (imageData == null)
			{
				return;
			}

			_UIManager.TitleScreen.Initialize(imageData.Image, imageData.Text, variables, showConfirmButton, imageData.PromptButtonText, countdownDuration);
			_UIManager.FadeIn(_UIManager.TitleScreen, fastFade ? Config.UITransitionFastDuration : Config.UITransitionNormalDuration);
		}

		public void DisplayTitle(Sprite image, LocalizedString title, Dictionary<string, IVariable> variables = null)
		{
			_UIManager.TitleScreen.Initialize(image, title, variables);
			_UIManager.FadeIn(_UIManager.TitleScreen, Config.UITransitionNormalDuration);
		}

		private IEnumerator DisplayTitleForAllPlayers(int imageID, float holdDuration)
		{
			if (holdDuration < Config.UITransitionNormalDuration)
			{
				Debug.LogError($"{nameof(holdDuration)} most not be smaller than {Config.UITransitionNormalDuration}");
			}

			RPC_DisplayTitle(imageID);
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(imageID);
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

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayTitle(int imageID, int roleVariable = -1, bool fastFade = false)
		{
			Dictionary<string, IVariable> variables = null;

			if (roleVariable > -1)
			{
				variables = new()
				{
					{ "Role",  _gameplayDatabaseManager.GetGameplayData<RoleData>(roleVariable).NameSingular }
				};
			}

			DisplayTitle(imageID, variables, fastFade: fastFade);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayTitle([RpcTarget] PlayerRef player, int imageID, int roleVariable = -1, bool fastFade = false)
		{
			Dictionary<string, IVariable> variables = null;

			if (roleVariable > -1)
			{
				variables = new()
				{
					{ "Role",  _gameplayDatabaseManager.GetGameplayData<RoleData>(roleVariable).NameSingular }
				};
			}

			DisplayTitle(imageID, variables, fastFade: fastFade);
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