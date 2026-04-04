using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory
{
    public class BlockEntityPureDaisy : BlockEntity
    {
        private readonly Vec3i[] offsets = new Vec3i[]
        {
            new Vec3i(-1, 0, -1), new Vec3i(0, 0, -1), new Vec3i(1, 0, -1),
            new Vec3i(-1, 0, 0),                       new Vec3i(1, 0, 0),
            new Vec3i(-1, 0, 1),  new Vec3i(0, 0, 1),  new Vec3i(1, 0, 1)
        };

        private Dictionary<Vec3i, int> progress = new Dictionary<Vec3i, int>();

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            // Запускаю таймер (тик каждую секунду)
            RegisterGameTickListener(OnTick, 1000);
        }


        // УМНАЯ ПРОВЕРКА БЛОКОВ "НА ЛЕТУ" (Без парсера)
        private string GetRecipeOutput(Block block)
        {
            if (block == null || block.Code == null) return null;

            // 1. ЗАЩИТА: Игнорирую блоки нашего мода (Жизнекамень, Жизнедерево, сами цветы)
            if (block.Code.Domain == "botaniastory") return null;

            // 2. ДЕРЕВО: Превращаю любые ванильные и модовые брёвна в Жизнедерево
            if (block.FirstCodePart() == "log" || block is BlockLog)
            {
                return "botaniastory:livingwood";
            }

            // 3. КАМЕНЬ: Превращаю любой камень в Жизнекамень
            if (block.BlockMaterial == EnumBlockMaterial.Stone)
            {
                return "botaniastory:livingrock";
            }

            return null; // Если это земля, песок, доски и т.д. — ничего не делает
        }

        private void OnTick(float dt)
        {
            foreach (Vec3i offset in offsets)
            {
                BlockPos targetPos = Pos.AddCopy(offset.X, offset.Y, offset.Z);
                Block block = Api.World.BlockAccessor.GetBlock(targetPos);

                string outputCode = GetRecipeOutput(block);

                if (outputCode != null)
                {
                    if (Api.Side == EnumAppSide.Client)
                    {
                        SpawnParticles(targetPos);
                        continue;
                    }

                    if (!progress.ContainsKey(offset)) progress[offset] = 0;
                    progress[offset]++;

                    // 30 секунд магии
                    if (progress[offset] >= 30)
                    {
                        Block resultBlock = Api.World.GetBlock(new AssetLocation(outputCode));
                        if (resultBlock != null)
                        {
                            Api.World.BlockAccessor.SetBlock(resultBlock.Id, targetPos);

                            // ЗВУКИ
                            if (outputCode == "botaniastory:livingwood")
                            {
                                Api.World.PlaySoundAt(new AssetLocation("game:sounds/walk/wood1"), targetPos.X, targetPos.Y, targetPos.Z);
                            }
                            else if (outputCode == "botaniastory:livingrock")
                            {
                                Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/stonecrush"), targetPos.X, targetPos.Y, targetPos.Z);
                            }
                        }
                        progress[offset] = 0;
                    }
                }
                else
                {
                    // Если блок убрали или он не подходит - сбрасываем прогресс
                    if (Api.Side == EnumAppSide.Server)
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
            Api.World.SpawnParticles(particles);
        }
    }
}