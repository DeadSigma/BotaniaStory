using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ItemWandOfTheForest : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            IWorldAccessor world = byEntity.World;
            BlockPos pos = blockSel.Position;
            Block block = world.BlockAccessor.GetBlock(pos);

            // 1. КЛИК ПО РАСПРОСТРАНИТЕЛЮ (Запоминаем координаты)
            if (block is BlockManaSpreader)
            {
                slot.Itemstack.Attributes.SetInt("spreaderX", pos.X);
                slot.Itemstack.Attributes.SetInt("spreaderY", pos.Y);
                slot.Itemstack.Attributes.SetInt("spreaderZ", pos.Z);
                slot.Itemstack.Attributes.SetBool("hasSpreader", true);

                // Правильный вывод в чат только для клиента
                if (world.Side == EnumAppSide.Client)
                {
                    var clientApi = world.Api as ICoreClientAPI;
                    clientApi?.ShowChatMessage("Распространитель выбран. Теперь кликни ПКМ по Бассейну маны.");
                }

                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            // 2. КЛИК ПО БАССЕЙНУ (Привязываем и поворачиваем)
            if (block is BlockManaPool && slot.Itemstack.Attributes.GetBool("hasSpreader"))
            {
                int sx = slot.Itemstack.Attributes.GetInt("spreaderX");
                int sy = slot.Itemstack.Attributes.GetInt("spreaderY");
                int sz = slot.Itemstack.Attributes.GetInt("spreaderZ");
                BlockPos spreaderPos = new BlockPos(sx, sy, sz);

                BlockEntityManaSpreader be = world.BlockAccessor.GetBlockEntity(spreaderPos) as BlockEntityManaSpreader;
                if (be != null)
                {
                    // Высчитываем разницу координат (от центра до центра)
                    double dx = pos.X - spreaderPos.X;
                    double dy = pos.Y - spreaderPos.Y;
                    double dz = pos.Z - spreaderPos.Z;

                    // МАГИЯ ТРИГОНОМЕТРИИ: Вычисляем углы поворота
                    be.Yaw = (float)Math.Atan2(dx, dz) + (float)Math.PI;

                    double distanceXZ = Math.Sqrt(dx * dx + dz * dz);
                    be.Pitch = (float)Math.Atan2(dy, distanceXZ);

                    // СОХРАНЯЕМ КООРДИНАТЫ ЦЕЛИ (Бассейна) В РАСПРОСТРАНИТЕЛЬ
                    be.TargetPos = pos.Copy();

                    // Просим игру перерисовать блок с новыми углами!
                    be.MarkDirty(true);

                    // Правильный вывод в чат
                    if (world.Side == EnumAppSide.Client)
                    {
                        var clientApi = world.Api as ICoreClientAPI;
                        clientApi?.ShowChatMessage("Связь установлена! Распространитель повернулся к Бассейну.");
                    }
                }

                // Очищаем память посоха, чтобы можно было привязать следующий
                slot.Itemstack.Attributes.RemoveAttribute("hasSpreader");
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}