using Vintagestory.API.Common;

namespace BotaniaStory
{
    public class BlockElvenGatewayCore : Block
    {
        // Исправлено: IWorldAccessor вместо IWorldClientAPI
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityElvenGatewayCore core)
            {
                core.OnInteract(byPlayer);
                return true;
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}