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

                    // Проверяем все поведения, прикрепленные к этому острову
                    foreach (var behavior in be.Behaviors)
                    {
                        // Авто-привязка ГЕНЕРИРУЮЩИХ цветов к Распространителю
                        if (behavior is BEBehaviorGeneratingFlower genFlower)
                        {
                            if (genFlower.LinkedSpreader == null)
                            {
                                genFlower.FindSpreader();
                                isDirty = true;
                            }

                            // Специфичная логика для Дневноцвета
                            if (behavior is BEBehaviorDaybloom daybloom && byPlayer != null && world.Side == EnumAppSide.Server)
                            {
                                daybloom.OwnerUID = byPlayer.PlayerUID;
                                BEBehaviorDaybloom.PlayerBloomsCount[daybloom.OwnerUID] =
                                    BEBehaviorDaybloom.PlayerBloomsCount.GetValueOrDefault(daybloom.OwnerUID, 0) + 1;
                                isDirty = true;
                            }
                        }

                        // Авто-привязка Амаранта к Бассейну маны
                        if (behavior is BEBehaviorJadedAmaranthus jaded)
                        {
                            if (jaded.LinkedPool == null)
                            {
                                jaded.AutoFindPool();
                                isDirty = true;
                            }
                        }

                        // Авто-привязка Воротка к Бассейну маны
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