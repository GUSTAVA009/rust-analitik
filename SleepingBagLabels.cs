using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("SleepingBagLabels", "yourname", "0.1.0")]
    [Description("Shows team-colored 3D labels on sleeping bags with streamer mode.")]
    public class SleepingBagLabels : CovalencePlugin
    {
        private const string CommandRoot = "sleepingbag";
        private const string PermUse = "sleepingbaglabels.use";
        private const string PermStream = "sleepingbaglabels.stream";
        private const string PermDebug = "sleepingbaglabels.debug";
        private const string PermToggle = "sleepingbaglabels.toggle";

        private Configuration _config;

        private class Configuration
        {
            public string TeamColorHex = "#57FF57"; // green
            public string OtherTeamColorHex = "#FFCC33"; // amber
            public string NoTeamColorHex = "#FFFFFF"; // white
            public float MaxDistance = 20f;
            public float RefreshSeconds = 0.25f;
            public bool DefaultStreamerHideNames = false;
            public bool AllowDebugOverlay = true;
        }

        private readonly HashSet<ulong> _streamerHidden = new HashSet<ulong>();
        private readonly Dictionary<ulong, float> _lastDrawAt = new Dictionary<ulong, float>();
        private readonly HashSet<ulong> _debugEnabled = new HashSet<ulong>();
        private readonly HashSet<ulong> _labelsEnabled = new HashSet<ulong>();
        private readonly Dictionary<ulong, SleepingBag> _lastBag = new Dictionary<ulong, SleepingBag>();
        private readonly Dictionary<ulong, float> _lastBagSeenAt = new Dictionary<ulong, float>();

        private void Init()
        {
            AddCovalenceCommand(CommandRoot, nameof(CmdSleepingBag));
            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermStream, this);
            permission.RegisterPermission(PermDebug, this);
            permission.RegisterPermission(PermToggle, this);
            LoadConfigValues();
        }

        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (_config.DefaultStreamerHideNames)
                {
                    _streamerHidden.Add(player.userID);
                }
                _labelsEnabled.Add(player.userID);
            }
            timer.Every(_config.RefreshSeconds, DrawLoopTick);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player == null) return;
            _labelsEnabled.Add(player.userID);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                ClearDraw(player);
            }
        }

        #region Config
        protected override void LoadDefaultConfig() => _config = new Configuration();

        private void LoadConfigValues()
        {
            try
            {
                _config = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintWarning("Using default config");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Commands
        private void CmdSleepingBag(IPlayer iPlayer, string command, string[] args)
        {
            var player = iPlayer.Object as BasePlayer;
            if (player == null) return;

            if (args.Length == 0)
            {
                iPlayer.Reply($"SleepingBagLabels: /{CommandRoot} on|off, /{CommandRoot} stream, /{CommandRoot} debug [on|off]");
                return;
            }

            var sub = args[0].ToLower();
            switch (sub)
            {
                case "on":
                case "+":
                case "enable":
                    if (!permission.UserHasPermission(player.UserIDString, PermToggle) && !permission.UserHasPermission(player.UserIDString, PermUse))
                    {
                        iPlayer.Reply("Нет прав на включение меток.");
                        return;
                    }
                    _labelsEnabled.Add(player.userID);
                    iPlayer.Reply("Метки включены.");
                    break;
                case "off":
                case "-":
                case "disable":
                    if (!permission.UserHasPermission(player.UserIDString, PermToggle) && !permission.UserHasPermission(player.UserIDString, PermUse))
                    {
                        iPlayer.Reply("Нет прав на отключение меток.");
                        return;
                    }
                    _labelsEnabled.Remove(player.userID);
                    iPlayer.Reply("Метки отключены.");
                    break;
                case "stream":
                case "streamer":
                    if (!permission.UserHasPermission(player.UserIDString, PermStream) && !permission.UserHasPermission(player.UserIDString, PermUse))
                    {
                        iPlayer.Reply("Нет прав на использование команды.");
                        return;
                    }
                    if (_streamerHidden.Contains(player.userID))
                    {
                        _streamerHidden.Remove(player.userID);
                        iPlayer.Reply("Streamer mode: names visible");
                    }
                    else
                    {
                        _streamerHidden.Add(player.userID);
                        iPlayer.Reply("Streamer mode: names hidden");
                    }
                    break;
                case "debug":
                    if (!permission.UserHasPermission(player.UserIDString, PermDebug))
                    {
                        iPlayer.Reply("Нет прав на отладочную команду.");
                        return;
                    }
                    var enable = true;
                    if (args.Length > 1)
                    {
                        var onoff = args[1].ToLower();
                        if (onoff == "off" || onoff == "0" || onoff == "false") enable = false;
                    }
                    if (enable)
                    {
                        _debugEnabled.Add(player.userID);
                        iPlayer.Reply("Debug overlay включен.");
                    }
                    else
                    {
                        _debugEnabled.Remove(player.userID);
                        iPlayer.Reply("Debug overlay выключен.");
                    }
                    break;
                default:
                    iPlayer.Reply("Unknown subcommand");
                    break;
            }
        }
        #endregion

        #region Drawing
        private void DrawLoopTick()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DrawForPlayer(player);
            }
        }

        private void DrawForPlayer(BasePlayer player, bool force = false)
        {
            if (player == null || !player.IsConnected) return;
            if (!_labelsEnabled.Contains(player.userID)) return; // per-player toggle

            var now = Time.realtimeSinceStartup;
            if (!force && _lastDrawAt.TryGetValue(player.userID, out var last) && now - last < _config.RefreshSeconds * 0.9f)
                return;

            _lastDrawAt[player.userID] = now;

            if (_debugEnabled.Contains(player.userID) && _config.AllowDebugOverlay)
            {
                var origin = player.eyes?.position ?? (player.transform.position + Vector3.up * 1.5f);
                var forward = player.eyes != null ? player.eyes.BodyForward() : player.transform.forward;
                var debugPos = origin + forward * 2.0f;
                var duration = Mathf.Max(_config.RefreshSeconds + 0.05f, 0.15f);
                player.SendConsoleCommand("ddraw.text", duration, ParseColor("#00FFFF", Color.cyan), debugPos, "DEBUG: SleepingBagLabels", 0.9f);
            }

            var bag = GetLookBag(player, _config.MaxDistance) ?? FindNearestBag(player, 3f);
            if (bag != null)
            {
                _lastBag[player.userID] = bag;
                _lastBagSeenAt[player.userID] = now;
            }
            else
            {
                if (_lastBag.TryGetValue(player.userID, out var cached) && _lastBagSeenAt.TryGetValue(player.userID, out var seenAt) && now - seenAt <= 0.35f)
                {
                    bag = cached; // stick briefly to avoid flicker
                }
            }

            if (bag == null) return;

            DrawBagForPlayer(player, bag);
        }

        private void ClearDraw(BasePlayer player)
        {
            // Safe no-op during tick; used only on unload cleanup with zero-duration
            player?.SendConsoleCommand("ddraw.text", 0f, Color.clear, Vector3.zero, "");
        }

        private void DrawText(BasePlayer player, Vector3 worldPos, string hexColor, string text)
        {
            var color = ParseColor(hexColor, Color.white);
            // duration slightly longer than tick, so it persists smoothly
            var duration = Mathf.Max(_config.RefreshSeconds + 0.05f, 0.15f);
            // size and align centered above bag
            player.SendConsoleCommand("ddraw.text", duration, color, worldPos, text, 0.8f);
        }

        private Color ParseColor(string hex, Color fallback)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return fallback;
        }

        private string GetOwnerName(ulong ownerId)
        {
            if (ownerId == 0) return "Sleeping Bag";
            var player = BasePlayer.FindByID(ownerId) ?? BasePlayer.FindSleeping(ownerId);
            if (player != null) return player.displayName ?? ownerId.ToString();
            if (covalence.Players != null)
            {
                var ipl = covalence.Players.FindPlayerById(ownerId.ToString());
                if (ipl != null) return ipl.Name ?? ownerId.ToString();
            }
            return ownerId.ToString();
        }

        private ulong GetBagOwnerId(SleepingBag bag)
        {
            if (bag == null) return 0;
            var deployer = bag.deployerUserID;
            if (deployer != 0) return deployer;
            return bag.OwnerID;
        }

        private bool IsSameTeam(ulong viewerId, ulong ownerId)
        {
            if (viewerId == 0 || ownerId == 0) return false;
            var rm = RelationshipManager.ServerInstance;
            var viewerTeam = rm?.FindPlayersTeam(viewerId);
            var ownerTeam = rm?.FindPlayersTeam(ownerId);
            if (viewerTeam == null || ownerTeam == null) return false;
            return viewerTeam.teamID != 0 && viewerTeam.teamID == ownerTeam.teamID;
        }

        private RaycastHit? GetLookHit(BasePlayer player, float maxDistance)
        {
            var eyes = player.eyes?.HeadRay() ?? new Ray(player.transform.position + Vector3.up * 1.5f, player.eyes?.BodyForward() ?? player.transform.forward);
            RaycastHit hit;
            // Broad layer mask to reduce missed hits; default Physics mask captures deployed entities
            if (Physics.Raycast(eyes, out hit, maxDistance)) return hit;
            return null;
        }

        private SleepingBag GetLookBag(BasePlayer player, float maxDistance)
        {
            var hit = GetLookHit(player, maxDistance);
            if (!hit.HasValue) return null;
            var entity = hit.Value.GetEntity();
            if (entity is SleepingBag bag) return bag;
            return entity?.GetComponentInParent<SleepingBag>();
        }

        private SleepingBag FindNearestBag(BasePlayer player, float radius)
        {
            var origin = player.eyes?.position ?? (player.transform.position + Vector3.up * 1.5f);
            var direction = player.eyes != null ? player.eyes.BodyForward() : player.transform.forward;
            var probe = origin + direction * Mathf.Min(radius, _config.MaxDistance);
            var list = Facepunch.Pool.GetList<SleepingBag>();
            try
            {
                Vis.Entities(probe, radius, list, Layers.Mask.Deployed);
                SleepingBag closest = null;
                var best = float.MaxValue;
                foreach (var sb in list)
                {
                    var d = Vector3.SqrMagnitude(sb.transform.position - probe);
                    if (d < best)
                    {
                        best = d;
                        closest = sb;
                    }
                }
                return closest;
            }
            finally
            {
                Facepunch.Pool.FreeList(ref list);
            }
        }

        private void DrawBagForPlayer(BasePlayer player, SleepingBag entity)
        {
            var ownerId = GetBagOwnerId(entity);
            var ownerName = GetOwnerName(ownerId);
            var isSameTeam = IsSameTeam(player.userID, ownerId);

            string color;
            if (ownerId == 0)
            {
                color = _config.NoTeamColorHex;
            }
            else
            {
                color = isSameTeam ? _config.TeamColorHex : _config.OtherTeamColorHex;
            }

            var label = ownerName; // всегда показываем ник владельца всем игрокам

            var worldPos = entity.transform.position + Vector3.up * 0.4f;
            DrawText(player, worldPos, color, label);
            // Optional: sphere removed to reduce console spam
        }
        #endregion
    }
}

