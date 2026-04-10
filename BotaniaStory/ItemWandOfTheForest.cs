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
        // ЛКМ (Левая Кнопка Мыши) - ОТМЕНА ПРИВЯЗКИ
        // ==========================================
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            bool hadFlower = slot.Itemstack.Attributes.HasAttribute("hasFlower");
            bool hadSpreader = slot.Itemstack.Attributes.HasAttribute("hasSpreader");

            if (hadFlower || hadSpreader)
            {
                slot.Itemstack.Attributes.RemoveAttribute("hasFlower");
                slot.Itemstack.Attributes.RemoveAttribute("hasSpreader");
                slot.MarkDirty();

                if (byEntity.World.Side == EnumAppSide.Client)
                {
                    var clientApi = byEntity.World.Api as ICoreClientAPI;
                    clientApi?.ShowChatMessage("Действие отменено. Память посоха очищена.");
                }

                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }

        // ==========================================
        // SHIFT + ПКМ в воздухе - Снятие искры
        // ==========================================
        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null || byEntity.World.Side == EnumAppSide.Client) return;

            if (byPlayer.Entity.Controls.Sneak && byPlayer.Entity.Controls.RightMouseDown)
            {
                IWorldAccessor world = byEntity.World;

                Vec3d eyePos = byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
                Vec3f viewVec = byEntity.Pos.GetViewVector();
                Vec3d lookDir = new Vec3d(viewVec.X, viewVec.Y, viewVec.Z);

                Entity[] nearbySparks = world.GetEntitiesAround(byEntity.Pos.XYZ, 5, 5, e => e is EntitySpark);

                foreach (Entity spark in nearbySparks)
                {
                    Vec3d dirToSpark = new Vec3d(spark.Pos.X - eyePos.X, spark.Pos.Y - eyePos.Y, spark.Pos.Z - eyePos.Z);
                    double distance = dirToSpark.Length();

                    if (distance < 4.5 && dirToSpark.Normalize().Dot(lookDir) > 0.98)
                    {
                        // Проверяем, есть ли на искре дополнитель
                        string currentAugment = spark.WatchedAttributes.GetString("augment", "none");

                        if (currentAugment != "none")
                        {
                            // 1. Снимаем дополнитель
                            spark.WatchedAttributes.SetString("augment", "none");
                            spark.WatchedAttributes.MarkAllDirty();

                            // Выдаем игроку предмет дополнителя (например, подчиненный)
                            string itemCode = "sparkaugment-" + currentAugment; // например: sparkaugment-recessive
                            Item augmentItem = world.GetItem(new AssetLocation("botaniastory", itemCode));
                            if (augmentItem != null)
                            {
                                world.SpawnItemEntity(new ItemStack(augmentItem), spark.Pos.XYZ);
                            }

                            AssetLocation popSound = new AssetLocation("game", "sounds/player/throw");
                            world.PlaySoundAt(popSound, spark.Pos.X, spark.Pos.Y, spark.Pos.Z, null, true, 16, 1f);
                            break;
                        }
                        else
                        {
                            // 2. Если дополнителя нет — снимаем саму искру
                            Item itemSpark = world.GetItem(new AssetLocation("botaniastory", "spark"));
                            if (itemSpark != null)
                            {
                                world.SpawnItemEntity(new ItemStack(itemSpark), spark.Pos.XYZ);
                            }

                            AssetLocation wandSound = new AssetLocation("botaniastory", "sounds/wand_bind");
                            world.PlaySoundAt(wandSound, spark.Pos.X, spark.Pos.Y, spark.Pos.Z, null, true, 16, 1f);

                            spark.Die();
                            break;
                        }
                    }
                }
            }
        }

        // ==========================================
        // ПКМ - ЛОГИКА ПРИВЯЗКИ И ИНФОРМАЦИИ
        // ==========================================
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!firstEvent) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            IWorldAccessor world = byEntity.World;

            // ==========================================
            // А. КАСТОМНЫЙ ПОИСК ИСКРЫ (Так как у неё нет хитбокса)
            // ==========================================
            EntitySpark targetSpark = null;

            // Получаем точку глаз игрока и вектор взгляда
            Vec3d eyePos = byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
            Vec3f viewVec = byEntity.Pos.GetViewVector();
            Vec3d lookDir = new Vec3d(viewVec.X, viewVec.Y, viewVec.Z);

            // Ищем искры в радиусе 5 блоков
            Entity[] nearbySparks = world.GetEntitiesAround(byEntity.Pos.XYZ, 5, 5, e => e is EntitySpark);

            foreach (Entity entity in nearbySparks)
            {
                if (entity is EntitySpark spark)
                {
                    // Проверяем, смотрим ли мы прямо на нее
                    Vec3d dirToSpark = new Vec3d(spark.Pos.X - eyePos.X, spark.Pos.Y - eyePos.Y, spark.Pos.Z - eyePos.Z);
                    if (dirToSpark.Length() < 4.5 && dirToSpark.Normalize().Dot(lookDir) > 0.98)
                    {
                        targetSpark = spark;
                        break; // Нашли искру, на которую смотрим!
                    }
                }
            }

            // Если мы "попали" взглядом по искре:
            if (targetSpark != null)
            {
                // ==========================================
                // 1. ЕСЛИ ЗАЖАТ SHIFT (Снятие руны или искры)
                // ==========================================
                if (byPlayer.Entity.Controls.Sneak)
                {
                    string currentAugment = targetSpark.WatchedAttributes.GetString("augment", "none");

                    // Если есть руна - снимаем только руну
                    if (currentAugment != "none")
                    {
                        targetSpark.WatchedAttributes.SetString("augment", "none");
                        targetSpark.WatchedAttributes.MarkAllDirty();

                        string itemCode = "sparkaugment-" + currentAugment;
                        Item augmentItem = world.GetItem(new AssetLocation("botaniastory", itemCode));
                        if (augmentItem != null)
                        {
                            world.SpawnItemEntity(new ItemStack(augmentItem), targetSpark.Pos.XYZ);
                        }
                        world.PlaySoundAt(new AssetLocation("game", "sounds/player/throw"), targetSpark.Pos.X, targetSpark.Pos.Y, targetSpark.Pos.Z, null, true, 16, 1f);
                    }
                    // Если руны нет - снимаем саму искру
                    else
                    {
                        Item itemSpark = world.GetItem(new AssetLocation("botaniastory", "spark"));
                        if (itemSpark != null)
                        {
                            world.SpawnItemEntity(new ItemStack(itemSpark), targetSpark.Pos.XYZ);
                        }
                        world.PlaySoundAt(new AssetLocation("botaniastory", "sounds/wand_bind"), targetSpark.Pos.X, targetSpark.Pos.Y, targetSpark.Pos.Z, null, true, 16, 1f);

                        targetSpark.Die();
                    }
                }
                // ==========================================
                // 2. ЕСЛИ ПРОСТО КЛИК (Показ лучей сети)
                // ==========================================
                else
                {
                    Vec3d sparkPos = targetSpark.Pos.XYZ.AddCopy(0, 0.1, 0);
                    int foundSparks = 0;

                    Entity[] linkedSparks = world.GetEntitiesAround(targetSpark.Pos.XYZ, 8, 8, e => e is EntitySpark && e.EntityId != targetSpark.EntityId);

                    foreach (Entity entity in linkedSparks)
                    {
                        if (entity is EntitySpark otherSpark)
                        {
                            Vec3d otherSparkPos = otherSpark.Pos.XYZ.AddCopy(0, 0.1, 0);
                            SpawnBindingParticles(world, sparkPos, otherSparkPos);
                            foundSparks++;
                        }
                    }

                    if (world.Side == EnumAppSide.Client)
                    {
                        var clientApi = world.Api as ICoreClientAPI;
                        clientApi?.ShowChatMessage($"Эта искра связана с другими искрами: {foundSparks} шт.");
                    }
                    world.PlaySoundAt(new AssetLocation("botaniastory", "sounds/wand_bind"), targetSpark.Pos.X, targetSpark.Pos.Y, targetSpark.Pos.Z, byPlayer, true, 16, 1f);
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            // ==========================================
            // Б. ЕСЛИ НЕ ПОПАЛИ ПО ИСКРЕ, ПЕРЕХОДИМ К БЛОКАМ
            // ==========================================

            // Если мы кликнули просто в воздух, ничего не делаем
            if (blockSel == null) return;

            BlockPos pos = blockSel.Position;
            Block block = world.BlockAccessor.GetBlock(pos);
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);

            AssetLocation wandSound = new AssetLocation("botaniastory", "sounds/wand_bind");

            bool hasFlowerInMemory = slot.Itemstack.Attributes.GetBool("hasFlower");
            bool hasSpreaderInMemory = slot.Itemstack.Attributes.GetBool("hasSpreader");

            // ЗАВЕРШЕНИЕ ПРИВЯЗКИ ЦВЕТКА -> К РАСПРОСТРАНИТЕЛЮ
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

            // ЗАВЕРШЕНИЕ ПРИВЯЗКИ РАСПРОСТРАНИТЕЛЯ -> К ЦЕЛИ
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

                slot.Itemstack.Attributes.RemoveAttribute("hasSpreader");
                slot.MarkDirty();

                handling = EnumHandHandling.Handled;
                return;
            }

            // НАЧАЛО ПРИВЯЗКИ (Если память посоха пуста)
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

        // ==========================================
        // МЕТОД ДЛЯ РИСОВАНИЯ ЛУЧЕЙ ИЗ ЧАСТИЦ (Универсальный)
        // ==========================================
        private void SpawnBindingParticles(IWorldAccessor world, Vec3d start, Vec3d end)
        {
            double distance = start.DistanceTo(end);
            Vec3d direction = (end - start).Normalize();

            SimpleParticleProperties beamParticles = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(255, 100, 255, 200), // Тот самый красивый бирюзово-зеленый
                new Vec3d(), new Vec3d(),
                new Vec3f(-0.05f, -0.05f, -0.05f),
                new Vec3f(0.05f, 0.05f, 0.05f),
                1.0f,
                0f,
                0.2f,
                0.4f,
                EnumParticleModel.Quad
            );

            beamParticles.VertexFlags = 128; // Заставляем светиться
            beamParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -0.5f);

            for (float i = 0; i < distance; i += 0.3f)
            {
                beamParticles.MinPos.Set(start.X + direction.X * i, start.Y + direction.Y * i, start.Z + direction.Z * i);
                world.SpawnParticles(beamParticles);
            }
        }
    }
}