using BotaniaStory.blockentity;
using BotaniaStory.blocks;
using BotaniaStory.Flora.GeneratingFlora;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.items
{
    public class ItemWandOfBinding : Item
    {
        // Интерфейс ищется у блока и его поведений
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

            // Генерирующие цветы привязываются к выбранному распространителю
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
                            if (flower == null) continue;

                            flower.LinkedSpreader = pos.Copy();
                            checkBe.MarkDirty(true);
                            boundCount++;
                            SpawnBindingParticles(world, pos, checkPos);
                        }
                    }
                }

                if (boundCount > 0)
                {
                    world.PlaySoundAt(bindSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            // Цветы привязываются к выбранному бассейну
            if (clickedBe is BlockEntityManaPool)
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
                            if (flower == null) continue;

                            flower.LinkedPool = pos.Copy();
                            checkBe.MarkDirty(true);
                            boundCount++;
                            SpawnBindingParticles(world, pos, checkPos);
                        }
                    }
                }

                if (boundCount > 0)
                {
                    world.PlaySoundAt(bindSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            ILinkableToSpreader clickedSpreaderLinkable = GetInterface<ILinkableToSpreader>(clickedBe);
            ILinkableToPool clickedPoolLinkable = GetInterface<ILinkableToPool>(clickedBe);

            // Генерирующие цветы распределяются между соседними распространителями
            if (clickedSpreaderLinkable != null)
            {
                BindGeneratingFlowersEvenly(world, pos, clickedSpreaderLinkable, byPlayer, bindSound, wandVolume);

                handling = EnumHandHandling.Handled;
                return;
            }

            // Функциональные цветы привязываются к одному бассейну
            if (clickedPoolLinkable != null)
            {
                BlockPos poolPos = clickedPoolLinkable.LinkedPool;

                // Бассейн ищется рядом с выбранным цветком
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

                if (poolPos != null)
                {
                    int radius = 6;

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
                                if (checkPos.Equals(pos)) continue;

                                BlockEntity checkBe = world.BlockAccessor.GetBlockEntity(checkPos);
                                ILinkableToPool targetFlower = GetInterface<ILinkableToPool>(checkBe);
                                if (targetFlower == null) continue;

                                targetFlower.LinkedPool = poolPos.Copy();
                                checkBe.MarkDirty(true);
                                SpawnBindingParticles(world, poolPos, checkPos);
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

        private void BindGeneratingFlowersEvenly(
            IWorldAccessor world,
            BlockPos centerPos,
            ILinkableToSpreader clickedFlower,
            IPlayer byPlayer,
            AssetLocation bindSound,
            float wandVolume)
        {
            const int radius = 6;

            List<BlockPos> spreaders = new List<BlockPos>();
            List<BlockPos> flowers = new List<BlockPos>();
            List<BlockEntity> flowerEntities = new List<BlockEntity>();

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos checkPos = centerPos.AddCopy(x, y, z);
                        BlockEntity checkBe = world.BlockAccessor.GetBlockEntity(checkPos);

                        if (checkBe is BlockEntityManaSpreader)
                        {
                            spreaders.Add(checkPos.Copy());
                        }

                        if (GetInterface<ILinkableToSpreader>(checkBe) != null)
                        {
                            flowers.Add(checkPos.Copy());
                            flowerEntities.Add(checkBe);
                        }
                    }
                }
            }

            // Старая привязка сохраняется, если рядом распространитель не найден
            if (spreaders.Count == 0 && clickedFlower.LinkedSpreader != null)
            {
                spreaders.Add(clickedFlower.LinkedSpreader.Copy());
            }

            if (spreaders.Count == 0 || flowers.Count == 0) return;

            spreaders.Sort((a, b) =>
            {
                double distanceA = DistanceSquared(centerPos, a);
                double distanceB = DistanceSquared(centerPos, b);

                int distanceCompare = distanceA.CompareTo(distanceB);
                if (distanceCompare != 0) return distanceCompare;

                int xCompare = a.X.CompareTo(b.X);
                if (xCompare != 0) return xCompare;

                int yCompare = a.Y.CompareTo(b.Y);
                if (yCompare != 0) return yCompare;

                return a.Z.CompareTo(b.Z);
            });

            int boundCount = 0;

            for (int i = 0; i < flowers.Count; i++)
            {
                BlockEntity flowerBe = flowerEntities[i];
                ILinkableToSpreader flower = GetInterface<ILinkableToSpreader>(flowerBe);
                if (flower == null) continue;

                BlockPos spreaderPos = spreaders[i % spreaders.Count];

                flower.LinkedSpreader = spreaderPos.Copy();
                flowerBe.MarkDirty(true);
                boundCount++;

                SpawnBindingParticles(world, spreaderPos, flowers[i]);
            }

            if (boundCount > 0)
            {
                world.PlaySoundAt(bindSound, centerPos.X + 0.5, centerPos.Y + 0.5, centerPos.Z + 0.5, byPlayer, true, 16, wandVolume);
            }
        }

        private double DistanceSquared(BlockPos from, BlockPos to)
        {
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double dz = to.Z - from.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        // Луч связи рисуется между распространителем и цветком
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
