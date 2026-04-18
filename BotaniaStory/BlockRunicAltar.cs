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

            // 1. ПКМ Посохом леса -> Завершить крафт (Contains спасет, если у посоха есть цвета в ID)
            if (!activeSlot.Empty && activeSlot.Itemstack.Collectible.Code.Path.Contains("wandoftheforest"))
            {
                if (be.TryCompleteCrafting(byPlayer)) return true;
            }

            // 2. ПКМ пустой рукой (Логика снятия предметов и АВТОКРАФТА)
            if (activeSlot.Empty)
            {
                // Зажат Shift -> снимаем предмет
                if (byPlayer.Entity.Controls.Sneak)
                {
                    if (be.TryTakeItem(byPlayer)) return true;
                }
                // Shift НЕ зажат -> пытаемся выложить рецепт автоматически
                else
                {
                    if (be.TryAutoCraft(byPlayer))
                    {
                        // Если автокрафт успешен, играем звук плюха!
                        world.PlaySoundAt(new AssetLocation("game:sounds/player/throw"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                        return true;
                    }
                }
            }

            // 3. ПКМ любым другим предметом -> Положить предмет на алтарь
            if (!activeSlot.Empty)
            {
                if (be.TryAddItem(activeSlot, byPlayer)) return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}