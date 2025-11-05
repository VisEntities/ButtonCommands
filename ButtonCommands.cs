/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Button Commands", "VisEntities", "2.2.0")]
    [Description("Run commands when an electric button is pressed.")]
    public class ButtonCommands : RustPlugin
    {
        #region Fields

        private static ButtonCommands _plugin;
        private StoredData _storedData;
        private readonly Dictionary<ulong, Dictionary<ulong, float>> _lastPressTimesByButtonAndPlayer = new Dictionary<ulong, Dictionary<ulong, float>>();

        #endregion Fields

        #region Stored Data

        public class StoredData
        {
            [JsonProperty("Press Buttons")]
            public Dictionary<ulong, ButtonData> PressButtons { get; set; } = new Dictionary<ulong, ButtonData>();
        }

        public class ButtonData
        {
            [JsonProperty("Require Button Powered")]
            public bool RequireButtonPowered { get; set; }

            [JsonProperty("Disable Power Output On Press")]
            public bool DisablePowerOutputOnPress { get; set; }

            [JsonProperty("Run Random Command")]
            public bool RunRandomCommand { get; set; }

            [JsonProperty("Cooldown Seconds")]
            public float CooldownSeconds { get; set; }

            [JsonProperty("Commands")]
            public List<CommandData> Commands { get; set; } = new List<CommandData>();
        }

        public class CommandData
        {
            [JsonProperty("Type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public CommandType Type { get; set; }

            [JsonProperty("Command")]
            public string Command { get; set; }
        }

        #endregion Stored Data

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
            _storedData = DataFileUtil.LoadOrCreate<StoredData>(DataFileUtil.GetFilePath());
        }

        private void Unload()
        {
            _lastPressTimesByButtonAndPlayer.Clear();
            _plugin = null;
        }

        private object OnButtonPress(PressButton button, BasePlayer player)
        {
            if (button == null || player == null)
                return null;

            ulong buttonId = button.net.ID.Value;
            if (!_storedData.PressButtons.ContainsKey(buttonId))
                return null;

            ButtonData buttonData = _storedData.PressButtons[buttonId];

            if (buttonData.RequireButtonPowered && !button.IsPowered())
                return null;

            if (buttonData.CooldownSeconds > 0f)
            {
                Dictionary<ulong, float> lastPressByPlayer;
                bool hasMap = _lastPressTimesByButtonAndPlayer.TryGetValue(buttonId, out lastPressByPlayer);
                if (!hasMap)
                {
                    lastPressByPlayer = new Dictionary<ulong, float>();
                    _lastPressTimesByButtonAndPlayer[buttonId] = lastPressByPlayer;
                }

                float lastPressTime;
                bool hasTime = lastPressByPlayer.TryGetValue(player.userID, out lastPressTime);
                if (hasTime)
                {
                    float now = Time.realtimeSinceStartup;
                    float elapsed = now - lastPressTime;
                    if (elapsed < buttonData.CooldownSeconds)
                    {
                        int remaining = Mathf.CeilToInt(buttonData.CooldownSeconds - elapsed);
                        string pretty = FormatDuration(remaining);
                        ReplyToPlayer(player, Lang.Error_CooldownActive, pretty);
                        return null;
                    }
                }

                lastPressByPlayer[player.userID] = Time.realtimeSinceStartup;
            }

            if (buttonData.RunRandomCommand && buttonData.Commands.Count > 0)
            {
                var cmd = buttonData.Commands[UnityEngine.Random.Range(0, buttonData.Commands.Count)];
                RunCommand(player, cmd.Type, cmd.Command);
            }
            else
            {
                foreach (var cmd in buttonData.Commands)
                {
                    RunCommand(player, cmd.Type, cmd.Command);
                }
            }

            if (buttonData.DisablePowerOutputOnPress)
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

        #region Helper Classes

        public static class DataFileUtil
        {
            private const string FOLDER = "";

            public static string GetFilePath(string filename = null)
            {
                if (filename == null)
                    filename = _plugin.Name;

                return Path.Combine(FOLDER, filename);
            }

            public static string[] GetAllFilePaths()
            {
                string[] filePaths = Interface.Oxide.DataFileSystem.GetFiles(FOLDER);
                for (int i = 0; i < filePaths.Length; i++)
                {
                    filePaths[i] = filePaths[i].Substring(0, filePaths[i].Length - 5);
                }

                return filePaths;
            }

            public static bool Exists(string filePath)
            {
                return Interface.Oxide.DataFileSystem.ExistsDatafile(filePath);
            }

            public static T Load<T>(string filePath) where T : class, new()
            {
                T data = Interface.Oxide.DataFileSystem.ReadObject<T>(filePath);
                if (data == null)
                    data = new T();

                return data;
            }

            public static T LoadIfExists<T>(string filePath) where T : class, new()
            {
                if (Exists(filePath))
                    return Load<T>(filePath);
                else
                    return null;
            }

            public static T LoadOrCreate<T>(string filePath) where T : class, new()
            {
                T data = LoadIfExists<T>(filePath);
                if (data == null)
                    data = new T();

                return data;
            }

            public static void Save<T>(string filePath, T data)
            {
                Interface.Oxide.DataFileSystem.WriteObject<T>(filePath, data);
            }

            public static void Delete(string filePath)
            {
                Interface.Oxide.DataFileSystem.DeleteDataFile(filePath);
            }
        }

        #endregion Helper Classes

        #region Helper Functions

        private static string FormatDuration(int totalSeconds)
        {
            if (totalSeconds <= 0)
                return "0s";

            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            List<string> parts = new List<string>();

            if (hours > 0)
                parts.Add(hours.ToString() + "h");
            if (minutes > 0)
                parts.Add(minutes.ToString() + "m");
            if (seconds > 0 || parts.Count == 0)
                parts.Add(seconds.ToString() + "s");

            return string.Join(" ", parts.ToArray());
        }

        #endregion Helper Functions

        #region Enums

        public enum CommandType
        {
            Chat,
            Server,
            Client
        }

        #endregion Enums

        #region Permissions

        private static class PermissionUtil
        {
            public const string ADMIN = "buttoncommands.admin";
            private static readonly List<string> _permissions = new List<string>
            {
                ADMIN,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions

        #region Commands

        [ConsoleCommand("bc.add")]
        private void cmdConsoleAddButton(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.ADMIN))
            {
                ReplyToPlayer(player, Lang.Error_NoPermission);
                return;
            }

            Ray ray = new Ray(player.eyes.position, player.eyes.BodyForward());
            if (Physics.Raycast(ray, out RaycastHit hit, 10f))
            {
                PressButton button = hit.GetEntity() as PressButton;
                if (button != null)
                {
                    ulong id = button.net.ID.Value;
                    if (_storedData.PressButtons.ContainsKey(id))
                    {
                        ReplyToPlayer(player, Lang.Error_AlreadyRegistered);
                        return;
                    }
                    ButtonData defaultData = new ButtonData
                    {
                        RequireButtonPowered = true,
                        DisablePowerOutputOnPress = true,
                        RunRandomCommand = false,
                        CooldownSeconds = 60f,
                        Commands = new List<CommandData>
                        {
                            new CommandData
                            {
                                Type = CommandType.Chat,
                                Command = "Hello, {PlayerName}!"
                            },
                            new CommandData
                            {
                                Type = CommandType.Server,
                                Command = "inventory.giveto {PlayerId} scrap 50"
                            },
                            new CommandData
                            {
                                Type = CommandType.Client,
                                Command = "heli.calltome"
                            }
                        }
                    };
                    _storedData.PressButtons[id] = defaultData;
                    DataFileUtil.Save(DataFileUtil.GetFilePath(), _storedData);
                    ReplyToPlayer(player, Lang.Info_ButtonRegistered);
                }
                else
                {
                    ReplyToPlayer(player, Lang.Error_NoButtonInSight);
                }
            }
            else
            {
                ReplyToPlayer(player, Lang.Error_NoButtonInRange);
            }
        }

        #endregion Commands

        #region Localization

        private class Lang
        {
            public const string Error_NoPermission = "Error.NoPermission";
            public const string Error_AlreadyRegistered = "Error.AlreadyRegistered";
            public const string Error_NoButtonInSight = "Error.NoButtonInSight";
            public const string Error_NoButtonInRange = "Error.NoButtonInRange";
            public const string Error_CooldownActive = "Error.CooldownActive";
            public const string Info_ButtonRegistered = "Info.ButtonRegistered";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.Error_NoPermission] = "You do not have permission to use this command.",
                [Lang.Error_AlreadyRegistered] = "This button already has commands assigned.",
                [Lang.Error_NoButtonInSight] = "You must be looking directly at a button to register it.",
                [Lang.Error_NoButtonInRange] = "You are too far away from the button to register it.",
                [Lang.Error_CooldownActive] = "You must wait {0} before using this button again.",
                [Lang.Info_ButtonRegistered] = "Button registered successfully. It will now run the assigned commands."
            }, this, "en");
        }

        private static string GetMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string userId;
            if (player != null)
                userId = player.UserIDString;
            else
                userId = null;

            string message = _plugin.lang.GetMessage(messageKey, _plugin, userId);

            if (args.Length > 0)
                message = string.Format(message, args);

            return message;
        }

        public static void ReplyToPlayer(BasePlayer player, string messageKey, params object[] args)
        {
            string message = GetMessage(player, messageKey, args);

            if (!string.IsNullOrWhiteSpace(message))
                _plugin.SendReply(player, message);
        }

        #endregion Localization
    }
}