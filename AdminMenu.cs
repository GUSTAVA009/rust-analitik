using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json.Converters;
 
namespace Oxide.Plugins
{
    [Info("AdminMenu", "k1lly0u", "0.2.0")]
    [Description("Modern admin menu with sleek design for managing groups, permissions, and commands")]
    class AdminMenu : RustPlugin
    {
        #region Fields 
        private StoredData storedData;
        private DynamicConfigFile data;

        private static AdminMenu ins;
        private Dictionary<string, string> uiColors = new Dictionary<string, string>();

        private enum MenuType { Permissions, Groups, Commands, Convars }

        private enum SelectType { Player, String }

        private enum PermSub { View, Player, Group }

        [JsonConverter(typeof(StringEnumConverter))]
        private enum CommSub { Chat, Console, Give, Player }  
        
        private enum GroupSub { View, UserGroups, AddGroup, CloneGroup, RemoveGroup }

        private enum ItemType { Weapon, Construction, Items, Resources, Attire, Tool, Medical, Food, Ammunition, Traps, Misc, Component, Electrical, Fun }

        private Dictionary<ItemType, List<KeyValuePair<string, ItemDefinition>>> itemList = new Dictionary<ItemType, List<KeyValuePair<string, ItemDefinition>>>();
        private Hash<ulong, SelectionData> selectData = new Hash<ulong, SelectionData>();
        private Hash<ulong, GroupData> groupCreator = new Hash<ulong, GroupData>();
        private Hash<ulong, Timer> popupTimers = new Hash<ulong, Timer>();
        private string[] charFilter = new string[] { "~", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };

        private List<KeyValuePair<string, bool>> permissionList = new List<KeyValuePair<string, bool>>();

        private const string USE_PERMISSION = "adminmenu.use";
        private const string PERM_PERMISSION = "adminmenu.permissions";
        private const string GROUP_PERMISSION = "adminmenu.groups";
        private const string CONVAR_PERMISSION = "adminmenu.convars";

        private const string GIVE_PERMISSION = "adminmenu.give";
        private const string GIVE_SELF_PERMISSION = "adminmenu.give.selfonly";
        private const string PLAYER_PERMISSION = "adminmenu.players";

        private const string PLAYER_KICKBAN_PERMISSION = "adminmenu.players.kickban";
        private const string PLAYER_MUTE_PERMISSION = "adminmenu.players.mute";
        private const string PLAYER_BLUERPRINTS_PERMISSION = "adminmenu.players.blueprints";
        private const string PLAYER_HURT_PERMISSION = "adminmenu.players.hurt";
        private const string PLAYER_HEAL_PERMISSION = "adminmenu.players.heal";
        private const string PLAYER_KILL_PERMISSION = "adminmenu.players.kill";
        private const string PLAYER_STRIP_PERMISSION = "adminmenu.players.strip";
        private const string PLAYER_TELEPORT_PERMISSION = "adminmenu.players.teleport";
        #endregion

        #region Classes
        private class SelectionData
        {
            public MenuType menuType;
            public string subType, selectDesc = string.Empty, returnCommand = string.Empty, target1_Name = string.Empty, target1_Id = string.Empty, target2_Name = string.Empty, target2_Id = string.Empty, character = string.Empty, kickBanReason = string.Empty;
            public bool requireTarget1, requireTarget2, isOnline, isGroup, forceOnline;
            public int pageNum, listNum;
        }

        private class GroupData { public string fromname = string.Empty, name = string.Empty, title = string.Empty, rank = string.Empty; public bool copyusers = false, isClone = false; }
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            permission.RegisterPermission(USE_PERMISSION, this);
            permission.RegisterPermission(PERM_PERMISSION, this);
            permission.RegisterPermission(GROUP_PERMISSION, this);
            permission.RegisterPermission(CONVAR_PERMISSION, this);
            permission.RegisterPermission(GIVE_PERMISSION, this);
            permission.RegisterPermission(PLAYER_PERMISSION, this);

            permission.RegisterPermission(GIVE_SELF_PERMISSION, this);
            permission.RegisterPermission(PLAYER_KICKBAN_PERMISSION, this);
            permission.RegisterPermission(PLAYER_MUTE_PERMISSION, this);
            permission.RegisterPermission(PLAYER_BLUERPRINTS_PERMISSION, this);
            permission.RegisterPermission(PLAYER_HURT_PERMISSION, this);
            permission.RegisterPermission(PLAYER_HEAL_PERMISSION, this);
            permission.RegisterPermission(PLAYER_KILL_PERMISSION, this);
            permission.RegisterPermission(PLAYER_STRIP_PERMISSION, this);
            permission.RegisterPermission(PLAYER_TELEPORT_PERMISSION, this);

            foreach(CustomCommands customCommand in configData.PlayerInfoCommands)
            {
                foreach(PlayerInfoCommandEntry command in customCommand.Commands)
                {
                    if (command.RequiredPermission.StartsWith("adminmenu.", StringComparison.OrdinalIgnoreCase))
                        permission.RegisterPermission(command.RequiredPermission, this);
                }
            }

            lang.RegisterMessages(Messages, this);

            data = Interface.Oxide.DataFileSystem.GetFile("AdminMenu/offline_players");
        }

        private void OnServerInitialized()
        {
            ins = this;
            LoadData();

            if (storedData == null || storedData.offlinePlayers == null)
                storedData = new StoredData();
            else storedData.RemoveOldPlayers();

            SetUIColors();

            foreach(var item in ItemManager.itemList)
            {
                ItemType itemType = (ItemType)Enum.Parse(typeof(ItemType), item.category.ToString(), true);
                if (!itemList.ContainsKey(itemType))
                    itemList.Add(itemType, new List<KeyValuePair<string, ItemDefinition>>());

                itemList[itemType].Add(new KeyValuePair<string, ItemDefinition>(item.displayName.english, item));
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        private void OnPlayerConnected(BasePlayer player) => storedData.OnPlayerInit(player.UserIDString);

        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyUI(player);
            storedData.AddOfflinePlayer(player.UserIDString);
        }

        private void OnPermissionRegistered(string name, Plugin owner) => UpdatePermissionList();

        private void OnPluginUnloaded(Plugin plugin) => UpdatePermissionList();

        private void OnServerSave() => SaveData();

        private void Unload()
        {      
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerDisconnected(player);

            ins = null;
        }
        #endregion

        #region Modern CUI Helper
        public class ModernUI
        {
            public static CuiElementContainer Container(string panelName, string color, string aMin, string aMax, bool useCursor = false, string parent = "Overlay")
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }

            public static void Panel(CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false, float borderRadius = 0f)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }

