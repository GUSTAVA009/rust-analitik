using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Sleeping Bag Labels", "Your Name", "1.0.3")]
    [Description("Shows 3D text labels above sleeping bags with owner names and team-based colors")]
    public class SleepingBagLabels : RustPlugin
    {
        #region Fields
        
        private const string PERMISSION_USE = "sleepingbaglabels.use";
        private const string PERMISSION_ADMIN = "sleepingbaglabels.admin";
        
        private readonly Dictionary<NetworkableId, TextMesh> _activeLables = new Dictionary<NetworkableId, TextMesh>();
        private readonly Dictionary<ulong, DateTime> _playerCooldowns = new Dictionary<ulong, DateTime>();
        private readonly Dictionary<ulong, PlayerSettings> _playerSettings = new Dictionary<ulong, PlayerSettings>();
        
        private PluginConfig _config;
        
        private class PlayerSettings
        {
            public bool ShowLabels { get; set; } = true;
            public float MaxDistance { get; set; } = 50f;
        }
        
        #endregion
        
        #region Configuration
        
        private class PluginConfig
        {
            [JsonProperty("Display Settings")]
            public DisplaySettings Display { get; set; } = new DisplaySettings();
            
            [JsonProperty("Color Settings")]
            public ColorSettings Colors { get; set; } = new ColorSettings();
            
            [JsonProperty("Streamer Settings")]
            public StreamerSettings Streamer { get; set; } = new StreamerSettings();
            
            [JsonProperty("Performance Settings")]
            public PerformanceSettings Performance { get; set; } = new PerformanceSettings();
        }
        
        private class DisplaySettings
        {
            [JsonProperty("Show sleeping bag labels")]
            public bool ShowLabels { get; set; } = true;
            
            [JsonProperty("Label height offset")]
            public float HeightOffset { get; set; } = 1.5f;
            
            [JsonProperty("Font size")]
            public int FontSize { get; set; } = 14;
            
            [JsonProperty("Max display distance")]
            public float MaxDistance { get; set; } = 50f;
            
            [JsonProperty("Show only when looking at bag")]
            public bool ShowOnlyWhenLooking { get; set; } = false;
            
            [JsonProperty("Show background")]
            public bool ShowBackground { get; set; } = true;
        }
        
        private class ColorSettings
        {
            [JsonProperty("Teammate color (hex)")]
            public string TeammateColor { get; set; } = "#00FF00"; // Green
            
            [JsonProperty("Enemy color (hex)")]
            public string EnemyColor { get; set; } = "#FF0000"; // Red
            
            [JsonProperty("Neutral color (hex)")]
            public string NeutralColor { get; set; } = "#FFFF00"; // Yellow
            
            [JsonProperty("Own sleeping bag color (hex)")]
            public string OwnColor { get; set; } = "#00FFFF"; // Cyan
        }
        
        private class StreamerSettings
        {
            [JsonProperty("Hide player names")]
            public bool HidePlayerNames { get; set; } = false;
            
            [JsonProperty("Replace name with text")]
            public string ReplacementText { get; set; } = "Player";
            
            [JsonProperty("Show only team/enemy indicator")]
            public bool ShowOnlyIndicator { get; set; } = false;
            
            [JsonProperty("Teammate indicator")]
            public string TeammateIndicator { get; set; } = "Teammate";
            
            [JsonProperty("Enemy indicator")]
            public string EnemyIndicator { get; set; } = "Enemy";
        }
        
        private class PerformanceSettings
        {
            [JsonProperty("Update frequency (seconds)")]
            public float UpdateFrequency { get; set; } = 1f;
            
            [JsonProperty("Max labels per player")]
            public int MaxLabelsPerPlayer { get; set; } = 20;
            
            [JsonProperty("Enable distance culling")]
            public bool EnableDistanceCulling { get; set; } = true;
        }
        
        #endregion
        
        #region Hooks
        
        private void Init()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            
            LoadConfig();
            
            // Start update timer
            if (_config.Display.ShowLabels)
            {
                timer.Every(_config.Performance.UpdateFrequency, UpdateAllLabels);
            }
        }
        
        private void OnServerInitialized()
        {
            // Initialize labels for existing sleeping bags
            foreach (var entity in BaseNetworkable.serverEntities.OfType<SleepingBag>())
            {
                CreateLabelForBag(entity);
            }
        }
        
        private void OnEntitySpawned(SleepingBag sleepingBag)
        {
            if (sleepingBag == null) return;
            
            NextTick(() => CreateLabelForBag(sleepingBag));
        }
        
        private void OnEntityKill(SleepingBag sleepingBag)
        {
            if (sleepingBag == null) return;
            
            RemoveLabelForBag(sleepingBag.net.ID);
        }
        
        private void Unload()
        {
            // Clean up all labels
            foreach (var label in _activeLables.Values)
            {
                if (label != null && label.gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(label.gameObject);
                }
            }
            _activeLables.Clear();
        }
        
        #endregion
        
        #region Player Settings
        
        private PlayerSettings GetPlayerSettings(ulong userId)
        {
            if (!_playerSettings.TryGetValue(userId, out var settings))
            {
                settings = new PlayerSettings();
                _playerSettings[userId] = settings;
            }
            return settings;
        }
        
        private void SavePlayerSettings()
        {
            // In a real implementation, you might want to save to a data file
            // For now, settings persist only during server session
        }
        
        #endregion
        
        #region Core Functions
        
        private void CreateLabelForBag(SleepingBag sleepingBag)
        {
            if (sleepingBag == null || sleepingBag.IsDestroyed) return;
            
            // Remove existing label if any
            RemoveLabelForBag(sleepingBag.net.ID);
            
            // Create label GameObject
            var labelObject = new GameObject("SleepingBagLabel");
            labelObject.transform.position = sleepingBag.transform.position + Vector3.up * _config.Display.HeightOffset;
            labelObject.transform.rotation = Quaternion.identity;
            
            // Add TextMesh component
            var textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.text = GetDisplayText(sleepingBag);
            textMesh.fontSize = _config.Display.FontSize;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            
            // Set color based on ownership
            var color = GetBagColor(sleepingBag, null); // Will be updated per player
            textMesh.color = HexToColor(_config.Colors.NeutralColor);
            
            // Add background if enabled
            if (_config.Display.ShowBackground)
            {
                var backgroundMaterial = CreateBackgroundMaterial();
                if (backgroundMaterial != null)
                {
                    var backgroundObject = new GameObject("Background");
                    backgroundObject.transform.SetParent(labelObject.transform);
                    backgroundObject.transform.localPosition = Vector3.zero;
                    
                    var backgroundMesh = backgroundObject.AddComponent<MeshRenderer>();
                    var backgroundFilter = backgroundObject.AddComponent<MeshFilter>();
                    
                    // Create simple quad mesh for background
                    backgroundFilter.mesh = CreateQuadMesh(textMesh.text.Length * 0.1f, 0.2f);
                    backgroundMesh.material = backgroundMaterial;
                    backgroundObject.transform.localPosition = new Vector3(0, 0, 0.01f);
                }
            }
            
            // Make label face camera
            var billboard = labelObject.AddComponent<Billboard>();
            
            // Store reference
            _activeLables[sleepingBag.net.ID] = textMesh;
        }
        
        private void RemoveLabelForBag(NetworkableId bagId)
        {
            if (_activeLables.TryGetValue(bagId, out var textMesh))
            {
                if (textMesh != null && textMesh.gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(textMesh.gameObject);
                }
                _activeLables.Remove(bagId);
            }
        }
        
        private void UpdateAllLabels()
        {
            var playersToUpdate = BasePlayer.activePlayerList.Where(p => 
                permission.UserHasPermission(p.UserIDString, PERMISSION_USE)).ToList();
            
            foreach (var player in playersToUpdate)
            {
                UpdateLabelsForPlayer(player);
            }
        }
        
        private void UpdateLabelsForPlayer(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            
            var playerPos = player.transform.position;
            var viewDirection = player.eyes.BodyForward();
            
            foreach (var kvp in _activeLables.ToList())
            {
                var bagId = kvp.Key;
                var textMesh = kvp.Value;
                
                if (textMesh == null || textMesh.gameObject == null)
                {
                    _activeLables.Remove(bagId);
                    continue;
                }
                
                var sleepingBag = BaseNetworkable.serverEntities.Find(bagId) as SleepingBag;
                if (sleepingBag == null)
                {
                    RemoveLabelForBag(bagId);
                    continue;
                }
                
                var distance = Vector3.Distance(playerPos, sleepingBag.transform.position);
                var shouldShow = ShouldShowLabel(player, sleepingBag, distance, viewDirection);
                
                // Update visibility for all players (this is a global label)
                textMesh.gameObject.SetActive(shouldShow);
                
                if (shouldShow)
                {
                    // Update text content
                    textMesh.text = GetDisplayText(sleepingBag);
                    
                    // Update color based on the first player found (simplified approach)
                    var color = GetBagColor(sleepingBag, player);
                    textMesh.color = color;
                    
                    // Update position to follow bag
                    textMesh.transform.position = sleepingBag.transform.position + Vector3.up * _config.Display.HeightOffset;
                }
            }
        }
        
        private bool ShouldShowLabel(BasePlayer viewer, SleepingBag bag, float distance, Vector3 viewDirection)
        {
            if (!_config.Display.ShowLabels) return false;
            
            var playerSettings = GetPlayerSettings(viewer.userID);
            if (!playerSettings.ShowLabels) return false;
            
            var maxDistance = Math.Min(_config.Display.MaxDistance, playerSettings.MaxDistance);
            if (distance > maxDistance) return false;
            
            if (_config.Display.ShowOnlyWhenLooking)
            {
                var directionToBag = (bag.transform.position - viewer.transform.position).normalized;
                var dot = Vector3.Dot(viewDirection, directionToBag);
                if (dot < 0.8f) return false; // Only show when looking directly at it
            }
            
            return true;
        }
        
        private string GetDisplayText(SleepingBag sleepingBag)
        {
            if (_config.Streamer.HidePlayerNames)
            {
                if (_config.Streamer.ShowOnlyIndicator)
                {
                    // Will be determined per player viewing
                    return _config.Streamer.ReplacementText;
                }
                return _config.Streamer.ReplacementText;
            }
            
            var ownerName = GetOwnerName(sleepingBag.OwnerID);
            return string.IsNullOrEmpty(ownerName) ? "Unknown" : ownerName;
        }
        
        private Color GetBagColor(SleepingBag sleepingBag, BasePlayer viewer)
        {
            if (viewer == null) return HexToColor(_config.Colors.NeutralColor);
            
            var ownerId = sleepingBag.OwnerID;
            
            // Own sleeping bag
            if (ownerId == viewer.userID)
            {
                return HexToColor(_config.Colors.OwnColor);
            }
            
            // Check if teammate
            if (IsTeammate(viewer, ownerId))
            {
                return HexToColor(_config.Colors.TeammateColor);
            }
            
            // Enemy or neutral
            return HexToColor(_config.Colors.EnemyColor);
        }
        
        private bool IsTeammate(BasePlayer player, ulong targetId)
        {
            if (player.currentTeam == 0) return false;
            
            var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
            if (team == null) return false;
            
            return team.members.Contains(targetId);
        }
        
        private string GetOwnerName(ulong userId)
        {
            var player = BasePlayer.FindByID(userId);
            if (player != null) return player.displayName;
            
            return covalence.Players.FindPlayerById(userId.ToString())?.Name ?? "Unknown";
        }
        
        #endregion
        
        #region Utility Functions
        
        private Color HexToColor(string hex)
        {
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);
            
            if (hex.Length != 6) return Color.white;
            
            try
            {
                var r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                var g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                var b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                
                return new Color(r, g, b, 1f);
            }
            catch
            {
                return Color.white;
            }
        }
        
        private Mesh CreateQuadMesh(float width, float height)
        {
            var mesh = new Mesh();
            
            var vertices = new Vector3[]
            {
                new Vector3(-width/2, -height/2, 0),
                new Vector3(width/2, -height/2, 0),
                new Vector3(width/2, height/2, 0),
                new Vector3(-width/2, height/2, 0)
            };
            
            var triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            var uvs = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            
            return mesh;
        }
        
        private Material CreateBackgroundMaterial()
        {
            // Try to find a suitable shader, fallback to standard shader if needed
            var shader = Shader.Find("Unlit/Color") ?? 
                        Shader.Find("Standard") ?? 
                        Shader.Find("UI/Default") ??
                        Shader.Find("Sprites/Default");
            
            if (shader == null)
            {
                PrintWarning("No suitable shader found for background material. Background will be disabled.");
                return null;
            }
            
            var material = new Material(shader);
            material.color = new Color(0, 0, 0, 0.5f);
            
            // Set additional properties for transparency if using Standard shader
            if (shader.name == "Standard")
            {
                material.SetFloat("_Mode", 3); // Transparent mode
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
            }
            
            return material;
        }
        
        #endregion
        
        #region Commands
        
        [ChatCommand("sleepingbag")]
        private void SleepingBagCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                SendReply(player, "You don't have permission to use this command!");
                return;
            }
            
            if (args.Length == 0)
            {
                var currentSettings = GetPlayerSettings(player.userID);
                SendReply(player, "🏠 Sleeping Bag Labels Commands:");
                SendReply(player, "/sleepingbag toggle - Toggle labels on/off");
                SendReply(player, "/sleepingbag distance <value> - Set max distance");
                SendReply(player, "/sleepingbag status - Show current settings");
                if (permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN))
                {
                    SendReply(player, "/sleepingbag reload - Reload configuration");
                    SendReply(player, "/sleepingbag debug - Show debug information");
                    SendReply(player, "/sleepingbag refresh - Refresh all labels");
                }
                SendReply(player, $"📊 Current: Labels {(currentSettings.ShowLabels ? "✅ ON" : "❌ OFF")}, Distance: {currentSettings.MaxDistance}m");
                return;
            }
            
            switch (args[0].ToLower())
            {
                case "toggle":
                    var playerSettings = GetPlayerSettings(player.userID);
                    playerSettings.ShowLabels = !playerSettings.ShowLabels;
                    SavePlayerSettings();
                    
                    if (playerSettings.ShowLabels)
                    {
                        SendReply(player, "✅ Sleeping bag labels are now ENABLED!");
                    }
                    else
                    {
                        SendReply(player, "❌ Sleeping bag labels are now DISABLED!");
                    }
                    break;
                    
                case "distance":
                    if (args.Length < 2 || !float.TryParse(args[1], out var distance))
                    {
                        SendReply(player, "Usage: /sleepingbag distance <value>");
                        return;
                    }
                    
                    if (distance < 1 || distance > 200)
                    {
                        SendReply(player, "Distance must be between 1 and 200 meters!");
                        return;
                    }
                    
                    var playerDistanceSettings = GetPlayerSettings(player.userID);
                    playerDistanceSettings.MaxDistance = distance;
                    SavePlayerSettings();
                    SendReply(player, $"🎯 Maximum label distance set to {distance} meters!");
                    break;
                    
                case "status":
                    var statusSettings = GetPlayerSettings(player.userID);
                    SendReply(player, "📊 Your Sleeping Bag Labels Settings:");
                    SendReply(player, $"   Labels: {(statusSettings.ShowLabels ? "✅ ENABLED" : "❌ DISABLED")}");
                    SendReply(player, $"   Max Distance: {statusSettings.MaxDistance} meters");
                    SendReply(player, $"   Global Settings: {(_config.Display.ShowLabels ? "✅ ON" : "❌ OFF")}");
                    break;
                    
                case "debug":
                    if (!permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN))
                    {
                        SendReply(player, "You don't have permission to use debug commands!");
                        return;
                    }
                    
                    var nearbyBags = UnityEngine.Object.FindObjectsOfType<SleepingBag>()
                        .Where(bag => Vector3.Distance(player.transform.position, bag.transform.position) < 100f)
                        .ToList();
                    
                    SendReply(player, $"🔍 Debug Info:");
                    SendReply(player, $"   Active Labels: {_activeLables.Count}");
                    SendReply(player, $"   Nearby Sleeping Bags: {nearbyBags.Count}");
                    SendReply(player, $"   Plugin Version: 1.0.3");
                    
                    foreach (var bag in nearbyBags.Take(5))
                    {
                        var distance = Vector3.Distance(player.transform.position, bag.transform.position);
                        var hasLabel = _activeLables.ContainsKey(bag.net.ID);
                        SendReply(player, $"   Bag at {distance:F1}m: {(hasLabel ? "✅ Has Label" : "❌ No Label")}");
                    }
                    break;
                    
                case "refresh":
                    if (!permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN))
                    {
                        SendReply(player, "You don't have permission to use admin commands!");
                        return;
                    }
                    
                    // Clear all existing labels
                    foreach (var label in _activeLables.Values)
                    {
                        if (label != null && label.gameObject != null)
                        {
                            UnityEngine.Object.DestroyImmediate(label.gameObject);
                        }
                    }
                    _activeLables.Clear();
                    
                    // Recreate all labels
                    foreach (var entity in BaseNetworkable.serverEntities.OfType<SleepingBag>())
                    {
                        CreateLabelForBag(entity);
                    }
                    
                    SendReply(player, "🔄 All sleeping bag labels have been refreshed!");
                    break;
                    
                case "reload":
                    if (!permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN))
                    {
                        SendReply(player, "You don't have permission to reload the configuration!");
                        return;
                    }
                    
                    LoadConfig();
                    SendReply(player, "Configuration reloaded!");
                    break;
                    
                default:
                    SendReply(player, "Unknown command. Use /sleepingbag for help.");
                    break;
            }
        }
        
        #endregion
        
        #region Configuration Management
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                {
                    throw new Exception("Config is null");
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error loading config: {ex.Message}");
                LoadDefaultConfig();
            }
            
            SaveConfig();
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating new default configuration file...");
            _config = new PluginConfig();
        }
        
        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }
        
        #endregion
    }
    
    #region Billboard Component
    
    public class Billboard : MonoBehaviour
    {
        private void Update()
        {
            // Make the text always face the camera
            var cameras = Camera.allCameras;
            if (cameras.Length > 0)
            {
                var camera = cameras[0]; // Use main camera
                transform.LookAt(transform.position + camera.transform.rotation * Vector3.forward,
                    camera.transform.rotation * Vector3.up);
            }
        }
    }
    
    #endregion
}