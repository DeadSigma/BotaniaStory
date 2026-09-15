using BotaniaStory.blockentity;
using BotaniaStory.Blocks;
using Vintagestory.API.Common;

namespace BotaniaStory.blocks
{
    public class BlockWardbloom : BlockBotaniaFlower
    {
        public override bool DoPlaceBlock(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel,
            ItemStack byItemStack)
        {
            bool placed = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
            if (!placed) return false;

            if (world.Side == EnumAppSide.Server)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                BEBehaviorWardbloom wardbloom = be?.GetBehavior<BEBehaviorWardbloom>();

                if (wardbloom != null && wardbloom.LinkedPool == null)
                {
                    wardbloom.AutoFindPool();
                    be.MarkDirty(true);
                }
            }

            return true;
        }

        public override bool OnBlockInteractStart(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            BEBehaviorWardbloom behavior = be?.GetBehavior<BEBehaviorWardbloom>();

            if (behavior?.HandleInteract(byPlayer) == true)
                return true;

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
