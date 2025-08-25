using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CupboardStackEditor", "StackEditor", "1.0.0")]
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

        #region Hooks
        void Init()
        {
            permission.RegisterPermission(config.Permission, this);
            LoadDefaultMessages();
        }

        void OnServerInitialized()
        {
            // Регистрируем команды
        }

        // Хук для обработки открытия шкафа
        void OnLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container == null || player == null) return;

            // Проверяем, что это шкаф (cupboard)
            var cupboard = container.GetComponent<BuildingPrivlidge>();
            if (cupboard == null) return;

            // Проверяем права доступа
            if (config.RequirePermission && !permission.UserHasPermission(player.UserIDString, config.Permission))
                return;

            // Добавляем кнопку Stack в интерфейс шкафа
            timer.Once(0.1f, () => AddStackButton(player, cupboard));
        }

        // Хук для закрытия интерфейса
        void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            if (container == null || player == null) return;

            var cupboard = container.GetComponent<BuildingPrivlidge>();
            if (cupboard == null) return;

            // Закрываем наш UI если он открыт
            CuiHelper.DestroyUi(player, "CupboardStackEditor");
        }
        #endregion

        #region UI
        private void AddStackButton(BasePlayer player, BuildingPrivlidge cupboard)
        {
            var elements = new CuiElementContainer();

            // Основная панель для кнопки Stack
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.8" },
                RectTransform = { AnchorMin = "0.02 0.85", AnchorMax = "0.15 0.92" },
                CursorEnabled = true
            }, "Overlay", "CupboardStackButton");

            // Кнопка Stack
            elements.Add(new CuiButton
            {
                Button = { 
                    Command = $"cupboard.stackeditor {cupboard.net.ID}",
                    Color = "0.2 0.6 0.2 0.8"
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { 
                    Text = GetMessage("Stack", player.UserIDString),
                    FontSize = 12,
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
                Image = { Color = "0.1 0.1 0.1 0.9" },
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7" },
                CursorEnabled = true
            }, "Overlay", "CupboardStackEditor");

            // Заголовок
            elements.Add(new CuiLabel
            {
                Text = { 
                    Text = GetMessage("CupboardStackEditor", player.UserIDString),
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1" }
            }, "CupboardStackEditor");

            // Текущие стаки в шкафу
            var items = cupboard.inventory.itemList;
            float yPos = 0.75f;

            elements.Add(new CuiLabel
            {
                Text = { 
                    Text = GetMessage("CurrentItems", player.UserIDString),
                    FontSize = 14,
                    Align = TextAnchor.MiddleLeft,
                    Color = "0.8 0.8 0.8 1"
                },
                RectTransform = { AnchorMin = "0.05 0.7", AnchorMax = "0.95 0.75" }
            }, "CupboardStackEditor");

            int itemIndex = 0;
            foreach (var item in items.Take(8)) // Ограничиваем количество отображаемых предметов
            {
                float itemY = yPos - (itemIndex * 0.08f);
                
                // Название предмета и текущее количество
                elements.Add(new CuiLabel
                {
                    Text = { 
                        Text = $"{item.info.displayName.english}: {item.amount}",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform = { AnchorMin = $"0.05 {itemY - 0.04}", AnchorMax = $"0.6 {itemY}" }
                }, "CupboardStackEditor");

                // Кнопки для изменения количества
                float buttonX = 0.62f;
                foreach (var stackSize in config.AvailableStackSizes)
                {
                    elements.Add(new CuiButton
                    {
                        Button = { 
                            Command = $"cupboard.setstack {cupboard.net.ID} {item.uid} {stackSize}",
                            Color = "0.2 0.4 0.6 0.8"
                        },
                        RectTransform = { AnchorMin = $"{buttonX} {itemY - 0.04}", AnchorMax = $"{buttonX + 0.06} {itemY}" },
                        Text = { 
                            Text = stackSize.ToString(),
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, "CupboardStackEditor");
                    
                    buttonX += 0.07f;
                }

                itemIndex++;
                if (itemIndex >= 6) break; // Ограничиваем количество отображаемых предметов
            }

            // Кнопка закрытия
            elements.Add(new CuiButton
            {
                Button = { 
                    Command = "cupboard.closeeditor",
                    Color = "0.6 0.2 0.2 0.8"
                },
                RectTransform = { AnchorMin = "0.4 0.05", AnchorMax = "0.6 0.15" },
                Text = { 
                    Text = GetMessage("Close", player.UserIDString),
                    FontSize = 12,
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

            if (config.RequirePermission && !permission.UserHasPermission(player.UserIDString, config.Permission))
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

            if (config.RequirePermission && !permission.UserHasPermission(player.UserIDString, config.Permission))
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

            // Изменяем количество
            item.amount = newAmount;
            item.MarkDirty();

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
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "CupboardStackEditor");
                CuiHelper.DestroyUi(player, "CupboardStackButton");
            }
        }
        #endregion
    }
}