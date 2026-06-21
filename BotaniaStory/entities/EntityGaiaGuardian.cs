using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace BotaniaStory.entities
{
    public class EntityGaiaGuardian : EntityHumanoid
    {
        private float minionScanTimer = 0f;

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            if (api.Side == EnumAppSide.Server)
            {
                WatchedAttributes.SetFloat("fallDamageMultiplier", 0f);
            }
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
            SaveSpawnCenter();
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            SaveSpawnCenter();
        }

        private void SaveSpawnCenter()
        {
            if (World.Side == EnumAppSide.Server && !WatchedAttributes.HasAttribute("gaiaSpawnPosX"))
            {
                WatchedAttributes.SetDouble("gaiaSpawnPosX", Pos.X);
                WatchedAttributes.SetDouble("gaiaSpawnPosY", Pos.Y);
                WatchedAttributes.SetDouble("gaiaSpawnPosZ", Pos.Z);
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (World.Side == EnumAppSide.Server)
            {
                Pos.Motion.X = 0;
                Pos.Motion.Z = 0;
                Controls.Forward = false;
                Controls.Backward = false;
                Controls.Left = false;
                Controls.Right = false;

                // ФАЗА ЛЕВИТАЦИИ
                if (WatchedAttributes.GetBool("isLevitating", false))
                {
                    Pos.Motion.Y = 0;
                    Controls.IsFlying = true; // ОТКЛЮЧАЕМ ГРАВИТАЦИЮ ДВИЖКА

                    // ТАЙМЕР БЕЗОПАСНОСТИ 1: Ждем пока все мобы заспавнятся
                    float graceTimer = WatchedAttributes.GetFloat("levitationGraceTimer", 0f);
                    if (graceTimer > 0)
                    {
                        WatchedAttributes.SetFloat("levitationGraceTimer", graceTimer - dt);
                        return; // Прерываем выполнение, ждем
                    }

                    // ТАЙМЕР БЕЗОПАСНОСТИ 2: Защита от софтлока (вечного зависания)
                    float maxTimer = WatchedAttributes.GetFloat("maxLevitationTimer", 60f);
                    if (maxTimer > 0)
                    {
                        WatchedAttributes.SetFloat("maxLevitationTimer", maxTimer - dt);
                    }
                    else
                    {
                        // Если время истекло, принудительно спускаем босса
                        WatchedAttributes.SetBool("isLevitating", false);
                        Controls.IsFlying = false;
                        return;
                    }

                    // Проверка прислужников раз в секунду
                    minionScanTimer += dt;
                    if (minionScanTimer > 1.0f)
                    {
                        minionScanTimer = 0f;

                        Entity[] minions = World.GetEntitiesAround(Pos.XYZ, 40f, 40f,
                            e => e.Alive && e.WatchedAttributes.GetLong("spawnedByGaia", 0) == this.EntityId);

                        // Если прислужников не осталось — Гайа падает вниз до истечения таймера
                        if (minions == null || minions.Length == 0)
                        {
                            WatchedAttributes.SetBool("isLevitating", false);
                            Controls.IsFlying = false; // ВКЛЮЧАЕМ ГРАВИТАЦИЮ ОБРАТНО
                        }
                    }

                    return; // Гайа заморожена в воздухе
                }
                else
                {
                    // На всякий случай гарантируем, что в обычной фазе гравитация работает
                    Controls.IsFlying = false;
                }

                // ОБЫЧНАЯ ФАЗА (поворот к игроку)
                IPlayer nearestPlayer = World.NearestPlayer(Pos.X, Pos.Y, Pos.Z);
                if (nearestPlayer?.Entity != null)
                {
                    double dx = nearestPlayer.Entity.Pos.X - Pos.X;
                    double dz = nearestPlayer.Entity.Pos.Z - Pos.Z;
                    float targetYaw = (float)Math.Atan2(dx, dz);
                    Pos.Yaw = targetYaw;
                }
            }
        }

        public override void OnHurt(DamageSource damageSource, float damage)
        {
            if (WatchedAttributes.GetBool("isLevitating", false)) return;

            if (damage > 1f) damage = 1f;
            base.OnHurt(damageSource, damage);
        }
    }
}