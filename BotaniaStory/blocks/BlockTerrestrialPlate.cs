using BotaniaStory.blockentity;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.blocks
{
    public class BlockTerrestrialPlate : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityTerrestrialPlate plate)
            {
                return plate.OnInteract(byPlayer);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}