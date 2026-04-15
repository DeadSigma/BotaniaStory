using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BlockRunicAltar : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityRunicAltar be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityRunicAltar;
            if (be == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            //  ПРОВЕРКА ПОСОХА ЛЕСА
            if (!activeSlot.Empty && activeSlot.Itemstack.Collectible.Code.Path == "wandoftheforest")
            {
                // Запускаем нашу разговорчивую отладку и логику крафта
                be.TryCompleteCrafting(byPlayer);

                // Всегда возвращаем true! Это говорит движку игры: 
                // "Мы обработали этот клик, не нужно передавать его дальше или отменять!"
                return true;
            }

            //  Если рука пустая и зажат Shift - забираем предметы
            if (activeSlot.Empty && byPlayer.Entity.Controls.Sneak)
            {
                if (be.TryTakeItem(byPlayer)) return true;
            }

            // Пытаемся положить предмет (Ингредиент или Жизнекамень)
            if (!activeSlot.Empty)
            {
                if (be.TryAddItem(activeSlot, byPlayer)) return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}