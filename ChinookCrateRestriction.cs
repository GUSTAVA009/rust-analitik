using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Chinook Crate Restriction", "RustGPT", "1.0.0")]
    [Description("Restricts chinooklockedcrate command after transport vehicle destruction")]
    public class ChinookCrateRestriction : RustPlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            public bool EnableRestriction = true;
            public int RestrictionDurationMinutes = 10;
            public List<string> RestrictedVehicles = new List<string>
            {
                "minicopter",
                "hotairballoon", 
                "transporthelicopter",
                "attackhelicopter"
            };
            public string RestrictionMessage = "Chinook locked crate is temporarily restricted after transport vehicle destruction.";
            public bool NotifyOnRestriction = true;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                LoadDefaultConfig();
                PrintWarning("Configuration file is corrupt, using default values");
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Fields

        private readonly Dictionary<ulong, DateTime> playerRestrictions = new Dictionary<ulong, DateTime>();
        private readonly HashSet<string> restrictedCommands = new HashSet<string>
        {
            "chinooklockedcrate",
            "codelockedhackablecrate"
        };

        #endregion

        #region Hooks

        private void Init()
        {
            LoadConfig();
            
            // Subscribe to command hooks
            foreach (var command in restrictedCommands)
            {
                Subscribe($"cmdConsole{command}");
                Subscribe($"cmdChat{command}");
            }
            
            PrintToConsole("Chinook Crate Restriction plugin loaded successfully");
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableRestriction || entity == null)
                return;

            var vehicleType = GetVehicleType(entity);
            if (string.IsNullOrEmpty(vehicleType) || !config.RestrictedVehicles.Contains(vehicleType))
                return;

            var attacker = info?.InitiatorPlayer;
            if (attacker == null)
                return;

            ApplyRestriction(attacker.userID, vehicleType);
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg?.Player() == null)
                return null;

            var command = arg.cmd?.Name?.ToLower();
            if (string.IsNullOrEmpty(command) || !restrictedCommands.Contains(command))
                return null;

            return CheckRestriction(arg.Player().userID, command);
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || string.IsNullOrEmpty(command))
                return null;

            var cmd = command.ToLower().Replace("/", "");
            if (!restrictedCommands.Contains(cmd))
                return null;

            return CheckRestriction(player.userID, cmd);
        }

        #endregion

        #region Core Methods

        private string GetVehicleType(BaseCombatEntity entity)
        {
            if (entity == null)
                return null;

            var shortName = entity.ShortPrefabName?.ToLower();
            if (string.IsNullOrEmpty(shortName))
                return null;

            // Map entity short names to our configured vehicle types
            if (shortName.Contains("minicopter"))
                return "minicopter";
            if (shortName.Contains("hotairballoon"))
                return "hotairballoon";
            if (shortName.Contains("transport") && shortName.Contains("helicopter"))
                return "transporthelicopter";
            if (shortName.Contains("attack") && shortName.Contains("helicopter"))
                return "attackhelicopter";

            return null;
        }

        private void ApplyRestriction(ulong playerId, string vehicleType)
        {
            var restrictionEnd = DateTime.Now.AddMinutes(config.RestrictionDurationMinutes);
            playerRestrictions[playerId] = restrictionEnd;

            var player = BasePlayer.FindByID(playerId);
            if (player != null && config.NotifyOnRestriction)
            {
                SendReply(player, $"Chinook crate commands restricted for {config.RestrictionDurationMinutes} minutes after destroying {vehicleType}");
            }

            PrintToConsole($"Applied chinook crate restriction to {player?.displayName ?? playerId.ToString()} for {config.RestrictionDurationMinutes} minutes after destroying {vehicleType}");
        }

        private object CheckRestriction(ulong playerId, string command)
        {
            if (!playerRestrictions.ContainsKey(playerId))
                return null;

            var restrictionEnd = playerRestrictions[playerId];
            if (DateTime.Now >= restrictionEnd)
            {
                playerRestrictions.Remove(playerId);
                return null;
            }

            var player = BasePlayer.FindByID(playerId);
            if (player != null)
            {
                var remainingTime = restrictionEnd - DateTime.Now;
                var remainingMinutes = Math.Ceiling(remainingTime.TotalMinutes);
                
                SendReply(player, $"{config.RestrictionMessage} Time remaining: {remainingMinutes} minutes");
            }

            return true; // Block the command
        }

        #endregion

        #region Commands

        [ConsoleCommand("chinookrestriction.check")]
        private void CmdCheckRestriction(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null && !arg.IsRcon)
                return;

            var player = arg.Player();
            if (player == null)
            {
                arg.ReplyWith("This command can only be used by players");
                return;
            }

            if (!playerRestrictions.ContainsKey(player.userID))
            {
                SendReply(player, "You have no active chinook crate restrictions");
                return;
            }

            var restrictionEnd = playerRestrictions[player.userID];
            var remainingTime = restrictionEnd - DateTime.Now;
            
            if (remainingTime.TotalSeconds <= 0)
            {
                playerRestrictions.Remove(player.userID);
                SendReply(player, "Your chinook crate restriction has expired");
                return;
            }

            var remainingMinutes = Math.Ceiling(remainingTime.TotalMinutes);
            SendReply(player, $"Chinook crate restriction active. Time remaining: {remainingMinutes} minutes");
        }

        [ConsoleCommand("chinookrestriction.clear")]
        private void CmdClearRestriction(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon && !arg.IsAdmin)
            {
                arg.ReplyWith("You don't have permission to use this command");
                return;
            }

            if (arg.Args == null || arg.Args.Length == 0)
            {
                playerRestrictions.Clear();
                arg.ReplyWith("All chinook crate restrictions cleared");
                PrintToConsole("All chinook crate restrictions cleared by admin");
                return;
            }

            var targetName = arg.Args[0];
            var targetPlayer = BasePlayer.Find(targetName);
            
            if (targetPlayer == null)
            {
                arg.ReplyWith($"Player '{targetName}' not found");
                return;
            }

            if (playerRestrictions.Remove(targetPlayer.userID))
            {
                arg.ReplyWith($"Chinook crate restriction cleared for {targetPlayer.displayName}");
                SendReply(targetPlayer, "Your chinook crate restriction has been cleared by an administrator");
                PrintToConsole($"Chinook crate restriction cleared for {targetPlayer.displayName} by admin");
            }
            else
            {
                arg.ReplyWith($"{targetPlayer.displayName} has no active chinook crate restrictions");
            }
        }

        #endregion

        #region Cleanup

        private void Unload()
        {
            playerRestrictions.Clear();
        }

        #endregion
    }
}