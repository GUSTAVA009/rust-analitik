using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Transport Command Blocker", "RustGPT", "1.0.0")]
    [Description("Blocks chinooklockedcrate command after destroying specified transports")] 
    public class TransportCommandBlocker : RustPlugin
    {
        private const string ChinookCrateAsset = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";

        private PluginConfig _config;
        private readonly Dictionary<ulong, float> _playerBlockUntil = new Dictionary<ulong, float>();

        #region Config
        private class PluginConfig
        {
            public float CooldownSeconds = 1800f; // 30 minutes
            public string[] BlockedCommands = new[] { "chinooklockedcrate" };
            public string[] BlockedSpawnAssets = new[] { ChinookCrateAsset };
            public string[] TrackedTransports = new[]
            {
                "minicopter",
                "hotairballoon",
                "transporthelicopter",
                "attackhelicopter"
            };
            public string DenyMessage = "Команда временно недоступна после уничтожения транспорта. Осталось: {0}";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
            }
            catch
            {
                PrintError("Failed to read config, creating new one");
                _config = null;
            }
            if (_config == null)
            {
                _config = new PluginConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new PluginConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }
        #endregion

        #region Helpers
        private static string FormatTime(float seconds)
        {
            if (seconds <= 0) return "0с";
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}ч {ts.Minutes}м";
            if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}м {ts.Seconds}с";
            return $"{ts.Seconds}с";
        }

        private bool IsTransportEntity(BaseCombatEntity entity)
        {
            if (entity == null) return false;
            var shortPrefab = StringPool.Get(entity.prefabID);
            if (string.IsNullOrEmpty(shortPrefab)) return false;
            var name = shortPrefab.ToLowerInvariant();

            // Heuristic by prefab path for safety
            return _config.TrackedTransports.Any(t => name.Contains(t));
        }

        private void BlockPlayer(ulong playerId)
        {
            _playerBlockUntil[playerId] = Time.realtimeSinceStartup + _config.CooldownSeconds;
        }

        private float GetRemaining(ulong playerId)
        {
            if (!_playerBlockUntil.TryGetValue(playerId, out var until)) return 0f;
            var remain = until - Time.realtimeSinceStartup;
            return remain > 0 ? remain : 0f;
        }
        #endregion

        #region Hooks
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!IsTransportEntity(entity)) return;
            var initiator = info?.InitiatorPlayer;
            if (initiator == null) return;
            BlockPlayer(initiator.userID);
        }

        // Chat command interception
        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            // Not used in modern Rust for chat commands, but keep just in case
            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null) return null;

            var cmd = arg?.cmd?.FullName;
            if (string.IsNullOrEmpty(cmd)) return null;

            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return null;

            // Chat command path: e.g., say .chinooklockedcrate or chat.say /chinooklockedcrate depending on chat plugins
            if (cmd.Equals("chat.say", StringComparison.OrdinalIgnoreCase))
            {
                var message = arg.GetString(0, string.Empty);
                if (string.IsNullOrEmpty(message)) return null;

                var text = message.TrimStart('/', '.').ToLowerInvariant();
                var cmdName = text.Split(new[] { ' ' }, 2)[0];
                if (_config.BlockedCommands.Contains(cmdName))
                {
                    var remaining = GetRemaining(player.userID);
                    if (remaining > 0)
                    {
                        player.ChatMessage(string.Format(_config.DenyMessage, FormatTime(remaining)));
                        return true; // block
                    }
                }
                return null;
            }

            // Direct console command path: player runs a command bound to chinooklockedcrate
            var nameOnly = cmd.ToLowerInvariant();
            var simpleName = arg?.cmd?.Name?.ToLowerInvariant();
            if (_config.BlockedCommands.Contains(nameOnly) || (!string.IsNullOrEmpty(simpleName) && _config.BlockedCommands.Contains(simpleName)))
            {
                var remaining = GetRemaining(player.userID);
                if (remaining > 0)
                {
                    player.ChatMessage(string.Format(_config.DenyMessage, FormatTime(remaining)));
                    return true;
                }
            }

            // Block spawn of specific asset via generic spawn commands
            if (nameOnly == "spawn" || nameOnly == "entity.spawn" || nameOnly == "spawnentity")
            {
                var arg0 = arg.GetString(0, string.Empty).ToLowerInvariant();
                if (_config.BlockedSpawnAssets.Any(a => a.Equals(arg0, StringComparison.OrdinalIgnoreCase) || arg0.Contains("codelockedhackablecrate") || arg0.Contains("chinooklockedcrate")))
                {
                    var remaining = GetRemaining(player.userID);
                    if (remaining > 0)
                    {
                        player.ChatMessage(string.Format(_config.DenyMessage, FormatTime(remaining)));
                        return true;
                    }
                }
            }

            return null;
        }
        #endregion
    }
}

