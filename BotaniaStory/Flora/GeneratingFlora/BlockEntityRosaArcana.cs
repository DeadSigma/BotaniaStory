using botaniastory;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BlockEntityRosaArcana : BlockEntityGeneratingFlower
    {
        private int spawnCooldown = 0;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            MaxMana = 6000;

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 500);
            }
            else
            {
                RegisterGameTickListener(OnClientTick, 100);
            }
        }

        private void OnServerTick(float dt)
        {
            bool dirty = false;

            if (spawnCooldown > 0) spawnCooldown--;

            if (CurrentMana < MaxMana)
            {
                IPlayer[] players = Api.World.GetPlayersAround(Pos.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3);

                foreach (IPlayer player in players)
                {
                    if (player?.Entity == null || !player.Entity.Alive) continue;

                    double currentStability = player.Entity.WatchedAttributes.GetDouble("temporalStability", 1.0);

                    // Цветок сосет ману, пока стабильность больше нуля
                    if (currentStability > 0.0)
                    {
                        double drainAmount = 0.02; // 2% за тик
                        double newStability = Math.Max(0.0, currentStability - drainAmount);

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

                        break;
                    }
                    // Если стабильность УЖЕ 0, мана не идет, но мы продолжаем наказывать АФКшера!
                    else if (currentStability <= 0.0)
                    {
                        ApplyZeroStabilityPunishment(player);
                        break;
                    }
                }
            }

            ProcessManaTransfer(ref dirty);
            if (dirty) MarkDirty(true);
        }

        // ЛОГИКА НАКАЗАНИЯ ПРИ 0% СТАБИЛЬНОСТИ
        private void ApplyZeroStabilityPunishment(IPlayer player)
        {
            // 1. Спавн саранчи-пилы каждые 10 секунд (20 тиков)
            if (spawnCooldown <= 0)
            {
                SpawnPunishmentMob("locust-corrupt-sawblade");
                spawnCooldown = 20;
            }

            // 2. Кастомный урон от розы. 
            // 1.0 урона каждые 0.5 сек = 2 ХП в секунду базово.
            float damage = 1.0f;

            // Если игрок замурован, удваиваем урон!
            if (IsPlayerBoxedIn(player.Entity))
            {
                damage *= 2.0f; // 4 ХП в секунду — никакой хил не спасет
            }

            // Наносим урон типом Poison, чтобы он игнорировал броню
            player.Entity.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Block,
                SourcePos = Pos.ToVec3d(),
                Type = EnumDamageType.Poison
            }, damage);
        }

        //  ПРОВЕРКА НА ЗАМУРОВЫВАНИЕ 
        private bool IsPlayerBoxedIn(Entity playerEntity)
        {
            IBlockAccessor ba = Api.World.BlockAccessor;
            BlockPos pPos = playerEntity.Pos.AsBlockPos;

            int solidCount = 0;

            // Проверяем сетку 3х3 вокруг игрока на уровне ног (y = 0) и на уровне головы (y = 1)
            for (int y = 0; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        // Пропускаем центральный столб, где стоит сам игрок
                        if (x == 0 && z == 0) continue;

                        BlockPos checkPos = pPos.AddCopy(x, y, z);
                        Block block = ba.GetBlock(checkPos);

                        // Считаем блок глухой преградой, если он имеет физическую коллизию
                        if (block.Id != 0 && block.CollisionBoxes != null && block.CollisionBoxes.Length > 0)
                        {
                            solidCount++;
                        }
                    }
                }
            }

            // Как работает подсчет из 16 блоков:
            // - Стоит в чистом поле: 0
            // - Стоит прижавшись к прямой стене: ~6
            // - Стоит в углу (2 глухие стены): ~10
            // - В глухой коробке с дверью (проход 1х2): 14
            // - В глухой коробке с "форточкой" (дырка 1х1): 15
            // - Полностью замурован: 16

            // Если 12 или более блоков вокруг игрока закрыты — он злостно обузит механику.
            return solidCount >= 12;
        }

        // ВСПОМОГАТЕЛЬНЫЙ МЕТОД СПАВНА 
        private void SpawnPunishmentMob(string entityCode)
        {
            EntityProperties type = Api.World.GetEntityType(new AssetLocation("game", entityCode));
            if (type == null) return;

            Entity mob = Api.World.ClassRegistry.CreateEntity(type);
            if (mob != null)
            {
                mob.Pos.X = Pos.X + 0.5 + (Api.World.Rand.NextDouble() * 4 - 2);
                mob.Pos.Y = Pos.Y + 1;
                mob.Pos.Z = Pos.Z + 0.5 + (Api.World.Rand.NextDouble() * 4 - 2);
                mob.Pos.SetFrom(mob.Pos);

                Api.World.SpawnEntity(mob);
            }
        }

        private void OnClientTick(float dt)
        {
            if (CurrentMana > 0 && CurrentMana < MaxMana && Api.World.Rand.NextDouble() < 0.1)
            {
                SimpleParticleProperties aura = new SimpleParticleProperties(
                    1, 1, ColorUtil.ToRgba(255, 255, 100, 255),
                    new Vec3d(Pos.X + 0.35, Pos.Y + 0.1, Pos.Z + 0.35),
                    new Vec3d(Pos.X + 0.65, Pos.Y + 0.5, Pos.Z + 0.65),
                    new Vec3f(-0.2f, 0.2f, -0.2f),
                    new Vec3f(0.2f, 0.8f, 0.2f),
                    1f, 0f, 0.5f, 1f, EnumParticleModel.Quad
                );

                Api.World.SpawnParticles(aura);
            }
        }
    }
}