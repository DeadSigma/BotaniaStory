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

            // ЗАБРАТЬ ПРЕДМЕТ ИЛИ АВТОКРАФТ (ПКМ пустой рукой)
            if (slot.Empty)
            {
                bool itemTaken = false;

                // Попытка забрать предметы (начиная с последнего добавленного)
                for (int i = be.inventory.Count - 1; i >= 0; i--)
                {
                    if (!be.inventory[i].Empty)
                    {
                        // Забираем ровно 1 штучку из слота
                        ItemStack stackToTake = be.inventory[i].TakeOut(1);

                        // Выдаем предмет игроку ТОЛЬКО на сервере, чтобы избежать фантомов
                        if (world.Side == EnumAppSide.Server)
                        {
                            if (!byPlayer.InventoryManager.TryGiveItemstack(stackToTake, true))
                            {
                                world.SpawnItemEntity(stackToTake, blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5));
                            }
                        }

                        be.inventory[i].MarkDirty();
                        be.MarkDirty(true); // Сообщаем серверу, что инвентарь блока нужно сохранить
                        be.UpdateRenderer();

                        PlayApothecarySound(world, blockSel.Position, "apothecary_splash");

                        itemTaken = true;
                        break;
                    }
                }

                // Если предмет успешно забран, прерываем выполнение.
                // Возвращаем true, говоря игре "Мы успешно обработали клик, всё идет по плану!"
                if (itemTaken)
                {
                    return true;
                }

                // ЛОГИКА АВТОКРАФТА


                if (be.HasWater && be.LastCraftedFlower != null)
                {
                    long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    // Проверяем, прошло ли меньше 20000 миллисекунд (20 секунд)
                    if (currentTime - be.LastCraftTime <= 20000)
                    {
                        if (BlockEntityApothecary.flowerRecipes.TryGetValue(be.LastCraftedFlower, out var recipe))
                        {
                            // Проверяем, есть ли всё нужное в карманах игрока (simulate: true)
                            if (CheckAndConsumePlayerItems(byPlayer, recipe, true))
                            {
                                // Забираем лепестки и семечко (simulate: false)
                                CheckAndConsumePlayerItems(byPlayer, recipe, false);

                                // Завершаем крафт
                                be.HasWater = false;
                                be.UpdateRenderer();

                                Block flowerBlock = world.GetBlock(new AssetLocation("botaniastory", be.LastCraftedFlower));
                                if (flowerBlock != null)
                                {
                                    world.SpawnItemEntity(new ItemStack(flowerBlock), blockSel.Position.ToVec3d().Add(0.5, 1.2, 0.5));
                                }

                                PlayApothecarySound(world, blockSel.Position, "apothecary_craft");

                                // Обновляем таймер, чтобы можно было продолжать спамить ПКМ!
                                be.LastCraftTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                return true;
                            }
                        }
                    }
                }

                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            // 
            // ВОДА (Налить/зачерпнуть)
            // 
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
                        world.PlaySoundAt(new AssetLocation("game:sounds/environment/smallsplash"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
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

                    // ВЫБРАСЫВАЕМ ВСЕ ПРЕДМЕТЫ, ЕСЛИ ЗАБРАЛИ ВОДУ
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
                    world.PlaySoundAt(new AssetLocation("sounds/environment/smallsplash"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    slot.MarkDirty();
                    return true;
                }
            }

            // ЕСЛИ НЕТ ВОДЫ - ПРЕДМЕТЫ КЛАСТЬ НЕЛЬЗЯ
            if (!be.HasWater) return base.OnBlockInteractStart(world, byPlayer, blockSel);


            // ПОЛОЖИТЬ ПРЕДМЕТ (тот же белый список, что и у брошенных предметов)
            if (!slot.Empty)
            {
                if (be.TryAddItem(slot, byPlayer))
                {
                    // ИГРАЕМ КАСТОМНЫЙ ЗВУК ПЛЮХА
                    PlayApothecarySound(world, blockSel.Position, "apothecary_splash");
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        // МЕТОД ДЛЯ АВТОКРАФТА (ПОИСК И ИЗЪЯТИЕ ПРЕДМЕТОВ)
        private bool CheckAndConsumePlayerItems(IPlayer player, Dictionary<string, int> recipe, bool simulate)
        {
            Dictionary<string, int> remainingItems = new Dictionary<string, int>(recipe);
            int needSeed = 1;

            Dictionary<string, int> foundItems = new Dictionary<string, int>();
            int foundSeeds = 0;

            // Считаем, есть ли всё необходимое в инвентарях игрока (хотбар + рюкзаки)
            foreach (var inv in player.InventoryManager.OpenedInventories)
            {
                foreach (var slot in inv)
                {
                    if (slot.Empty) continue;
                    string path = slot.Itemstack.Collectible.Code.Path;

                    // Ищем семена
                    if (needSeed > 0 && path.StartsWith("treeseed")) foundSeeds += slot.StackSize;

                    // Ищем нужные лепестки
                    if (remainingItems.ContainsKey(path))
                    {
                        if (foundItems.ContainsKey(path)) foundItems[path] += slot.StackSize;
                        else foundItems[path] = slot.StackSize;
                    }
                }
            }

            // Хватает ли семечка?
            if (foundSeeds < needSeed) return false;

            // Хватает ли всех лепестков?
            foreach (var req in remainingItems)
            {
                if (!foundItems.ContainsKey(req.Key) || foundItems[req.Key] < req.Value) return false;
            }

            // Если мы просто проверяли (simulate), то возвращаем успех, ничего не трогая
            if (simulate) return true;

            // Если всё есть, реально забираем предметы
            int seedsToTake = needSeed;
            Dictionary<string, int> itemsToTake = new Dictionary<string, int>(recipe);

            foreach (var inv in player.InventoryManager.OpenedInventories)
            {
                foreach (var slot in inv)
                {
                    if (slot.Empty) continue;
                    string path = slot.Itemstack.Collectible.Code.Path;

                    if (seedsToTake > 0 && path.StartsWith("treeseed"))
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
        // ОТПРАВКА СЕТЕВОГО ПАКЕТА ЗВУКА ИЗ БЛОКА
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