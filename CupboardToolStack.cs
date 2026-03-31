using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("CupboardToolStack", "YourName", "1.0.0")]
    [Description("Плагин для изменения stack предметов с UI")]
    public class CupboardToolStack : RustPlugin
    {
        private Dictionary<ulong, int> playerStackSizes = new Dictionary<ulong, int>();
        private Dictionary<ulong, BasePlayer> playersWithOpenUI = new Dictionary<ulong, BasePlayer>();
        
        protected override void LoadDefaultConfig()
        {
            Config["DefaultStackSize"] = 1000;
            Config["MaxStackSize"] = 10000;
            SaveConfig();
        }
        
        void Init()
        {
            Puts("CupboardToolStack инициализирован");
        }
        
        [ChatCommand("stack")]
        void StackCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /stack <размер> или /stack info");
                return;
            }
            
            if (args[0].ToLower() == "info")
            {
                int currentSize = GetPlayerStackSize(player.userID);
                player.ChatMessage("Текущий размер stack'а: " + currentSize);
                return;
            }
            
            if (args[0].ToLower() == "reset")
            {
                int defaultSize = Config.Get<int>("DefaultStackSize");
                SetPlayerStackSize(player.userID, defaultSize);
                player.ChatMessage("Размер stack'а изменен на " + defaultSize);
                return;
            }
            
            if (args[0].ToLower() == "status" && player.IsAdmin)
            {
                player.ChatMessage("Плагин CupboardToolStack активен");
                player.ChatMessage("Всего игроков с настройками: " + playerStackSizes.Count);
                player.ChatMessage("Стандартный размер: " + Config.Get<int>("DefaultStackSize"));
                player.ChatMessage("Максимальный размер: " + Config.Get<int>("MaxStackSize"));
                return;
            }
            
            if (args[0].ToLower() == "admin" && player.IsAdmin)
            {
                if (args.Length < 3)
                {
                    player.ChatMessage("Использование: /stack admin <steamid> <размер>");
                    return;
                }
                
                if (!ulong.TryParse(args[1], out ulong targetPlayerId))
                {
                    player.ChatMessage("Неверный Steam ID игрока");
                    return;
                }
                
                if (!int.TryParse(args[2], out int adminStackSize))
                {
                    player.ChatMessage("Неверный размер stack'а");
                    return;
                }
                
                SetPlayerStackSize(targetPlayerId, adminStackSize);
                player.ChatMessage($"Размер stack'а для игрока {targetPlayerId} установлен на {adminStackSize}");
                return;
            }
            
            if (!int.TryParse(args[0], out int stackSize))
            {
                int maxSize = Config.Get<int>("MaxStackSize");
                player.ChatMessage("Неверный размер stack'а. Используйте число от 1 до " + maxSize);
                return;
            }
            
            int maxAllowedSize = Config.Get<int>("MaxStackSize");
            if (stackSize < 1 || stackSize > maxAllowedSize)
            {
                player.ChatMessage("Неверный размер stack'а. Используйте число от 1 до " + maxAllowedSize);
                return;
            }
            
            SetPlayerStackSize(player.userID, stackSize);
            player.ChatMessage("Размер stack'а изменен на " + stackSize);
        }
        
        [ConsoleCommand("stack")]
        void StackConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length == 0)
            {
                arg.ReplyWith("Использование: stack <размер> или stack help");
                return;
            }
            
            if (arg.Args[0].ToLower() == "help")
            {
                arg.ReplyWith("Команды:\nstack <размер> - установить размер stack'а\nstack help - показать справку");
                return;
            }
            
            if (arg.Args[0].ToLower() == "admin")
            {
                if (arg.Args.Length < 3)
                {
                    arg.ReplyWith("Использование: stack admin <steamid> <размер>");
                    return;
                }
                
                if (!ulong.TryParse(arg.Args[1], out ulong targetPlayerId))
                {
                    arg.ReplyWith("Неверный Steam ID игрока");
                    return;
                }
                
                if (!int.TryParse(arg.Args[2], out int adminStackSize))
                {
                    arg.ReplyWith("Неверный размер stack'а");
                    return;
                }
                
                SetPlayerStackSize(targetPlayerId, adminStackSize);
                arg.ReplyWith($"Размер stack'а для игрока {targetPlayerId} установлен на {adminStackSize}");
                return;
            }
            
            if (arg.Args[0].ToLower() == "global")
            {
                if (arg.Args.Length < 2)
                {
                    arg.ReplyWith("Использование: stack global <размер>");
                    return;
                }
                
                if (!int.TryParse(arg.Args[1], out int globalStackSize))
                {
                    arg.ReplyWith("Неверный размер stack'а");
                    return;
                }
                
                SetGlobalStackSize(globalStackSize);
                arg.ReplyWith($"Глобальный размер stack'а установлен на {globalStackSize} для всех игроков");
                return;
            }
            
            if (!int.TryParse(arg.Args[0], out int stackSize))
            {
                int maxSize = Config.Get<int>("MaxStackSize");
                arg.ReplyWith($"Неверный размер stack'а. Используйте число от 1 до {maxSize}");
                return;
            }
            
            int maxAllowedSize = Config.Get<int>("MaxStackSize");
            if (stackSize < 1 || stackSize > maxAllowedSize)
            {
                arg.ReplyWith($"Неверный размер stack'а. Используйте число от 1 до {maxAllowedSize}");
                return;
            }
            
            if (arg.Player() != null)
            {
                SetPlayerStackSize(arg.Player().userID, stackSize);
                arg.ReplyWith($"Размер stack'а изменен на {stackSize}");
            }
            else
            {
                arg.ReplyWith($"Размер stack'а установлен на {stackSize} для всех игроков");
            }
        }
        
        private int GetPlayerStackSize(ulong playerId)
        {
            if (playerStackSizes.TryGetValue(playerId, out int size))
                return size;
            
            return Config.Get<int>("DefaultStackSize");
        }
        
        private void SetPlayerStackSize(ulong playerId, int size)
        {
            playerStackSizes[playerId] = size;
        }
        
        private void SetGlobalStackSize(int size)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SetPlayerStackSize(player.userID, size);
            }
            
            Config["DefaultStackSize"] = size;
            SaveConfig();
        }
        
        void OnPlayerInit(BasePlayer player)
        {
            if (!playerStackSizes.ContainsKey(player.userID))
            {
                playerStackSizes[player.userID] = Config.Get<int>("DefaultStackSize");
            }
        }
        
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null)
            {
                playerStackSizes.Remove(player.userID);
                playersWithOpenUI.Remove(player.userID);
            }
        }
        
        void OnServerSave()
        {
            SaveData();
        }
        
        void SaveData()
        {
            var data = new Dictionary<string, object>();
            foreach (var kvp in playerStackSizes)
            {
                data[kvp.Key.ToString()] = kvp.Value;
            }
            
            Interface.Oxide.DataFileSystem.WriteObject("CupboardToolStack", data);
        }
        
        void LoadData()
        {
            var data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, object>>("CupboardToolStack");
            if (data != null)
            {
                playerStackSizes.Clear();
                foreach (var kvp in data)
                {
                    if (ulong.TryParse(kvp.Key, out ulong playerId) && kvp.Value is int stackSize)
                    {
                        playerStackSizes[playerId] = stackSize;
                    }
                }
            }
        }
        
        void OnServerInitialized()
        {
            LoadData();
            LoadDefaultConfig();
            Puts("CupboardToolStack загружен успешно!");
        }
        
        void Unload()
        {
            SaveData();
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (playersWithOpenUI.ContainsKey(player.userID))
                {
                    CuiHelper.DestroyUi(player, "StackSettingsUI");
                }
            }
        }
        
        [ConsoleCommand("stackui.open")]
        void OpenStackUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            ShowStackSettingsUI(player);
        }
        
        [ConsoleCommand("stackui.close")]
        void CloseStackUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            CuiHelper.DestroyUi(player, "StackSettingsUI");
            playersWithOpenUI.Remove(player.userID);
        }
        
        [ConsoleCommand("stackui.test")]
        void TestStackUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            Puts($"Тестирую UI для игрока {player.displayName}");
            ShowStackSettingsUI(player);
        }
        
        [ConsoleCommand("stackui.set")]
        void SetStackSize(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            if (arg.Args == null || arg.Args.Length < 1) return;
            
            if (int.TryParse(arg.Args[0], out int stackSize))
            {
                int maxAllowedSize = Config.Get<int>("MaxStackSize");
                if (stackSize >= 1 && stackSize <= maxAllowedSize)
                {
                    SetPlayerStackSize(player.userID, stackSize);
                    player.ChatMessage("Размер stack'а изменен на " + stackSize);
                    
                    ShowStackSettingsUI(player);
                }
                else
                {
                    player.ChatMessage("Неверный размер stack'а. Используйте число от 1 до " + maxAllowedSize);
                }
            }
        }
        
        [ConsoleCommand("stackui.input")]
        void HandleInput(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            if (arg.Args == null || arg.Args.Length < 1) return;
            
            string input = arg.Args[0];
            if (int.TryParse(input, out int stackSize))
            {
                int maxAllowedSize = Config.Get<int>("MaxStackSize");
                if (stackSize >= 1 && stackSize <= maxAllowedSize)
                {
                    SetPlayerStackSize(player.userID, stackSize);
                    player.ChatMessage("Размер stack'а изменен на " + stackSize);
                    
                    ShowStackSettingsUI(player);
                }
                else
                {
                    player.ChatMessage("Неверный размер stack'а. Используйте число от 1 до " + maxAllowedSize);
                }
            }
            else
            {
                player.ChatMessage("Введите корректное число!");
            }
        }
        
        private void ShowStackSettingsUI(BasePlayer player)
        {
            if (player == null) return;
            
            playersWithOpenUI[player.userID] = player;
            
            var currentStackSize = GetPlayerStackSize(player.userID);
            var maxStackSize = Config.Get<int>("MaxStackSize");
            var defaultStackSize = Config.Get<int>("DefaultStackSize");
            
            var container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                Image = { Color = "0.05 0.05 0.05 0.98" },
                RectTransform = { AnchorMin = "0.25 0.15", AnchorMax = "0.75 0.85" },
                CursorEnabled = true
            }, "Overlay", "StackSettingsUI");
            
            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.6 0.1 1" },
                RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 1" }
            }, "StackSettingsUI", "TitleBar");
            
            container.Add(new CuiLabel
            {
                Text = { Text = "⚙️ НАСТРОЙКИ STACK'А", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "TitleBar");
            
            container.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 1" },
                RectTransform = { AnchorMin = "0.05 0.75", AnchorMax = "0.95 0.9" }
            }, "StackSettingsUI", "InfoPanel");
            
            container.Add(new CuiLabel
            {
                Text = { Text = $"📊 Текущий размер: {currentStackSize}", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.05 0.6", AnchorMax = "0.95 0.8" }
            }, "InfoPanel");
            
            container.Add(new CuiLabel
            {
                Text = { Text = $"📈 Максимальный: {maxStackSize}", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.05 0.35", AnchorMax = "0.45 0.55" }
            }, "InfoPanel");
            
            container.Add(new CuiLabel
            {
                Text = { Text = $"📋 Стандартный: {defaultStackSize}", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.55 0.35", AnchorMax = "0.95 0.55" }
            }, "InfoPanel");
            
            container.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.12 0.12 1" },
                RectTransform = { AnchorMin = "0.05 0.35", AnchorMax = "0.95 0.7" }
            }, "StackSettingsUI", "StackInputPanel");
            
            container.Add(new CuiLabel
            {
                Text = { Text = "🎯 Выберите размер stack'а:", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.02 0.9", AnchorMax = "0.98 1" }
            }, "StackInputPanel");
            
            container.Add(new CuiButton
            {
                Button = { Color = "0.2 0.6 0.2 1", Command = "stackui.set 500" },
                Text = { Text = "500", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.02 0.55", AnchorMax = "0.23 0.7" }
            }, "StackInputPanel");
            
            container.Add(new CuiButton
            {
                Button = { Color = "0.2 0.6 0.2 1", Command = "stackui.set 1000" },
                Text = { Text = "1000", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.25 0.55", AnchorMax = "0.46 0.7" }
            }, "StackInputPanel");
            
            container.Add(new CuiButton
            {
                Button = { Color = "0.2 0.6 0.2 1", Command = "stackui.set 2000" },
                Text = { Text = "2000", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.48 0.55", AnchorMax = "0.69 0.7" }
            }, "StackInputPanel");
            
            container.Add(new CuiButton
            {
                Button = { Color = "0.2 0.6 0.2 1", Command = "stackui.set 5000" },
                Text = { Text = "5000", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.71 0.55", AnchorMax = "0.92 0.7" }
            }, "StackInputPanel");
            
            container.Add(new CuiButton
            {
                Button = { Color = "0.3 0.7 0.3 1", Command = "stackui.set 1500" },
                Text = { Text = "1500", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.23 0.5" }
            }, "StackInputPanel");
            
            container.Add(new CuiButton
            {
                Button = { Color = "0.3 0.7 0.3 1", Command = "stackui.set 3000" },
                Text = { Text = "3000", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.25 0.35", AnchorMax = "0.46 0.5" }
            }, "StackInputPanel");
            
            container.Add(new CuiButton
            {
                Button = { Color = "0.3 0.7 0.3 1", Command = "stackui.set 7500" },
                Text = { Text = "7500", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.48 0.35", AnchorMax = "0.69 0.5" }
            }, "StackInputPanel");
            
            container.Add(new CuiButton
            {
                Button = { Color = "0.3 0.7 0.3 1", Command = "stackui.set 10000" },
                Text = { Text = "10000", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.71 0.35", AnchorMax = "0.92 0.5" }
            }, "StackInputPanel");
            
            container.Add(new CuiButton
            {
                Button = { Color = "0.8 0.4 0.4 1", Command = "stackui.set " + defaultStackSize },
                Text = { Text = "🔄 Сбросить", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.02 0.1", AnchorMax = "0.48 0.25" }
            }, "StackInputPanel");
            
            container.Add(new CuiButton
            {
                Button = { Color = "0.6 0.3 0.3 1", Command = "stackui.close" },
                Text = { Text = "❌ Закрыть", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.4 0.05", AnchorMax = "0.6 0.15" }
            }, "StackSettingsUI");
            
            CuiHelper.DestroyUi(player, "StackSettingsUI");
            CuiHelper.AddUi(player, container);
        }
        
        void OnEntityBuilt(StorageContainer container, BasePlayer player)
        {
            if (player == null) return;
            
            NextTick(() => {
                if (container != null && !container.IsDestroyed)
                {
                    ModifyContainerStacks(container, player);
                }
            });
        }
        
        void OnLootEntity(BasePlayer player, BuildingPrivlidge entity)
        {
            if (player == null || entity == null) return;
            
            Puts($"OnLootEntity triggered for BuildingPrivlidge");
            
            AddStackButtonToLootUI(player, entity);
        }
        
        void OnPlayerLootEnd(PlayerLoot inventory)
        {
            if (!(inventory.entitySource is BuildingPrivlidge)) return;
            var player = inventory?.baseEntity as BasePlayer;
            if (player == null) return;
            
            CuiHelper.DestroyUi(player, "StackButtonUI");
        }
        
        private void AddStackButtonToLootUI(BasePlayer player, BaseEntity entity)
        {
            if (player == null) return;
            
            var cont = new CuiElementContainer();
            
            // Position the button next to the "УПРАВЛЕНИЕ СОДЕРЖАНИЕМ" button in the cupboard interface
            cont.Add(new CuiPanel()
            {
                Image =
                {
                    Color = "0 0 0 0",
                },
                RectTransform =
                {
                    AnchorMin = "0 1",
                    AnchorMax = "0 1",
                    OffsetMin = "240 -35",  // Positioned to the right of content management button
                    OffsetMax = "335 -10"   // Adjusted width and height for better visibility
                }
            }, "Overlay", "StackButtonUI");
            
            cont.Add(new CuiElement()
            {
                Name = "StackButtonUIGlobal",
                Parent = "StackButtonUI",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.2 0.6 0.8 0.9"  // Blue color to distinguish from other buttons
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            
            cont.Add(new CuiButton()
            {
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "stackui.open",
                },
                Text =
                {
                    Text = "⚙️ Stack",
                    Align = TextAnchor.MiddleCenter,
                    FontSize = 12,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
            }, "StackButtonUIGlobal");
            
            CuiHelper.DestroyUi(player, "StackButtonUI");
            CuiHelper.AddUi(player, cont);
        }
        
        private void ModifyContainerStacks(StorageContainer container, BasePlayer player)
        {
            if (container == null || container.inventory == null) return;
            
            try
            {
                var slots = container.inventory.itemList;
                if (slots != null)
                {
                    foreach (var item in slots)
                    {
                        if (item != null && item.amount < GetPlayerStackSize(player.userID))
                        {
                            item.amount = Mathf.Min(item.amount, GetPlayerStackSize(player.userID));
                            item.MarkDirty();
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки доступа к инвентарю
            }
        }
        
        [ChatCommand("stackui")]
        void StackUICommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                ShowStackSettingsUI(player);
                return;
            }
            
            if (args[0].ToLower() == "open")
            {
                ShowStackSettingsUI(player);
            }
            else if (args[0].ToLower() == "close")
            {
                CuiHelper.DestroyUi(player, "StackSettingsUI");
                playersWithOpenUI.Remove(player.userID);
            }
            else if (args[0].ToLower() == "test")
            {
                var uiContainer = new CuiElementContainer();
                
                uiContainer.Add(new CuiButton
                {
                    Button = { Color = "0.2 0.8 0.2 1", Command = "stackui.open" },
                    Text = { Text = "⚙️ Stack", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.85 0.9", AnchorMax = "0.98 0.98" }
                }, "Overlay", "StackButtonUI");
                
                CuiHelper.DestroyUi(player, "StackButtonUI");
                CuiHelper.AddUi(player, uiContainer);
                player.ChatMessage("Тестовая кнопка добавлена!");
            }
        }
    }
}