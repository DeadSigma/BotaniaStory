using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory
{
    public class BlockApothecary : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            BlockEntityApothecary be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityApothecary;
            if (be == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            // ==========================================
            // 1. ЗАБРАТЬ ПРЕДМЕТ (Shift + ПКМ пустой рукой)
            // ==========================================
            if (slot.Empty && byPlayer.Entity.Controls.Sneak)
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
            // 3. СНАЧАЛА ПРОВЕРЯЕМ КРАФТ
            // ==========================================
            if (!slot.Empty && slot.Itemstack.Collectible.Code.Path.StartsWith("treeseed"))
            {
                int whitePetals = 0;
                int others = 0;

                foreach (var invSlot in be.inventory)
                {
                    if (invSlot.Empty) continue;
                    if (invSlot.Itemstack.Collectible.Code.Path == "mysticalpetal-white") whitePetals += invSlot.StackSize;
                    else others++;
                }

                if (whitePetals == 4 && others == 0) // Строгий рецепт
                {
                    slot.TakeOut(1);
                    be.inventory.Clear();
                    be.HasWater = false;
                    be.UpdateRenderer();

                    Block daisy = world.GetBlock(new AssetLocation("botaniastory:puredaisy"));
                    if (daisy != null) world.SpawnItemEntity(new ItemStack(daisy), blockSel.Position.ToVec3d().Add(0.5, 1.2, 0.5));

                    // ИГРАЕМ ТВОЙ КАСТОМНЫЙ ЗВУК МАГИИ
                    world.PlaySoundAt(new AssetLocation("botaniastory:sounds/apothecary_craft"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    return true;
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