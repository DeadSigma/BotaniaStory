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
            // 1. ЗАЩИТА ОТ УДЕРЖАНИЯ: Реагируем только на самый первый момент клика
            if (!firstEvent || blockSel == null) return;

            // 2. ОБЪЯВЛЯЕМ ВСЕ ПЕРЕМЕННЫЕ В САМОМ НАЧАЛЕ
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return; // Защита от краша, если кликнул не игрок

            IWorldAccessor world = byEntity.World;
            BlockPos pos = blockSel.Position;
            Block block = world.BlockAccessor.GetBlock(pos);
            bool isSneaking = byPlayer.Entity.Controls.Sneak; // Проверяем зажат ли Shift


            // ==========================================
            // А. КЛИК ПО ЦВЕТКУ (Shift + ПКМ) - Запоминаем цветок
            // ==========================================
            if (isSneaking)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);

                // Проверяем, генерирующий ли это цветок (Дневноцвет или Эндофлейм)
                if (be is BotaniaStory.Flora.GeneratingFlora.BlockEntityDaybloom ||
                    be is BotaniaStory.Flora.GeneratingFlora.BlockEntityEndoflame)
                {
                    slot.Itemstack.Attributes.SetInt("flowerX", pos.X);
                    slot.Itemstack.Attributes.SetInt("flowerY", pos.Y);
                    slot.Itemstack.Attributes.SetInt("flowerZ", pos.Z);
                    slot.Itemstack.Attributes.SetBool("hasFlower", true);
                    slot.MarkDirty(); // Обязательно сохраняем!

                    if (world.Side == EnumAppSide.Client)
                    {
                        var clientApi = world.Api as ICoreClientAPI;
                        clientApi?.ShowChatMessage("Цветок выбран. Теперь нажми Shift+ПКМ по Распространителю маны.");
                    }

                    handling = EnumHandHandling.Handled;
                    return;
                }
            }

            // ==========================================
            // Б. КЛИК ПО РАСПРОСТРАНИТЕЛЮ (Shift + ПКМ) - Привязываем цветок!
            // ==========================================
            if (isSneaking && block is ManaSpreader && slot.Itemstack.Attributes.GetBool("hasFlower"))
            {
                int fx = slot.Itemstack.Attributes.GetInt("flowerX");
                int fy = slot.Itemstack.Attributes.GetInt("flowerY");
                int fz = slot.Itemstack.Attributes.GetInt("flowerZ");
                BlockPos flowerPos = new BlockPos(fx, fy, fz);

                BlockEntity flowerEntity = world.BlockAccessor.GetBlockEntity(flowerPos);

                // Присваиваем координаты распространителя нашему цветку
                if (flowerEntity is BotaniaStory.Flora.GeneratingFlora.BlockEntityDaybloom daybloom)
                {
                    daybloom.LinkedSpreader = pos.Copy();
                    daybloom.MarkDirty(true);
                }
                else if (flowerEntity is BotaniaStory.Flora.GeneratingFlora.BlockEntityEndoflame endoflame)
                {
                    endoflame.LinkedSpreader = pos.Copy();
                    endoflame.MarkDirty(true);
                }

                // Очищаем память посоха о цветке
                slot.Itemstack.Attributes.RemoveAttribute("hasFlower");
                slot.MarkDirty();

                if (world.Side == EnumAppSide.Client)
                {
                    var clientApi = world.Api as ICoreClientAPI;
                    clientApi?.ShowChatMessage("Цветок успешно привязан к Распространителю!");
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            // ==========================================
            // В. КЛИК ПО РАСПРОСТРАНИТЕЛЮ (Без Shift) - Запоминаем Распространитель
            // ==========================================
            if (!isSneaking && block is ManaSpreader)
            {
                slot.Itemstack.Attributes.SetInt("spreaderX", pos.X);
                slot.Itemstack.Attributes.SetInt("spreaderY", pos.Y);
                slot.Itemstack.Attributes.SetInt("spreaderZ", pos.Z);
                slot.Itemstack.Attributes.SetBool("hasSpreader", true);

                slot.MarkDirty();

                if (world.Side == EnumAppSide.Client)
                {
                    var clientApi = world.Api as ICoreClientAPI;
                    clientApi?.ShowChatMessage("Распространитель выбран. Теперь кликни ПКМ по Бассейну маны.");
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            // ==========================================
            // Г. КЛИК ПО БАССЕЙНУ (Без Shift) - Привязываем Распространитель к Бассейну
            // ==========================================
            if (!isSneaking && block is BlockManaPool && slot.Itemstack.Attributes.GetBool("hasSpreader"))
            {
                int sx = slot.Itemstack.Attributes.GetInt("spreaderX");
                int sy = slot.Itemstack.Attributes.GetInt("spreaderY");
                int sz = slot.Itemstack.Attributes.GetInt("spreaderZ");
                BlockPos spreaderPos = new BlockPos(sx, sy, sz);

                BlockEntityManaSpreader be = world.BlockAccessor.GetBlockEntity(spreaderPos) as BlockEntityManaSpreader;
                if (be != null)
                {
                    double dx = pos.X - spreaderPos.X;
                    double dy = pos.Y - spreaderPos.Y;
                    double dz = pos.Z - spreaderPos.Z;

                    be.Yaw = (float)Math.Atan2(dx, dz) + (float)Math.PI;
                    double distanceXZ = Math.Sqrt(dx * dx + dz * dz);
                    be.Pitch = (float)Math.Atan2(dy, distanceXZ);

                    be.TargetPos = pos.Copy();
                    be.MarkDirty(true);

                    if (world.Side == EnumAppSide.Client)
                    {
                        var clientApi = world.Api as ICoreClientAPI;
                        clientApi?.ShowChatMessage("Связь установлена! Распространитель повернулся к Бассейну.");
                    }
                }

                slot.Itemstack.Attributes.RemoveAttribute("hasSpreader");
                slot.MarkDirty();

                handling = EnumHandHandling.Handled;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}