using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BlockEndoflame : BlockPlant
    {
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool placed = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (placed)
            {
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