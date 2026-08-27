using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using BotaniaStory.blockentity;

namespace BotaniaStory.items
{
    public class ItemRodOfTheSeas : Item
    {
        private const int ManaCost = 5000;
        private const int RapidManaCost = 100000;

        private const int ModeWater = 0;
        private const int ModeRapidWater = 1;

        private SkillItem[] toolModes;

        private Block waterSourceBlock;
        private Block rapidWaterSourceBlock;
        private bool rapidLookupFailed;

        // Режимы инструмента

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            toolModes = new SkillItem[]
            {
                new SkillItem()
                {
                    Code = new AssetLocation("water"),
                    Name = Lang.Get("botaniastory:rodmode-water")
                }.WithIcon(capi, LoadModeIcon(capi, "botaniastory:textures/icons/rodmode_water.svg", "game:textures/icons/heatmap.svg")),

                new SkillItem()
                {
                    Code = new AssetLocation("rapidwater"),
                    Name = Lang.Get("botaniastory:rodmode-rapidwater")
                }.WithIcon(capi, LoadModeIcon(capi, "botaniastory:textures/icons/rodmode_rapidwater.svg", "game:textures/icons/rocks.svg"))
            };
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (toolModes != null)
            {
                for (int i = 0; i < toolModes.Length; i++) toolModes[i]?.Dispose();
                toolModes = null;
            }
            base.OnUnloaded(api);
        }

        private LoadedTexture LoadModeIcon(ICoreClientAPI capi, string modPath, string fallbackPath)
        {
            AssetLocation loc = new AssetLocation(modPath);
            if (capi.Assets.TryGet(loc) == null) loc = new AssetLocation(fallbackPath);
            return capi.Gui.LoadSvgWithPadding(loc, 48, 48, 5, ColorUtil.WhiteArgb);
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            return toolModes;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            return slot?.Itemstack?.Attributes?.GetInt("toolMode", ModeWater) ?? ModeWater;
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
            slot.MarkDirty();
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            int mode = inSlot.Itemstack.Attributes.GetInt("toolMode", ModeWater);
            string modeName = Lang.Get(mode == ModeRapidWater ? "botaniastory:rodmode-rapidwater" : "botaniastory:rodmode-water");
            dsc.AppendLine(Lang.Get("botaniastory:rodmode-current", modeName));
        }

