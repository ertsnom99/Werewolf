using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using Werewolf.Network.Configs;

namespace Werewolf
{
    public class MainMenuManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Menu")]
        [SerializeField]
        private MainMenu _mainMenu;

        [SerializeField]
        private RoomMenu _roomMenu;

        [Header("Network")]
        [SerializeField]
        private NetworkRunner _runnerPrefab;

        private NetworkRunner _runner;

        #region Connection
        public void JoinGame()
        {
            if (!_mainMenu)
            {
                Debug.LogError("_mainMenu of the MainMenuManager is null");
                return;
            }

            if (!_roomMenu)
            {
                Debug.LogError("_roomMenu of the MainMenuManager is null");
                return;
            }

            _mainMenu.DisableMenu("Joining session...");

            _runner = GetRunner("Client");
            ConnectToServer(_runner, _mainMenu.GetSessionName());
        }

        public void LeaveGame()
        {
            _runner.Shutdown();
        }

        private NetworkRunner GetRunner(string name)
        {
            var runner = Instantiate(_runnerPrefab);
            runner.name = name;
            runner.AddCallbacks(this);

            return runner;
        }

        private Task<StartGameResult> ConnectToServer(NetworkRunner runner, string sessionName)
        {
            // Create the NetworkSceneInfo
            SceneRef scene = SceneRef.FromIndex((int)SceneDefs.MENU);
            NetworkSceneInfo sceneInfo = new NetworkSceneInfo();

            if (scene.IsValid)
            {
                sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
            }

            return runner.StartGame(new StartGameArgs()
            {
                SessionName = sessionName,
                GameMode = GameMode.Client,
                SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
                Scene = scene,
            });
        }

        #endregion

        #region UI
        private void DisplayMainMenu()
        {
            _mainMenu.gameObject.SetActive(true);
            _roomMenu.gameObject.SetActive(false);
        }

        private void DisplayRoomMenu()
        {
            _mainMenu.gameObject.SetActive(false);
            _roomMenu.gameObject.SetActive(true);
        }
        #endregion

        #region Unused Callbacks
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Log.Info($"{nameof(OnShutdown)}: {nameof(shutdownReason)}: {shutdownReason}");

            switch (shutdownReason)
            {
                case ShutdownReason.Ok:
                    DisplayMainMenu();
                    _mainMenu.ResetMenu("");
                    break;
                case ShutdownReason.DisconnectedByPluginLogic:
                    DisplayMainMenu();
                    _mainMenu.ResetMenu("The server was closed");
                    break;
                case ShutdownReason.GameNotFound:
                    _mainMenu.ResetMenu($"No session named {_mainMenu.GetSessionName()} was found");
                    break;
            }
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Log.Info($"{nameof(OnConnectedToServer)}: {nameof(runner.CurrentConnectionType)}: {runner.CurrentConnectionType}, {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");

            // Join success
            DisplayRoomMenu();

            // TODO: send nickname once connected
            // TODO: Update player list
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Log.Info($"{nameof(OnDisconnectedFromServer)} - {reason}: {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            Log.Info($"{nameof(OnConnectRequest)}: {nameof(request.RemoteAddress)}: {request.RemoteAddress}");
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Log.Info($"{nameof(OnConnectFailed)}: {nameof(remoteAddress)}: {remoteAddress}, {nameof(reason)}: {reason}");
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        public void OnSceneLoadDone(NetworkRunner runner) { }

        public void OnSceneLoadStart(NetworkRunner runner) { }
        #endregion
    }
}