using BotaniaStory.blockentity;
using BotaniaStory.blocks;
using BotaniaStory.Flora.GeneratingFlora;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.items
{
    public class ItemWandOfBinding : Item
    {
        // Универсальный искатель интерфейсов (как в Посохе Леса и HUD)
        private T GetInterface<T>(BlockEntity be) where T : class
        {
            if (be == null) return null;
            if (be is T entityInterface) return entityInterface;
            foreach (var behavior in be.Behaviors)
            {
                if (behavior is T behaviorInterface) return behaviorInterface;
            }
            return null;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!firstEvent || blockSel == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            IWorldAccessor world = byEntity.World;
            BlockPos pos = blockSel.Position;
            Block block = world.BlockAccessor.GetBlock(pos);
            BlockEntity clickedBe = world.BlockAccessor.GetBlockEntity(pos);

            AssetLocation bindSound = new AssetLocation("botaniastory", "sounds/effect/translocate");

            float wandVolume = 1f;
            if (world.Api is ICoreClientAPI)
            {
                wandVolume = (BotaniaStoryModSystem.ClientConfig?.WandVolume ?? 50) / 100f;
            }

            // 1. КЛИК ПО РАСПРОСТРАНИТЕЛЮ (Массовая привязка генерирующих цветов)
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
                            BlockEntity checkBe = world.BlockAccessor.GetBlockEntity(checkPos);

                            ILinkableToSpreader flower = GetInterface<ILinkableToSpreader>(checkBe);
                            if (flower != null)
                            {
                                flower.LinkedSpreader = pos.Copy();
                                checkBe.MarkDirty(true); // Сохраняем родительский блок!
                                boundCount++;
                                SpawnBindingParticles(world, pos, checkPos);
                            }
                        }
                    }
                }

                if (boundCount > 0) world.PlaySoundAt(bindSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);

                handling = EnumHandHandling.Handled;
                return;
            }

            // 2. КЛИК ПО БАССЕЙНУ (Массовая привязка всех цветов ILinkableToPool)
            else if (clickedBe is BlockEntityManaPool)
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
                            BlockEntity checkBe = world.BlockAccessor.GetBlockEntity(checkPos);

                            ILinkableToPool flower = GetInterface<ILinkableToPool>(checkBe);
                            if (flower != null)
                            {
                                flower.LinkedPool = pos.Copy();
                                checkBe.MarkDirty(true);
                                boundCount++;
                                SpawnBindingParticles(world, pos, checkPos);
                            }
                        }
                    }
                }
                if (boundCount > 0) world.PlaySoundAt(bindSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);

                handling = EnumHandHandling.Handled;
                return;
            }

            // Проверяем интерфейсы кликнутого блока
            ILinkableToSpreader clickedSpreaderLinkable = GetInterface<ILinkableToSpreader>(clickedBe);
            ILinkableToPool clickedPoolLinkable = GetInterface<ILinkableToPool>(clickedBe);

            // 3. КЛИК ПО ГЕНЕРИРУЮЩЕМУ ЦВЕТКУ (Копирование привязки к распространителю на соседей)
            if (clickedSpreaderLinkable != null)
            {
                if (clickedSpreaderLinkable.LinkedSpreader != null)
                {
                    int radius = 6;
                    int boundCount = 0;
                    BlockPos spreaderPos = clickedSpreaderLinkable.LinkedSpreader;

                    SpawnBindingParticles(world, spreaderPos, pos);

                    for (int x = -radius; x <= radius; x++)
                    {
                        for (int y = -radius; y <= radius; y++)
                        {
                            for (int z = -radius; z <= radius; z++)
                            {
                                BlockPos checkPos = pos.AddCopy(x, y, z);
                                BlockEntity checkBe = world.BlockAccessor.GetBlockEntity(checkPos);

                                ILinkableToSpreader flower = GetInterface<ILinkableToSpreader>(checkBe);
                                if (flower != null && !checkPos.Equals(pos))
                                {
                                    flower.LinkedSpreader = spreaderPos.Copy();
                                    checkBe.MarkDirty(true);
                                    boundCount++;
                                    SpawnBindingParticles(world, spreaderPos, checkPos);
                                }
                            }
                        }
                    }

                    if (boundCount > 0) world.PlaySoundAt(bindSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            // 4. КЛИК ПО ФУНКЦИОНАЛЬНОМУ ЦВЕТКУ (Копирование привязки к бассейну на соседей)
            else if (clickedPoolLinkable != null)
            {
                BlockPos poolPos = clickedPoolLinkable.LinkedPool;

                // Ищем бассейн, если не привязан
                if (poolPos == null)
                {
                    int searchRadius = 6;
                    for (int x = -searchRadius; x <= searchRadius; x++)
                    {
                        for (int y = -searchRadius; y <= searchRadius; y++)
                        {
                            for (int z = -searchRadius; z <= searchRadius; z++)
                            {
                                BlockPos checkPos = pos.AddCopy(x, y, z);
                                if (world.BlockAccessor.GetBlockEntity(checkPos) is BlockEntityManaPool)
                                {
                                    poolPos = checkPos.Copy();
                                    break;
                                }
                            }
                            if (poolPos != null) break;
                        }
                        if (poolPos != null) break;
                    }
                }

                // Если нашли - связываем массово
                if (poolPos != null)
                {
                    int radius = 6;
                    int boundCount = 0;

                    clickedPoolLinkable.LinkedPool = poolPos.Copy();
                    clickedBe.MarkDirty(true);

                    SpawnBindingParticles(world, poolPos, pos);

                    for (int x = -radius; x <= radius; x++)
                    {
                        for (int y = -radius; y <= radius; y++)
                        {
                            for (int z = -radius; z <= radius; z++)
                            {
                                BlockPos checkPos = pos.AddCopy(x, y, z);
                                BlockEntity checkBe = world.BlockAccessor.GetBlockEntity(checkPos);

                                ILinkableToPool targetFlower = GetInterface<ILinkableToPool>(checkBe);
                                if (targetFlower != null && !checkPos.Equals(pos))
                                {
                                    targetFlower.LinkedPool = poolPos.Copy();
                                    checkBe.MarkDirty(true);
                                    boundCount++;
                                    SpawnBindingParticles(world, poolPos, checkPos);
                                }
                            }
                        }
                    }
                    world.PlaySoundAt(bindSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        // МЕТОД ДЛЯ РИСОВАНИЯ ЛУЧЕЙ ИЗ ЧАСТИЦ
        private void SpawnBindingParticles(IWorldAccessor world, BlockPos spreaderPos, BlockPos flowerPos)
        {
            Vec3d start = new Vec3d(spreaderPos.X + 0.5, spreaderPos.Y + 0.5, spreaderPos.Z + 0.5);
            Vec3d end = new Vec3d(flowerPos.X + 0.5, flowerPos.Y + 0.2, flowerPos.Z + 0.5);

            double distance = start.DistanceTo(end);
            Vec3d direction = (end - start).Normalize();

            SimpleParticleProperties beamParticles = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(255, 100, 255, 200),
                new Vec3d(), new Vec3d(),
                new Vec3f(-0.05f, -0.05f, -0.05f),
                new Vec3f(0.05f, 0.05f, 0.05f),
                1.0f,
                0f,
                0.2f,
                0.4f,
                EnumParticleModel.Quad
            );

            beamParticles.VertexFlags = 128;
            beamParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -0.5f);

            for (float i = 0; i < distance; i += 0.3f)
            {
                beamParticles.MinPos.Set(start.X + direction.X * i, start.Y + direction.Y * i, start.Z + direction.Z * i);
                world.SpawnParticles(beamParticles);
            }
        }
    }
}