        // Основное взаимодействие

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || !firstEvent) return;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player != null && !byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return;

            IWorldAccessor world = byEntity.World;
            int mode = GetToolMode(slot, player, blockSel);
            int manaCost = mode == ModeRapidWater ? RapidManaCost : ManaCost;

            // Ищем планшет с нужным количеством маны. Если такого нет - прерываем действие
            ItemSlot tabletSlot = GetValidManaTablet(player, manaCost);
            if (tabletSlot == null) return;

            // РЕЖИМ 1: БУРНАЯ ВОДА (только установка блока, в тару её налить нельзя)
            if (mode == ModeRapidWater)
            {
                Block rapid = GetRapidWaterSourceBlock(world);
                if (rapid == null)
                {
                    (player as IServerPlayer)?.SendIngameError("norapidwater", Lang.Get("botaniastory:rodmode-norapidwater"));
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }

                if (TryPlaceLiquid(world, blockSel, rapid, out BlockPos rapidPos))
                {
                    ConsumeMana(tabletSlot, manaCost);
                    world.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), rapidPos.X, rapidPos.Y, rapidPos.Z, player);
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }

                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            // Режим 0: Обычная вода

            BlockPos pos = blockSel.Position;
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);

            Item waterPortion = world.GetItem(new AssetLocation("game", "waterportion"));
            if (waterPortion == null) return;

            // 1. Лепестковый аптекарь
            if (be is BlockEntityApothecary apothecary)
            {
                if (!apothecary.HasWater)
                {
                    if (world.Side == EnumAppSide.Server)
                    {
                        apothecary.HasWater = true;
                        apothecary.UpdateRenderer();
                        apothecary.MarkDirty(true);
                    }

                    ConsumeMana(tabletSlot, manaCost);
                    world.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), pos.X, pos.Y, pos.Z, player);
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            // 2. Бочки, Ведра, Перегонные кубы
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
                            if (invSlot.Empty || (!invSlot.Empty && invSlot.Itemstack.Equals(world, new ItemStack(waterPortion), GlobalConstants.IgnoredStackAttributes)))
                            {
                                int maxCapacity = 1000;
                                if (invSlot is ItemSlotLiquidOnly liqSlot) maxCapacity = (int)(liqSlot.CapacityLitres * 100);

                                int currentAmount = invSlot.Empty ? 0 : invSlot.Itemstack.StackSize;

                                if (currentAmount < maxCapacity)
                                {
                                    if (world.Side == EnumAppSide.Server)
                                    {
                                        invSlot.Itemstack = new ItemStack(waterPortion, Math.Min(maxCapacity, currentAmount + 1000));
                                        invSlot.MarkDirty();
                                        beContainer.MarkDirty(true);
                                    }

                                    ConsumeMana(tabletSlot, manaCost);
                                    world.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), pos.X, pos.Y, pos.Z, player);
                                    handling = EnumHandHandling.PreventDefault;
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            // 3. Костёр (Умное поочередное заполнение)
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
                    InventoryGeneric dummyInv = new InventoryGeneric(1, "dummywater-1", world.Api, null);
                    ItemSlot dummySlot = dummyInv[0];

                    for (int i = 1; i < firepit.Inventory.Count; i++)
                    {
                        var ingSlot = firepit.Inventory[i];

                        // Пропускаем слоты с чужими предметами (котелок, дрова, морковка)
                        if (!ingSlot.Empty && !ingSlot.Itemstack.Equals(world, new ItemStack(waterPortion), GlobalConstants.IgnoredStackAttributes))
                            continue;

                        // Если этот слот уже доверху забит нашей водой (600 порций) - пропуск
                        if (!ingSlot.Empty && ingSlot.Itemstack.StackSize >= 600)
                            continue;

                        dummySlot.Itemstack = new ItemStack(waterPortion, 1);
                        int moved = dummySlot.TryPutInto(world, ingSlot, 1);

                        if (moved > 0 || (!ingSlot.Empty && ingSlot.Itemstack.Equals(world, new ItemStack(waterPortion), GlobalConstants.IgnoredStackAttributes)))
                        {
                            if (world.Side == EnumAppSide.Server)
                            {
                                ingSlot.Itemstack.StackSize = 600;
                                ingSlot.MarkDirty();
                                firepit.MarkDirty(true);
                            }

                            ConsumeMana(tabletSlot, manaCost);
                            world.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), pos.X, pos.Y, pos.Z, player);
                            handling = EnumHandHandling.PreventDefault;
                            return;
                        }
                    }

                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            // 4. Разлив воды на землю
            Block water = GetWaterSourceBlock(world);
            if (water != null && TryPlaceLiquid(world, blockSel, water, out BlockPos waterPos))
            {
                ConsumeMana(tabletSlot, manaCost);
                world.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), waterPos.X, waterPos.Y, waterPos.Z, player);
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        // Установка жидкости

        // Ставит жидкость строго в fluid-слой, иначе она не течёт и водяное колесо её не увидит
        private bool TryPlaceLiquid(IWorldAccessor world, BlockSelection blockSel, Block liquidBlock, out BlockPos placedPos)
        {
            IBlockAccessor ba = world.BlockAccessor;
            placedPos = blockSel.Position.AddCopy(blockSel.Face);

            Block solidAt = ba.GetBlock(placedPos, BlockLayersAccess.Solid);
            if (solidAt == null || solidAt.Replaceable < 6000) return false;

            Block fluidAt = ba.GetBlock(placedPos, BlockLayersAccess.Fluid);
            if (fluidAt != null && fluidAt.BlockId == liquidBlock.BlockId) return false; // уже стоит то же самое - не тратим ману

            if (world.Side == EnumAppSide.Server)
            {
                if (solidAt.BlockId != 0) ba.SetBlock(0, placedPos, BlockLayersAccess.Solid); // сносим траву/цветы
                ba.SetBlock(liquidBlock.BlockId, placedPos, BlockLayersAccess.Fluid);
                ba.TriggerNeighbourBlockUpdate(placedPos);
                liquidBlock.OnNeighbourBlockChange(world, placedPos, placedPos); // запускаем растекание самого источника
                ba.MarkBlockDirty(placedPos);
            }

            return true;
        }

        private Block GetWaterSourceBlock(IWorldAccessor world)
        {
            if (waterSourceBlock == null) waterSourceBlock = ResolveLiquidSource(world, "water");
            return waterSourceBlock;
        }

        private Block GetRapidWaterSourceBlock(IWorldAccessor world)
        {
            if (rapidWaterSourceBlock == null && !rapidLookupFailed)
            {
                rapidWaterSourceBlock = ResolveLiquidSource(world, "rapidwater");
                if (rapidWaterSourceBlock == null)
                {
                    rapidLookupFailed = true;
                    world.Logger.Warning("[BotaniaStory] ItemRodOfTheSeas: не найден блок-источник rapidwater. Режим бурной воды отключён.");
                }
            }
            return rapidWaterSourceBlock;
        }

        private Block ResolveLiquidSource(IWorldAccessor world, string firstCodePart)
        {
            string[] candidates = new string[]
            {
                firstCodePart + "-still-7",
                firstCodePart + "-7",
                firstCodePart
            };

            foreach (string code in candidates)
            {
                Block b = world.GetBlock(new AssetLocation("game", code));
                if (b != null) return b;
            }

            // Ничего не подошло - сканируем реестр блоков
            Block fallback = null;
            foreach (Block b in world.Blocks)
            {
                if (b?.Code == null || b.Code.Domain != "game") continue;

                string path = b.Code.Path;
                if (!path.StartsWith(firstCodePart + "-", StringComparison.Ordinal)) continue;
                if (path.Contains("flowing")) continue;

                if (path.EndsWith("-7", StringComparison.Ordinal)) return b;
                if (fallback == null) fallback = b;
            }

            return fallback;
        }

        // Вспомогательные методы для работы с маной

        // Ищет в инвентаре игрока первый попавшийся планшет маны, в котором есть необходимое количество маны
        private ItemSlot GetValidManaTablet(IPlayer player, int requiredMana)
        {
            if (player == null) return null;

            foreach (var inv in player.InventoryManager.OpenedInventories)
            {
                foreach (var slot in inv)
                {
                    if (slot.Empty) continue;

                    if (slot.Itemstack.Item is ItemManaTablet tablet)
                    {
                        if (tablet.GetMana(slot.Itemstack) >= requiredMana)
                        {
                            return slot;
                        }
                    }
                }
            }
            return null;
        }

        // Списывает ману из найденного слота с планшетом
        private void ConsumeMana(ItemSlot tabletSlot, int amount)
        {
            if (tabletSlot?.Itemstack?.Item is ItemManaTablet tablet)
            {
                int currentMana = tablet.GetMana(tabletSlot.Itemstack);
                tablet.SetMana(tabletSlot.Itemstack, currentMana - amount);
                tabletSlot.MarkDirty();
            }
        }
    }
}