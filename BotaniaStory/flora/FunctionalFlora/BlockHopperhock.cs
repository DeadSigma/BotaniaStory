using BotaniaStory.blockentity;
using BotaniaStory.Blocks;
using Vintagestory.API.Common;

namespace BotaniaStory.blocks
{
    public class BlockHopperhock : BlockBotaniaFlower
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            BEBehaviorHopperhock behavior = be?.GetBehavior<BEBehaviorHopperhock>();

            if (behavior == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            ItemSlot activeHandSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            // Попытка положить листок в вороток (ПКМ с листком в руке)
            if (!activeHandSlot.Empty && activeHandSlot.Itemstack.Collectible.Code.Path.Contains("filterscroll"))
            {
                bool isBlacklist = activeHandSlot.Itemstack.Collectible.Code.Path.Contains("black");
                int targetSlot = isBlacklist ? 1 : 0; // 0 для белого, 1 для черного

                if (behavior.FilterInventory[targetSlot].Empty)
                {
                    behavior.FilterInventory[targetSlot].Itemstack = activeHandSlot.TakeOut(1);
                    behavior.FilterInventory[targetSlot].MarkDirty();
                    activeHandSlot.MarkDirty();
                    behavior.Blockentity.MarkDirty(true);
                    return true;
                }
            }

            // Попытка забрать листки из цветка пустой рукой (просто ПКМ)
            if (activeHandSlot.Empty)
            {
                // Сначала пытаемся отдать черный, если нет - белый
                for (int i = 1; i >= 0; i--)
                {
                    if (!behavior.FilterInventory[i].Empty)
                    {
                        ItemStack leafToReturn = behavior.FilterInventory[i].TakeOut(1);
                        if (!byPlayer.InventoryManager.TryGiveItemstack(leafToReturn))
                        {
                            world.SpawnItemEntity(leafToReturn, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                        }
                        behavior.FilterInventory[i].MarkDirty();
                        behavior.Blockentity.MarkDirty(true);
                        return true;
                    }
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}