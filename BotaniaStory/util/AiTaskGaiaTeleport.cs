using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using BotaniaStory.entities;

namespace BotaniaStory.entities.ai
{
    public class AiTaskGaiaTeleport : AiTaskBase
    {
        private int cooldownMs = 5000;
        private float range = 15f;
        private float maxDistanceFromSpawn = EntityGaiaGuardian.ArenaRadius;

        private long lastTeleportMs;

        public AiTaskGaiaTeleport(EntityAgent entity, JsonObject taskConfig, JsonObject fallbackConfig)
            : base(entity, taskConfig, fallbackConfig)
        {
            if (taskConfig != null)
            {
                cooldownMs = taskConfig["cooldownMs"].AsInt(5000);
                range = taskConfig["range"].AsFloat(15f);
                // Телепорт не может закинуть Гайю дальше границы арены (жестко ограничено радиусом)
                maxDistanceFromSpawn = Math.Min(taskConfig["maxDistanceFromSpawn"].AsFloat(EntityGaiaGuardian.ArenaRadius), EntityGaiaGuardian.ArenaRadius);
            }
        }

        public override bool ShouldExecute()
        {
            if (entity.WatchedAttributes.GetFloat("gaiaBirthTimer", 0f) > 0f) return false;

            // Если Гайа сейчас левитирует в центре, тоже запрещаем телепортацию
            if (entity.WatchedAttributes.GetBool("isLevitating", false)) return false;

            if (entity.World.ElapsedMilliseconds - lastTeleportMs < cooldownMs) return false;

            IPlayer targetPlayer = entity.World.NearestPlayer(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            if (targetPlayer?.Entity == null) return false;
            if (targetPlayer.Entity.Pos.DistanceTo(entity.Pos) > 20) return false;

            return true;
        }

        public override void StartExecute()
        {
            lastTeleportMs = entity.World.ElapsedMilliseconds;

            IPlayer targetPlayer = entity.World.NearestPlayer(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            if (targetPlayer?.Entity == null) return;
            Entity target = targetPlayer.Entity;

            Random rand = entity.World.Rand;
            double offsetX = (rand.NextDouble() - 0.5) * range * 2;
            double offsetZ = (rand.NextDouble() - 0.5) * range * 2;

            // Центрирование +0.5 учитываем ДО клампа, чтобы финальная точка гарантированно была внутри радиуса
            double tx = target.Pos.X + offsetX + 0.5;
            double tz = target.Pos.Z + offsetZ + 0.5;

            Vec3d spawn = GetSpawnPos();
            double ddx = tx - spawn.X;
            double ddz = tz - spawn.Z;
            double d = Math.Sqrt(ddx * ddx + ddz * ddz);
            if (d > maxDistanceFromSpawn && d > 1e-4)
            {
                double k = maxDistanceFromSpawn / d;
                tx = spawn.X + ddx * k;
                tz = spawn.Z + ddz * k;
            }

            // Перемещаем Гайю, используя исключительно Pos
            entity.Pos.SetPos(tx, spawn.Y + 1.0, tz);

            entity.World.PlaySoundAt(new AssetLocation("botaniastory", "sounds/gaia_teleport"), entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
        }

        public override bool ContinueExecute(float dt) => false;

        private Vec3d GetSpawnPos()
        {
            return new Vec3d(
                entity.WatchedAttributes.GetDouble("gaiaSpawnPosX", entity.Pos.X),
                entity.WatchedAttributes.GetDouble("gaiaSpawnPosY", entity.Pos.Y),
                entity.WatchedAttributes.GetDouble("gaiaSpawnPosZ", entity.Pos.Z));
        }
    }
}