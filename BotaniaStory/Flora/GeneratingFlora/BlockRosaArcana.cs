using Vintagestory.API.Common;
using Vintagestory.GameContent;
using BotaniaStory.Blocks;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BlockRosaArcana : BlockBotaniaFlower
    {
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool placed = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (placed)
            {
                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityGeneratingFlower flower)
                {
                    // Ищем распространитель маны при установке
                    flower.FindSpreader();
                    flower.MarkDirty(true);
                }
            }
            return placed;
        }
    }
}