using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.util
{
    public class EntityBehaviorPlayerMeditation(Entity entity) : EntityBehavior(entity)
    {
        private const float MeditationThreshold = 10f; // секунд сидения до начала эффекта
        private const float EffectInterval = 10.5f;     // как часто превращаем цветок
        private const float AmbientInterval = 0.25f;   // как часто пускаем "ауру"
        private const int transformRadius = 4;

        private float sitDuration = 0f;
        private float effectTick = 0f;
        private float ambientTick = 0f;

        private readonly string[] flowerColors = [
            "white", "orange", "magenta", "lightblue", "yellow", "lime", "pink", "gray",
            "lightgray", "cyan", "purple", "blue", "brown", "green", "red", "black"
        ];

        public override string PropertyName() => "playermeditation";

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if (entity.World.Side != EnumAppSide.Server) return;

            // Ванильное сидение на G выставляет этот флаг. Он синхронизируется на сервер,
            // в отличие от анимации сидения (она клиентская/предсказанная)
            EntityControls controls = (entity as EntityAgent)?.Controls;
            bool isSitting = controls != null && controls.FloorSitting;

            if (!isSitting)
            {
                sitDuration = 0f;
                effectTick = 0f;
                ambientTick = 0f;
                return;
            }

            sitDuration += deltaTime;

            // Аура частиц идёт с самого начала; после порога становится интенсивнее
            ambientTick += deltaTime;
            if (ambientTick >= AmbientInterval)
            {
                SpawnAuraParticles(sitDuration >= MeditationThreshold);
                ambientTick = 0f;
            }

            // После 10 секунд начинаем превращать соседние цветы
            if (sitDuration >= MeditationThreshold)
            {
                effectTick += deltaTime;
                if (effectTick >= EffectInterval)
                {
                    TryTransformNearbyFlower();
                    effectTick = 0f;
                }
            }
        }

        // Превращение цветов

        private void TryTransformNearbyFlower()
        {
            BlockPos playerPos = entity.Pos.AsBlockPos;
            IBlockAccessor blockAccessor = entity.World.BlockAccessor;

            for (int i = 0; i < 8; i++)
            {
                int xOffset = entity.World.Rand.Next(-transformRadius, transformRadius + 1);
                int zOffset = entity.World.Rand.Next(-transformRadius, transformRadius + 1);
                int yOffset = entity.World.Rand.Next(-2, 3);

                BlockPos checkPos = playerPos.AddCopy(xOffset, yOffset, zOffset);
                Block targetBlock = blockAccessor.GetBlock(checkPos);

                if (targetBlock?.Code == null) continue;

                if (targetBlock.Code.Domain == "game" &&
                    targetBlock.Code.Path.StartsWith("flower-") &&
                    !targetBlock.Code.Path.Contains("mystical"))
                {
                    string randomColor = flowerColors[entity.World.Rand.Next(flowerColors.Length)];

                    AssetLocation mysticalLoc = new("botaniastory", "mysticalflower-" + randomColor + "-free");
                    Block mysticalFlowerBlock = entity.World.GetBlock(mysticalLoc);

                    if (mysticalFlowerBlock != null)
                    {
                        blockAccessor.SetBlock(mysticalFlowerBlock.BlockId, checkPos);
                        SpawnTransformBurst(checkPos);

                        entity.World.PlaySoundAt(
                            new("game", "sounds/block/plant"),
                            checkPos.X + 0.5, checkPos.Y + 0.5, checkPos.Z + 0.5,
                            null, true, 16, 1f);

                        return;
                    }
                }
            }
        }

        // Частицы

        // Аура вокруг игрока, пока он сидит. intense = true после порога медитации
        private void SpawnAuraParticles(bool intense)
        {
            double cx = entity.Pos.X;
            double cy = entity.Pos.Y;
            double cz = entity.Pos.Z;
            const float r = 0.85f;

            int min = intense ? 2 : 1;
            int max = intense ? 5 : 2;

            SimpleParticleProperties aura = new(
                min, max,
                ColorUtil.ToRgba(200, 200, 120, 255), // мягкий фиолетово-розовый (a, r, g, b)
                new Vec3d(cx - r, cy + 0.1, cz - r),
                new Vec3d(cx + r, cy + 1.3, cz + r),
                new Vec3f(-0.05f, 0.15f, -0.05f),
                new Vec3f(0.05f, 0.45f, 0.05f),
                1.2f,     // время жизни
                -0.02f,   // гравитация (вверх)
                0.1f, 0.3f,
                EnumParticleModel.Quad
            )
            {
                SelfPropelled = true
            };

            entity.World.SpawnParticles(aura);
        }

        // Вспышка в момент превращения цветка
        private void SpawnTransformBurst(BlockPos pos)
        {
            SimpleParticleProperties burst = new(
                5, 10,
                ColorUtil.ToRgba(255, 150, 255, 150),
                new Vec3d(pos.X, pos.Y, pos.Z),
                new Vec3d(pos.X + 1, pos.Y + 0.5, pos.Z + 1),
                new Vec3f(-0.5f, 0.5f, -0.5f),
                new Vec3f(0.5f, 1f, 0.5f),
                1.5f, -0.05f, 0.2f, 0.4f,
                EnumParticleModel.Quad
            );

            entity.World.SpawnParticles(burst);
        }
    }
}