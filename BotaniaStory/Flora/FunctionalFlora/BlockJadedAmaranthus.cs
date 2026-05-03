using BotaniaStory.blockentity;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using BotaniaStory.Blocks;

namespace BotaniaStory
{
    public class BlockJadedAmaranthus : BlockBotaniaFlower
    {
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            // Сначала выполняем стандартную установку
            base.OnBlockPlaced(world, blockPos, byItemStack);

            // Затем, только на сервере, заставляем цветок искать бассейн
            if (world.Side == EnumAppSide.Server)
            {
                if (world.BlockAccessor.GetBlockEntity(blockPos) is BlockEntityJadedAmaranthus be)
                {
                    be.AutoFindPool();
                }
            }
        }
    }
}