using Fusion;
using System.Collections;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public partial class GameManager
	{
		public void DisplayTitle(int imageID, float countdownDuration = -1, bool showConfirmButton = false, string confirmButtonText = "", bool fastFade = false)
		{
			if (!Config.ImagesData.GetImageData(imageID, out ImageData titleData))
			{
				return;
			}

			_UIManager.TitleScreen.Initialize(titleData.Image, titleData.Text, countdownDuration, showConfirmButton, confirmButtonText);
			_UIManager.FadeIn(_UIManager.TitleScreen, fastFade ? Config.UITransitionFastDuration : Config.UITransitionNormalDuration);
		}

		public void DisplayTitle(Sprite image, string title, float countdownDuration = -1, bool showConfirmButton = false, string confirmButtonText = "", bool fastFade = false)
		{
			_UIManager.TitleScreen.Initialize(image, title, countdownDuration, showConfirmButton, confirmButtonText);
			_UIManager.FadeIn(_UIManager.TitleScreen, fastFade ? Config.UITransitionFastDuration : Config.UITransitionNormalDuration);
		}

		private IEnumerator DisplayTitleForAllPlayers(int imageID, float holdDuration)
		{
			if (holdDuration < Config.UITransitionNormalDuration)
			{
				Debug.LogError("holdDuration most not be smaller than Config.UITransitionNormalDuration");
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
		public void RPC_DisplayTitle(int imageID)
		{
			DisplayTitle(imageID);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayTitle([RpcTarget] PlayerRef player, string title)
		{
			DisplayTitle(null, title);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayTitle([RpcTarget] PlayerRef player, int imageID)
		{
			DisplayTitle(imageID);
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