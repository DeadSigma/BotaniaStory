using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace BotaniaStory
{
    public class BlockManaPool : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // 1. ПРОВЕРКА НА ПОСОХ: Если в руках Посох леса - игнорируем клик блоком!
            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (!activeSlot.Empty && activeSlot.Itemstack.Item is ItemWandOfTheForest)
            {
                return false; // Возвращаем false, чтобы клик перешел к посоху
            }

            // 2. СТАРЫЙ ТЕСТОВЫЙ КОД (Заливаем 50,000 маны)
            BlockEntityManaPool be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityManaPool;
            if (be != null)
            {
                be.CurrentMana += 50000;
                if (be.CurrentMana > be.MaxMana) be.CurrentMana = be.MaxMana;

                be.MarkDirty(true);

                if (world.Side == EnumAppSide.Client)
                {
                    var clientApi = world.Api as ICoreClientAPI;
                    clientApi?.ShowChatMessage($"Мана: {be.CurrentMana} / {be.MaxMana}");
                }

                return true;
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}