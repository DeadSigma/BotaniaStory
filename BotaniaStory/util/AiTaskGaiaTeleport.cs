using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory.entities.ai
{
    public class AiTaskGaiaTeleport : AiTaskBase
    {
        private int cooldownMs = 5000;
        private float range = 15f;
        private float maxDistanceFromSpawn = 20f;

        private long lastTeleportMs;

        public AiTaskGaiaTeleport(EntityAgent entity, JsonObject taskConfig, JsonObject fallbackConfig)
            : base(entity, taskConfig, fallbackConfig)
        {
            if (taskConfig != null)
            {
                cooldownMs = taskConfig["cooldownMs"].AsInt(5000);
                range = taskConfig["range"].AsFloat(15f); // Синхронизировано с твоим JSON (там range: 15)
                maxDistanceFromSpawn = taskConfig["maxDistanceFromSpawn"].AsFloat(20f);
            }
        }

        public override bool ShouldExecute()
        {
            // БЛОКИРОВКА ТЕЛЕПОРТА: если Гайа сейчас левитирует в центре, запрещаем телепортацию
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

            double tx = target.Pos.X + offsetX;
            double tz = target.Pos.Z + offsetZ;

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
            entity.Pos.SetPos(tx + 0.5, spawn.Y + 1.0, tz + 0.5);

            entity.World.PlaySoundAt(new AssetLocation("botaniastory", "sounds/enderman_teleport"), entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
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