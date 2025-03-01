/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Button Commands", "VisEntities", "1.0.0")]
    [Description("Runs commands when an electric button is pressed.")]
    public class ButtonCommands : RustPlugin
    {
        #region Fields

        private static ButtonCommands _plugin;
        private static Configuration _config;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Require Button Powered")]
            public bool RequireButtonPowered { get; set; }

            [JsonProperty("Disable Power Output On Press")]
            public bool DisablePowerOutputOnPress { get; set; }

            [JsonProperty("Run Random Command")]
            public bool RunRandomCommand { get; set; }

            [JsonProperty("Commands To Run")]
            public List<CommandConfig> CommandsToRun { get; set; }
        }

        private class CommandConfig
        {
            [JsonProperty("Type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public CommandType Type { get; set; }

            [JsonProperty("Command")]
            public string Command { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                RequireButtonPowered = true,
                DisablePowerOutputOnPress = true,
                RunRandomCommand = false,
                CommandsToRun = new List<CommandConfig>
                {
                    new CommandConfig
                    {
                        Type = CommandType.Chat,
                        Command = "Hello, {PlayerName}!"
                    },
                    new CommandConfig
                    {
                        Type = CommandType.Server,
                        Command = "inventory.giveto {PlayerId} scrap 50"
                    },
                    new CommandConfig
                    {
                        Type = CommandType.Client,
                        Command = "heli.calltome"
                    }
                }
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private object OnButtonPress(PressButton button, BasePlayer player)
        {
            if (player == null)
                return null;

            if (_config.RequireButtonPowered && !button.IsOn())
                return null;

            if (_config.RunRandomCommand && _config.CommandsToRun.Count > 0)
            {
                var cmd = _config.CommandsToRun[UnityEngine.Random.Range(0, _config.CommandsToRun.Count)];
                RunCommand(player, cmd.Type, cmd.Command);
            }
            else
            {
                foreach (var cmd in _config.CommandsToRun)
                {
                    RunCommand(player, cmd.Type, cmd.Command);
                }
            }

            if (_config.DisablePowerOutputOnPress)
                return true;
            else
                return null;
        }

        #endregion Oxide Hooks

        #region Command Execution

        private void RunCommand(BasePlayer player, CommandType type, string command)
        {
            string withPlaceholdersReplaced = command
                .Replace("{PlayerId}", player.UserIDString)
                .Replace("{PlayerName}", player.displayName)
                .Replace("{PositionX}", player.transform.position.x.ToString())
                .Replace("{PositionY}", player.transform.position.y.ToString())
                .Replace("{PositionZ}", player.transform.position.z.ToString())
                .Replace("{Grid}", MapHelper.PositionToString(player.transform.position));

            if (type == CommandType.Chat)
            {
                player.Command(string.Format("chat.say \"{0}\"", withPlaceholdersReplaced));
            }
            else if (type == CommandType.Client)
            {
                player.Command(withPlaceholdersReplaced);
            }
            else if (type == CommandType.Server)
            {
                Server.Command(withPlaceholdersReplaced);
            }
        }

        #endregion Command Execution

        #region Enums

        private enum CommandType
        {
            Chat,
            Server,
            Client
        }

        #endregion Enums
    }
}