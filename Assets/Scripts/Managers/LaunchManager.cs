using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Werewolf.Network.Configs;

namespace Werewolf
{
    public class LaunchManager : MonoBehaviour
    {
        /// <summary>
        /// Network Runner Prefab used to Spawn a new Runner used by the Server
        /// </summary>
        [SerializeField]
        private NetworkRunner _runnerPrefab;

        private async void Start()
        {
            await Task.Yield();
#if UNITY_SERVER
            DedicatedServerConfig config = DedicatedServerConfig.FillConfig();

            Log.Info(config);

            // Add a new Runner component
            NetworkRunner runner = Instantiate(_runnerPrefab);

            // Start the Server
            StartGameResult result = await StartSimulation(runner, config);

            // Check if all went fine
            if (result.Ok)
            {
                Log.Info($"Runner Start DONE");
            }
            else
            {
                // Quit the application if startup fails
                Log.Info($"Error while starting Server: {result.ShutdownReason}");

                // it can be used any error code that can be read by an external application
                // using 0 means all went fine
                Application.Quit(1);
            }
#else
            SceneManager.LoadScene((int)SceneDefs.MENU, LoadSceneMode.Single);
#endif
        }


#if UNITY_SERVER
        private Task<StartGameResult> StartSimulation(NetworkRunner runner, DedicatedServerConfig serverConfig) => StartSimulation(
            runner,
            serverConfig.SessionName,
            serverConfig.SessionProperties,
            serverConfig.Port,
            serverConfig.Lobby,
            serverConfig.PublicIP,
            serverConfig.PublicPort
        );

        private Task<StartGameResult> StartSimulation(
            NetworkRunner runner,
            string SessionName,
            Dictionary<string, SessionProperty> customProps = null,
            ushort port = 0,
            string customLobby = null,
            string customPublicIP = null,
            ushort customPublicPort = 0
        )
        {
            // Build Custom External Address
            NetAddress? externalAddr = null;

            if (string.IsNullOrEmpty(customPublicIP) == false && customPublicPort > 0)
            {
                if (IPAddress.TryParse(customPublicIP, out IPAddress _))
                {
                    externalAddr = NetAddress.CreateFromIpPort(customPublicIP, customPublicPort);
                }
                else
                {
                    Log.Warn("Unable to parse 'Custom Public IP'");
                }
            }

            // Create the NetworkSceneInfo
            SceneRef scene = SceneRef.FromIndex((int)SceneDefs.MENU);
            NetworkSceneInfo sceneInfo = new NetworkSceneInfo();

            if (scene.IsValid)
            {
                sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
            }

            // Start Runner
            return runner.StartGame(new StartGameArgs()
            {
                SessionName = SessionName,
                GameMode = GameMode.Server,
                SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
                Scene = scene,
                SessionProperties = customProps,
                Address = NetAddress.Any(port),
                CustomPublicAddress = externalAddr,
                CustomLobbyName = customLobby,
            });
        }
#endif
    }
}
