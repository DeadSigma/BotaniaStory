using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using BotaniaStory.util;
using BotaniaStory.systems;

namespace BotaniaStory.items
{
    public class ItemTerraShatterer : Item, IManaRepairable
    {

        // Мана хранится в атрибутах предмета
        public int GetMaxMana(ItemStack stack)
        {
            return stack.Item.Attributes?["manaCapacity"]?.AsInt(1000000) ?? 1000000;
        }

        public int GetCurrentMana(ItemStack stack)
        {
            return stack.Attributes.GetInt("currentMana", 0);
        }

        public static bool IsTerraShatterer(ItemStack stack)
        {
            if (stack?.Item is ItemTerraShatterer) return true;

            AssetLocation code = stack?.Collectible?.Code;
            return code?.Domain == "botaniastory"
                && code.Path.StartsWith("pickaxe-terrashatterer-", StringComparison.Ordinal);
        }

        public static bool IsActive(ItemStack stack)
        {
            return IsTerraShatterer(stack) && (stack.Item.Variant?["state"] ?? "off") == "on";
        }

        public static int GetRank(ItemStack stack)
        {
            if (!IsTerraShatterer(stack)) return 0;
            return int.TryParse(stack.Item.Variant?["rank"], out int rank) ? rank : 0;
        }

        // Состояние меняется только на сервере
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {

            if (!firstEvent) return;


            handling = EnumHandHandling.Handled;


            string currentState = slot.Itemstack.Item.Variant["state"];
            if (string.IsNullOrEmpty(currentState)) currentState = "off";


            string newState = (currentState == "on") ? "off" : "on";
            string currentRank = slot.Itemstack.Item.Variant["rank"] ?? "0";


            string newPath = $"pickaxe-terrashatterer-{currentRank}-{newState}";
            AssetLocation newCode = new AssetLocation(slot.Itemstack.Item.Code.Domain, newPath);


            if (byEntity.World.Side == EnumAppSide.Server)
            {
                Item newItem = byEntity.World.GetItem(newCode);

                if (newItem != null)
                {

                    ItemStack newStack = new ItemStack(newItem);


                    if (slot.Itemstack.Attributes != null)
                    {
                        newStack.Attributes = slot.Itemstack.Attributes.Clone() as ITreeAttribute;
                    }


                    slot.Itemstack = newStack;
                    slot.MarkDirty();


                    if (newState == "on")
                    {
                        byEntity.World.PlaySoundAt(new AssetLocation("botaniastory:sounds/terrashatterer_on"), byEntity, null, true, 16f, 1f);
                    }
                }
                else
                {

                    byEntity.World.Logger.Error($"[BotaniaStory] ОШИБКА: Не удалось найти предмет с кодом {newCode}");
                }
            }


            if (byEntity.World.Side == EnumAppSide.Client)
            {
                (byEntity as EntityPlayer)?.Player?.Entity.AnimManager.StartAnimation("interact");
            }
        }


        // Ранг повышается после заполнения запаса маны
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


            string currentState = stack.Item.Variant["state"] ?? "off";
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


        // Дополнительные блоки ломаются только во включенном состоянии
        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);


            TerraShattererCaveBarrierSystem.RegisterBrokenBlock(world, blockSel.Position, player);

            bool targetBroken = base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
            if (!targetBroken) return false;


            string state = itemslot.Itemstack.Item.Variant["state"] ?? "off";
            if (state == "off") return true;

            string rankStr = itemslot.Itemstack.Item.Variant["rank"];
            if (!int.TryParse(rankStr, out int rank)) rank = 0;

            int manaCostPerBlock = 100;

            if (rank == 0) return true;


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
                            TerraShattererCaveBarrierSystem.RegisterBrokenBlock(world, currentPos, player);
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


        // Сначала расходуется внешняя мана
        public bool ConsumeMana(ItemStack stack, IPlayer player, int amount)
        {
            if (player != null)
            {

                if (ManaHelper.TryConsumeMana(player.Entity, amount)) return true;


                amount = ManaHelper.GetDiscountedCost(player.Entity, amount);
            }

            return ConsumeMana(stack, amount);
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


        // Прочность заменяется расходом маны
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0)
            {
                base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
            }
        }
    }
}
