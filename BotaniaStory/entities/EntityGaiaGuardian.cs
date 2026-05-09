using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace BotaniaStory.entities
{
    public class EntityGaiaGuardian : EntityHumanoid
    {
        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            // Здесь можно инициализировать частицы, звуки при спавне или эффекты свечения
            if (api.Side == EnumAppSide.Server)
            {
                // Пример: Защита от падения
                WatchedAttributes.SetFloat("fallDamageMultiplier", 0f);
            }
        }

        public override void OnHurt(DamageSource damageSource, float damage)
        {
            // Здесь можно ограничить максимальный урон за один удар, как в оригинальной Botania
            if (damage > 15f) damage = 15f;

            base.OnHurt(damageSource, damage);
        }
    }
}