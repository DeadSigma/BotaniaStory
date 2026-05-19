using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory
{
    public class BEBehaviorPureDaisy : BlockEntityBehavior
    {
        private readonly Vec3i[] offsets = new Vec3i[]
        {
            new Vec3i(-1, 0, -1), new Vec3i(0, 0, -1), new Vec3i(1, 0, -1),
            new Vec3i(-1, 0, 0),                       new Vec3i(1, 0, 0),
            new Vec3i(-1, 0, 1),  new Vec3i(0, 0, 1),  new Vec3i(1, 0, 1)
        };

        private Dictionary<Vec3i, int> progress = new Dictionary<Vec3i, int>();

        // Обязательный публичный конструктор для движка
        public BEBehaviorPureDaisy(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            // Запускаем таймер, привязываясь к базовой сущности блока
            this.Blockentity.RegisterGameTickListener(OnTick, 1000);
        }

        private string GetRecipeOutput(Block block)
        {
            if (block == null || block.Code == null) return null;
            if (block.Code.Domain == "botaniastory") return null;

            if (block.FirstCodePart() == "log" || block is BlockLog)
            {
                return "botaniastory:livingwood-normal";
            }
            if (block.BlockMaterial == EnumBlockMaterial.Stone)
            {
                return "botaniastory:livingrock";
            }

            return null;
        }

        private void OnTick(float dt)
        {
            foreach (Vec3i offset in offsets)
            {
                // Используем координаты родительского блока
                BlockPos targetPos = this.Blockentity.Pos.AddCopy(offset.X, offset.Y, offset.Z);
                Block block = this.Api.World.BlockAccessor.GetBlock(targetPos);

                string outputCode = GetRecipeOutput(block);

                if (outputCode != null)
                {
                    if (this.Api.Side == EnumAppSide.Client)
                    {
                        SpawnParticles(targetPos);
                        continue;
                    }

                    if (!progress.ContainsKey(offset)) progress[offset] = 0;
                    progress[offset]++;

                    if (progress[offset] >= 30)
                    {
                        Block resultBlock = this.Api.World.GetBlock(new AssetLocation(outputCode));
                        if (resultBlock != null)
                        {
                            this.Api.World.BlockAccessor.SetBlock(resultBlock.Id, targetPos);

                            if (outputCode == "botaniastory:livingwood-normal")
                            {
                                this.Api.World.PlaySoundAt(new AssetLocation("game:sounds/block/planks"), targetPos.X, targetPos.Y, targetPos.Z);
                            }
                            else if (outputCode == "botaniastory:livingrock")
                            {
                                this.Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/stonecrush"), targetPos.X, targetPos.Y, targetPos.Z);
                            }
                        }
                        progress[offset] = 0;
                    }
                }
                else
                {
                    if (this.Api.Side == EnumAppSide.Server)
                    {
                        progress.Remove(offset);
                    }
                }
            }
        }

        private void SpawnParticles(BlockPos pos)
        {
            SimpleParticleProperties particles = new SimpleParticleProperties(
                1, 2,
                ColorUtil.ToRgba(255, 230, 255, 255),
                new Vec3d(pos.X, pos.Y + 0.1, pos.Z),
                new Vec3d(pos.X + 1, pos.Y + 1.1, pos.Z + 1),
                new Vec3f(-0.2f, 0.2f, -0.2f),
                new Vec3f(0.2f, 0.5f, 0.2f),
                1.5f,
                -0.05f,
                0.1f, 0.4f,
                EnumParticleModel.Quad
            );

            particles.AddPos.Set(new Vec3d(0, 0, 0));
            this.Api.World.SpawnParticles(particles);
        }
    }
}