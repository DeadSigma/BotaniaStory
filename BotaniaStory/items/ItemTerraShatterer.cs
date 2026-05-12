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

        // Вместо const теперь берем объем маны напрямую из itemtypes.json!
        public int GetMaxMana(ItemStack stack)
        {
            // Движок сам подставит нужную цифру для текущего ранга (0, 1, 2 и т.д.)
            return stack.Item.Attributes?["manaCapacity"]?.AsInt(1000000) ?? 1000000;
        }

        public int GetCurrentMana(ItemStack stack)
        {
            return stack.Attributes.GetInt("currentMana", 0);
        }

        // 2. ЛОГИКА БАССЕЙНА И ЭВОЛЮЦИИ

        // Бассейн маны должен вызывать этот метод, когда передает ману в кирку
        public void ReceiveMana(ItemSlot slot, int amount, IWorldAccessor world)
        {
            ItemStack stack = slot.Itemstack;
            int currentMana = GetCurrentMana(stack);
            int maxMana = GetMaxMana(stack);

            currentMana += amount;

            if (currentMana >= maxMana)
            {
                bool evolved = UpgradeRank(slot, currentMana, world);

                // ФОЛБЭК: Если эволюционировать не вышло (уперлись в 5-й ранг или предмет не найден),
                // ОБЯЗАНЫ сохранить ману, уперев её в лимит. Иначе кирка будет есть её бесконечно.
                if (!evolved)
                {
                    stack.Attributes.SetInt("currentMana", maxMana);
                    slot.MarkDirty();
                }
            }
            else
            {
                // Иначе просто сохраняем новую ману
                stack.Attributes.SetInt("currentMana", currentMana);
                slot.MarkDirty();
            }
        }

        private bool UpgradeRank(ItemSlot slot, int currentMana, IWorldAccessor world)
        {
            ItemStack stack = slot.Itemstack;

            // Читаем текущий ранг
            string currentRankStr = stack.Item.Variant["rank"];
            if (!int.TryParse(currentRankStr, out int currentRank)) return false;

            int nextRank = currentRank + 1;

            // Если это уже максимальный ранг (5)
            if (nextRank > 5) return false;

            // Надежный метод смены ранга: отрезаем старую цифру и приклеиваем новую
            string path = stack.Collectible.Code.Path;
            string newPath = path.Substring(0, path.LastIndexOf('-') + 1) + nextRank;
            AssetLocation newCode = new AssetLocation(stack.Collectible.Code.Domain, newPath);

            Item nextItem = world.GetItem(newCode);

            if (nextItem != null)
            {
                ItemStack nextStack = new ItemStack(nextItem);

                // Копируем все старые данные
                if (stack.Attributes != null)
                {
                    nextStack.Attributes = stack.Attributes.Clone() as ITreeAttribute;
                }

                nextStack.Attributes.SetInt("currentMana", currentMana);
                nextStack.Attributes.SetInt("toolLevel", nextRank);

                // Заменяем старую кирку
                slot.Itemstack = nextStack;
                slot.MarkDirty();

                return true; // Эволюция успешна!
            }

            return false;
        }

        // 3. ОТОБРАЖЕНИЕ В ИНТЕРФЕЙСЕ

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool boolVal)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, boolVal);

            int currentMana = GetCurrentMana(inSlot.Itemstack);
            int maxMana = GetMaxMana(inSlot.Itemstack);
            string rank = inSlot.Itemstack.Item.Variant["rank"];

            float displayMana = currentMana / 1000f;
            float displayMax = maxMana / 1000f;

            dsc.AppendLine("\n" + Lang.Get("botaniastory:info-terrashatterer-rank", rank));
            dsc.AppendLine(Lang.Get("botaniastory:info-mana-display", displayMana.ToString("0.##"), displayMax.ToString("0.##")));
        }

        // 4. ЛОГИКА РАЗРУШЕНИЯ БЛОКОВ (AoE)
        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);

            // 1. ВСЕГДА ЛОМАЕМ ЦЕЛЕВОЙ БЛОК ПЕРВЫМ
            bool targetBroken = base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
            if (!targetBroken) return false;

            // Читаем текущий ранг
            string rankStr = itemslot.Itemstack.Item.Variant["rank"];
            if (!int.TryParse(rankStr, out int rank)) rank = 0;

            int manaCostPerBlock = 100;

            // Проверяем только ранг. Проверку на currentMana отсюда убрали, 
            // так как теперь мана может лежать в планшете игрока, а не только в самой кирке!
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

                        //  Теперь передаем объект player в метод проверки маны
                        if (ConsumeMana(itemslot.Itemstack, player, manaCostPerBlock))
                        {
                            world.BlockAccessor.BreakBlock(currentPos, player);
                            anyExtraBlockBroken = true;
                        }
                        else
                        {
                            // Если мана кончилась (и в планшетах, и в кирке)
                            itemslot.MarkDirty();
                            return true;
                        }
                    }
                }
            }

            if (anyExtraBlockBroken) itemslot.MarkDirty();

            return true;
        }

        // 5. УМНЫЙ РАСХОД МАНЫ (Планшет приоритетнее)
        public bool ConsumeMana(ItemStack stack, IPlayer player, int amount)
        {
            // 1. Ищем планшет маны в инвентаре игрока
            if (player != null)
            {
                foreach (var inv in player.InventoryManager.Inventories.Values)
                {
                    // Сканируем только хотбар и рюкзаки (чтобы не тянуть ману из сундуков)
                    if (inv.ClassName != "hotbar" && inv.ClassName != "backpack") continue;

                    foreach (ItemSlot slot in inv)
                    {
                        if (slot.Empty) continue;

                        // Если нашли Планшет Маны
                        if (slot.Itemstack.Item is ItemManaTablet tablet)
                        {
                            int tabletMana = tablet.GetMana(slot.Itemstack);

                            // Если в этом планшете хватает маны
                            if (tabletMana >= amount)
                            {
                                tablet.SetMana(slot.Itemstack, tabletMana - amount);
                                slot.MarkDirty(); // Обязательно сохраняем изменения в планшете!
                                return true;      // Мана успешно списана
                            }
                        }
                    }
                }
            }

            // 2. Если планшета нет или он пуст — пытаемся взять ману из самой кирки
            int current = GetCurrentMana(stack);
            if (current >= amount)
            {
                stack.Attributes.SetInt("currentMana", current - amount);
                return true;
            }

            // Маны нигде нет
            return false;
        }

        // Затраты маны
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