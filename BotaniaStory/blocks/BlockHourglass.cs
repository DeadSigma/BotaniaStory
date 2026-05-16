using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace BotaniaStory.blocks
{
    public class BlockHourglass : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is blockentity.BlockEntityHourglass be)
            {
                ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

                // Игрок кликает ПУСТОЙ рукой - забираем песок
                if (hotbarSlot.Empty)
                {
                    bool taken = be.TryTakeSand(byPlayer);
                    if (taken)
                    {
                        world.PlaySoundAt(new AssetLocation("sounds/block/sand"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    }
                    return true;
                }

                // Игрок кликает рукой с предметом - пытаемся добавить песок
                if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Code.Path.Contains("sand"))
                {
                    bool added = be.TryAddSand(hotbarSlot);
                    if (added)
                    {
                        world.PlaySoundAt(new AssetLocation("sounds/block/sand"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    }
                    return true;
                }
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}