            public static void Label(CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, string color = "1 1 1 1")
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }   

            public static void Button(CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, string textColor = "1 1 1 1", float borderRadius = 0f)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align, Color = textColor }
                },
                panel);
            }           

            public static void ModernButton(CuiElementContainer container, string panel, string bgColor, string hoverColor, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, string textColor = "1 1 1 1")
            {
                // Main button
                container.Add(new CuiButton
                {
                    Button = { Color = bgColor, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align, Color = textColor }
                },
                panel);

                // Subtle border effect
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0.2" },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            public static void Card(CuiElementContainer container, string panel, string bgColor, string aMin, string aMax, string title = "", string content = "")
            {
                // Card background with subtle shadow
                container.Add(new CuiPanel
                {
                    Image = { Color = bgColor },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

                // Card border
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0.1" },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

                if (!string.IsNullOrEmpty(title))
                {
                    Label(container, panel, title, 16, $"{aMin.Split(' ')[0]} {aMax.Split(' ')[1] - 0.05f}", aMax, TextAnchor.UpperLeft, "0.8 0.8 0.8 1");
                }

                if (!string.IsNullOrEmpty(content))
                {
                    Label(container, panel, content, 12, aMin, $"{aMax.Split(' ')[0]} {aMin.Split(' ')[1] + 0.05f}", TextAnchor.LowerLeft, "0.6 0.6 0.6 1");
                }
            }
           
            public static void Input(CuiElementContainer container, string panel, string color, string text, int size, string command, string aMin, string aMax, string placeholder = "")
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Align = TextAnchor.MiddleLeft,
                            CharsLimit = 50,                            
                            Command = command + text,
                            Color = color,
                            FontSize = size,
                            Text = text,
                            PlaceholderText = placeholder,
                            NeedsKeyboard = true
                        },
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax }
                    }                
                });
            }

            public static void Toggle(CuiElementContainer container, string panel, string color, int fontSize, string aMin, string aMax, string command, bool isOn, string text = "")
            {
                // Toggle background
                UI.Panel(container, panel, color, aMin, aMax);

                // Toggle indicator
                if (isOn)
                {
                    UI.Label(container, panel, "✓", fontSize, aMin, aMax, TextAnchor.MiddleCenter, "0 1 0 1");
                }
                else
                {
                    UI.Label(container, panel, "✗", fontSize, aMin, aMax, TextAnchor.MiddleCenter, "1 0 0 1");
                }

                // Clickable area
                UI.Button(container, panel, "0 0 0 0", string.Empty, 0, aMin, aMax, command);

                // Label if provided
                if (!string.IsNullOrEmpty(text))
                {
                    UI.Label(container, panel, text, fontSize - 2, $"{aMax.Split(' ')[0]} {aMin.Split(' ')[1]}", $"{aMax.Split(' ')[0] + 0.1f} {aMax.Split(' ')[1]}", TextAnchor.MiddleLeft);
                }
            }

            public static void ProgressBar(CuiElementContainer container, string panel, string bgColor, string fillColor, float progress, string aMin, string aMax)
            {
                // Background
                container.Add(new CuiPanel
                {
                    Image = { Color = bgColor },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

                // Progress fill
                if (progress > 0)
                {
                    string fillMax = $"{aMin.Split(' ')[0] + (float.Parse(aMax.Split(' ')[0]) - float.Parse(aMin.Split(' ')[0])) * progress} {aMax.Split(' ')[1]}";
                    container.Add(new CuiPanel
                    {
                        Image = { Color = fillColor },
                        RectTransform = { AnchorMin = aMin, AnchorMax = fillMax }
                    },
                    panel);
                }
            }

            public static void Icon(CuiElementContainer container, string panel, string icon, int size, string aMin, string aMax, string color = "1 1 1 1")
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = TextAnchor.MiddleCenter, Text = icon, Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            static public string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.TrimStart('#');
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }

            static public string GradientColor(string startColor, string endColor, float progress)
            {
                var start = ParseColor(startColor);
                var end = ParseColor(endColor);
                
                return $"{start.r + (end.r - start.r) * progress} {start.g + (end.g - start.g) * progress} {start.b + (end.b - start.b) * progress} {start.a + (end.a - start.a) * progress}";
            }

            private static (float r, float g, float b, float a) ParseColor(string color)
            {
                var parts = color.Split(' ');
                return (float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
            }
        }
        #endregion

        #region Modern UI Creation 
        const string UIMain = "AMUI_MenuMain";        
        const string UIElement = "AMUI_MenuElement";
        const string UIPopup = "AMUI_PopupMessage";
        const string UISidebar = "AMUI_Sidebar";
        const string UIContent = "AMUI_Content";
              
        private void OpenAdminMenu(BasePlayer player)
        {
            DestroyUI(player);
            CreateModernMainMenu(player);
        }

        private void CreateModernMainMenu(BasePlayer player)
        {
            CuiElementContainer container = ModernUI.Container(UIMain, uiColors["bg1"], "0.02 0.05", "0.98 0.95", true);
            
            // Main background with gradient effect
            ModernUI.Panel(container, UIMain, uiColors["bg1"], "0 0", "1 1");
            
            // Header section
            CreateModernHeader(container, player);
            
            // Sidebar navigation
            CreateModernSidebar(container, player);
            
            // Main content area
            CreateModernContentArea(container, player);
            
            CuiHelper.AddUi(player, container);
            CreateMenuCommands(player, CommSub.Chat);
        }

        private void CreateModernHeader(CuiElementContainer container, BasePlayer player)
        {
            // Header background
            ModernUI.Panel(container, UIMain, uiColors["header"], "0 0.9", "1 1");
            
            // Logo/Title area
            ModernUI.Label(container, UIMain, "⚡ ADMIN PANEL", 28, "0.02 0.92", "0.3 0.98", TextAnchor.MiddleLeft, uiColors["accent"]);
            ModernUI.Label(container, UIMain, $"v{Version}", 14, "0.02 0.9", "0.3 0.92", TextAnchor.MiddleLeft, uiColors["text_secondary"]);
            
            // User info
            ModernUI.Label(container, UIMain, $"👤 {player.displayName}", 16, "0.7 0.92", "0.98 0.98", TextAnchor.MiddleRight, uiColors["text_primary"]);
            ModernUI.Label(container, UIMain, $"🆔 {player.UserIDString}", 12, "0.7 0.9", "0.98 0.92", TextAnchor.MiddleRight, uiColors["text_secondary"]);
            
            // Close button
            ModernUI.ModernButton(container, UIMain, uiColors["danger"], uiColors["danger_hover"], "✕", 18, "0.95 0.92", "0.98 0.98", "amui.switchelement exit", TextAnchor.MiddleCenter, uiColors["text_primary"]);
        }

        private void CreateModernSidebar(CuiElementContainer container, BasePlayer player)
        {
            // Sidebar background
            ModernUI.Panel(container, UIMain, uiColors["sidebar"], "0 0.1", "0.25 0.9");
            
            // Navigation buttons
            float buttonHeight = 0.08f;
            float startY = 0.85f;
            
            // Commands button
            CreateSidebarButton(container, "📋 COMMANDS", "0.02 0.77", "0.23 0.85", "amui.switchelement commands", MenuType.Commands, player);
            
            if (HasPermission(player.UserIDString, PERM_PERMISSION))
                CreateSidebarButton(container, "🔐 PERMISSIONS", "0.02 0.68", "0.23 0.76", "amui.switchelement permissions", MenuType.Permissions, player);
            
            if (HasPermission(player.UserIDString, GROUP_PERMISSION))
                CreateSidebarButton(container, "👥 GROUPS", "0.02 0.59", "0.23 0.67", "amui.switchelement groups", MenuType.Groups, player);
            
            if (HasPermission(player.UserIDString, CONVAR_PERMISSION))
                CreateSidebarButton(container, "⚙️ CONVARS", "0.02 0.5", "0.23 0.58", "amui.switchelement convars", MenuType.Convars, player);
            
            // Quick stats
            CreateQuickStats(container, player);
        }

        private void CreateSidebarButton(CuiElementContainer container, string text, string aMin, string aMax, string command, MenuType menuType, BasePlayer player)
        {
            string bgColor = uiColors["button_secondary"];
            string textColor = uiColors["text_primary"];
            
            ModernUI.ModernButton(container, UIMain, bgColor, uiColors["button_hover"], text, 14, aMin, aMax, command, TextAnchor.MiddleLeft, textColor);
        }

        private void CreateQuickStats(CuiElementContainer container, BasePlayer player)
        {
            // Stats background
            ModernUI.Panel(container, UIMain, uiColors["card"], "0.02 0.1", "0.23 0.25");
            
            ModernUI.Label(container, UIMain, "📊 QUICK STATS", 16, "0.05 0.2", "0.2 0.25", TextAnchor.MiddleLeft, uiColors["accent"]);
            
            int onlineCount = BasePlayer.activePlayerList.Count;
            int totalGroups = GetGroups().Count;
            int totalPerms = permissionList.Count;
            
            ModernUI.Label(container, UIMain, $"👥 Online: {onlineCount}", 12, "0.05 0.15", "0.2 0.18", TextAnchor.MiddleLeft, uiColors["text_secondary"]);
            ModernUI.Label(container, UIMain, $"👥 Groups: {totalGroups}", 12, "0.05 0.12", "0.2 0.15", TextAnchor.MiddleLeft, uiColors["text_secondary"]);
            ModernUI.Label(container, UIMain, $"🔐 Permissions: {totalPerms}", 12, "0.05 0.09", "0.2 0.12", TextAnchor.MiddleLeft, uiColors["text_secondary"]);
        }

        private void CreateModernContentArea(CuiElementContainer container, BasePlayer player)
        {
            // Content background
            ModernUI.Panel(container, UIMain, uiColors["content"], "0.27 0.1", "0.98 0.9");
            
            // Content header
            ModernUI.Panel(container, UIMain, uiColors["content_header"], "0.27 0.85", "0.98 0.9");
        }

        private void CreateModernMenuButtons(CuiElementContainer container, MenuType menuType, string playerId)
        {
            // Sub-navigation tabs
            ModernUI.Panel(container, UIContent, uiColors["tab_bar"], "0.005 0.925", "0.995 0.99");
            
            switch (menuType)
            {
                case MenuType.Commands:
                    CreateCommandTabs(container, playerId);
                    break;
                case MenuType.Permissions:
                    CreatePermissionTabs(container, playerId);
                    break;
                case MenuType.Groups:
                    CreateGroupTabs(container, playerId);
                    break;
                case MenuType.Convars:
                    CreateConvarTabs(container, playerId);
                    break;
            }
        }

        private void CreateCommandTabs(CuiElementContainer container, string playerId)
        {
            float tabWidth = 0.15f;
            float startX = 0.02f;
            
            CreateTab(container, "💬 Chat", CommSub.Chat, startX, playerId);
            CreateTab(container, "🖥️ Console", CommSub.Console, startX + tabWidth, playerId);
            
            if (HasPermission(playerId, GIVE_PERMISSION))
                CreateTab(container, "🎁 Give", CommSub.Give, startX + tabWidth * 2, playerId);
            
            if (HasPermission(playerId, PLAYER_PERMISSION))
                CreateTab(container, "👤 Player", CommSub.Player, startX + tabWidth * 3, playerId);
        }

        private void CreatePermissionTabs(CuiElementContainer container, string playerId)
        {
            float tabWidth = 0.2f;
            float startX = 0.02f;
            
            CreateTab(container, "👁️ View", PermSub.View, startX, playerId);
            CreateTab(container, "👤 Player", PermSub.Player, startX + tabWidth, playerId);
            CreateTab(container, "👥 Group", PermSub.Group, startX + tabWidth * 2, playerId);
        }

        private void CreateGroupTabs(CuiElementContainer container, string playerId)
        {
            float tabWidth = 0.15f;
            float startX = 0.02f;
            
            CreateTab(container, "👁️ View", GroupSub.View, startX, playerId);
            CreateTab(container, "➕ Add", GroupSub.AddGroup, startX + tabWidth, playerId);
            CreateTab(container, "📋 Clone", GroupSub.CloneGroup, startX + tabWidth * 2, playerId);
            CreateTab(container, "🗑️ Remove", GroupSub.RemoveGroup, startX + tabWidth * 3, playerId);
            CreateTab(container, "👥 Users", GroupSub.UserGroups, startX + tabWidth * 4, playerId);
        }

        private void CreateConvarTabs(CuiElementContainer container, string playerId)
        {
            // Convars don't have sub-tabs
        }

        private void CreateTab(CuiElementContainer container, string text, object subType, float x, string playerId)
        {
            string bgColor = uiColors["tab_inactive"];
            string textColor = uiColors["text_primary"];
            
            ModernUI.ModernButton(container, UIContent, bgColor, uiColors["tab_hover"], text, 14, $"{x} 0.93", $"{x + 0.13f} 0.98", $"amui.switchelement {GetMenuTypeFromSub(subType)} {subType.ToString().ToLower()}", TextAnchor.MiddleCenter, textColor);
        }

        private string GetMenuTypeFromSub(object subType)
        {
            if (subType is CommSub) return "commands";
            if (subType is PermSub) return "permissions";
            if (subType is GroupSub) return "groups";
            return "convars";
        }

        private void CreateModernMenuPermissions(BasePlayer player, int page = 0, string filter = "")
        {
            CuiElementContainer container = ModernUI.Container(UIElement, "0 0 0 0", "0.27 0.1", "0.98 0.9");
            CreateModernMenuButtons(container, MenuType.Permissions, player.UserIDString);
            CreateModernCharacterFilter(container, player.userID, filter, $"amui.switchelement permissions view 0");

            List<KeyValuePair<string, bool>> permList = new List<KeyValuePair<string, bool>>(permissionList);
            if (!string.IsNullOrEmpty(filter) && filter != "~")
                permList = permList.Where(x => x.Key.StartsWith(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            permList.OrderBy(x => x.Key);

            // Pagination
            CreateModernPagination(container, player, page, permList.Count, 72, $"amui.switchelement permissions view {{0}} {filter}");

            // Permission grid
            CreateModernPermissionGrid(container, permList, page, player);

            CuiHelper.DestroyUi(player, UIElement);
            CuiHelper.AddUi(player, container);
        }

        private void CreateModernPermissionGrid(CuiElementContainer container, List<KeyValuePair<string, bool>> permList, int page, BasePlayer player)
        {
            int count = 0;
            for (int i = page * 72; i < permList.Count; i++)
            {
                KeyValuePair<string, bool> perm = permList[i];
                float[] position = CalculateModernButtonPos(count);
                
                if (!perm.Value)
                {
                    // Category header
                    ModernUI.Card(container, UIElement, uiColors["category"], $"{position[0]} {position[1]}", $"{position[2]} {position[3]}", perm.Key, "");
                }
                else
                {    
                    // Permission item
                    string bgColor = uiColors["permission_item"];
                    string textColor = uiColors["text_primary"];
                    
                    ModernUI.ModernButton(container, UIElement, bgColor, uiColors["permission_hover"], perm.Key, 10, $"{position[0]} {position[1]}", $"{position[2]} {position[3]}", "", TextAnchor.MiddleLeft, textColor);
                }
                ++count;

                if (count >= 72)
                    break;
            }
        }

        private void CreateModernPagination(CuiElementContainer container, BasePlayer player, int currentPage, int totalItems, int itemsPerPage, string commandTemplate)
        {
            int totalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage);
            
            if (totalPages <= 1) return;

            // Pagination background
            ModernUI.Panel(container, UIElement, uiColors["pagination"], "0.02 0.01", "0.98 0.05");

            // Page info
            ModernUI.Label(container, UIElement, $"Page {currentPage + 1} of {totalPages} ({totalItems} items)", 12, "0.02 0.02", "0.3 0.04", TextAnchor.MiddleLeft, uiColors["text_secondary"]);

            // Navigation buttons
            if (currentPage > 0)
            {
                string prevCommand = commandTemplate.Replace("{0}", (currentPage - 1).ToString());
                ModernUI.ModernButton(container, UIElement, uiColors["button_primary"], uiColors["button_hover"], "◀ Previous", 12, "0.7 0.02", "0.85 0.04", prevCommand);
            }

            if (currentPage < totalPages - 1)
            {
                string nextCommand = commandTemplate.Replace("{0}", (currentPage + 1).ToString());
                ModernUI.ModernButton(container, UIElement, uiColors["button_primary"], uiColors["button_hover"], "Next ▶", 12, "0.86 0.02", "0.98 0.04", nextCommand);
            }
        }

        private void CreateModernCharacterFilter(CuiElementContainer container, ulong playerId, string currentCharacter, string returnCommand)
        {
            // Filter background
            ModernUI.Panel(container, UIElement, uiColors["filter_bg"], "0.02 0.06", "0.98 0.09");
            
            ModernUI.Label(container, UIElement, "🔍 Filter:", 14, "0.02 0.07", "0.1 0.08", TextAnchor.MiddleLeft, uiColors["text_primary"]);

            float buttonWidth = 0.03f;
            float startX = 0.12f;
            
            for (int i = 0; i < charFilter.Length; i++)
            {
                string character = charFilter[i];
                float x = startX + (buttonWidth * i);
                
                string bgColor = currentCharacter == character ? uiColors["filter_active"] : uiColors["filter_inactive"];
                string textColor = currentCharacter == character ? uiColors["text_primary"] : uiColors["text_secondary"];
                
                string command = currentCharacter == character ? "" : $"{(string.IsNullOrEmpty(returnCommand) ? "amui.filterchar" : returnCommand)} {character}";
                
                ModernUI.ModernButton(container, UIElement, bgColor, uiColors["filter_hover"], character, 12, $"{x} 0.065", $"{x + buttonWidth - 0.002f} 0.085", command, TextAnchor.MiddleCenter, textColor);
            }
        }

        private float[] CalculateModernButtonPos(int number)
        {
            // Modern grid layout with better spacing
            Vector2 position = new Vector2(0.02f, 0.75f);
            Vector2 dimensions = new Vector2(0.15f, 0.05f);
            float offsetY = 0;
            float offsetX = 0;
            
            int row = number / 6;
            int col = number % 6;
            
            offsetX = (0.01f + dimensions.x) * col;
            offsetY = (-0.01f - dimensions.y) * row;
            
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private void CreateModernPopup(BasePlayer player, string message, string type = "info")
        {
            string bgColor = type == "error" ? uiColors["danger"] : type == "success" ? uiColors["success"] : uiColors["info"];
            string icon = type == "error" ? "❌" : type == "success" ? "✅" : "ℹ️";
            
            CuiElementContainer container = ModernUI.Container(UIPopup, bgColor, "0.3 0.85", "0.7 0.95");
            
            // Popup background with rounded effect
            ModernUI.Panel(container, UIPopup, bgColor, "0 0", "1 1");
            
            // Icon and message
            ModernUI.Icon(container, UIPopup, icon, 20, "0.05 0.3", "0.15 0.7", uiColors["text_primary"]);
            ModernUI.Label(container, UIPopup, message, 16, "0.2 0.2", "0.95 0.8", TextAnchor.MiddleLeft, uiColors["text_primary"]);

            Timer destroyIn;
            if (popupTimers.TryGetValue(player.userID, out destroyIn))
                destroyIn.Destroy();
            popupTimers[player.userID] = timer.Once(5, () =>
            {
                CuiHelper.DestroyUi(player, UIPopup);
                popupTimers.Remove(player.userID);
            });

            CuiHelper.DestroyUi(player, UIPopup);
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region UI Functions
        private void SetUIColors()
        {            
            // Modern dark theme colors
            uiColors.Add("bg1", ModernUI.Color("#1a1a1a", 0.98f)); // Main background
            uiColors.Add("bg2", ModernUI.Color("#2a2a2a", 0.95f)); // Secondary background
            uiColors.Add("bg3", ModernUI.Color("#3a3a3a", 0.9f)); // Tertiary background
            
            // Header and navigation
            uiColors.Add("header", ModernUI.Color("#0d1117", 0.95f)); // Header background
            uiColors.Add("sidebar", ModernUI.Color("#161b22", 0.9f)); // Sidebar background
            uiColors.Add("content", ModernUI.Color("#21262d", 0.85f)); // Content background
            uiColors.Add("content_header", ModernUI.Color("#30363d", 0.8f)); // Content header
            
            // Buttons and interactive elements
            uiColors.Add("button_primary", ModernUI.Color("#238636", 0.9f)); // Primary button (green)
            uiColors.Add("button_secondary", ModernUI.Color("#21262d", 0.9f)); // Secondary button
            uiColors.Add("button_hover", ModernUI.Color("#30363d", 0.9f)); // Button hover state
            uiColors.Add("button_danger", ModernUI.Color("#da3633", 0.9f)); // Danger button (red)
            
            // Tabs and navigation
            uiColors.Add("tab_bar", ModernUI.Color("#21262d", 0.9f)); // Tab bar background
            uiColors.Add("tab_active", ModernUI.Color("#238636", 0.9f)); // Active tab
            uiColors.Add("tab_inactive", ModernUI.Color("#30363d", 0.8f)); // Inactive tab
            uiColors.Add("tab_hover", ModernUI.Color("#3a3a3a", 0.9f)); // Tab hover
            
            // Cards and panels
            uiColors.Add("card", ModernUI.Color("#21262d", 0.9f)); // Card background
            uiColors.Add("category", ModernUI.Color("#30363d", 0.8f)); // Category header
            
            // Text colors
            uiColors.Add("text_primary", ModernUI.Color("#f0f6fc", 1f)); // Primary text
            uiColors.Add("text_secondary", ModernUI.Color("#8b949e", 1f)); // Secondary text
            uiColors.Add("text_muted", ModernUI.Color("#6e7681", 1f)); // Muted text
            
            // Accent colors
            uiColors.Add("accent", ModernUI.Color("#58a6ff", 1f)); // Blue accent
            uiColors.Add("success", ModernUI.Color("#238636", 0.9f)); // Success green
            uiColors.Add("warning", ModernUI.Color("#d29922", 0.9f)); // Warning yellow
            uiColors.Add("danger", ModernUI.Color("#da3633", 0.9f)); // Danger red
            uiColors.Add("info", ModernUI.Color("#58a6ff", 0.9f)); // Info blue
            
            // Hover states
            uiColors.Add("danger_hover", ModernUI.Color("#f85149", 0.9f)); // Danger hover
            uiColors.Add("success_hover", ModernUI.Color("#2ea043", 0.9f)); // Success hover
            
            // Filters and search
            uiColors.Add("filter_bg", ModernUI.Color("#21262d", 0.8f)); // Filter background
            uiColors.Add("filter_active", ModernUI.Color("#238636", 0.9f)); // Active filter
            uiColors.Add("filter_inactive", ModernUI.Color("#30363d", 0.7f)); // Inactive filter
            uiColors.Add("filter_hover", ModernUI.Color("#3a3a3a", 0.8f)); // Filter hover
            
            // Permissions
            uiColors.Add("permission_item", ModernUI.Color("#21262d", 0.8f)); // Permission item
            uiColors.Add("permission_hover", ModernUI.Color("#30363d", 0.9f)); // Permission hover
            
            // Pagination
            uiColors.Add("pagination", ModernUI.Color("#161b22", 0.8f)); // Pagination background
            
            // Legacy compatibility
            uiColors.Add("button1", uiColors["button_primary"]);
            uiColors.Add("button2", uiColors["button_secondary"]);
            uiColors.Add("button3", uiColors["tab_active"]);
            uiColors.Add("close", uiColors["danger"]);
        }

        private List<string> GetGroups() => permission.GetGroups().ToList();

        private bool CreateGroup(string name, string title, int rank) => permission.CreateGroup(name, title, rank);

        private bool CloneGroup(string fromname, string name, string title, int rank, bool cloneUsers)
        {
            if (permission.CreateGroup(name, title, rank))
            {
                string[] perms = permission.GetGroupPermissions(fromname);
                for (int i = 0; i < perms.Length; i++)
                {
                    permission.GrantGroupPermission(name, perms[i], null);
                }

                if (cloneUsers)
                {
                    string[] users = permission.GetUsersInGroup(fromname);
                    for (int i = 0; i < users.Length; i++)
                    {
                        string userId = ToUserID(users[i]);
                        if (!string.IsNullOrEmpty(userId))
                            AddToGroup(userId, name);
                    }
                }
                return true;
            }
            return false;
        }

        private string ToUserID(string name) => name.Split(' ')?[0] ?? string.Empty;

        private string ToDisplayName(string name) => name.Substring(18).TrimStart('(').TrimEnd(')');

        private void RemoveGroup(string name) => permission.RemoveGroup(name);

        private void AddToGroup(string userId, string groupId) => permission.AddUserGroup(userId, groupId);

        private void RemoveFromGroup(string userId, string groupId) => permission.RemoveUserGroup(userId, groupId);

        private bool HasGroup(string userId, string groupId) => permission.UserHasGroup(userId, groupId);

        private List<KeyValuePair<string, string>> GetUsersInGroupFormatted(string groupId) => permission.GetUsersInGroup(groupId).Select(x => new KeyValuePair<string,string>(ToUserID(x), ToDisplayName(x))).ToList();
        
        private List<string> GetPermissions()
        {
            List<string> permissions = permission.GetPermissions().ToList();
            permissions.RemoveAll(x => x.ToLower().StartsWith("oxide."));
            return permissions;
        }

        private void GrantPermission(string groupOrID, string perm, bool isGroup = false)
        {
            if (isGroup)
                permission.GrantGroupPermission(groupOrID, perm, null);
            else permission.GrantUserPermission(groupOrID, perm, null);
        }

        private void RevokePermission(string groupOrID, string perm, bool isGroup = false)
        {
            if (isGroup)
                permission.RevokeGroupPermission(groupOrID, perm);
            else permission.RevokeUserPermission(groupOrID, perm);
        }

        private bool HasPermission(string groupOrID, string perm, bool isGroup = false)
        {
            if (isGroup)
                return permission.GroupHasPermission(groupOrID, perm);
            return permission.UserHasPermission(groupOrID, perm);
        }        

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UIElement);
            CuiHelper.DestroyUi(player, UIMain);
            CuiHelper.DestroyUi(player, UIPopup);
        }
        
        private T ParseType<T>(string type) => (T)Enum.Parse(typeof(T), type, true);

        private void UpdatePermissionList()
        {
            permissionList.Clear();
            List<string> permissions = GetPermissions();
            permissions.Sort();

            string lastName = string.Empty;
            foreach(string perm in permissions)
            {
                string name = string.Empty;
                if (perm.Contains("."))
                {
                    string permStart = perm.Substring(0, perm.IndexOf("."));
                    name = plugins.PluginManager.GetPlugins().ToList().Find(x => x?.Name?.ToLower() == permStart)?.Title ?? permStart;
                }
                else name = perm;
                if (lastName != name)
                {
                    permissionList.Add(new KeyValuePair<string, bool>(name, false));
                    lastName = name;
                }

                permissionList.Add(new KeyValuePair<string, bool>(perm, true));
            }
        }

        private string StripTags(string str)
        {
            if (str.StartsWith("[") && str.Contains("]") && str.Length > str.IndexOf("]"))            
                str = str.Substring(str.IndexOf("]") + 1).Trim();
            
            if (str.StartsWith("[") && str.Contains("]") && str.Length > str.IndexOf("]"))
                StripTags(str);

            return str;
        }
        #endregion

        #region Commands
        [ChatCommand("admin")]
        private void cmdAdmin(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, USE_PERMISSION)) return;
            OpenAdminMenu(player);
        }        
        #endregion

        #region Config        
        private ConfigData configData;

        private class Colors
        {          
            [JsonProperty(PropertyName = "Panel - Dark")]
            public UIColor Panel1 { get; set; }
            [JsonProperty(PropertyName = "Panel - Medium")]
            public UIColor Panel2 { get; set; }
            [JsonProperty(PropertyName = "Panel - Light")]
            public UIColor Panel3 { get; set; }
            [JsonProperty(PropertyName = "Button - Primary")]
            public UIColor Button1 { get; set; }
            [JsonProperty(PropertyName = "Button - Secondary")]
            public UIColor Button2 { get; set; }
            [JsonProperty(PropertyName = "Button - Selected")]
            public UIColor Button3 { get; set; }
                        
            public class UIColor
            {
                public string Color { get; set; }
                public float Alpha { get; set; }
            }
        }

        private class CommandEntry
        {
            public string Name { get; set; }
            public string Command { get; set; }
            public string Description { get; set; }
            public bool CloseOnRun { get; set; }
        }

        private class PlayerInfoCommandEntry : CommandEntry
        {            
            public string RequiredPlugin { get; set; }

            public string RequiredPermission { get; set; }

            [JsonProperty(PropertyName = "Command Type ( Chat, Console )")]
            public CommSub CommandType { get; set; }            
        }

        private class CustomCommands
        {
            public string Name { get; set; }

            public List<PlayerInfoCommandEntry> Commands { get; set; }
        }

        private class ConfigData
        {
            public Colors Colors { get; set; }

            [JsonProperty(PropertyName = "Chat Command List")]
            public List<CommandEntry> ChatCommands { get; set; }

            [JsonProperty(PropertyName = "Console Command List")]
            public List<CommandEntry> ConsoleCommands { get; set; }

            [JsonProperty(PropertyName = "Player Info Custom Commands")]
            public List<CustomCommands> PlayerInfoCommands { get; set; }

            [JsonProperty(PropertyName = "Give amounts per category")]
            public Dictionary<ItemCategory, int[]> GiveAmounts { get; set; }

            [JsonProperty(PropertyName = "Use different permissions for each section of the player administration tab")]
            public bool UsePlayerAdminPermissions { get; set; }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Colors = new Colors
                {
                    Panel1 = new Colors.UIColor { Color = "#1a1a1a", Alpha = 0.98f },
                    Panel2 = new Colors.UIColor { Color = "#2a2a2a", Alpha = 0.95f },
                    Panel3 = new Colors.UIColor { Color = "#3a3a3a", Alpha = 0.9f },
                    Button1 = new Colors.UIColor { Color = "#238636", Alpha = 0.9f },
                    Button2 = new Colors.UIColor { Color = "#21262d", Alpha = 0.9f },
                    Button3 = new Colors.UIColor { Color = "#58a6ff", Alpha = 0.9f }
                },
                ChatCommands = new List<CommandEntry>
                {
                    new CommandEntry
                    {
                        Name = "TP to 0 0 0",
                        Command = "/tp 0 0 0",
                        Description = "Teleport self to 0 0 0"
                    },
                    new CommandEntry
                    {
                        Name = "TP to player",
                        Command = "/tp {target1_name}",
                        Description = "Teleport self to player"
                    },
                    new CommandEntry
                    {
                        Name = "TP P2P",
                        Command = "/tp {target1_name} {target2_name}",
                        Description = "Teleport player to player"
                    },
                    new CommandEntry
                    {
                        Name = "God",
                        Command = "/god",
                        Description = "Toggle god mode"
                    }
                },
                ConsoleCommands = new List<CommandEntry>
                {
                    new CommandEntry
                    {
                        Name = "Set time to 9",
                        Command = "env.time 9",
                        Description = "Set the time to 9am"
                    },
                    new CommandEntry
                    {
                        Name = "Set to to 22",
                        Command = "env.time 22",
                        Description = "Set the time to 10pm"
                    },
                    new CommandEntry
                    {
                        Name = "TP P2P",
                        Command = "teleport.topos {target1_name} {target2_name}",
                        Description = "Teleport player to player"
                    },
                    new CommandEntry
                    {
                        Name = "Call random strike",
                        Command = "airstrike strike random",
                        Description = "Call a random Airstrike"
                    }
                },
                PlayerInfoCommands = new List<CustomCommands>
                {
                    new CustomCommands
                    {
                        Name = "Backpacks",
                        Commands = new List<PlayerInfoCommandEntry>
                        {
                            new PlayerInfoCommandEntry
                            {
                                RequiredPlugin = "Backpacks",
                                RequiredPermission = "backpacks.admin",
                                Name = "View Backpack",
                                CloseOnRun = true,
                                Command = "/viewbackpack {target1_id}",
                                CommandType = CommSub.Chat
                            }
                        }
                    },
                    new CustomCommands
                    {
                        Name = "InventoryViewer",
                        Commands = new List<PlayerInfoCommandEntry>
                        {
                            new PlayerInfoCommandEntry
                            {
                                RequiredPlugin = "InventoryViewer",
                                RequiredPermission = "inventoryviewer.allowed",
                                Name = "View Inventory",
                                CloseOnRun = true,
                                Command = "/viewinv {target1_id}",
                                CommandType = CommSub.Chat
                            }
                        }
                    },
                    new CustomCommands
                    {
                        Name = "Freeze",
                        Commands = new List<PlayerInfoCommandEntry>
                        {
                            new PlayerInfoCommandEntry
                            {
                                RequiredPlugin = "Freeze",
                                RequiredPermission = "freeze.use",
                                Name = "Freeze",
                                CloseOnRun = false,
                                Command = "/freeze {target1_id}",
                                CommandType = CommSub.Chat
                            },
                            new PlayerInfoCommandEntry
                            {
                                RequiredPlugin = "Freeze",
                                RequiredPermission = "freeze.use",
                                Name = "Unfreeze",
                                CloseOnRun = false,
                                Command = "/unfreeze {target1_id}",
                                CommandType = CommSub.Chat
                            }
                        }
                    }
                },
                GiveAmounts = new Dictionary<ItemCategory, int[]>
                {
                    [ItemCategory.Ammunition] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Attire] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Common] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Component] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Construction] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Electrical] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Food] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Fun] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Items] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Medical] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Misc] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Resources] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Tool] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Traps] = new int[] { 1, 10, 100, 1000 },
                    [ItemCategory.Weapon] = new int[] { 1, 10, 100, 1000 },
                },
                UsePlayerAdminPermissions = false,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(0, 1, 41))
                configData.GiveAmounts = baseConfig.GiveAmounts;

            if (configData.Version < new VersionNumber(0, 1, 51))
                configData.PlayerInfoCommands = baseConfig.PlayerInfoCommands;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Data Management
        private void SaveData() => data.WriteObject(storedData);

        private void LoadData()
        {
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
        }

        private class StoredData
        {
            public Hash<string, double> offlinePlayers = new Hash<string, double>();

            public void AddOfflinePlayer(string userId) => offlinePlayers[userId] = CurrentTime();

            public void OnPlayerInit(string userId)
            {
                if (offlinePlayers.ContainsKey(userId))
                    offlinePlayers.Remove(userId);                
            }

            public void RemoveOldPlayers()
            {
                double currentTime = CurrentTime();

                for (int i = offlinePlayers.Count - 1; i >= 0; i--)
                {
                    var user = offlinePlayers.ElementAt(i);
                    if (currentTime - user.Value > 604800)
                        offlinePlayers.Remove(user);
                }
            }

            public List<IPlayer> GetOfflineList() => ins.covalence.Players.All.Where(x => offlinePlayers.ContainsKey(x.Id)).ToList();

            public double CurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;            
        }
        #endregion

        #region Localization
        private string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["title"] = "<color=#58a6ff>⚡ Admin Panel v{0}</color>",
            ["exit"] = "Exit",
            ["view"] = "View",
            ["player"] = "Player Permissions",
            ["group"] = "Group Permissions",
            ["usergroups"] = "User Groups",
            ["addgroup"] = "Create Group",
            ["clonegroup"] = "Clone Group",
            ["removegroup"] = "Remove Group",
            ["chat"] = "Chat Commands",
            ["console"] = "Console Commands",
            ["command"] = "Command",
            ["description"] = "Description",
            ["use"] = "Use",
            ["back"] = "Back",
            ["next"] = "Next",
            ["return"] = "Return",
            ["selectplayer"] = "Select a player",
            ["togglepermplayer"] = "Toggle permissions for player : {0}",
            ["togglepermgroup"] = "Toggle permissions for group : {0}",
            ["togglegroupplayer"] = "Toggle groups for player : {0}",
            ["groupview"] = "Viewing players in group : {0}",
            ["giveitem"] = "Select a player to give : {0} x {1}",
            ["selectgroup"] = "Select a group",
            ["selectremovegroup"] = "Select a group to remove. <color=#da3633>WARNING! This can not be undone</color>",
            ["selecttarget"] = "Select a target",
            ["onlineplayers"] = "Online Players",
            ["offlineplayers"] = "Offline Players",
            ["inputhelper"] = "To create a new group type a group name, title, and rank. Press Enter after completing each field.\nOnce you are ready hit the 'Create' button",
            ["clonehelper"] = "To clone a group type the group name you want to clone, a new group name, title, and rank. Press Enter after completing each field.\nOnce you are ready hit the 'Clone' button",
            ["create"] = "Create",
            ["clone"] = "Clone",
            ["fromgroupname"] = "Clone From:",
            ["groupname"] = "Name:",
            ["grouptitle"] = "Title (optional):",
            ["grouprank"] = "Rank (optional):",
            ["reset"] = "Reset",
            ["nogroupname"] = "You must set a group name",
            ["nofromgroupname"] = "You must supply a valid existing group name to clone from",
            ["invalidfromgroupname"] = "The group name {0} does not exist",
            ["groupcreated"] = "You have successfully created the group: {0}",
            ["groupcloned"] = "You have successfully cloned the group: {0} to {1}",
            ["copyusers"] = "Copy users:",
            ["commandrun"] = "You have run the command : {0}",
            ["groupremoved"] = "You have removed the group : {0}",
            ["uiwarning"] = "** Note ** Close any other UI plugins you have running that automatically refresh (LustyMap or InfoPanel for example). Having these open will cause your input boxes to continually refresh!",
            ["give"] = "Give Items",
            ["playerinfo"] = "Player Info",
            ["Weapon"] = "Weapon",
            ["Construction"] = "Construction",
            ["Items"] = "Items",
            ["Resources"] = "Resources",
            ["Attire"] = "Attire",
            ["Tool"] = "Tool",
            ["Medical"] = "Medical",
            ["Food"] = "Food",
            ["Ammunition"] = "Ammunition",
            ["Traps"] = "Traps",
            ["Misc"] = "Misc",
            ["Component"] = "Component",
            ["noplayer"] = "Unable to find the specified player",
            ["gaveitem"] = "You gave {0}x {1} to {2}",
            ["chatmute.success"] = "You have chat muted {0}",
            ["chatunmute.success"] = "You have disabled chat mute for {0}",
            ["stripinv.success"] = "You have stripped {0}'s inventory",
            ["resetmetabolism.success"] = "You have reset {0}'s metabolism",
            ["hurt.success"] = "You have deducted {0}% of {1}'s current health",
            ["heal.success"] = "You have healed {0} by {1}% of their max health",
            ["teleport.success"] = "You have teleported to {0}'s position",
            ["kill.success"] = "You have killed {0}",
            ["kick.success"] = "You have kicked {0}",
            ["ban.success"] = "You have banned {0}",
            ["resetblueprints.success"] = "You have reset {0}'s blueprint",
            ["unlockblueprints.success"] = "You have given {0} all available blueprints",
            ["action.ban"] = "Ban",
            ["action.banwithreason"] = "Ban {0} with reason?\n<size=10>Press enter when finished typing, or leave empty for default reason</size>",
            ["action.kick"] = "Kick",
            ["action.kickwithreason"] = "Kick {0} with reason?\n<size=10>Press enter when finished typing, or leave empty for default reason</size>",
            ["action.cancel"] = "Cancel",
            ["action.kill"] = "Kill",
            ["action.mutechat"] = "Mute Chat",
            ["action.mutevoice"] = "Mute Voice",
            ["action.unmutechat"] = "Unmute Chat",
            ["action.unmutevoice"] = "Unmute Voice",
            ["action.stripinventory"] = "Strip Inventory",
            ["action.resetblueprints"] = "Reset Blueprints",
            ["action.giveblueprints"] = "Give Blueprints",
            ["action.resetmetabolism"] = "Reset Metabolism",
            ["action.hurt25"] = "Hurt 25%",
            ["action.hurt50"] = "Hurt 50%",
            ["action.hurt75"] = "Hurt 75%",
            ["action.heal25"] = "Heal 25%",
            ["action.heal50"] = "Heal 50%",
            ["action.heal75"] = "Heal 75%",
            ["action.heal100"] = "Heal 100%",
            ["action.teleportselfto"] = "Teleport Self To",
            ["action.permissions"] = "View Permissions",
        };
        #endregion
    }
}