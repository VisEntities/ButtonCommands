﻿/*
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
    [Info("Button Commands", "VisEntities", "2.0.0")]
    [Description("Run commands when an electric button is pressed.")]
    public class ButtonCommands : RustPlugin
    {
        #region Fields

        private static ButtonCommands _plugin;
        private StoredData _storedData;

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
            _plugin = null;
        }

        private object OnButtonPress(PressButton button, BasePlayer player)
        {
            if (button == null || player == null)
                return null;

            ulong buttonId = button.net.ID.Value;
            if (!_storedData.PressButtons.ContainsKey(buttonId))
                return null;

            ButtonData btnData = _storedData.PressButtons[buttonId];

            if (btnData.RequireButtonPowered && !button.IsOn())
                return null;

            if (btnData.RunRandomCommand && btnData.Commands.Count > 0)
            {
                var cmd = btnData.Commands[UnityEngine.Random.Range(0, btnData.Commands.Count)];
                RunCommand(player, cmd.Type, cmd.Command);
            }
            else
            {
                foreach (var cmd in btnData.Commands)
                {
                    RunCommand(player, cmd.Type, cmd.Command);
                }
            }

            if (btnData.DisablePowerOutputOnPress)
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

        private static class Cmd
        {
            public const string ADD = "bc.add";
        }

        [ConsoleCommand(Cmd.ADD)]
        private void cmdAddButton(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.ADMIN))
            {
                MessagePlayer(player, Lang.NoPermission);
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
                        MessagePlayer(player, Lang.AlreadyRegistered);
                        return;
                    }
                    ButtonData defaultData = new ButtonData
                    {
                        RequireButtonPowered = true,
                        DisablePowerOutputOnPress = true,
                        RunRandomCommand = false,
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
                    MessagePlayer(player, Lang.ButtonRegistered);
                }
                else
                {
                    MessagePlayer(player, Lang.NoButtonSight);
                }
            }
            else
            {
                MessagePlayer(player, Lang.NoButtonRange);
            }
        }

        #endregion Commands

        #region Localization

        private class Lang
        {
            public const string NoPermission = "NoPermission";
            public const string AlreadyRegistered = "AlreadyRegistered";
            public const string ButtonRegistered = "ButtonRegistered";
            public const string NoButtonSight = "NoButtonSight";
            public const string NoButtonRange = "NoButtonRange";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.NoPermission] = "You do not have permission to use this command.",
                [Lang.AlreadyRegistered] = "This button is already registered.",
                [Lang.ButtonRegistered] = "Button registered successfully.",
                [Lang.NoButtonSight] = "No button found in your line of sight.",
                [Lang.NoButtonRange] = "No button found within range.",
            }, this, "en");
        }

        private static string GetMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string message = _plugin.lang.GetMessage(messageKey, _plugin, player.UserIDString);

            if (args.Length > 0)
                message = string.Format(message, args);

            return message;
        }

        public static void MessagePlayer(BasePlayer player, string messageKey, params object[] args)
        {
            string message = GetMessage(player, messageKey, args);
            _plugin.SendReply(player, message);
        }

        #endregion Localization
    }
}