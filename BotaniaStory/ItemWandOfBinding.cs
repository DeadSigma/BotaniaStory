using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using BotaniaStory.Flora.GeneratingFlora; // Подключаем общие классы цветов

namespace BotaniaStory
{
    public class ItemWandOfBinding : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!firstEvent || blockSel == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            IWorldAccessor world = byEntity.World;
            BlockPos pos = blockSel.Position;
            Block block = world.BlockAccessor.GetBlock(pos);

            // ==========================================
            // КЛИК ПО РАСПРОСТРАНИТЕЛЮ (Массовая привязка)
            // ==========================================
            if (block is ManaSpreader)
            {
                int radius = 6;
                int boundCount = 0;

                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        for (int z = -radius; z <= radius; z++)
                        {
                            BlockPos checkPos = pos.AddCopy(x, y, z);
                            BlockEntity be = world.BlockAccessor.GetBlockEntity(checkPos);

                            // МАГИЯ НАСЛЕДОВАНИЯ: Эта строка автоматически поймает 
                            // и Дневноцвет, и Эндофлейм, и все твои будущие цветы!
                            if (be is BlockEntityGeneratingFlower flower)
                            {
                                flower.LinkedSpreader = pos.Copy();
                                flower.MarkDirty(true);
                                boundCount++;
                            }
                        }
                    }
                }

                if (world.Side == EnumAppSide.Client)
                {
                    var clientApi = world.Api as ICoreClientAPI;
                    if (boundCount > 0)
                    {
                        clientApi?.ShowChatMessage($"Посох связывания подключил цветы: {boundCount} шт. к этому Распространителю.");
                    }
                    else
                    {
                        clientApi?.ShowChatMessage("Поблизости нет генерирующих цветов для привязки.");
                    }
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}