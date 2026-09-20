using BotaniaStory.blockentity;
using BotaniaStory.items.lenses;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.blocks
{
    public abstract class BlockManaSpreaderBase : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!(world.BlockAccessor.GetBlockEntity(blockSel.Position) is IManaLensHost host))
            {
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            ItemSlot slot = byPlayer?.InventoryManager?.ActiveHotbarSlot;
            bool holdsLens = slot?.Itemstack?.Item is ItemManaLens;
            bool emptyHand = slot == null || slot.Empty;

            if (!holdsLens && !(emptyHand && host.LensStack != null))
            {
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            if (world.Side == EnumAppSide.Server)
            {
                host.InteractLens(byPlayer);
            }

            return true;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (world.Side == EnumAppSide.Server &&
                world.BlockAccessor.GetBlockEntity(pos) is IManaLensHost host)
            {
                ItemStack lens = host.TakeLens();

                if (lens != null && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
                {
                    world.SpawnItemEntity(lens, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5));
                }
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }
    }
}
