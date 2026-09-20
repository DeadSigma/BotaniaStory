using BotaniaStory.blockentity;
using Vintagestory.API.Common;

namespace BotaniaStory.blocks
{
    public class BlockRunicAltar : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!(world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityRunicAltar be))
                return base.OnBlockInteractStart(world, byPlayer, blockSel);

            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            // Посохом завершается готовый крафт
            if (!activeSlot.Empty && activeSlot.Itemstack.Collectible.Code.Path.Contains("wandoftheforest"))
            {
                if (be.TryCompleteCrafting(byPlayer)) return true;
            }

            if (activeSlot.Empty)
            {
                if (byPlayer.Entity.Controls.Sneak)
                {
                    if (be.TryTakeItem(byPlayer)) return true;
                }
                else
                {
                    if (be.TryAutoCraft(byPlayer))
                    {
                        world.PlaySoundAt(new AssetLocation("game:sounds/player/throw"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                        return true;
                    }
                }
            }

            if (!activeSlot.Empty)
            {
                if (be.TryAddItem(activeSlot, byPlayer)) return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}