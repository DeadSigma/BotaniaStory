using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory.entities.ai
{
    public class AiTaskGaiaSpawnMobs : AiTaskBase
    {
        private int mobsSpawned = 0;
        private float spawnCooldown = 0f;
        private int totalMobsToSpawn = 15;

        private const double LevitationHeight = 12.0;

        public AiTaskGaiaSpawnMobs(EntityAgent entity, JsonObject taskConfig, JsonObject fallbackConfig)
            : base(entity, taskConfig, fallbackConfig)
        {
        }

        public override bool ShouldExecute()
        {
            int stage = entity.WatchedAttributes.GetInt("spawnedMobsStage", 0);
            if (stage >= 2) return false; // Обе стадии уже пройдены

            EntityBehaviorHealth bh = entity.GetBehavior<EntityBehaviorHealth>();
            if (bh == null || bh.MaxHealth <= 0) return false;

            float healthPercentage = bh.Health / bh.MaxHealth;

            if (stage == 0 && healthPercentage <= 0.7f) return true;

            if (stage == 1 && healthPercentage <= 0.3f) return true;

            return false;
        }

        public override void StartExecute()
        {
            if (entity.World.Side != EnumAppSide.Server) return;

            // Переход на следующую стадию
            int currentStage = entity.WatchedAttributes.GetInt("spawnedMobsStage", 0);
            entity.WatchedAttributes.SetInt("spawnedMobsStage", currentStage + 1);

            entity.WatchedAttributes.SetBool("isLevitating", true);

            entity.WatchedAttributes.SetFloat("levitationGraceTimer", 12.0f);

            // Если игрок не убьет мобов за это время, босс все равно упадет
            entity.WatchedAttributes.SetFloat("maxLevitationTimer", 72.0f);

            Vec3d center = GetArenaCenter();

            // Взлёт на 12 блоков вверх
            entity.Pos.SetPos(center.X + 0.5, center.Y + LevitationHeight, center.Z + 0.5);
            entity.Pos.Motion.Set(0, 0, 0);

            entity.PositionBeforeFalling.Set(entity.Pos.X, center.Y, entity.Pos.Z);

            entity.IsTeleport = true;

            entity.World.PlaySoundAt(new AssetLocation("botaniastory", "sounds/gaia_teleport"), entity.Pos.X, entity.Pos.Y, entity.Pos.Z);

            mobsSpawned = 0;
            spawnCooldown = 0f;
        }

        public override bool ContinueExecute(float dt)
        {
            if (mobsSpawned >= totalMobsToSpawn) return false;

            spawnCooldown -= dt;
            if (spawnCooldown <= 0)
            {
                spawnCooldown = 0.5f; // Задержка между спавном каждого моба
                SpawnOneMob();
                mobsSpawned++;
            }

            return true;
        }

        private void SpawnOneMob()
        {
            if (entity.World.Side != EnumAppSide.Server) return;

            Vec3d center = GetArenaCenter();
            Random rand = entity.World.Rand;

            int stage = entity.WatchedAttributes.GetInt("spawnedMobsStage", 1);
            string[] mobsToSpawn;

            // Выбор пула монстров в зависимости от стадии
            if (stage == 1)
            {
                // Стадия 1
                mobsToSpawn = new[] { "drifter-normal", "drifter-surface", "locust-bronze" };
            }
            else
            {
                // Стадия 2
                mobsToSpawn = new[] { "drifter-deep", "locust-bronze", "drifter-corrupt", "locust-corrupt-sawblade" };
            }

            string code = mobsToSpawn[rand.Next(mobsToSpawn.Length)];
            EntityProperties type = entity.World.GetEntityType(new AssetLocation(code));

            if (type == null)
            {
                if (entity.World.Api is ICoreServerAPI sapi)
                {
                    IPlayer player = sapi.World.NearestPlayer(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
                    if (player is IServerPlayer sPlayer)
                    {
                        sapi.SendIngameError(sPlayer, "gaiaspawn_error", $"[BotaniaStory] Ошибка: код моба '{code}' не найден!");
                    }
                }
                return;
            }

            double angle = rand.NextDouble() * GameMath.TWOPI;
            double dist = 2.0 + rand.NextDouble() * 5.0; // Спавн в радиусе 2-7 блоков от босса
            int x = (int)Math.Floor(center.X + Math.Cos(angle) * dist);
            int z = (int)Math.Floor(center.Z + Math.Sin(angle) * dist);
            double y = center.Y + LevitationHeight;

            Entity mob = entity.World.ClassRegistry.CreateEntity(type);

            mob.Pos.SetPos(x + 0.5, y, z + 0.5);
            mob.Pos.Dimension = entity.Pos.Dimension;
            mob.Pos.SetYaw((float)(rand.NextDouble() * GameMath.TWOPI));
            mob.Pos.Motion.Set(0, 0, 0);

            mob.PositionBeforeFalling.Set(mob.Pos.X, center.Y, mob.Pos.Z);

            mob.Attributes.SetString("origin", "gaiaguardian");
            mob.WatchedAttributes.SetLong("spawnedByGaia", entity.EntityId);

            entity.World.SpawnEntity(mob);

            entity.World.PlaySoundAt(new AssetLocation("botaniastory", "sounds/gaia_teleport"), mob.Pos.X, mob.Pos.Y, mob.Pos.Z);
        }

        private Vec3d GetArenaCenter()
        {
            return new Vec3d(
                entity.WatchedAttributes.GetDouble("gaiaSpawnPosX", entity.Pos.X),
                entity.WatchedAttributes.GetDouble("gaiaSpawnPosY", entity.Pos.Y),
                entity.WatchedAttributes.GetDouble("gaiaSpawnPosZ", entity.Pos.Z));
        }
    }
}