using BotaniaStory.blockentity;
using BotaniaStory.items;
using Vintagestory.API.Common;

namespace BotaniaStory.blocks
{
    public class BlockElvenGatewayCore : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // 1. Берем активный слот из рук игрока
            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            // 2. Проверяем, не пустые ли руки и держит ли игрок именно Посох Леса
            if (!activeSlot.Empty && activeSlot.Itemstack.Item is ItemWandOfTheForest)
            {
                // 3. Если в руках посох, то разрешаем взаимодействие с ядром
                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityElvenGatewayCore core)
                {
                    core.OnInteract(byPlayer);
                    return true; // Говорим игре, что клик успешно обработан
                }
            }

            // Если игрок кликает пустой рукой, мечом, киркой и т.д. — 
            // игнорируем взаимодействие (портал не отреагирует)
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}