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

        private Configuration _config;

        private class Configuration
        {
            public string TeamColorHex = "#57FF57"; // green
            public string OtherTeamColorHex = "#FFCC33"; // amber
            public string NoTeamColorHex = "#FFFFFF"; // white
            public float MaxDistance = 20f;
            public float RefreshSeconds = 0.25f;
            public bool DefaultStreamerHideNames = false;
        }

        private readonly HashSet<ulong> _streamerHidden = new HashSet<ulong>();
        private readonly Dictionary<ulong, float> _lastDrawAt = new Dictionary<ulong, float>();

        private void Init()
        {
            AddCovalenceCommand(CommandRoot, nameof(CmdSleepingBag));
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
            }
            timer.Every(_config.RefreshSeconds, DrawLoopTick);
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
                iPlayer.Reply($"SleepingBagLabels: /{CommandRoot} stream - toggle hide names, /{CommandRoot} debug - test draw");
                return;
            }

            var sub = args[0].ToLower();
            switch (sub)
            {
                case "stream":
                case "streamer":
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
                    var nearest = FindNearestBag(player, 15f);
                    if (nearest != null)
                    {
                        iPlayer.Reply($"SleepingBagLabels debug: drawing label for '{GetOwnerName(nearest.OwnerID)}' at {Vector3.Distance(player.transform.position, nearest.transform.position):0.0}m");
                        DrawBagForPlayer(player, nearest);
                    }
                    else
                    {
                        iPlayer.Reply("SleepingBagLabels debug: no sleeping bag within 15m");
                    }
                    // Always draw a debug text 2m in front to verify ddraw works on this client
                    var origin = player.eyes?.position ?? (player.transform.position + Vector3.up * 1.5f);
                    var forward = player.eyes != null ? player.eyes.BodyForward() : player.transform.forward;
                    var debugPos = origin + forward * 2.0f;
                    player.SendConsoleCommand("ddraw.text", 3f, ParseColor("#00FFFF", Color.cyan), debugPos, "DEBUG: SleepingBagLabels", 0.9f);
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

            var now = Time.realtimeSinceStartup;
            if (!force && _lastDrawAt.TryGetValue(player.userID, out var last) && now - last < _config.RefreshSeconds * 0.9f)
                return;

            _lastDrawAt[player.userID] = now;

            ClearDraw(player);

            var bag = GetLookBag(player, _config.MaxDistance) ?? FindNearestBag(player, 3f);
            if (bag == null) return;

            DrawBagForPlayer(player, bag);
        }

        private void ClearDraw(BasePlayer player)
        {
            player?.SendConsoleCommand("ddraw.clear");
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

        private bool IsSameTeam(ulong viewerId, ulong ownerId)
        {
            if (viewerId == 0 || ownerId == 0) return false;
            var rm = RelationshipManager.Instance ?? RelationshipManager.ServerInstance;
            var viewer = rm?.FindTeam(viewerId);
            var owner = rm?.FindTeam(ownerId);
            if (viewer == null || owner == null) return false;
            return viewer.teamID != 0 && viewer.teamID == owner.teamID;
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
            var ownerId = entity.OwnerID;
            var ownerName = GetOwnerName(ownerId);
            var isSameTeam = IsSameTeam(player.userID, ownerId);

            var color = isSameTeam ? _config.TeamColorHex : _config.OtherTeamColorHex;
            if (ownerId == 0)
                color = _config.NoTeamColorHex;

            var label = _streamerHidden.Contains(player.userID) ? "Sleeping Bag" : ownerName;

            var worldPos = entity.transform.position + Vector3.up * 0.4f;
            DrawText(player, worldPos, color, label);
            // Add a small sphere for debug visibility juxtaposed with text for a brief moment
            var duration = Mathf.Max(_config.RefreshSeconds + 0.05f, 0.15f);
            player.SendConsoleCommand("ddraw.sphere", duration * 0.9f, ParseColor("#00FFFF", Color.cyan), worldPos, 0.02f);
        }
        #endregion
    }
}

