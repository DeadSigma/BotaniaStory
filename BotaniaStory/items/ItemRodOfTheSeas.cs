using System;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using BotaniaStory.blockentity;
using BotaniaStory.util;

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

        // Обработка жидкостных слотов

        internal static bool HandleInventorySlotClick(ItemSlot targetSlot, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            if (targetSlot == null || !(sourceSlot?.Itemstack?.Collectible is ItemRodOfTheSeas)) return true;
            if (op == null || (op.MouseButton != EnumMouseButton.Left && op.MouseButton != EnumMouseButton.Right)) return true;

            // Вход бочки перенаправляется в жидкостный слот
            if (targetSlot is ItemSlotBarrelInput) targetSlot = targetSlot.Inventory?[1] as ItemSlotLiquidOnly;

            float capacityLitres;
            if (targetSlot is ItemSlotLiquidOnly liquidOnly) capacityLitres = liquidOnly.CapacityLitres;
            else if (targetSlot is ItemSlotWatertight watertight) capacityLitres = watertight.capacityLitres;
            else return true;

            // Бурная вода в таре блокируется
            if (sourceSlot.Itemstack.Attributes.GetInt("toolMode", ModeWater) != ModeWater) return false;

            IWorldAccessor world = op.World;
            Item waterPortion = world?.GetItem(new AssetLocation("game", "waterportion"));
            if (waterPortion == null) return false;

            ItemStack waterStack = new ItemStack(waterPortion, 1);
            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(waterStack);
            int itemsPerLitre = Math.Max(1, (int)Math.Round(props?.ItemsPerLitre ?? 100f));

            if (!targetSlot.Empty && !targetSlot.Itemstack.Equals(world, waterStack, GlobalConstants.IgnoredStackAttributes)) return false;

            EntityAgent byEntity = op.ActingPlayer?.Entity as EntityAgent;
            if (byEntity == null || !ManaHelper.HasMana(byEntity, ManaCost)) return false;

            int moved;
            if (op.MouseButton == EnumMouseButton.Left)
            {
                int freeAmount = (int)(capacityLitres * itemsPerLitre) - targetSlot.StackSize;
                if (freeAmount <= 0) return false;

                moved = op.CtrlDown ? Math.Min(itemsPerLitre, freeAmount) : freeAmount;

                if (targetSlot.Empty) targetSlot.Itemstack = new ItemStack(waterPortion, moved);
                else targetSlot.Itemstack.StackSize += moved;
            }
            else
            {
                if (targetSlot.Empty) return false;

                moved = op.CtrlDown ? Math.Min(itemsPerLitre, targetSlot.StackSize) : targetSlot.StackSize;
                targetSlot.TakeOut(moved);
            }

            targetSlot.MarkDirty();
            op.MovedQuantity = moved;
            ManaHelper.TryConsumeMana(byEntity, ManaCost);

            var pos = byEntity.Pos;
            world.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), pos.X, pos.InternalY, pos.Z, op.ActingPlayer);
            return false;
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

            // при нехватке маны действие прерывается
            // манаброня: скидка учитывается внутри HasMana
            if (!ManaHelper.HasMana(byEntity, manaCost)) return;

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
                    ConsumeMana(byEntity, manaCost);
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

                    ConsumeMana(byEntity, manaCost);
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

                                    ConsumeMana(byEntity, manaCost);
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

                            ConsumeMana(byEntity, manaCost);
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
                ConsumeMana(byEntity, manaCost);
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

        // мана списывается из всех планшетов игрока по очереди
        // манаброня: скидка учитывается внутри TryConsumeMana
        private void ConsumeMana(EntityAgent byEntity, int amount)
        {
            ManaHelper.TryConsumeMana(byEntity, amount);
        }
    }

    public class RodOfTheSeasInventoryPatchSystem : ModSystem
    {
        private const string HarmonyId = "botaniastory.rodoftheseas.inventory";
        private static readonly object PatchLock = new object();
        private static Harmony harmony;
        private static int users;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            lock (PatchLock)
            {
                users++;
                if (harmony != null) return;

                harmony = new Harmony(HarmonyId);
                HarmonyMethod prefix = new HarmonyMethod(typeof(RodOfTheSeasInventoryPatchSystem), nameof(ActivateSlotPrefix));
                Type[] signature = { typeof(ItemSlot), typeof(ItemStackMoveOperation).MakeByRefType() };

                var baseMethod = AccessTools.DeclaredMethod(typeof(ItemSlot), nameof(ItemSlot.ActivateSlot), signature);
                var liquidOnlyMethod = AccessTools.DeclaredMethod(typeof(ItemSlotLiquidOnly), nameof(ItemSlot.ActivateSlot), signature);

                if (baseMethod != null)
                {
                    harmony.Patch(baseMethod, prefix: prefix);
                }
                else
                {
                    api.Logger.Error("[BotaniaStory] RodOfTheSeas: ItemSlot.ActivateSlot не найден");
                }

                if (liquidOnlyMethod != null)
                {
                    harmony.Patch(liquidOnlyMethod, prefix: prefix);
                }
                else
                {
                    api.Logger.Error("[BotaniaStory] RodOfTheSeas: ItemSlotLiquidOnly.ActivateSlot не найден");
                }
            }
        }

        public override void Dispose()
        {
            lock (PatchLock)
            {
                users = Math.Max(0, users - 1);
                if (users == 0 && harmony != null)
                {
                    harmony.UnpatchAll(HarmonyId);
                    harmony = null;
                }
            }

            base.Dispose();
        }

        public static bool ActivateSlotPrefix(ItemSlot __instance, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            return ItemRodOfTheSeas.HandleInventorySlotClick(__instance, sourceSlot, ref op);
        }
    }
}
