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
                // Ищем ЛЮБОЙ генерирующий цветок (работает и для Daybloom, и для Endoflame)
                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityGeneratingFlower flower)
                {
                    flower.FindSpreader();
                    flower.MarkDirty(true);
                }
            }
            return placed;
        }
    }
}