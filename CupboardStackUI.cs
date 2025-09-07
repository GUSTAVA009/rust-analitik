using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CupboardStackUI", "StackEditor", "2.2.0")]
    [Description("Admin-only plugin for editing stack amounts in cupboards through cupboard.tool interface")]
    public class CupboardStackUI : RustPlugin
    {
        #region Config
        private Configuration config;

        public class Configuration
        {
            public List<int> AvailableStackSizes { get; set; } = new List<int> { 5000, 2000, 3000, 4000 };
            public List<string> AllowedItemTypes { get; set; } = new List<string>(); // Whitelist of allowed item types
            public string Permission { get; set; } = "CupboardStackUI.use";
            public bool RequirePermission { get; set; } = true; // Always require permission (admin only)
            public int MaxItemsDisplay { get; set; } = 8;
            public float UIUpdateDelay { get; set; } = 0.2f;
            public bool EnableLogging { get; set; } = true;
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
                    PrintWarning("Config is null, loading default");
                    LoadDefaultConfig();
                }

                // Validate and fix configuration
                if (config.AvailableStackSizes == null || config.AvailableStackSizes.Count == 0)
                {
                    config.AvailableStackSizes = new List<int> { 5000, 2000, 3000, 4000 };
                }

                if (config.AllowedItemTypes == null)
                {
                    config.AllowedItemTypes = new List<string>();
                }

                // Ensure admin-only access
                config.RequirePermission = true;
            }
            catch (Exception ex)
            {
                PrintError($"Failed to load config: {ex.Message}");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Fields
        private readonly Dictionary<ulong, BuildingPrivlidge> playerCupboards = new Dictionary<ulong, BuildingPrivlidge>();
        private readonly Dictionary<ulong, DateTime> lastUIUpdate = new Dictionary<ulong, DateTime>();
        #endregion

        #region Hooks
        void Init()
        {
            LoadDefaultMessages();
        }

        void OnServerInitialized()
        {
            // Grant permissions only to administrators
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsAdmin)
                {
                    permission.GrantUserPermission(player.UserIDString, config.Permission, this);
                    if (config.EnableLogging)
                    {
                        Puts($"Permission {config.Permission} granted to admin: {player.displayName}");
                    }
                }
            }
            
            Puts($"CupboardStackUI loaded. Admin-only access enabled. Permission: {config.Permission}");
        }

        // Enhanced security for stack size modification
        object OnMaxStackable(Item item)
        {
            try
            {
                if (item?.parent?.entityOwner is BuildingPrivlidge cupboard)
                {
                    // Only apply to allowed item types if whitelist is configured
                    if (config.AllowedItemTypes.Count > 0 && !IsAllowedItemType(item.info))
                    {
                        return null;
                    }

                    // Additional security check - ensure item is in a valid state
                    if (item.IsValid() && item.amount > 0)
                    {
                        return config.AvailableStackSizes.Max();
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnMaxStackable: {ex.Message}");
            }
            return null;
        }

        // Enhanced cupboard access hook with admin-only check
        void OnLootEntity(BasePlayer player, StorageContainer container)
        {
            try
            {
                if (container == null || player == null) return;

                // Check if player is admin first
                if (!player.IsAdmin)
                {
                    return; // Block access for non-admins
                }

                var cupboard = container.GetComponent<BuildingPrivlidge>();
                if (cupboard == null) return;

                // Double-check permission (admin-only)
                if (!permission.UserHasPermission(player.UserIDString, config.Permission))
                {
                    if (config.EnableLogging)
                    {
                        PrintWarning($"Admin {player.displayName} lacks permission {config.Permission}");
                    }
                    return;
                }

                // Store cupboard reference for player
                playerCupboards[player.userID] = cupboard;
                
                // Add stack button with delay to prevent UI conflicts
                timer.Once(config.UIUpdateDelay, () => 
                {
                    if (player != null && player.IsConnected)
                    {
                        AddStackButton(player);
                    }
                });
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnLootEntity: {ex.Message}");
            }
        }

        // Enhanced cleanup hook
        void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            try
            {
                if (container == null || player == null) return;

                var cupboard = container.GetComponent<BuildingPrivlidge>();
                if (cupboard == null) return;

                // Clean up UI with delay to prevent flicker
                timer.Once(0.1f, () =>
                {
                    if (player != null && player.IsConnected)
                    {
                        CuiHelper.DestroyUi(player, "CupboardStackUI");
                        CuiHelper.DestroyUi(player, "CupboardStackButton");
                    }
                });
                
                // Clean up player data
                if (playerCupboards.ContainsKey(player.userID))
                {
                    playerCupboards.Remove(player.userID);
                }

                if (lastUIUpdate.ContainsKey(player.userID))
                {
                    lastUIUpdate.Remove(player.userID);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnLootEntityEnd: {ex.Message}");
            }
        }

        // Auto-grant permissions to new admins
        void OnPlayerConnected(BasePlayer player)
        {
            try
            {
                if (player.IsAdmin)
                {
                    timer.Once(1f, () => 
                    {
                        if (player != null && player.IsConnected)
                        {
                            permission.GrantUserPermission(player.UserIDString, config.Permission, this);
                            if (config.EnableLogging)
                            {
                                Puts($"Permission {config.Permission} auto-granted to new admin: {player.displayName}");
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnPlayerConnected: {ex.Message}");
            }
        }

        // Clean up on player disconnect
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            try
            {
                if (playerCupboards.ContainsKey(player.userID))
                {
                    playerCupboards.Remove(player.userID);
                }

                if (lastUIUpdate.ContainsKey(player.userID))
                {
                    lastUIUpdate.Remove(player.userID);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnPlayerDisconnected: {ex.Message}");
            }
        }
        #endregion

        #region UI
        private void AddStackButton(BasePlayer player)
        {
            try
            {
                // Remove existing button first
                CuiHelper.DestroyUi(player, "CupboardStackButton");

                var elements = new CuiElementContainer();

                // Main panel for Stack button - positioned in top right corner
                elements.Add(new CuiPanel
                {
                    Image = { Color = "0.2 0.6 0.2 0.9" },
                    RectTransform = { AnchorMin = "0.82 0.88", AnchorMax = "0.98 0.95" },
                    CursorEnabled = true
                }, "Overlay", "CupboardStackButton");

                // Stack button
                elements.Add(new CuiButton
                {
                    Button = { 
                        Command = "cupboard.stackeditor",
                        Color = "0 0 0 0"
                    },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { 
                        Text = GetMessage("Stack", player.UserIDString),
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, "CupboardStackButton");

                CuiHelper.AddUi(player, elements);
            }
            catch (Exception ex)
            {
                PrintError($"Error in AddStackButton: {ex.Message}");
            }
        }

        private void ShowStackEditor(BasePlayer player, BuildingPrivlidge cupboard)
        {
            try
            {
                // Check for UI update throttling
                if (lastUIUpdate.ContainsKey(player.userID))
                {
                    var timeSinceLastUpdate = DateTime.Now - lastUIUpdate[player.userID];
                    if (timeSinceLastUpdate.TotalMilliseconds < 100) // 100ms throttle
                    {
                        return;
                    }
                }

                lastUIUpdate[player.userID] = DateTime.Now;

                // Close existing UI
                CuiHelper.DestroyUi(player, "CupboardStackUI");

                var elements = new CuiElementContainer();

                // Main panel
                elements.Add(new CuiPanel
                {
                    Image = { Color = "0.1 0.1 0.1 0.95" },
                    RectTransform = { AnchorMin = "0.25 0.25", AnchorMax = "0.75 0.75" },
                    CursorEnabled = true
                }, "Overlay", "CupboardStackUI");

                // Header
                elements.Add(new CuiLabel
                {
                    Text = { 
                        Text = GetMessage("CupboardStackUI", player.UserIDString),
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 0.98" }
                }, "CupboardStackUI");

                // Current items in cupboard
                var items = cupboard.inventory.itemList.ToList();
                float yPos = 0.82f;
                float itemHeight = 0.08f; // Increased height for better spacing
                float buttonWidth = 0.07f; // Reduced button width
                float buttonSpacing = 0.01f; // Spacing between buttons

                elements.Add(new CuiLabel
                {
                    Text = { 
                        Text = GetMessage("CurrentItems", player.UserIDString),
                        FontSize = 16,
                        Align = TextAnchor.MiddleLeft,
                        Color = "0.8 0.8 0.8 1"
                    },
                    RectTransform = { AnchorMin = "0.05 0.78", AnchorMax = "0.95 0.85" }
                }, "CupboardStackUI");

                int itemIndex = 0;
                int maxItems = Math.Min(items.Count, config.MaxItemsDisplay);
                
                for (int i = 0; i < maxItems; i++)
                {
                    var item = items[i];
                    if (item == null || !item.IsValid()) continue;

                    float itemY = yPos - (itemIndex * itemHeight);
                    
                    // Item name and current amount - reduced width to make room for buttons
                    elements.Add(new CuiLabel
                    {
                        Text = { 
                            Text = $"{item.info.displayName.english}: {item.amount}",
                            FontSize = 12, // Reduced font size
                            Align = TextAnchor.MiddleLeft,
                            Color = "1 1 1 1"
                        },
                        RectTransform = { AnchorMin = $"0.05 {itemY - 0.03}", AnchorMax = $"0.45 {itemY + 0.02}" }
                    }, "CupboardStackUI");

                    // Stack size buttons - positioned to fit within the interface
                    float buttonX = 0.47f; // Start buttons after item name
                    int buttonCount = 0;
                    int maxButtonsPerRow = 4; // Limit buttons per row
                    
                    foreach (var stackSize in config.AvailableStackSizes)
                    {
                        if (buttonCount >= maxButtonsPerRow) break; // Prevent overflow
                        
                        var buttonColor = "0.2 0.4 0.6 0.8";
                        if (item.amount == stackSize)
                        {
                            buttonColor = "0.2 0.6 0.2 0.8"; // Green for current value
                        }

                        elements.Add(new CuiButton
                        {
                            Button = { 
                                Command = $"cupboard.setstack {itemIndex} {stackSize}",
                                Color = buttonColor
                            },
                            RectTransform = { 
                                AnchorMin = $"{buttonX} {itemY - 0.03}", 
                                AnchorMax = $"{buttonX + buttonWidth} {itemY + 0.02}" 
                            },
                            Text = { 
                                Text = stackSize.ToString(),
                                FontSize = 10, // Reduced font size
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            }
                        }, "CupboardStackUI");
                        
                        buttonX += buttonWidth + buttonSpacing;
                        buttonCount++;
                    }

                    itemIndex++;
                }

                // Close button
                elements.Add(new CuiButton
                {
                    Button = { 
                        Command = "cupboard.closeeditor",
                        Color = "0.6 0.2 0.2 0.8"
                    },
                    RectTransform = { AnchorMin = "0.35 0.05", AnchorMax = "0.65 0.12" },
                    Text = { 
                        Text = GetMessage("Close", player.UserIDString),
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, "CupboardStackUI");

                CuiHelper.AddUi(player, elements);
            }
            catch (Exception ex)
            {
                PrintError($"Error in ShowStackEditor: {ex.Message}");
                SendReply(player, GetMessage("ErrorOccurred", player.UserIDString));
            }
        }
        #endregion

        #region Commands
        [ConsoleCommand("cupboard.stackeditor")]
        private void CmdStackEditor(ConsoleSystem.Arg arg)
        {
            try
            {
                var player = arg.Player();
                if (player == null) return;

                // Admin-only access check
                if (!player.IsAdmin)
                {
                    SendReply(player, GetMessage("AdminOnly", player.UserIDString));
                    return;
                }

                // Permission check
                if (!permission.UserHasPermission(player.UserIDString, config.Permission))
                {
                    SendReply(player, GetMessage("NoPermission", player.UserIDString));
                    return;
                }

                // Get cupboard from stored references
                if (!playerCupboards.ContainsKey(player.userID))
                {
                    SendReply(player, GetMessage("CupboardNotFound", player.UserIDString));
                    return;
                }

                var cupboard = playerCupboards[player.userID];
                if (cupboard == null || cupboard.IsDestroyed)
                {
                    playerCupboards.Remove(player.userID);
                    SendReply(player, GetMessage("CupboardUnavailable", player.UserIDString));
                    return;
                }

                // Verify player has access to cupboard
                if (!cupboard.IsAuthed(player))
                {
                    SendReply(player, GetMessage("NotAuthorized", player.UserIDString));
                    return;
                }

                ShowStackEditor(player, cupboard);
            }
            catch (Exception ex)
            {
                PrintError($"Error in CmdStackEditor: {ex.Message}");
                if (arg.Player() != null)
                {
                    SendReply(arg.Player(), GetMessage("ErrorOccurred", arg.Player().UserIDString));
                }
            }
        }

        [ConsoleCommand("cupboard.setstack")]
        private void CmdSetStack(ConsoleSystem.Arg arg)
        {
            try
            {
                var player = arg.Player();
                if (player == null) return;

                // Admin-only access check
                if (!player.IsAdmin)
                {
                    return; // Silent return for non-admins
                }

                // Permission check
                if (!permission.UserHasPermission(player.UserIDString, config.Permission))
                {
                    return;
                }

                // Validate arguments
                if (arg.Args?.Length < 2)
                {
                    SendReply(player, GetMessage("InvalidArguments", player.UserIDString));
                    return;
                }

                if (!int.TryParse(arg.Args[0], out int itemIndex) ||
                    !int.TryParse(arg.Args[1], out int newAmount))
                {
                    SendReply(player, GetMessage("InvalidArguments", player.UserIDString));
                    return;
                }

                // Get cupboard from stored references
                if (!playerCupboards.ContainsKey(player.userID))
                {
                    SendReply(player, GetMessage("CupboardNotFound", player.UserIDString));
                    return;
                }

                var cupboard = playerCupboards[player.userID];
                if (cupboard == null || cupboard.IsDestroyed || !cupboard.IsAuthed(player))
                {
                    SendReply(player, GetMessage("CupboardUnavailable", player.UserIDString));
                    return;
                }

                var items = cupboard.inventory.itemList;
                if (itemIndex < 0 || itemIndex >= items.Count)
                {
                    SendReply(player, GetMessage("InvalidItemIndex", player.UserIDString));
                    return;
                }

                var item = items[itemIndex];
                if (item == null || !item.IsValid())
                {
                    SendReply(player, GetMessage("InvalidItem", player.UserIDString));
                    return;
                }

                // Validate new amount
                if (!config.AvailableStackSizes.Contains(newAmount))
                {
                    SendReply(player, GetMessage("InvalidStackSize", player.UserIDString));
                    return;
                }

                // Additional security: check if item type is allowed
                if (config.AllowedItemTypes.Count > 0 && !IsAllowedItemType(item.info))
                {
                    SendReply(player, GetMessage("ItemTypeNotAllowed", player.UserIDString));
                    return;
                }

                // Change item amount
                item.amount = newAmount;
                item.MarkDirty();
                
                // Update cupboard inventory
                cupboard.inventory.MarkDirty();

                // Update UI
                ShowStackEditor(player, cupboard);

                SendReply(player, string.Format(GetMessage("StackChanged", player.UserIDString), newAmount, item.info.displayName.english));

                if (config.EnableLogging)
                {
                    Puts($"Admin {player.displayName} changed stack size of {item.info.displayName.english} to {newAmount} in cupboard");
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in CmdSetStack: {ex.Message}");
                if (arg.Player() != null)
                {
                    SendReply(arg.Player(), GetMessage("ErrorOccurred", arg.Player().UserIDString));
                }
            }
        }

        [ConsoleCommand("cupboard.closeeditor")]
        private void CmdCloseEditor(ConsoleSystem.Arg arg)
        {
            try
            {
                var player = arg.Player();
                if (player == null) return;

                CuiHelper.DestroyUi(player, "CupboardStackUI");
            }
            catch (Exception ex)
            {
                PrintError($"Error in CmdCloseEditor: {ex.Message}");
            }
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminOnly"] = "This feature is only available to administrators.",
                ["NoPermission"] = "You don't have permission to use this command.",
                ["NotAuthorized"] = "You are not authorized to access this cupboard.",
                ["StackChanged"] = "Stack amount changed to {0} for {1}",
                ["CupboardStackUI"] = "Cupboard Stack Editor (Admin Only)",
                ["CurrentItems"] = "Current Items:",
                ["Close"] = "Close",
                ["Stack"] = "STACK",
                ["CupboardNotFound"] = "Error: Cupboard not found. Please close and reopen the cupboard.",
                ["CupboardUnavailable"] = "Error: Cupboard is unavailable.",
                ["InvalidArguments"] = "Invalid arguments provided.",
                ["InvalidItemIndex"] = "Invalid item index.",
                ["InvalidItem"] = "Invalid item.",
                ["InvalidStackSize"] = "Invalid stack size. Please use one of the available sizes.",
                ["ItemTypeNotAllowed"] = "This item type is not allowed to be modified.",
                ["ErrorOccurred"] = "An error occurred. Please try again."
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminOnly"] = "Эта функция доступна только администраторам.",
                ["NoPermission"] = "У вас нет прав для использования этой команды.",
                ["NotAuthorized"] = "Вы не авторизованы для доступа к этому шкафу.",
                ["StackChanged"] = "Количество стака изменено на {0} для {1}",
                ["CupboardStackUI"] = "Редактор стаков шкафа (только для админов)",
                ["CurrentItems"] = "Текущие предметы:",
                ["Close"] = "Закрыть",
                ["Stack"] = "СТАК",
                ["CupboardNotFound"] = "Ошибка: шкаф не найден. Попробуйте закрыть и снова открыть шкаф.",
                ["CupboardUnavailable"] = "Ошибка: шкаф недоступен.",
                ["InvalidArguments"] = "Предоставлены неверные аргументы.",
                ["InvalidItemIndex"] = "Неверный индекс предмета.",
                ["InvalidItem"] = "Неверный предмет.",
                ["InvalidStackSize"] = "Неверный размер стака. Используйте один из доступных размеров.",
                ["ItemTypeNotAllowed"] = "Этот тип предмета нельзя изменять.",
                ["ErrorOccurred"] = "Произошла ошибка. Попробуйте еще раз."
            }, this, "ru");
        }

        private string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion

        #region Helpers
        private void SendReply(BasePlayer player, string message)
        {
            try
            {
                player.ChatMessage($"<color=#00ff00>[Cupboard Stack Editor]</color> {message}");
            }
            catch (Exception ex)
            {
                PrintError($"Error sending reply to player: {ex.Message}");
            }
        }

        private bool IsAllowedItemType(ItemDefinition itemDef)
        {
            try
            {
                if (config.AllowedItemTypes == null || config.AllowedItemTypes.Count == 0)
                {
                    return true; // Allow all if no whitelist configured
                }

                return config.AllowedItemTypes.Contains(itemDef.shortname);
            }
            catch (Exception ex)
            {
                PrintError($"Error checking allowed item type: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Cleanup
        void Unload()
        {
            try
            {
                // Clean up UI for all players
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player != null && player.IsConnected)
                    {
                        CuiHelper.DestroyUi(player, "CupboardStackUI");
                        CuiHelper.DestroyUi(player, "CupboardStackButton");
                    }
                }
                
                // Clean up data
                playerCupboards.Clear();
                lastUIUpdate.Clear();
                
                Puts("CupboardStackUI unloaded successfully");
            }
            catch (Exception ex)
            {
                PrintError($"Error during unload: {ex.Message}");
            }
        }
        #endregion
    }
}