using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CupboardStackUI", "CursorAssistant", "1.0.1")]
    [Description("Allows editing stack sizes inside Tool Cupboard via UI with sizes 5000/2000/3000/4000.")]
    public class CupboardStackUI : RustPlugin
    {
        private const string UiRootButton = "TCStackUI.Button";
        private const string UiRootPanel = "TCStackUI.Panel";
        private const string PermissionAdmin = "cupboardstackui.admin";

        private readonly Dictionary<ulong, BuildingPrivlidge> playerIdToPrivlidge = new Dictionary<ulong, BuildingPrivlidge>();

        private readonly int[] availableStackSizes = new[] { 5000, 2000, 3000, 4000 };

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermissionAdmin, this);
            permission.GrantGroupPermission("admin", PermissionAdmin, this);
        }

        private bool HasAccess(BasePlayer player)
        {
            if (player == null)
            {
                return false;
            }
            if ((player.net?.connection?.authLevel ?? 0) >= 2 || player.IsAdmin)
            {
                return true;
            }
            return permission.UserHasPermission(player.UserIDString, PermissionAdmin);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            var priv = entity as BuildingPrivlidge;
            if (player == null || priv == null)
            {
                return;
            }

            if (!HasAccess(player))
            {
                return;
            }

            playerIdToPrivlidge[player.userID] = priv;
            ShowStackButton(player);
        }

        private void OnLootEntityEnd(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            CleanupUi(player);
            playerIdToPrivlidge.Remove(player.userID);
        }

        private void OnEntityDeath(BuildingPrivlidge priv, HitInfo info)
        {
            if (priv == null)
            {
                return;
            }
            foreach (var kv in playerIdToPrivlidge.Where(kv => kv.Value == priv).ToList())
            {
                if (BasePlayer.FindByID(kv.Key) is BasePlayer player)
                {
                    CleanupUi(player);
                }
                playerIdToPrivlidge.Remove(kv.Key);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null)
            {
                return;
            }

            CleanupUi(player);
            playerIdToPrivlidge.Remove(player.userID);
        }

        [ConsoleCommand("tcstack.open")]
        private void ConsoleCommand_OpenUI(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null)
            {
                return;
            }

            if (!HasAccess(player))
            {
                player.ChatMessage("Недостаточно прав.");
                return;
            }

            if (!playerIdToPrivlidge.ContainsKey(player.userID) || player.inventory.loot.entitySource == null)
            {
                return;
            }

            ShowSelectionPanel(player);
        }

        [ConsoleCommand("tcstack.select")]
        private void ConsoleCommand_Select(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null)
            {
                return;
            }

            if (!HasAccess(player))
            {
                player.ChatMessage("Недостаточно прав.");
                return;
            }

            if (!playerIdToPrivlidge.TryGetValue(player.userID, out var priv) || priv == null)
            {
                player.ChatMessage("Нет активного шкафа.");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 1)
            {
                player.ChatMessage("Не выбрано значение стака.");
                return;
            }

            int targetSize;
            if (!int.TryParse(arg.Args[0], out targetSize))
            {
                player.ChatMessage("Некорректное значение стака.");
                return;
            }

            if (!availableStackSizes.Contains(targetSize))
            {
                player.ChatMessage("Доступные стаки: 5000, 2000, 3000, 4000");
                return;
            }

            var wasLooting = player.inventory?.loot?.entitySource as BuildingPrivlidge;
            if (wasLooting == null || wasLooting.net == null || priv.net == null || wasLooting.net.ID != priv.net.ID)
            {
                player.ChatMessage("Откройте шкаф и попробуйте снова.");
                return;
            }

            var result = RepackCupboard(priv, targetSize);
            if (!string.IsNullOrEmpty(result))
            {
                player.ChatMessage(result);
            }
            else
            {
                player.ChatMessage($"Стаки в шкафу упорядочены по {targetSize}.");
            }

            HideSelectionPanel(player);
        }

        private void ShowStackButton(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiRootButton);
            CuiHelper.DestroyUi(player, UiRootPanel);

            var container = new CuiElementContainer();

            var panel = new CuiPanel
            {
                CursorEnabled = false,
                RectTransform =
                {
                    AnchorMin = "0.83 0.78",
                    AnchorMax = "0.93 0.84"
                },
                Image = { Color = "0.05 0.05 0.05 0.7" }
            };

            var panelName = container.Add(panel, "Hud", UiRootButton);

            var button = new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Button =
                {
                    Color = "0.2 0.6 0.2 0.9",
                    Command = "tcstack.open"
                },
                Text =
                {
                    Text = "STACK",
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1",
                    FontSize = 14
                }
            };

            container.Add(button, panelName);

            CuiHelper.AddUi(player, container);
        }

        private void ShowSelectionPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiRootPanel);

            var container = new CuiElementContainer();

            var bg = new CuiPanel
            {
                CursorEnabled = true,
                RectTransform =
                {
                    AnchorMin = "0.72 0.65",
                    AnchorMax = "0.93 0.90"
                },
                Image = { Color = "0.08 0.08 0.08 0.95" }
            };

            var bgName = container.Add(bg, "Hud", UiRootPanel);

            var titleLabel = new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.05 0.83",
                    AnchorMax = "0.95 0.98"
                },
                Text =
                {
                    Text = "Выберите размер стака",
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1",
                    FontSize = 16
                }
            };

            container.Add(titleLabel, bgName);

            var sizes = availableStackSizes.Distinct().ToArray();
            Array.Sort(sizes);

            for (int i = 0; i < sizes.Length; i++)
            {
                var row = i / 2;
                var col = i % 2;

                var minX = 0.08f + col * 0.48f;
                var maxX = minX + 0.44f;
                var maxY = 0.75f - row * 0.32f;
                var minY = maxY - 0.24f;

                var btn = new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"{minX} {minY}",
                        AnchorMax = $"{maxX} {maxY}"
                    },
                    Button =
                    {
                        Color = "0.25 0.45 0.85 0.95",
                        Command = $"tcstack.select {sizes[i]}"
                    },
                    Text =
                    {
                        Text = sizes[i].ToString(),
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1",
                        FontSize = 16
                    }
                };

                container.Add(btn, bgName);
            }

            var closeBtn = new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.90 0.02",
                    AnchorMax = "0.98 0.10"
                },
                Button =
                {
                    Color = "0.75 0.25 0.25 0.90",
                    Command = "tcstack.close"
                },
                Text =
                {
                    Text = "X",
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1",
                    FontSize = 14
                }
            };

            container.Add(closeBtn, bgName);

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("tcstack.close")]
        private void ClosePanelCommand(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null)
            {
                return;
            }
            HideSelectionPanel(player);
        }

        private void HideSelectionPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiRootPanel);
        }

        private void CleanupUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiRootButton);
            CuiHelper.DestroyUi(player, UiRootPanel);
        }

        private string RepackCupboard(BuildingPrivlidge priv, int targetStackSize)
        {
            if (priv == null || priv.inventory == null)
            {
                return "Шкаф недоступен.";
            }

            var container = priv.inventory;
            var positionToDrop = priv.transform.position + Vector3.up * 1.0f;

            var totalsByItemId = new Dictionary<int, long>();
            var skinsByItemId = new Dictionary<int, ulong>();

            var items = container.itemList.ToList();
            foreach (var item in items)
            {
                if (item == null || item.info == null)
                {
                    continue;
                }

                var id = item.info.itemid;
                if (!totalsByItemId.ContainsKey(id))
                {
                    totalsByItemId[id] = 0;
                    skinsByItemId[id] = item.skin;
                }
                totalsByItemId[id] += item.amount;

                item.RemoveFromContainer();
                item.Remove();
            }

            var capacity = container.capacity;
            var usedSlots = 0;

            foreach (var pair in totalsByItemId)
            {
                var itemId = pair.Key;
                var totalAmount = pair.Value;
                var skinId = skinsByItemId[itemId];
                var itemDef = ItemManager.FindItemDefinition(itemId);
                if (itemDef == null)
                {
                    continue;
                }

                var stackLimit = targetStackSize;
                if (itemDef.stackable > 0)
                {
                    stackLimit = Math.Max(1, Math.Min(targetStackSize, itemDef.stackable));
                }

                while (totalAmount > 0)
                {
                    if (usedSlots >= capacity)
                    {
                        var dropAmount = (int)Mathf.Clamp(totalAmount, 1, int.MaxValue);
                        var dropItem = ItemManager.CreateByItemID(itemId, dropAmount, skinId);
                        dropItem?.Drop(positionToDrop, Vector3.zero);
                        totalAmount = 0;
                        break;
                    }

                    var createAmount = (int)Mathf.Clamp(totalAmount >= stackLimit ? stackLimit : totalAmount, 1, int.MaxValue);
                    var newItem = ItemManager.CreateByItemID(itemId, createAmount, skinId);
                    if (newItem == null)
                    {
                        return "Не удалось создать предмет.";
                    }

                    if (!newItem.MoveToContainer(container))
                    {
                        newItem.Drop(positionToDrop, Vector3.zero);
                    }
                    else
                    {
                        usedSlots++;
                    }

                    totalAmount -= createAmount;
                }
            }

            return null;
        }
    }
}

