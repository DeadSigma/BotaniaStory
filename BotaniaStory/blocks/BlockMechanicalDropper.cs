using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace BotaniaStory.blocks
{
    public class BlockMechanicalDropper : Block, IMechanicalPowerBlock
    {

        public bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase connector)
        {
            BlockFacing dropFacing = BlockFacing.DOWN;
            if (Variant.ContainsKey("facing"))
            {
                dropFacing = BlockFacing.FromCode(Variant["facing"]);
            }

            // Разрешаем подключать вал к любой стороне, кроме отверстия выброса
            return face != dropFacing;
        }

        public MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be != null)
            {
                BEBehaviorMPConsumer behavior = be.GetBehavior<BEBehaviorMPConsumer>();
                return behavior?.Network;
            }
            return null;
        }

        public void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }
        public void DidDisconnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }

        // ПРАВИЛЬНАЯ УСТАНОВКА БЛОКА

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            // Берем ту сторону, по которой кликнул игрок
            BlockFacing facing = blockSel.Face;

            // Если зажат Shift, разворачиваем (чтобы ставить "внутрь/от себя")
            if (byPlayer.Entity.Controls.Sneak)
            {
                facing = facing.Opposite;
            }

            AssetLocation newCode = CodeWithVariant("facing", facing.Code);
            Block blockToPlace = world.GetBlock(newCode);

            if (blockToPlace == null) blockToPlace = this;

            if (blockToPlace.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                blockToPlace.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }
            return false;
        }

        // ИНТЕРФЕЙС СУНДУКА

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Проверяем на BlockEntityGenericContainer
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityGenericContainer be)
            {
                return be.OnPlayerRightClick(byPlayer, blockSel);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}