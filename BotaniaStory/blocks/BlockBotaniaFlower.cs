using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.Blocks
{
    public class BlockBotaniaFlower : Block
    {
        // Проверяем условия перед установкой блока
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            BlockPos posBelow = blockSel.Position.DownCopy();
            Block blockBelow = world.BlockAccessor.GetBlock(posBelow);

            // Проверяем, есть ли у блока снизу твердая верхняя грань для крепления
            if (!blockBelow.CanAttachBlockAt(world.BlockAccessor, this, posBelow, BlockFacing.UP))
            {
                failureCode = "requiresteadiersurface";
                return false;
            }

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldgenRandom, BlockPatchAttributes attributes = null)
        {
            BlockPos posBelow = pos.DownCopy();
            Block blockBelow = blockAccessor.GetBlock(posBelow);

            // 1. Проверяем, можно ли прикрепиться к блоку снизу (чтобы не висели в воздухе)
            if (!blockBelow.CanAttachBlockAt(blockAccessor, this, posBelow, BlockFacing.UP))
            {
                return false;
            }

            // 2. Получаем блок, куда игра пытается воткнуть цветок
            Block currentBlock = blockAccessor.GetBlock(pos);

            // 3. Запрещаем спавн в воде (или любой другой жидкости)
            if (currentBlock.IsLiquid())
            {
                return false;
            }

            // 4. Запрещаем спавн поверх других цветов (чтобы не было башен)
            if (currentBlock is BlockBotaniaFlower)
            {
                return false;
            }

            // Если все проверки пройдены, разрешаем генератору поставить блок
            return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldgenRandom, attributes);
        }

        // Вызывается, когда соседние блоки обновляются (например, ломают землю под цветком)
        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neigbourPos)
        {
            BlockPos posBelow = pos.DownCopy();
            Block blockBelow = world.BlockAccessor.GetBlock(posBelow);

            // Если земля пропала или изменилась так, что цветок больше не держится
            if (!blockBelow.CanAttachBlockAt(world.BlockAccessor, this, posBelow, BlockFacing.UP))
            {
                // Разрушаем цветок (он выпадет как предмет, если настроен дроп)
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            base.OnNeighbourBlockChange(world, pos, neigbourPos);
        }
    }
}