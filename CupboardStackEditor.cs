using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CupboardStackEditor", "StackEditor", "2.0.1")]
    [Description("Plugin for editing stack amounts in cupboards through cupboard.tool interface")]
    public class CupboardStackEditor : RustPlugin
    {
        #region Config
        private Configuration config;

        public class Configuration
        {
            public List<int> AvailableStackSizes { get; set; } = new List<int> { 5000, 2000, 3000, 4000 };
            public string Permission { get; set; } = "cupboardstackeditor.use";
            public bool RequirePermission { get; set; } = false;
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

                if (config.AvailableStackSizes == null || config.AvailableStackSizes.Count == 0)
                {
                    config.AvailableStackSizes = new List<int> { 5000, 2000, 3000, 4000 };
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Fields
        private readonly Dictionary<uint, int> modifiedItems = new Dictionary<uint, int>();
        #endregion

        #region Hooks
        void Init()
        {
            LoadDefaultMessages();
        }

        void OnServerInitialized()
        {
            // Регистрируем права после загрузки конфигурации
            permission.RegisterPermission(config.Permission, this);
            
            // Автоматически предоставляем права всем администраторам
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsAdmin)
                {
                    permission.GrantUserPermission(player.UserIDString, config.Permission, this);
                }
            }
            
            Puts($"CupboardStackEditor загружен. Право доступа: {config.Permission}");
        }

        // Главный хук для изменения максимального размера стака
        object OnMaxStackable(Item item)
        {
            if (item?.parent?.entityOwner is BuildingPrivlidge)
            {
                // Если у предмета есть пользовательский размер стака
                if (modifiedItems.ContainsKey(item.uid))
                {
                    return modifiedItems[item.uid];
                }
                // Иначе возвращаем максимальный из доступных размеров
                return config.AvailableStackSizes.Max();
            }
            return null;
        }

        // Хук для обработки открытия шкафа
        void OnLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container == null || player == null) return;

            // Проверяем, что это шкаф (cupboard)
            var cupboard = container.GetComponent<BuildingPrivlidge>();
            if (cupboard == null) return;

            // Проверяем права доступа (администраторы всегда имеют доступ)
            if (config.RequirePermission && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, config.Permission))
                return;

            // Добавляем кнопку Stack в интерфейс шкафа
            timer.Once(0.2f, () => AddStackButton(player, cupboard));
        }

        // Хук для закрытия интерфейса
        void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            if (container == null || player == null) return;

            var cupboard = container.GetComponent<BuildingPrivlidge>();
            if (cupboard == null) return;

            // Закрываем наш UI если он открыт
            CuiHelper.DestroyUi(player, "CupboardStackEditor");
            CuiHelper.DestroyUi(player, "CupboardStackButton");
        }

        // Хук для удаления предметов - очищаем данные
        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container?.entityOwner is BuildingPrivlidge && modifiedItems.ContainsKey(item.uid))
            {
                modifiedItems.Remove(item.uid);
            }
        }

        // Хук для автоматического предоставления прав новым администраторам
        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                timer.Once(1f, () => 
                {
                    permission.GrantUserPermission(player.UserIDString, config.Permission, this);
                    Puts($"Права {config.Permission} автоматически предоставлены администратору {player.displayName}");
                });
            }
        }
        #endregion

        #region UI
        private void AddStackButton(BasePlayer player, BuildingPrivlidge cupboard)
        {
            // Сначала убираем старую кнопку
            CuiHelper.DestroyUi(player, "CupboardStackButton");

            var elements = new CuiElementContainer();

            // Основная панель для кнопки Stack - размещаем в правом верхнем углу
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.2 0.6 0.2 0.9" },
                RectTransform = { AnchorMin = "0.82 0.88", AnchorMax = "0.98 0.95" },
                CursorEnabled = true
            }, "Hud", "CupboardStackButton");

            // Кнопка Stack
            elements.Add(new CuiButton
            {
                Button = { 
                    Command = $"cupboard.stackeditor {cupboard.net.ID}",
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

        private void ShowStackEditor(BasePlayer player, BuildingPrivlidge cupboard)
        {
            // Сначала закрываем существующий UI
            CuiHelper.DestroyUi(player, "CupboardStackEditor");

            var elements = new CuiElementContainer();

            // Основная панель
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.95" },
                RectTransform = { AnchorMin = "0.25 0.25", AnchorMax = "0.75 0.75" },
                CursorEnabled = true
            }, "Overlay", "CupboardStackEditor");

            // Заголовок
            elements.Add(new CuiLabel
            {
                Text = { 
                    Text = GetMessage("CupboardStackEditor", player.UserIDString),
                    FontSize = 18,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 0.98" }
            }, "CupboardStackEditor");

            // Текущие стаки в шкафу
            var items = cupboard.inventory.itemList.ToList();
            float yPos = 0.82f;

            elements.Add(new CuiLabel
            {
                Text = { 
                    Text = GetMessage("CurrentItems", player.UserIDString),
                    FontSize = 16,
                    Align = TextAnchor.MiddleLeft,
                    Color = "0.8 0.8 0.8 1"
                },
                RectTransform = { AnchorMin = "0.05 0.78", AnchorMax = "0.95 0.85" }
            }, "CupboardStackEditor");

            int itemIndex = 0;
            foreach (var item in items.Take(8)) // Ограничиваем количество отображаемых предметов
            {
                float itemY = yPos - (itemIndex * 0.09f);
                
                // Название предмета и текущее количество
                elements.Add(new CuiLabel
                {
                    Text = { 
                        Text = $"{item.info.displayName.english}: {item.amount}",
                        FontSize = 14,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform = { AnchorMin = $"0.05 {itemY - 0.04}", AnchorMax = $"0.55 {itemY + 0.01}" }
                }, "CupboardStackEditor");

                // Кнопки для изменения количества
                float buttonX = 0.58f;
                foreach (var stackSize in config.AvailableStackSizes)
                {
                    var buttonColor = "0.2 0.4 0.6 0.8";
                    if (item.amount == stackSize)
                    {
                        buttonColor = "0.2 0.6 0.2 0.8"; // Зеленый для текущего значения
                    }

                    elements.Add(new CuiButton
                    {
                        Button = { 
                            Command = $"cupboard.setstack {cupboard.net.ID} {item.uid} {stackSize}",
                            Color = buttonColor
                        },
                        RectTransform = { AnchorMin = $"{buttonX} {itemY - 0.04}", AnchorMax = $"{buttonX + 0.08} {itemY + 0.01}" },
                        Text = { 
                            Text = stackSize.ToString(),
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, "CupboardStackEditor");
                    
                    buttonX += 0.09f;
                }

                itemIndex++;
                if (itemIndex >= 7) break; // Ограничиваем количество отображаемых предметов
            }

            // Кнопка закрытия
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
            }, "CupboardStackEditor");

            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Commands
        [ConsoleCommand("cupboard.stackeditor")]
        private void CmdStackEditor(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (config.RequirePermission && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, config.Permission))
            {
                SendReply(player, GetMessage("NoPermission", player.UserIDString));
                return;
            }

            if (!ulong.TryParse(arg.Args[0], out ulong cupboardId))
                return;

            var cupboard = BaseNetworkable.serverEntities.Find(cupboardId) as BuildingPrivlidge;
            if (cupboard == null)
                return;

            // Проверяем, что игрок имеет доступ к шкафу
            if (!cupboard.IsAuthed(player))
            {
                SendReply(player, GetMessage("NotAuthorized", player.UserIDString));
                return;
            }

            ShowStackEditor(player, cupboard);
        }

        [ConsoleCommand("cupboard.setstack")]
        private void CmdSetStack(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (config.RequirePermission && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, config.Permission))
                return;

            if (arg.Args.Length < 3) return;

            if (!ulong.TryParse(arg.Args[0], out ulong cupboardId) ||
                !uint.TryParse(arg.Args[1], out uint itemId) ||
                !int.TryParse(arg.Args[2], out int newAmount))
                return;

            var cupboard = BaseNetworkable.serverEntities.Find(cupboardId) as BuildingPrivlidge;
            if (cupboard == null || !cupboard.IsAuthed(player))
                return;

            var item = cupboard.inventory.FindItemUID(itemId);
            if (item == null) return;

            // Проверяем, что новое количество входит в разрешенные значения
            if (!config.AvailableStackSizes.Contains(newAmount))
                return;

            // Сохраняем информацию о модифицированном предмете
            modifiedItems[item.uid] = Math.Max(newAmount, item.info.stackable);

            // Изменяем количество предмета
            item.amount = newAmount;
            item.MarkDirty();
            
            // Обновляем инвентарь шкафа
            cupboard.inventory.MarkDirty();

            // Обновляем UI
            ShowStackEditor(player, cupboard);

            SendReply(player, string.Format(GetMessage("StackChanged", player.UserIDString), newAmount, item.info.displayName.english));
        }

        [ConsoleCommand("cupboard.closeeditor")]
        private void CmdCloseEditor(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "CupboardStackEditor");
        }

        [ChatCommand("cupboard")]
        private void CmdCupboard(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, GetMessage("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "Доступные команды:");
                SendReply(player, "/cupboard grant <steamid/name> - Выдать права игроку");
                SendReply(player, "/cupboard revoke <steamid/name> - Отозвать права у игрока");
                SendReply(player, "/cupboard check <steamid/name> - Проверить права игрока");
                return;
            }

            switch (args[0].ToLower())
            {
                case "grant":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Использование: /cupboard grant <steamid/name>");
                        return;
                    }
                    GrantPermissionCommand(player, args[1]);
                    break;

                case "revoke":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Использование: /cupboard revoke <steamid/name>");
                        return;
                    }
                    RevokePermissionCommand(player, args[1]);
                    break;

                case "check":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Использование: /cupboard check <steamid/name>");
                        return;
                    }
                    CheckPermissionCommand(player, args[1]);
                    break;

                default:
                    SendReply(player, "Неизвестная команда. Используйте /cupboard для справки.");
                    break;
            }
        }

        private void GrantPermissionCommand(BasePlayer admin, string target)
        {
            var targetPlayer = FindPlayer(target);
            if (targetPlayer != null)
            {
                permission.GrantUserPermission(targetPlayer.UserIDString, config.Permission, this);
                SendReply(admin, $"Права {config.Permission} выданы игроку {targetPlayer.displayName}");
                SendReply(targetPlayer, "Вам выданы права для использования редактора стаков шкафа!");
            }
            else
            {
                // Попробуем найти по SteamID
                if (target.Length == 17 && ulong.TryParse(target, out ulong steamId))
                {
                    permission.GrantUserPermission(target, config.Permission, this);
                    SendReply(admin, $"Права {config.Permission} выданы игроку с SteamID {target}");
                }
                else
                {
                    SendReply(admin, "Игрок не найден. Используйте точное имя или SteamID.");
                }
            }
        }

        private void RevokePermissionCommand(BasePlayer admin, string target)
        {
            var targetPlayer = FindPlayer(target);
            if (targetPlayer != null)
            {
                permission.RevokeUserPermission(targetPlayer.UserIDString, config.Permission);
                SendReply(admin, $"Права {config.Permission} отозваны у игрока {targetPlayer.displayName}");
                SendReply(targetPlayer, "У вас отозваны права для использования редактора стаков шкафа.");
            }
            else
            {
                if (target.Length == 17 && ulong.TryParse(target, out ulong steamId))
                {
                    permission.RevokeUserPermission(target, config.Permission);
                    SendReply(admin, $"Права {config.Permission} отозваны у игрока с SteamID {target}");
                }
                else
                {
                    SendReply(admin, "Игрок не найден. Используйте точное имя или SteamID.");
                }
            }
        }

        private void CheckPermissionCommand(BasePlayer admin, string target)
        {
            var targetPlayer = FindPlayer(target);
            if (targetPlayer != null)
            {
                bool hasPermission = permission.UserHasPermission(targetPlayer.UserIDString, config.Permission);
                string status = hasPermission ? "ЕСТЬ" : "НЕТ";
                SendReply(admin, $"Игрок {targetPlayer.displayName}: права {config.Permission} - {status}");
                if (targetPlayer.IsAdmin)
                {
                    SendReply(admin, "Примечание: Игрок является администратором и имеет доступ независимо от прав.");
                }
            }
            else
            {
                if (target.Length == 17 && ulong.TryParse(target, out ulong steamId))
                {
                    bool hasPermission = permission.UserHasPermission(target, config.Permission);
                    string status = hasPermission ? "ЕСТЬ" : "НЕТ";
                    SendReply(admin, $"SteamID {target}: права {config.Permission} - {status}");
                }
                else
                {
                    SendReply(admin, "Игрок не найден. Используйте точное имя или SteamID.");
                }
            }
        }

        private BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains(nameOrId.ToLower()) || 
                    player.UserIDString == nameOrId)
                {
                    return player;
                }
            }
            return null;
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command.",
                ["NotAuthorized"] = "You are not authorized to access this cupboard.",
                ["StackChanged"] = "Stack amount changed to {0} for {1}",
                ["CupboardStackEditor"] = "Cupboard Stack Editor",
                ["CurrentItems"] = "Current Items:",
                ["Close"] = "Close",
                ["Stack"] = "STACK"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "У вас нет прав для использования этой команды.",
                ["NotAuthorized"] = "Вы не авторизованы для доступа к этому шкафу.",
                ["StackChanged"] = "Количество стака изменено на {0} для {1}",
                ["CupboardStackEditor"] = "Редактор стаков шкафа",
                ["CurrentItems"] = "Текущие предметы:",
                ["Close"] = "Закрыть",
                ["Stack"] = "СТАК"
            }, this, "ru");
        }

        private string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion

        #region Helpers
        private void SendReply(BasePlayer player, string message)
        {
            player.ChatMessage($"<color=#00ff00>[Cupboard Stack Editor]</color> {message}");
        }
        #endregion

        #region Cleanup
        void Unload()
        {
            // Очищаем UI для всех игроков
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "CupboardStackEditor");
                CuiHelper.DestroyUi(player, "CupboardStackButton");
            }
            
            // Очищаем данные о модифицированных предметах
            modifiedItems.Clear();
        }
        #endregion
    }
}