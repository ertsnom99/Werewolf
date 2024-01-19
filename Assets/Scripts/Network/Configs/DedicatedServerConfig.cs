using Fusion;
using System.Collections.Generic;

namespace Werewolf.Network.Configs
{
    public class DedicatedServerConfig
    {
        public string SessionName { get; set; }
        public int MaxPlayerCount { get; set; }
        public string Lobby { get; set; }
        public ushort Port { get; set; } = 27015;
        public ushort PublicPort { get; set; }
        public string PublicIP { get; set; }
        public Dictionary<string, SessionProperty> SessionProperties { get; private set; } = new Dictionary<string, SessionProperty>();

        private DedicatedServerConfig() { }

        public static DedicatedServerConfig FillConfig()
        {
            DedicatedServerConfig config = new DedicatedServerConfig();

            // Session Name
            if (CommandLineUtilities.TryGetArg(out string sessionName, "-session"))
            {
                config.SessionName = sessionName;
            }

            // Max Player Count
            if (CommandLineUtilities.TryGetArg(out string maxPlayerCountString, "-maxPlayerCount") && int.TryParse(maxPlayerCountString, out int maxPlayerCount))
            {
                config.MaxPlayerCount = maxPlayerCount;
            }

            // Server Lobby
            if (CommandLineUtilities.TryGetArg(out string customLobby, "-lobby"))
            {
                config.Lobby = customLobby;
            }

            // Server Port
            if (CommandLineUtilities.TryGetArg(out string customPort, "-port", "-PORT") && ushort.TryParse(customPort, out ushort port))
            {
                config.Port = port;
            }

            // Custom Public Port
            if (CommandLineUtilities.TryGetArg(out string customPublicPort, "-publicport") && ushort.TryParse(customPublicPort, out ushort publicPort))
            {
                config.PublicPort = publicPort;
            }

            // Custom Public IP
            if (CommandLineUtilities.TryGetArg(out string customPublicIP, "-publicip"))
            {
                config.PublicIP = customPublicIP;
            }

            // Server Properties
            List<(string, string)> argsCustomProps = CommandLineUtilities.GetArgumentList("-P");

            foreach ((string, string) item in argsCustomProps)
            {
                string key = item.Item1;
                string value = item.Item2;

                if (int.TryParse(value, out int result))
                {
                    config.SessionProperties.Add(key, result);
                    continue;
                }

                config.SessionProperties.Add(key, value);
            }

            return config;
        }

        public override string ToString()
        {
            string properties = string.Empty;

            foreach (KeyValuePair<string, SessionProperty> item in SessionProperties)
            {
                properties += $"{item.Value}={item.Value}, ";
            }

            return $"[{nameof(DedicatedServerConfig)}]: " +
                   $"{nameof(SessionName)}={SessionName}, " +
                   $"{nameof(Lobby)}={Lobby}, " +
                   $"{nameof(Port)}={Port}, " +
                   $"{nameof(PublicIP)}={PublicIP}, " +
                   $"{nameof(PublicPort)}={PublicPort}, " +
                   $"{nameof(SessionProperties)}={properties}]";
        }
    }
}