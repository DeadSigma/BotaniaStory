using Vintagestory.API.Common;
using Vintagestory.API.MathTools; // Не забудь этот using для BlockPos и BlockFacing!
using BotaniaStory.blockentity;
using BotaniaStory.Flora.GeneratingFlora;
using System.Collections.Generic;

namespace BotaniaStory.Blocks
{
    public class BlockBotaniaFlower : Block
    {

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            BlockPos posBelow = blockSel.Position.DownCopy();
            Block blockBelow = world.BlockAccessor.GetBlock(posBelow);

            if (!blockBelow.CanAttachBlockAt(world.BlockAccessor, this, posBelow, BlockFacing.UP))
            {
                failureCode = "requiresteadiersurface";
                return false;
            }

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldgenRandom, BlockPatchAttributes attributes = null)
        {
            BlockPos posBelow = pos.DownCopy();
            Block blockBelow = blockAccessor.GetBlock(posBelow);

            if (!blockBelow.CanAttachBlockAt(blockAccessor, this, posBelow, BlockFacing.UP))
            {
                return false;
            }

            Block currentBlock = blockAccessor.GetBlock(pos);

            if (currentBlock.IsLiquid())
            {
                return false;
            }

            if (currentBlock is BlockBotaniaFlower)
            {
                return false;
            }

            return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldgenRandom, attributes);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neigbourPos)
        {
            BlockPos posBelow = pos.DownCopy();
            Block blockBelow = world.BlockAccessor.GetBlock(posBelow);

            if (!blockBelow.CanAttachBlockAt(world.BlockAccessor, this, posBelow, BlockFacing.UP))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            base.OnNeighbourBlockChange(world, pos, neigbourPos);
        }


        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            // Сначала ставим сам блок
            bool placed = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (placed)
            {
                // Получаем сущность блока (Generic или Парящий Остров)
                BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);

                if (be != null)
                {
                    bool isDirty = false;

                    // Проверяем все поведения, прикрепленные к этому блоку
                    foreach (var behavior in be.Behaviors)
                    {
                        // 1. Авто-привязка ГЕНЕРИРУЮЩИХ цветов к Распространителю
                        if (behavior is BEBehaviorGeneratingFlower genFlower)
                        {
                            if (genFlower.LinkedSpreader == null)
                            {
                                genFlower.FindSpreader();
                                isDirty = true;
                            }

                            // Специфичная логика для Дневноцвета (установка владельца)
                            if (behavior is BEBehaviorDaybloom daybloom && byPlayer != null && world.Side == EnumAppSide.Server)
                            {
                                daybloom.OwnerUID = byPlayer.PlayerUID;
                                BEBehaviorDaybloom.PlayerBloomsCount[daybloom.OwnerUID] =
                                    BEBehaviorDaybloom.PlayerBloomsCount.GetValueOrDefault(daybloom.OwnerUID, 0) + 1;
                                isDirty = true;
                            }
                        }

                        // 2. Авто-привязка Амаранта к Бассейну маны
                        if (behavior is BEBehaviorJadedAmaranthus jaded)
                        {
                            if (jaded.LinkedPool == null)
                            {
                                jaded.AutoFindPool();
                                isDirty = true;
                            }
                        }

                        // 3. Авто-привязка Воротка к Бассейну маны
                        if (behavior is BEBehaviorHopperhock hopperhock)
                        {
                            if (hopperhock.LinkedPool == null)
                            {
                                hopperhock.AutoFindPool();
                                isDirty = true;
                            }
                        }
                    }

                    // Если хоть одно поведение обновило данные, сохраняем блок
                    if (isDirty)
                    {
                        be.MarkDirty(true);
                    }
                }
            }
            return placed;
        }
    }
}