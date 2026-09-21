using BotaniaStory.blockentity;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory.blocks
{
    public class BlockApothecary : Block
    {


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            BlockEntityApothecary be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityApothecary;
            if (be == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            // Пустой рукой забирается последний предмет или повторяется последний крафт
            if (slot.Empty)
            {
                bool itemTaken = false;

                for (int i = be.inventory.Count - 1; i >= 0; i--)
                {
                    if (!be.inventory[i].Empty)
                    {
                        ItemStack stackToTake = be.inventory[i].TakeOut(1);

                        // Предмет выдаётся только на сервере
                        if (world.Side == EnumAppSide.Server)
                        {
                            if (!byPlayer.InventoryManager.TryGiveItemstack(stackToTake, true))
                            {
                                world.SpawnItemEntity(stackToTake, blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5));
                            }
                        }

                        be.inventory[i].MarkDirty();
                        be.MarkDirty(true);
                        be.UpdateRenderer();

                        PlayApothecarySound(world, blockSel.Position, "apothecary_splash");

                        itemTaken = true;
                        break;
                    }
                }

                if (itemTaken)
                {
                    return true;
                }

                // Последний рецепт повторяется в течение 20 секунд


                if (be.HasWater && be.LastCraftedFlower != null)
                {
                    long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (currentTime - be.LastCraftTime <= 20000)
                    {
                        if (BlockEntityApothecary.flowerRecipes.TryGetValue(be.LastCraftedFlower, out var recipe))
                        {
                            // Состав рецепта проверяется без расхода предметов
                            if (CheckAndConsumePlayerItems(byPlayer, recipe, true))
                            {
                                // Ингредиенты списываются после успешной проверки
                                CheckAndConsumePlayerItems(byPlayer, recipe, false);

                                be.HasWater = false;
                                be.UpdateRenderer();

                                Block flowerBlock = world.GetBlock(new AssetLocation("botaniastory", be.LastCraftedFlower));
                                if (flowerBlock != null)
                                {
                                    world.SpawnItemEntity(new ItemStack(flowerBlock), blockSel.Position.ToVec3d().Add(0.5, 1.2, 0.5));
                                }

                                PlayApothecarySound(world, blockSel.Position, "apothecary_craft");

                                // Время последнего крафта обновляется для повторного создания
                                be.LastCraftTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                return true;
                            }
                        }
                    }
                }

                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            // Вода переносится порциями по 10 литров
            if (!slot.Empty && slot.Itemstack.Collectible is BlockLiquidContainerBase liquidContainer)
            {
                ItemStack liquidInside = liquidContainer.GetContent(slot.Itemstack);

                if (!be.HasWater && liquidInside != null && IsApothecaryWater(liquidInside))
                {
                    if (liquidContainer.GetCurrentLitres(slot.Itemstack) >= 10f)
                    {
                        if (world.Side == EnumAppSide.Server)
                        {
                            ItemStack singleContainer = slot.TakeOut(1);

                            liquidContainer.TryTakeLiquid(singleContainer, 10f);

                            if (!byPlayer.InventoryManager.TryGiveItemstack(singleContainer, true))
                            {
                                world.SpawnItemEntity(
                                    singleContainer,
                                    blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
                                );
                            }

                            slot.MarkDirty();

                            be.HasWater = true;
                            be.MarkDirty(true);
                        }

                        world.PlaySoundAt(
                            new AssetLocation("game:sounds/environment/smallsplash"),
                            blockSel.Position.X,
                            blockSel.Position.Y,
                            blockSel.Position.Z,
                            byPlayer
                        );

                        return true;
                    }
                }

                if (be.HasWater)
                {
                    bool canFill =
                        liquidInside == null ||
                        (
                            liquidInside.Collectible.Code.Domain == "game" &&
                            liquidInside.Collectible.Code.Path == "waterportion" &&
                            liquidContainer.GetCurrentLitres(slot.Itemstack) + 10f <= liquidContainer.CapacityLitres
                        );

                    if (liquidInside == null)
                    {
                        canFill = liquidContainer.CapacityLitres >= 10f;
                    }

                    if (canFill)
                    {
                        if (world.Side == EnumAppSide.Server)
                        {
                            ItemStack singleContainer = slot.TakeOut(1);
                            Item waterItem = world.GetItem(new AssetLocation("game:waterportion"));

                            if (singleContainer != null && waterItem != null)
                            {
                                ItemStack waterStack = new ItemStack(waterItem, 999999);

                                int moved = liquidContainer.TryPutLiquid(singleContainer, waterStack, 10f);

                                if (moved > 0)
                                {
                                    if (!byPlayer.InventoryManager.TryGiveItemstack(singleContainer, true))
                                    {
                                        world.SpawnItemEntity(
                                            singleContainer,
                                            blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
                                        );
                                    }

                                    be.HasWater = false;

                                    for (int i = 0; i < be.inventory.Count; i++)
                                    {
                                        if (!be.inventory[i].Empty)
                                        {
                                            world.SpawnItemEntity(
                                                be.inventory[i].TakeOut(be.inventory[i].StackSize),
                                                blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
                                            );

                                            be.inventory[i].MarkDirty();
                                        }
                                    }

                                    be.UpdateRenderer();
                                    be.MarkDirty(true);
                                    slot.MarkDirty();

                                }
                                else
                                {
                                    byPlayer.InventoryManager.TryGiveItemstack(singleContainer, true);
                                    slot.MarkDirty();
                                }
                            }
                        }

                        world.PlaySoundAt(
                            new AssetLocation("game:sounds/environment/smallsplash"),
                            blockSel.Position.X,
                            blockSel.Position.Y,
                            blockSel.Position.Z,
                            byPlayer
                        );

                        return true;
                    }
                }
            }

            // Без воды ингредиенты не принимаются
            if (!be.HasWater) return base.OnBlockInteractStart(world, byPlayer, blockSel);


            if (!slot.Empty)
            {
                if (be.TryAddItem(slot, byPlayer))
                {
                    PlayApothecarySound(world, blockSel.Position, "apothecary_splash");
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        private static bool IsApothecaryWater(ItemStack stack)
        {
            if (stack?.Collectible?.Code == null) return false;

            AssetLocation code = stack.Collectible.Code;

            return
                (code.Domain == "game" && code.Path == "waterportion") ||
                (code.Domain == "hydrateordiedrate" && code.Path == "waterportion-fresh-well-clean");
        }
        private bool CheckAndConsumePlayerItems(IPlayer player, Dictionary<string, int> recipe, bool simulate)
        {
            Dictionary<string, int> remainingItems = new Dictionary<string, int>(recipe);
            int needSeed = 1;

            Dictionary<string, int> foundItems = new Dictionary<string, int>();
            int foundSeeds = 0;

            // Ингредиенты ищутся во всех открытых инвентарях игрока
            foreach (var inv in player.InventoryManager.OpenedInventories)
            {
                foreach (var slot in inv)
                {
                    if (slot.Empty) continue;
                    string path = slot.Itemstack.Collectible.Code.Path;

                    if (needSeed > 0 && (path.StartsWith("treeseed") || path.StartsWith("seeds-"))) foundSeeds += slot.StackSize;

                    if (remainingItems.ContainsKey(path))
                    {
                        if (foundItems.ContainsKey(path)) foundItems[path] += slot.StackSize;
                        else foundItems[path] = slot.StackSize;
                    }
                }
            }

            if (foundSeeds < needSeed) return false;

            foreach (var req in remainingItems)
            {
                if (!foundItems.ContainsKey(req.Key) || foundItems[req.Key] < req.Value) return false;
            }

            // При проверке предметы не расходуются
            if (simulate) return true;

            int seedsToTake = needSeed;
            Dictionary<string, int> itemsToTake = new Dictionary<string, int>(recipe);

            foreach (var inv in player.InventoryManager.OpenedInventories)
            {
                foreach (var slot in inv)
                {
                    if (slot.Empty) continue;
                    string path = slot.Itemstack.Collectible.Code.Path;

                    if (seedsToTake > 0 && (path.StartsWith("treeseed") || path.StartsWith("seeds-")))
                    {
                        int take = System.Math.Min(seedsToTake, slot.StackSize);
                        slot.TakeOut(take);
                        seedsToTake -= take;
                        slot.MarkDirty();
                    }

                    if (itemsToTake.ContainsKey(path) && itemsToTake[path] > 0)
                    {
                        int take = System.Math.Min(itemsToTake[path], slot.StackSize);
                        slot.TakeOut(take);
                        itemsToTake[path] -= take;
                        slot.MarkDirty();
                    }
                }
            }
            return true;
        }
        // Звук рассылается игрокам через сетевой канал
        private void PlayApothecarySound(IWorldAccessor world, BlockPos pos, string soundName)
        {
            if (world.Side == EnumAppSide.Server)
            {
                Vintagestory.API.Server.ICoreServerAPI sapi = world.Api as Vintagestory.API.Server.ICoreServerAPI;
                if (sapi != null)
                {
                    var channel = sapi.Network.GetChannel("botanianetwork");
                    channel.BroadcastPacket(new PlayManaSoundPacket()
                    {
                        Position = new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5),
                        SoundName = soundName
                    });
                }
            }
        }
    }
}