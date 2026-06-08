using Vintagestory.API.Common;
using BotaniaStory.blockentity;
using BotaniaStory.Flora.GeneratingFlora;
using System.Collections.Generic;

namespace BotaniaStory.Blocks
{
    public class BlockBotaniaFloatingIsland : Block
    {
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool placed = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (placed)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);

                if (be != null)
                {
                    bool isDirty = false;

                    foreach (var behavior in be.Behaviors)
                    {
                        if (behavior is BEBehaviorGeneratingFlower genFlower)
                        {
                            if (genFlower.LinkedSpreader == null)
                            {
                                genFlower.FindSpreader();
                                isDirty = true;
                            }

                            if (behavior is BEBehaviorDaybloom daybloom && byPlayer != null && world.Side == EnumAppSide.Server)
                            {
                                daybloom.OwnerUID = byPlayer.PlayerUID;
                                BEBehaviorDaybloom.PlayerBloomsCount[daybloom.OwnerUID] =
                                    BEBehaviorDaybloom.PlayerBloomsCount.GetValueOrDefault(daybloom.OwnerUID, 0) + 1;
                                isDirty = true;
                            }
                        }

                        if (behavior is BEBehaviorJadedAmaranthus jaded)
                        {
                            if (jaded.LinkedPool == null)
                            {
                                jaded.AutoFindPool();
                                isDirty = true;
                            }
                        }

                        if (behavior is BEBehaviorHopperhock hopperhock)
                        {
                            if (hopperhock.LinkedPool == null)
                            {
                                hopperhock.AutoFindPool();
                                isDirty = true;
                            }
                        }
                    }

                    if (isDirty)
                    {
                        be.MarkDirty(true);
                    }
                }
            }
            return placed;
        }

        // ==============================================================
        // ДОБАВЛЕНО ВЗАИМОДЕЙСТВИЕ КАК У ОБЫЧНЫХ ЦВЕТОВ (ДЛЯ ФИЛЬТРОВ)
        // ==============================================================
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);

            // Пробуем получить поведение воротка, если этот остров им является
            BEBehaviorHopperhock hopperhock = be?.GetBehavior<BEBehaviorHopperhock>();

            if (hopperhock != null)
            {
                ItemSlot activeHandSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

                // 1. Попытка положить листок в вороток (ПКМ с листком в руке)
                if (!activeHandSlot.Empty && activeHandSlot.Itemstack.Collectible.Code.Path.Contains("filterscroll"))
                {
                    bool isBlacklist = activeHandSlot.Itemstack.Collectible.Code.Path.Contains("black");
                    int targetSlot = isBlacklist ? 1 : 0; // 0 для белого, 1 для черного

                    if (hopperhock.FilterInventory[targetSlot].Empty)
                    {
                        hopperhock.FilterInventory[targetSlot].Itemstack = activeHandSlot.TakeOut(1);
                        hopperhock.FilterInventory[targetSlot].MarkDirty();
                        activeHandSlot.MarkDirty();
                        hopperhock.Blockentity.MarkDirty(true);
                        return true;
                    }
                }

                // 2. Попытка забрать листки из цветка пустой рукой (просто ПКМ)
                if (activeHandSlot.Empty)
                {
                    for (int i = 1; i >= 0; i--)
                    {
                        if (!hopperhock.FilterInventory[i].Empty)
                        {
                            ItemStack leafToReturn = hopperhock.FilterInventory[i].TakeOut(1);
                            if (!byPlayer.InventoryManager.TryGiveItemstack(leafToReturn))
                            {
                                world.SpawnItemEntity(leafToReturn, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                            }
                            hopperhock.FilterInventory[i].MarkDirty();
                            hopperhock.Blockentity.MarkDirty(true);
                            return true;
                        }
                    }
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}