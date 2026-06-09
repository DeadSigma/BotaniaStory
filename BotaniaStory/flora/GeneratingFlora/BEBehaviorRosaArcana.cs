using botaniastory;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BEBehaviorRosaArcana : BEBehaviorGeneratingFlower
    {
        private int spawnCooldown = 0;

        // Обязательный конструктор
        public BEBehaviorRosaArcana(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            MaxMana = 6000;

            if (api.Side == EnumAppSide.Server)
            {
                this.Blockentity.RegisterGameTickListener(OnServerTick, 500);
            }
            else
            {
                this.Blockentity.RegisterGameTickListener(OnClientTick, 100);
            }
        }

        private void OnServerTick(float dt)
        {
            bool dirty = false;

            if (spawnCooldown > 0) spawnCooldown--;

            if (CurrentMana < MaxMana)
            {
                // 1. Получаем центральную точку цветка
                Vec3d flowerPos = this.Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5);

                // 2. Обходим оптимизацию Tungsten, перебирая всех игроков напрямую
                foreach (IPlayer player in this.Api.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null || !player.Entity.Alive) continue;

                    // 3. Проверяем дистанцию вручную (куб 3x3x3 вокруг цветка)
                    double dx = Math.Abs(player.Entity.Pos.X - flowerPos.X);
                    double dy = Math.Abs(player.Entity.Pos.Y - flowerPos.Y);
                    double dz = Math.Abs(player.Entity.Pos.Z - flowerPos.Z);

                    // Если игрок в радиусе 3 блоков
                    if (dx <= 3 && dy <= 3 && dz <= 3)
                    {
                        double currentStability = player.Entity.WatchedAttributes.GetDouble("temporalStability", 1.0);

                        // Цветок сосет ману, пока стабильность больше нуля
                        if (currentStability > 0.0)
                        {
                            double drainAmount = 0.02; // 2% за тик
                            double newStability = Math.Max(0.0, currentStability - drainAmount);

                            // SetDouble сам помечает атрибут для сетевой синхронизации
                            player.Entity.WatchedAttributes.SetDouble("temporalStability", newStability);

                            CurrentMana += 60;
                            if (CurrentMana > MaxMana) CurrentMana = MaxMana;
                            dirty = true;

                            // Проверяем наказания по стабильности
                            if (newStability <= 0.0)
                            {
                                ApplyZeroStabilityPunishment(player);
                            }
                            else if (newStability < 0.15 && spawnCooldown <= 0)
                            {
                                SpawnPunishmentMob("drifter-deep");
                                spawnCooldown = 20; // 10 секунд кулдауна
                            }

                            // Выходим из цикла, так как цветок работает только с одним игроком за тик
                            break;
                        }
                        else if (currentStability <= 0.0)
                        {
                            ApplyZeroStabilityPunishment(player);
                            break;
                        }
                    }
                }
            }

            ProcessManaTransfer(ref dirty);
            if (dirty) this.Blockentity.MarkDirty(true);
        }

        private void ApplyZeroStabilityPunishment(IPlayer player)
        {
            if (spawnCooldown <= 0)
            {
                SpawnPunishmentMob("locust-corrupt-sawblade");
                spawnCooldown = 20;
            }

            float damage = 1.0f;

            if (IsPlayerBoxedIn(player.Entity))
            {
                damage *= 2.0f;
            }

            player.Entity.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Block,
                SourcePos = this.Blockentity.Pos.ToVec3d(),
                Type = EnumDamageType.Poison
            }, damage);
        }

        private bool IsPlayerBoxedIn(Entity playerEntity)
        {
            IBlockAccessor ba = this.Api.World.BlockAccessor;
            BlockPos pPos = playerEntity.Pos.AsBlockPos;

            int solidCount = 0;

            for (int y = 0; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && z == 0) continue;

                        BlockPos checkPos = pPos.AddCopy(x, y, z);
                        Block block = ba.GetBlock(checkPos);

                        if (block.Id != 0 && block.CollisionBoxes != null && block.CollisionBoxes.Length > 0)
                        {
                            solidCount++;
                        }
                    }
                }
            }

            return solidCount >= 12;
        }

        private void SpawnPunishmentMob(string entityCode)
        {
            EntityProperties type = this.Api.World.GetEntityType(new AssetLocation("game", entityCode));
            if (type == null) return;

            Entity mob = this.Api.World.ClassRegistry.CreateEntity(type);
            if (mob != null)
            {
                mob.Pos.X = this.Blockentity.Pos.X + 0.5 + (this.Api.World.Rand.NextDouble() * 4 - 2);
                mob.Pos.Y = this.Blockentity.Pos.Y + 1;
                mob.Pos.Z = this.Blockentity.Pos.Z + 0.5 + (this.Api.World.Rand.NextDouble() * 4 - 2);
                mob.Pos.SetFrom(mob.Pos);

                this.Api.World.SpawnEntity(mob);
            }
        }

        private void OnClientTick(float dt)
        {
            if (CurrentMana > 0 && CurrentMana < MaxMana && this.Api.World.Rand.NextDouble() < 0.1)
            {
                SimpleParticleProperties aura = new SimpleParticleProperties(
                    1, 1, ColorUtil.ToRgba(255, 255, 100, 255),
                    new Vec3d(this.Blockentity.Pos.X + 0.35, this.Blockentity.Pos.Y + 0.1, this.Blockentity.Pos.Z + 0.35),
                    new Vec3d(this.Blockentity.Pos.X + 0.65, this.Blockentity.Pos.Y + 0.5, this.Blockentity.Pos.Z + 0.65),
                    new Vec3f(-0.2f, 0.2f, -0.2f),
                    new Vec3f(0.2f, 0.8f, 0.2f),
                    1f, 0f, 0.5f, 1f, EnumParticleModel.Quad
                );

                this.Api.World.SpawnParticles(aura);
            }
        }
    }
}