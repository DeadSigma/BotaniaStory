using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ItemWandOfTheForest : Item
    {
        // ==========================================
        // НОВОЕ: ЛКМ (Левая Кнопка Мыши) - ОТМЕНА ПРИВЯЗКИ
        // ==========================================
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            bool hadFlower = slot.Itemstack.Attributes.HasAttribute("hasFlower");
            bool hadSpreader = slot.Itemstack.Attributes.HasAttribute("hasSpreader");

            // Если в памяти посоха есть сохраненный цветок или распространитель
            if (hadFlower || hadSpreader)
            {
                slot.Itemstack.Attributes.RemoveAttribute("hasFlower");
                slot.Itemstack.Attributes.RemoveAttribute("hasSpreader");
                slot.MarkDirty(); // Не забываем сохранить очистку

                if (byEntity.World.Side == EnumAppSide.Client)
                {
                    var clientApi = byEntity.World.Api as ICoreClientAPI;
                    clientApi?.ShowChatMessage("Действие отменено. Память посоха очищена.");
                }

                // Предотвращаем стандартное действие (разрушение блока)
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }

        // ==========================================
        // ПКМ - ЛОГИКА ПРИВЯЗКИ И ИНФОРМАЦИИ
        // ==========================================
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!firstEvent || blockSel == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            IWorldAccessor world = byEntity.World;
            BlockPos pos = blockSel.Position;
            Block block = world.BlockAccessor.GetBlock(pos);
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            bool isSneaking = byPlayer.Entity.Controls.Sneak;

            AssetLocation wandSound = new AssetLocation("botaniastory", "sounds/wand_bind");

            // ==========================================
            // А. SHIFT + ПКМ (Снятие искры)
            // ==========================================
            if (isSneaking)
            {
                // Получаем точку глаз игрока и вектор направления его взгляда
                Vec3d eyePos = byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
                Vec3f viewVec = byEntity.Pos.GetViewVector(); // Используем Pos вместо устаревшего SidedPos
                Vec3d lookDir = new Vec3d(viewVec.X, viewVec.Y, viewVec.Z); // Явно конвертируем float в double

                // Ищем все искры в радиусе 8 блоков
                Entity[] nearbySparks = world.GetEntitiesAround(byEntity.Pos.XYZ, 8, 8, e => e is EntitySpark);

                foreach (Entity spark in nearbySparks)
                {
                    // Вычисляем вектор от глаз игрока до искры
                    Vec3d dirToSpark = new Vec3d(spark.Pos.X - eyePos.X, spark.Pos.Y - eyePos.Y, spark.Pos.Z - eyePos.Z).Normalize();

                    // Скалярное произведение (Dot) покажет, смотрим ли мы прямо на искру
                    // Значение > 0.96 означает, что искра находится почти в центре экрана
                    if (dirToSpark.Dot(lookDir) > 0.96)
                    {
                        if (world.Side == EnumAppSide.Server)
                        {
                            // Спавним предмет искры и убиваем сущность
                            ItemStack sparkStack = new ItemStack(world.GetItem(new AssetLocation("botaniastory", "spark")));
                            world.SpawnItemEntity(sparkStack, spark.Pos.XYZ);
                            spark.Die();
                        }

                        // Проигрываем магический звук посоха для обратной связи
                        world.PlaySoundAt(wandSound, spark.Pos.X, spark.Pos.Y, spark.Pos.Z, byPlayer, true, 16, 1f);

                        handling = EnumHandHandling.PreventDefaultAction;
                        return; // Искра поймана, прерываем выполнение!
                    }
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            // --- ДАЛЕЕ ИДЕТ ЛОГИКА ДЛЯ ПРОСТОГО ПКМ (БЕЗ SHIFT) ---

            bool hasFlowerInMemory = slot.Itemstack.Attributes.GetBool("hasFlower");
            bool hasSpreaderInMemory = slot.Itemstack.Attributes.GetBool("hasSpreader");

            // ==========================================
            // Б. ЗАВЕРШЕНИЕ ПРИВЯЗКИ ЦВЕТКА -> К РАСПРОСТРАНИТЕЛЮ
            // ==========================================
            if (hasFlowerInMemory)
            {
                if (block is ManaSpreader)
                {
                    int fx = slot.Itemstack.Attributes.GetInt("flowerX");
                    int fy = slot.Itemstack.Attributes.GetInt("flowerY");
                    int fz = slot.Itemstack.Attributes.GetInt("flowerZ");
                    BlockPos flowerPos = new BlockPos(fx, fy, fz);

                    BlockEntity flowerEntity = world.BlockAccessor.GetBlockEntity(flowerPos);

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

                    // Очищаем память посоха
                    slot.Itemstack.Attributes.RemoveAttribute("hasFlower");
                    slot.MarkDirty();

                    if (world.Side == EnumAppSide.Client)
                    {
                        (world.Api as ICoreClientAPI)?.ShowChatMessage("Цветок успешно привязан к Распространителю!");
                    }

                    world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, 1f);
                }
                else
                {
                    if (world.Side == EnumAppSide.Client)
                    {
                        (world.Api as ICoreClientAPI)?.ShowChatMessage("Вы выбрали цветок. Кликните ПКМ по Распространителю маны или нажмите ЛКМ для отмены.");
                    }
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            // ==========================================
            // В. ЗАВЕРШЕНИЕ ПРИВЯЗКИ РАСПРОСТРАНИТЕЛЯ -> К ЦЕЛИ
            // ==========================================
            if (hasSpreaderInMemory)
            {
                int sx = slot.Itemstack.Attributes.GetInt("spreaderX");
                int sy = slot.Itemstack.Attributes.GetInt("spreaderY");
                int sz = slot.Itemstack.Attributes.GetInt("spreaderZ");
                BlockPos spreaderPos = new BlockPos(sx, sy, sz);

                BlockEntityManaSpreader spreaderBE = world.BlockAccessor.GetBlockEntity(spreaderPos) as BlockEntityManaSpreader;
                if (spreaderBE != null)
                {
                    double dx = pos.X - spreaderPos.X;
                    double dy = pos.Y - spreaderPos.Y;
                    double dz = pos.Z - spreaderPos.Z;

                    spreaderBE.Yaw = (float)Math.Atan2(dx, dz) + (float)Math.PI;
                    double distanceXZ = Math.Sqrt(dx * dx + dz * dz);
                    spreaderBE.Pitch = (float)Math.Atan2(dy, distanceXZ);

                    spreaderBE.TargetPos = pos.Copy();
                    spreaderBE.MarkDirty(true);

                    if (world.Side == EnumAppSide.Client)
                    {
                        var clientApi = world.Api as ICoreClientAPI;
                        if (block is BlockManaPool)
                        {
                            clientApi?.ShowChatMessage("Связь установлена! Распространитель привязан к Бассейну.");
                        }
                        else
                        {
                            clientApi?.ShowChatMessage("Распространитель маны повернут к новым координатам!");
                        }
                    }

                    world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, 1f);
                }

                // Очищаем память посоха
                slot.Itemstack.Attributes.RemoveAttribute("hasSpreader");
                slot.MarkDirty();

                handling = EnumHandHandling.Handled;
                return;
            }

            // ==========================================
            // Г. НАЧАЛО ПРИВЯЗКИ (Если память посоха пуста)
            // ==========================================

            // Если кликнули по цветку
            if (be is BotaniaStory.Flora.GeneratingFlora.BlockEntityDaybloom ||
                be is BotaniaStory.Flora.GeneratingFlora.BlockEntityEndoflame)
            {
                slot.Itemstack.Attributes.SetInt("flowerX", pos.X);
                slot.Itemstack.Attributes.SetInt("flowerY", pos.Y);
                slot.Itemstack.Attributes.SetInt("flowerZ", pos.Z);
                slot.Itemstack.Attributes.SetBool("hasFlower", true);
                slot.MarkDirty();

                if (world.Side == EnumAppSide.Client)
                {
                    (world.Api as ICoreClientAPI)?.ShowChatMessage("Цветок выбран. Теперь нажмите ПКМ по Распространителю маны.");
                }

                world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, 1f);
                handling = EnumHandHandling.Handled;
                return;
            }

            // Если кликнули по распространителю
            if (block is ManaSpreader)
            {
                slot.Itemstack.Attributes.SetInt("spreaderX", pos.X);
                slot.Itemstack.Attributes.SetInt("spreaderY", pos.Y);
                slot.Itemstack.Attributes.SetInt("spreaderZ", pos.Z);
                slot.Itemstack.Attributes.SetBool("hasSpreader", true);
                slot.MarkDirty();

                if (world.Side == EnumAppSide.Client)
                {
                    (world.Api as ICoreClientAPI)?.ShowChatMessage("Распространитель выбран. Кликните ПКМ по Бассейну (или блоку) для привязки.");
                }

                world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, 1f);
                handling = EnumHandHandling.Handled;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}