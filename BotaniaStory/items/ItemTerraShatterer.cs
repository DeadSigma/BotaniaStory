using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.items
{
    public class ItemTerraShatterer : Item
    {
        // 1. ЧТЕНИЕ НАСТРОЕК ИЗ JSON
        public int GetMaxMana(ItemStack stack)
        {
            return stack.Item.Attributes?["manaCapacity"]?.AsInt(1000000) ?? 1000000;
        }

        public int GetCurrentMana(ItemStack stack)
        {
            return stack.Attributes.GetInt("currentMana", 0);
        }

        // 2. ВЗАИМОДЕЙСТВИЕ (ПКМ) - ВКЛЮЧЕНИЕ И ВЫКЛЮЧЕНИЕ
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            // 1. Реагируем ТОЛЬКО на первый момент клика, игнорируем удержание кнопки
            if (!firstEvent) return;

            // 2. Говорим движку: "Мы сами обработали этот клик, дефолтные действия не нужны"
            handling = EnumHandHandling.Handled;

            // 3. Читаем текущее состояние предмета (защита от null)
            string currentState = slot.Itemstack.Item.Variant["state"];
            if (string.IsNullOrEmpty(currentState)) currentState = "off";

            // 4. Определяем, на что будем менять
            string newState = (currentState == "on") ? "off" : "on";
            string currentRank = slot.Itemstack.Item.Variant["rank"] ?? "0"; // Получаем ранг

            // 5. Железобетонно собираем код вручную, чтобы движок не отрезал слова
            string newPath = $"pickaxe-terrashatterer-{currentRank}-{newState}";
            AssetLocation newCode = new AssetLocation(slot.Itemstack.Item.Code.Domain, newPath);

            // 6. Основная логика замены должна быть СТРОГО на сервере
            if (byEntity.World.Side == EnumAppSide.Server)
            {
                Item newItem = byEntity.World.GetItem(newCode);

                if (newItem != null)
                {
                    // Создаем новый предмет
                    ItemStack newStack = new ItemStack(newItem);

                    // Копируем ману и атрибуты
                    if (slot.Itemstack.Attributes != null)
                    {
                        newStack.Attributes = slot.Itemstack.Attributes.Clone() as ITreeAttribute;
                    }

                    // Заменяем предмет в слоте и обновляем инвентарь
                    slot.Itemstack = newStack;
                    slot.MarkDirty();

                    // Воспроизводим звук ТОЛЬКО если кирка включается
                    if (newState == "on")
                    {
                        byEntity.World.PlaySoundAt(new AssetLocation("botaniastory:sounds/terrashatterer_on"), byEntity, null, true, 16f, 1f);
                    }
                }
                else
                {
                    // Если по какой-то причине движок не нашел on/off вариант, он напишет это в консоль сервера
                    byEntity.World.Logger.Error($"[BotaniaStory] ОШИБКА: Не удалось найти предмет с кодом {newCode}");
                }
            }

            // 7. Визуальная отдача: проигрываем анимацию взаимодействия на клиенте
            if (byEntity.World.Side == EnumAppSide.Client)
            {
                (byEntity as EntityPlayer)?.Player?.Entity.AnimManager.StartAnimation("interact");
            }
        }


        // 3. ЛОГИКА БАССЕЙНА И ЭВОЛЮЦИИ
        public void ReceiveMana(ItemSlot slot, int amount, IWorldAccessor world)
        {
            ItemStack stack = slot.Itemstack;
            int currentMana = GetCurrentMana(stack);
            int maxMana = GetMaxMana(stack);

            currentMana += amount;

            if (currentMana >= maxMana)
            {
                bool evolved = UpgradeRank(slot, currentMana, world);

                if (!evolved)
                {
                    stack.Attributes.SetInt("currentMana", maxMana);
                    slot.MarkDirty();
                }
            }
            else
            {
                stack.Attributes.SetInt("currentMana", currentMana);
                slot.MarkDirty();
            }
        }

        private bool UpgradeRank(ItemSlot slot, int currentMana, IWorldAccessor world)
        {
            ItemStack stack = slot.Itemstack;

            string currentRankStr = stack.Item.Variant["rank"];
            if (!int.TryParse(currentRankStr, out int currentRank)) return false;

            int nextRank = currentRank + 1;
            if (nextRank > 5) return false;

            // Надежный метод смены ранга: движок сам заменит нужный кусок кода
            string currentState = stack.Item.Variant["state"] ?? "off"; // Сохраняем текущее состояние включения
            string newPath = $"pickaxe-terrashatterer-{nextRank}-{currentState}";
            AssetLocation newCode = new AssetLocation(stack.Collectible.Code.Domain, newPath);
            Item nextItem = world.GetItem(newCode);

            if (nextItem != null)
            {
                ItemStack nextStack = new ItemStack(nextItem);

                if (stack.Attributes != null)
                {
                    nextStack.Attributes = stack.Attributes.Clone() as ITreeAttribute;
                }

                nextStack.Attributes.SetInt("currentMana", currentMana);
                nextStack.Attributes.SetInt("toolLevel", nextRank);

                slot.Itemstack = nextStack;
                slot.MarkDirty();

                return true;
            }

            return false;
        }

        // 4. ОТОБРАЖЕНИЕ В ИНТЕРФЕЙСЕ
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool boolVal)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, boolVal);

            int currentMana = GetCurrentMana(inSlot.Itemstack);
            int maxMana = GetMaxMana(inSlot.Itemstack);
            string rank = inSlot.Itemstack.Item.Variant["rank"];
            string state = inSlot.Itemstack.Item.Variant["state"] ?? "off";

            float displayMana = currentMana / 1000f;
            float displayMax = maxMana / 1000f;

            dsc.AppendLine("\n" + Lang.Get("botaniastory:info-terrashatterer-rank", rank));

            // Добавляем строчку с текущим статусом 
            string stateLang = state == "on" ? "Active" : "Inactive";

            dsc.AppendLine(Lang.Get("botaniastory:info-mana-display", displayMana.ToString("0.##"), displayMax.ToString("0.##")));
        }

        // 5. ЛОГИКА РАЗРУШЕНИЯ БЛОКОВ (AoE)
        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);

            // ВСЕГДА ЛОМАЕМ ЦЕЛЕВОЙ БЛОК ПЕРВЫМ
            bool targetBroken = base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
            if (!targetBroken) return false;

            // Проверяем, включен ли Землекрушитель
            string state = itemslot.Itemstack.Item.Variant["state"] ?? "off";
            if (state == "off") return true; // Если выключен - массовой копки нет

            string rankStr = itemslot.Itemstack.Item.Variant["rank"];
            if (!int.TryParse(rankStr, out int rank)) rank = 0;

            int manaCostPerBlock = 100;

            if (rank == 0) return true;

            // --- ЛОГИКА МАССОВОГО РАЗРУШЕНИЯ ---
            int xzRadius = 0, yUp = 0, yDown = 1;
            switch (rank)
            {
                case 1: xzRadius = 0; yUp = 1; yDown = 1; break;
                case 2: xzRadius = 1; yUp = 1; yDown = 1; break;
                case 3: xzRadius = 2; yUp = 3; yDown = 1; break;
                case 4: xzRadius = 3; yUp = 5; yDown = 1; break;
                case 5: xzRadius = 4; yUp = 7; yDown = 1; break;
            }

            int xMin = 0, xMax = 0, yMin = 0, yMax = 0, zMin = 0, zMax = 0;
            if (blockSel.Face == BlockFacing.UP || blockSel.Face == BlockFacing.DOWN)
            {
                xMin = -xzRadius; xMax = xzRadius;
                zMin = -xzRadius; zMax = xzRadius;
            }
            else if (blockSel.Face == BlockFacing.NORTH || blockSel.Face == BlockFacing.SOUTH)
            {
                xMin = -xzRadius; xMax = xzRadius;
                yMin = -yDown; yMax = yUp;
            }
            else if (blockSel.Face == BlockFacing.EAST || blockSel.Face == BlockFacing.WEST)
            {
                zMin = -xzRadius; zMax = xzRadius;
                yMin = -yDown; yMax = yUp;
            }

            BlockPos targetPos = blockSel.Position;
            bool anyExtraBlockBroken = false;

            for (int x = xMin; x <= xMax; x++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    for (int z = zMin; z <= zMax; z++)
                    {
                        if (x == 0 && y == 0 && z == 0) continue;

                        BlockPos currentPos = targetPos.AddCopy(x, y, z);
                        Block block = world.BlockAccessor.GetBlock(currentPos);

                        if (block.Id == 0 || block.RequiredMiningTier > this.ToolTier) continue;
                        if (world.BlockAccessor.GetBlockEntity(currentPos) != null) continue;

                        if (ConsumeMana(itemslot.Itemstack, player, manaCostPerBlock))
                        {
                            world.BlockAccessor.BreakBlock(currentPos, player);
                            anyExtraBlockBroken = true;
                        }
                        else
                        {
                            itemslot.MarkDirty();
                            return true;
                        }
                    }
                }
            }

            if (anyExtraBlockBroken) itemslot.MarkDirty();

            return true;
        }

        // 6. РАСХОД МАНЫ (Планшет приоритетнее)
        public bool ConsumeMana(ItemStack stack, IPlayer player, int amount)
        {
            if (player != null)
            {
                foreach (var inv in player.InventoryManager.Inventories.Values)
                {
                    if (inv.ClassName != "hotbar" && inv.ClassName != "backpack") continue;

                    foreach (ItemSlot slot in inv)
                    {
                        if (slot.Empty) continue;

                        if (slot.Itemstack.Item is ItemManaTablet tablet)
                        {
                            int tabletMana = tablet.GetMana(slot.Itemstack);

                            if (tabletMana >= amount)
                            {
                                tablet.SetMana(slot.Itemstack, tabletMana - amount);
                                slot.MarkDirty();
                                return true;
                            }
                        }
                    }
                }
            }

            int current = GetCurrentMana(stack);
            if (current >= amount)
            {
                stack.Attributes.SetInt("currentMana", current - amount);
                return true;
            }

            return false;
        }

        public bool ConsumeMana(ItemStack stack, int amount)
        {
            int current = GetCurrentMana(stack);
            if (current >= amount)
            {
                stack.Attributes.SetInt("currentMana", current - amount);
                return true;
            }
            return false;
        }
    }
}