using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using BotaniaStory.blockentity;
using Vintagestory.API.Config;

namespace BotaniaStory.items
{
    public class ItemRodOfTheSeas : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || !firstEvent) return;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player != null && !byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return;

            BlockPos pos = blockSel.Position;
            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(pos);

            Item waterPortion = byEntity.World.GetItem(new AssetLocation("game", "waterportion"));
            if (waterPortion == null) return;

            // Лепестковый аптекарь
            if (be is BlockEntityApothecary apothecary)
            {
                if (!apothecary.HasWater)
                {
                    if (byEntity.World.Side == EnumAppSide.Server)
                    {
                        apothecary.HasWater = true;
                        apothecary.UpdateRenderer();
                        apothecary.MarkDirty(true);
                    }

                    byEntity.World.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), pos.X, pos.Y, pos.Z, player);
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            //  2. (Бочки, Ведра, Перегонные кубы) 
            if (be is BlockEntityContainer beContainer)
            {
                bool isBucket = be.GetType().Name.Contains("Bucket");
                bool isLiquidFriendly = be is BlockEntityLiquidContainer || isBucket ||
                                        be.GetType().Name.Contains("Barrel") ||
                                        be.GetType().Name.Contains("Boiler");

                if (isLiquidFriendly)
                {
                    foreach (var invSlot in beContainer.Inventory)
                    {
                        if (invSlot is ItemSlotLiquidOnly || isBucket)
                        {
                            if (invSlot.Empty || (!invSlot.Empty && invSlot.Itemstack.Equals(byEntity.World, new ItemStack(waterPortion), GlobalConstants.IgnoredStackAttributes)))
                            {
                                int maxCapacity = 1000;
                                if (invSlot is ItemSlotLiquidOnly liqSlot) maxCapacity = (int)(liqSlot.CapacityLitres * 100);

                                int currentAmount = invSlot.Empty ? 0 : invSlot.Itemstack.StackSize;

                                if (currentAmount < maxCapacity)
                                {
                                    if (byEntity.World.Side == EnumAppSide.Server)
                                    {
                                        invSlot.Itemstack = new ItemStack(waterPortion, Math.Min(maxCapacity, currentAmount + 1000));
                                        invSlot.MarkDirty();
                                        beContainer.MarkDirty(true);
                                    }

                                    byEntity.World.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), pos.X, pos.Y, pos.Z, player);
                                    handling = EnumHandHandling.PreventDefault;
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            //  КОСТЕР (Умное поочередное заполнение) 
            if (be is BlockEntityFirepit firepit)
            {
                bool hasPot = false;
                foreach (var slotInFirepit in firepit.Inventory)
                {
                    if (!slotInFirepit.Empty && slotInFirepit.Itemstack.Collectible.Code.Path.Contains("pot"))
                    {
                        hasPot = true;
                        break;
                    }
                }

                if (hasPot)
                {
                    InventoryGeneric dummyInv = new InventoryGeneric(1, "dummywater-1", byEntity.World.Api, null);
                    ItemSlot dummySlot = dummyInv[0];

                    foreach (var ingSlot in firepit.Inventory)
                    {
                        // Пропускаем слоты с чужими предметами (котелок, дрова, морковка)
                        if (!ingSlot.Empty && !ingSlot.Itemstack.Equals(byEntity.World, new ItemStack(waterPortion), GlobalConstants.IgnoredStackAttributes))
                            continue;

                        // Если этот слот уже доверху забит нашей водой (600 порций) — просто пропускаем его и идем к следующему!
                        if (!ingSlot.Empty && ingSlot.Itemstack.StackSize >= 600)
                            continue;

                        dummySlot.Itemstack = new ItemStack(waterPortion, 1);
                        int moved = dummySlot.TryPutInto(byEntity.World, ingSlot, 1);

                        // Если пустой слот для ингредиента принял 1 каплю 
                        // ИЛИ если это слот с нашей водой, но там её меньше 600:
                        if (moved > 0 || (!ingSlot.Empty && ingSlot.Itemstack.Equals(byEntity.World, new ItemStack(waterPortion), GlobalConstants.IgnoredStackAttributes)))
                        {
                            if (byEntity.World.Side == EnumAppSide.Server)
                            {
                                // Заливаем 6 литров в ЭТОТ слот
                                ingSlot.Itemstack.StackSize = 600;
                                ingSlot.MarkDirty();
                                firepit.MarkDirty(true);
                            }

                            byEntity.World.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), pos.X, pos.Y, pos.Z, player);
                            handling = EnumHandHandling.PreventDefault;

                            // Выходим из метода. Следующий клик пропустит этот заполненный слот и заполнит следующий.
                            return;
                        }
                    }

                    // Если все 4 слота уже полные по 600, мы попадем сюда. 
                    // Отменяем стандартное действие, чтобы вода не вылилась на землю возле костра.
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            // РАЗЛИВАЕМ ВОДУ НА ЗЕМЛЮ 
            BlockPos placePos = blockSel.Position.AddCopy(blockSel.Face);
            Block blockAtPlacePos = byEntity.World.BlockAccessor.GetBlock(placePos);

            if (blockAtPlacePos != null && blockAtPlacePos.Replaceable >= 6000)
            {
                Block waterBlock = byEntity.World.GetBlock(new AssetLocation("game", "water-still-7"))
                                ?? byEntity.World.GetBlock(new AssetLocation("game", "water-7"));

                if (waterBlock != null)
                {
                    if (byEntity.World.Side == EnumAppSide.Server)
                    {
                        byEntity.World.BlockAccessor.SetBlock(waterBlock.BlockId, placePos);
                        byEntity.World.BlockAccessor.TriggerNeighbourBlockUpdate(placePos);
                        waterBlock.OnNeighbourBlockChange(byEntity.World, placePos, placePos);
                        byEntity.World.BlockAccessor.MarkBlockDirty(placePos);
                    }

                    byEntity.World.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), placePos.X, placePos.Y, placePos.Z, player);
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}