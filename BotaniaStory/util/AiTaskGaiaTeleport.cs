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
        private long lastTeleportMs;
        private int cooldownMs = 5000;
        private float range = 10f;

        // Передаем taskConfig и fallbackConfig в базовый класс
        public AiTaskGaiaTeleport(EntityAgent entity, JsonObject taskConfig, JsonObject fallbackConfig)
            : base(entity, taskConfig, fallbackConfig)
        {
            // Читаем конфиг прямо в конструкторе
            if (taskConfig != null)
            {
                cooldownMs = taskConfig["cooldownMs"].AsInt(5000);
                range = taskConfig["range"].AsFloat(10f);
            }
        }

        public override bool ShouldExecute()
        {
            if (entity.World.ElapsedMilliseconds - lastTeleportMs < cooldownMs) return false;

            // Ищем ближайшего игрока в радиусе 20 блоков
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

            BlockPos targetPos = new BlockPos(
                (int)(target.Pos.X + offsetX),
                (int)target.Pos.Y,
                (int)(target.Pos.Z + offsetZ)
            );

            targetPos.Y = entity.World.BlockAccessor.GetTerrainMapheightAt(targetPos);

            entity.World.PlaySoundAt(new AssetLocation("botaniastory", "sounds/enderman_teleport"), entity.Pos.X, entity.Pos.Y, entity.Pos.Z);

            entity.Pos.SetPos(targetPos.X + 0.5, targetPos.Y + 1, targetPos.Z + 0.5);
            entity.Pos.SetFrom(entity.Pos);

            entity.World.PlaySoundAt(new AssetLocation("botaniastory", "sounds/enderman_teleport"), entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
        }

        public override bool ContinueExecute(float dt) => false;
    }
}