using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BlockDaybloom : BlockPlant
    {
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool placed = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (placed)
            {
                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityGeneratingFlower flower)
                {
                    // === НОВОЕ: Записываем Владельца для лимита Дневноцветов ===
                    if (flower is BlockEntityDaybloom daybloom && byPlayer != null && world.Side == EnumAppSide.Server)
                    {
                        daybloom.OwnerUID = byPlayer.PlayerUID;
                        // Увеличиваем счетчик цветов игрока
                        BlockEntityDaybloom.PlayerBloomsCount[daybloom.OwnerUID] =
                            BlockEntityDaybloom.PlayerBloomsCount.GetValueOrDefault(daybloom.OwnerUID, 0) + 1;
                    }

                    // Твоя логика: цветок сразу ищет распространитель
                    flower.FindSpreader();
                    flower.MarkDirty(true);
                }
            }
            return placed;
        }
    }
}