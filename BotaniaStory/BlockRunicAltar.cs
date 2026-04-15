using Vintagestory.API.Common;

namespace BotaniaStory
{
    public class BlockRunicAltar : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!(world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityRunicAltar be))
                return base.OnBlockInteractStart(world, byPlayer, blockSel);

            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!activeSlot.Empty && activeSlot.Itemstack.Collectible.Code.Path == "wandoftheforest")
            {
                be.TryCompleteCrafting(byPlayer);
                return true;
            }

            if (activeSlot.Empty && byPlayer.Entity.Controls.Sneak)
            {
                if (be.TryTakeItem(byPlayer)) return true;
            }

            if (!activeSlot.Empty)
            {
                if (be.TryAddItem(activeSlot, byPlayer)) return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}