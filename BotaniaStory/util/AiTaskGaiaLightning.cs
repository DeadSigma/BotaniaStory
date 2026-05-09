using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory.entities.ai
{
    public class AiTaskGaiaLightning : AiTaskBase
    {
        private long lastAttackMs;
        private int cooldownMs = 8000;
        private float damage = 10f;

        public AiTaskGaiaLightning(EntityAgent entity, JsonObject taskConfig, JsonObject fallbackConfig)
            : base(entity, taskConfig, fallbackConfig)
        {
            if (taskConfig != null)
            {
                cooldownMs = taskConfig["cooldownMs"].AsInt(8000);
                damage = taskConfig["damage"].AsFloat(10f);
            }
        }

        public override bool ShouldExecute()
        {
            if (entity.World.ElapsedMilliseconds - lastAttackMs < cooldownMs) return false;

            IPlayer targetPlayer = entity.World.NearestPlayer(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            if (targetPlayer?.Entity == null) return false;
            if (targetPlayer.Entity.Pos.DistanceTo(entity.Pos) > 15) return false;

            return true;
        }

        public override void StartExecute()
        {
            lastAttackMs = entity.World.ElapsedMilliseconds;

            IPlayer targetPlayer = entity.World.NearestPlayer(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            if (targetPlayer?.Entity == null) return;
            Entity target = targetPlayer.Entity;

            // 1. Наносим наш кастомный урон от босса
            DamageSource dmgSource = new DamageSource()
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = entity,
                Type = EnumDamageType.Electricity
            };
            target.ReceiveDamage(dmgSource, damage);

            // 2. Получаем доступ к ванильной погодной системе сервера
            var sapi = entity.World.Api as ICoreServerAPI;
            var weatherSys = sapi.ModLoader.GetModSystem<WeatherSystemServer>(true);

            // 3. Бьем стандартной молнией прямо в игрока!
            if (weatherSys != null)
            {
                // Метод SpawnLightningFlash сделает всю грязную работу за нас:
                // отправит пакеты клиентам, нарисует вспышку, проиграет звук и заспавнит частицы.
                weatherSys.SpawnLightningFlash(target.Pos.XYZ);
            }
        }

        public override bool ContinueExecute(float dt) => false;
    }
}