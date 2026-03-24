using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory
{
    public class BlockApothecary : Block
    {

        // ==========================================
        // БАЗА ДАННЫХ РЕЦЕПТОВ ЦВЕТОВ
        // Формат: "название_блока_цветка" -> { "лепесток1" : количество, "лепесток2" : количество }
        // ==========================================
        private readonly Dictionary<string, Dictionary<string, int>> flowerRecipes = new Dictionary<string, Dictionary<string, int>>
        {
            { "puredaisy", new Dictionary<string, int> { { "mysticalpetal-white", 4 } } },
            { "daybloom", new Dictionary<string, int> { { "mysticalpetal-yellow", 2 }, { "mysticalpetal-orange", 1 }, { "mysticalpetal-lightblue", 1 } } },
            { "agricarnation", new Dictionary<string, int> { { "mysticalpetal-lime", 2 }, { "mysticalpetal-green", 1 }, { "mysticalpetal-yellow", 1 } } }
            // Добавляй новые цветы сюда по аналогии!
        };
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            BlockEntityApothecary be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityApothecary;
            if (be == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            // ==========================================
            // 1. ЗАБРАТЬ ПРЕДМЕТ ( ПКМ пустой рукой)
            // ==========================================
            if (slot.Empty)
            {
                for (int i = be.inventory.Count - 1; i >= 0; i--)
                {
                    if (!be.inventory[i].Empty)
                    {
                        ItemStack stackToTake = be.inventory[i].TakeOut(1);
                        if (!byPlayer.InventoryManager.TryGiveItemstack(stackToTake, true))
                        {
                            world.SpawnItemEntity(stackToTake, blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5));
                        }

                        be.inventory[i].MarkDirty();
                        be.UpdateRenderer();

                        // ИГРАЕМ ТВОЙ КАСТОМНЫЙ ЗВУК
                        world.PlaySoundAt(new AssetLocation("botaniastory:sounds/apothecary_splash"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                        return true;
                    }
                }
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            // ==========================================
            // 2. ВОДА (Налить/зачерпнуть)
            // ==========================================
            if (!slot.Empty && slot.Itemstack.Collectible is BlockLiquidContainerBase liquidContainer)
            {
                ItemStack liquidInside = liquidContainer.GetContent(slot.Itemstack);

                // НАЛИТЬ ВОДУ В ПУСТОЙ АПТЕКАРЬ
                if (!be.HasWater && liquidInside != null && liquidInside.Collectible.Code.Path == "waterportion")
                {
                    if (liquidInside.StackSize >= 1000)
                    {
                        be.HasWater = true;
                        be.MarkDirty(true);
                        world.PlaySoundAt(new AssetLocation("game:sounds/environment/water-splash"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                        liquidInside.StackSize -= 1000;
                        if (liquidInside.StackSize <= 0) liquidContainer.SetContent(slot.Itemstack, null);
                        else liquidContainer.SetContent(slot.Itemstack, liquidInside);
                        slot.MarkDirty();
                        return true;
                    }
                }

                // ЗАБРАТЬ ВОДУ ИЗ ПОЛНОГО АПТЕКАРЯ
                if (be.HasWater && (liquidInside == null || (liquidInside.Collectible.Code.Path == "waterportion" && liquidInside.StackSize + 1000 <= liquidContainer.CapacityLitres * 100)))
                {
                    if (liquidInside == null) liquidInside = new ItemStack(world.GetItem(new AssetLocation("game:waterportion")), 1000);
                    else liquidInside.StackSize += 1000;
                    liquidContainer.SetContent(slot.Itemstack, liquidInside);

                    be.HasWater = false;

                    // ИСПРАВЛЕНИЕ: ВЫБРАСЫВАЕМ ВСЕ ПРЕДМЕТЫ, ЕСЛИ ЗАБРАЛИ ВОДУ
                    for (int i = 0; i < be.inventory.Count; i++)
                    {
                        if (!be.inventory[i].Empty)
                        {
                            // Выкидываем предметы прямо над алтарем
                            world.SpawnItemEntity(be.inventory[i].TakeOut(be.inventory[i].StackSize), blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5));
                            be.inventory[i].MarkDirty();
                        }
                    }

                    be.UpdateRenderer(); // Очищаем рендер лепестков
                    be.MarkDirty(true);
                    world.PlaySoundAt(new AssetLocation("game:sounds/environment/water-splash"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    slot.MarkDirty();
                    return true;
                }
            }

            // ЕСЛИ НЕТ ВОДЫ — ПРЕДМЕТЫ КЛАСТЬ НЕЛЬЗЯ
            if (!be.HasWater) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            // ==========================================
            // 3. УМНЫЙ КРАФТ ЦВЕТОВ
            // ==========================================
            if (!slot.Empty && slot.Itemstack.Collectible.Code.Path.StartsWith("treeseed"))
            {
                Dictionary<string, int> currentPetals = new Dictionary<string, int>();
                int nonPetalCount = 0;

                foreach (var invSlot in be.inventory)
                {
                    if (invSlot.Empty) continue;
                    string code = invSlot.Itemstack.Collectible.Code.Path;

                    if (code.StartsWith("mysticalpetal-"))
                    {
                        if (currentPetals.ContainsKey(code)) currentPetals[code] += invSlot.StackSize;
                        else currentPetals[code] = invSlot.StackSize;
                    }
                    else nonPetalCount++;
                }

                if (nonPetalCount == 0 && currentPetals.Count > 0)
                {
                    string craftedFlower = null;

                    foreach (var recipe in flowerRecipes)
                    {
                        bool match = true;

                        // Быстрая проверка: совпадает ли количество видов?
                        if (recipe.Value.Count != currentPetals.Count) continue;

                        // Детальная проверка количеств
                        foreach (var req in recipe.Value)
                        {
                            if (!currentPetals.ContainsKey(req.Key) || currentPetals[req.Key] != req.Value)
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match)
                        {
                            craftedFlower = recipe.Key;
                            break;
                        }
                    }

                    if (craftedFlower != null)
                    {
                        slot.TakeOut(1);
                        be.inventory.Clear();
                        be.HasWater = false;
                        be.UpdateRenderer();

                        Block flowerBlock = world.GetBlock(new AssetLocation("botaniastory", craftedFlower));
                        if (flowerBlock != null) world.SpawnItemEntity(new ItemStack(flowerBlock), blockSel.Position.ToVec3d().Add(0.5, 1.2, 0.5));

                        world.PlaySoundAt(new AssetLocation("botaniastory:sounds/apothecary_craft"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                        return true;
                    }
                }
            }

            // ==========================================
            // 4. ПОЛОЖИТЬ ПРЕДМЕТ (УМНЫЙ WHITELIST)
            // ==========================================
            if (!slot.Empty)
            {
                string[] allowedKeywords = new string[]
                {
                    "petal", "flower", "mushroom", "berry", "fruit", "vine", "fern", "seed", "root"
                };

                bool isAllowed = false;
                string itemPath = slot.Itemstack.Collectible.Code.Path;

                foreach (string keyword in allowedKeywords)
                {
                    if (itemPath.Contains(keyword))
                    {
                        isAllowed = true;
                        break;
                    }
                }

                if (isAllowed)
                {
                    for (int i = 0; i < be.inventory.Count; i++)
                    {
                        if (be.inventory[i].Empty)
                        {
                            be.inventory[i].Itemstack = slot.TakeOut(1);
                            slot.MarkDirty();
                            be.inventory[i].MarkDirty();
                            be.UpdateRenderer();

                            // ИГРАЕМ ТВОЙ КАСТОМНЫЙ ЗВУК ПЛЮХА
                            world.PlaySoundAt(new AssetLocation("botaniastory:sounds/apothecary_splash"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                            return true;
                        }
                    }
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        
    }
}