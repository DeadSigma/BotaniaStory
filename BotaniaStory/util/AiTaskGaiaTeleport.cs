using System;
using System.Collections.Generic;
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
        private int cooldownMs = 3500;
        private int rageCooldownMs = 2000;
        private float range = 15f;

        // Минимальное расстояние между старой и новой позицией
        private float minTeleportDistance = 7f;
        private float minRecentTeleportDistance = 5f;
        private int rememberedTeleportPositions = 4;
        private float cooldownJitter = 0.15f;
        private int damageTeleportDelayMinMs = 80;
        private int damageTeleportDelayMaxMs = 180;
        private int dotTeleportCooldownMinMs = 320;
        private int dotTeleportCooldownMaxMs = 650;
        private int dotTeleportBurstDurationMs = 2200;

        private float maxDistanceFromSpawn = EntityGaiaGuardian.ArenaRadius;

        private const int TeleportPositionAttempts = 40;

        // Поверхности ищем только около исходного уровня арены
        // Поэтому земля далеко под летающей ареной не будет считаться
        private const int SurfaceSearchAbove = 12;
        private const int SurfaceSearchBelow = 2;

        private const double SurfaceEpsilon = 0.002;

        // Насколько близко ноги Гайи должны находиться к найденной поверхности,  чтобы считать, что она действительно стоит на ней
        private const double SupportTolerance = 0.35;
        private const float GaiaIICooldownMultiplier = 0.50f;

        private long nextTeleportMs;
        private long damageTeleportBurstUntilMs;
        private readonly Queue<Vec3d> recentTeleportPositions = new Queue<Vec3d>();

        public AiTaskGaiaTeleport(
            EntityAgent entity,
            JsonObject taskConfig,
            JsonObject fallbackConfig)
            : base(entity, taskConfig, fallbackConfig)
        {
            if (taskConfig != null)
            {
                cooldownMs =
                    taskConfig["cooldownMs"].AsInt(3500);
                rageCooldownMs =
                     taskConfig["rageCooldownMs"]
                         .AsInt(2000);
                range =
                    taskConfig["range"].AsFloat(15f);

                minTeleportDistance =
                    taskConfig["minTeleportDistance"].AsFloat(7f);

                minRecentTeleportDistance =
                    taskConfig["minRecentTeleportDistance"].AsFloat(5f);

                rememberedTeleportPositions = Math.Max(
                    1,
                    taskConfig["rememberedTeleportPositions"].AsInt(4)
                );

                cooldownJitter = Math.Max(
                    0f,
                    Math.Min(0.45f, taskConfig["cooldownJitter"].AsFloat(0.15f))
                );

                damageTeleportDelayMinMs = Math.Max(0,
                    taskConfig["damageTeleportDelayMinMs"].AsInt(80));
                damageTeleportDelayMaxMs = Math.Max(damageTeleportDelayMinMs,
                    taskConfig["damageTeleportDelayMaxMs"].AsInt(180));
                dotTeleportCooldownMinMs = Math.Max(120,
                    taskConfig["dotTeleportCooldownMinMs"].AsInt(320));
                dotTeleportCooldownMaxMs = Math.Max(dotTeleportCooldownMinMs,
                    taskConfig["dotTeleportCooldownMaxMs"].AsInt(650));
                dotTeleportBurstDurationMs = Math.Max(500,
                    taskConfig["dotTeleportBurstDurationMs"].AsInt(2200));

                maxDistanceFromSpawn = Math.Min(
                    taskConfig["maxDistanceFromSpawn"]
                        .AsFloat(EntityGaiaGuardian.ArenaRadius),
                    EntityGaiaGuardian.ArenaRadius
                );
            }
        }

        // AI
        public override bool ShouldExecute()
        {
            if (entity.WatchedAttributes.GetFloat(
                    "gaiaBirthTimer",
                    0f) > 0f)
            {
                return false;
            }

            if (entity.WatchedAttributes.GetBool(
                    "isLevitating",
                    false))
            {
                return false;
            }

            long now = entity.World.ElapsedMilliseconds;

            // Первый обычный телепорт ждёт свой интервал. Получение урона
            // может независимо ускорить nextTeleportMs через NotifyDamaged().
            if (nextTeleportMs <= 0)
            {
                ScheduleNextTeleport(now);
                return false;
            }

            if (now < nextTeleportMs)
                return false;

            EntityPlayer target = GetNearestParticipant();
            if (target == null)
            {
                ScheduleNextTeleport(now);
                return false;
            }

            if (target.Pos.HorDistanceTo(entity.Pos) > 20)
                return false;

            return true;
        }


        public override void StartExecute()
        {
            long now = entity.World.ElapsedMilliseconds;

            // Во время DoT-серии назначаем следующий быстрый случайный прыжок.
            // В обычном состоянии возвращаемся к штатному боевому cooldown.
            ScheduleNextTeleport(now);

            EntityPlayer target = GetNearestParticipant();
            if (target == null)
                return;

            Vec3d spawn = GetSpawnPos();

            // 1. Сначала пытаемся выбрать действительно случайную точку около игрока.
            // Точки за кругом отбрасываются, а не прижимаются к противоположной кромке.
            if (TryFindRandomTeleportPosition(
                target,
                spawn,
                out Vec3d destination))
            {
                entity.WatchedAttributes.SetBool(
                    "gaiaTeleportBlocked",
                    false
                );

                TeleportTo(destination);
                return;
            }


            // 2. Случайные точки не подошли. Фолбэк нужен для частично разрушенной арены.
            if (TryFindNearestArenaSupport(
                  entity,
                  entity.Pos.X,
                  entity.Pos.Z,
                  minTeleportDistance,
                  maxDistanceFromSpawn,
                  out destination,
                  true,
                  target.Pos.Y))
            {
                entity.WatchedAttributes.SetBool(
                    "gaiaTeleportBlocked",
                    false
                );

                TeleportTo(destination);
                return;
            }


            entity.WatchedAttributes.SetBool(
                "gaiaTeleportBlocked",
                true
            );
        }


        private EntityPlayer GetNearestParticipant()
        {
            if (entity is EntityGaiaGuardian gaia)
            {
                return gaia.GetNearestRitualParticipant();
            }

            return entity.World.NearestPlayer(
                entity.Pos.X,
                entity.Pos.Y,
                entity.Pos.Z
            )?.Entity;
        }


        private int GetActiveCooldown()
        {
            bool rage =
                entity.WatchedAttributes.GetBool(
                    "gaiaRageMode",
                    false
                );

            int activeCooldown =
                rage
                    ? rageCooldownMs
                    : cooldownMs;

            if (entity.WatchedAttributes.GetInt(
                    "gaiaLevel",
                    1) >= 2)
            {
                activeCooldown =
                    Math.Max(
                        1,
                        (int)Math.Round(
                            activeCooldown *
                            GaiaIICooldownMultiplier
                        )
                    );
            }

            return activeCooldown;
        }


        private void ScheduleNextTeleport(long now)
        {
            if (now < damageTeleportBurstUntilMs)
            {
                nextTeleportMs = now + RandomDelay(
                    ScaleDelayForGaiaLevel(dotTeleportCooldownMinMs),
                    ScaleDelayForGaiaLevel(dotTeleportCooldownMaxMs)
                );
                return;
            }

            int activeCooldown = GetActiveCooldown();

            double jitter =
                1.0 +
                ((entity.World.Rand.NextDouble() * 2.0) - 1.0) *
                cooldownJitter;

            nextTeleportMs =
                now +
                Math.Max(250, (int)Math.Round(activeCooldown * jitter));
        }


        private int ScaleDelayForGaiaLevel(int delayMs)
        {
            if (entity.WatchedAttributes.GetInt("gaiaLevel", 1) < 2)
                return delayMs;

            return Math.Max(1, (int)Math.Round(delayMs * GaiaIICooldownMultiplier));
        }


        private int RandomDelay(int minMs, int maxMs)
        {
            if (maxMs <= minMs) return minMs;
            return minMs + entity.World.Rand.Next(maxMs - minMs + 1);
        }


        /// <summary>
        /// Вызывается EntityGaiaGuardian после фактически полученного урона.
        /// Обычный удар ускоряет один следующий телепорт. Fire/DoT дополнительно
        /// включает короткое окно быстрых хаотичных телепортов.
        /// </summary>
        public void NotifyDamaged(DamageSource damageSource)
        {
            if (entity.World.Side != EnumAppSide.Server) return;
            if (damageSource == null || damageSource.Type == EnumDamageType.Heal) return;
            if (entity.WatchedAttributes.GetFloat("gaiaBirthTimer", 0f) > 0f) return;
            if (entity.WatchedAttributes.GetBool("isLevitating", false)) return;

            long now = entity.World.ElapsedMilliseconds;

            bool continuousDamage =
                damageSource.Type == EnumDamageType.Fire ||
                damageSource.Duration > TimeSpan.Zero ||
                damageSource.DamageOverTimeType != 0;

            if (continuousDamage)
            {
                damageTeleportBurstUntilMs = Math.Max(
                    damageTeleportBurstUntilMs,
                    now + dotTeleportBurstDurationMs
                );
            }

            long requestedTeleportMs = now + RandomDelay(
                ScaleDelayForGaiaLevel(damageTeleportDelayMinMs),
                ScaleDelayForGaiaLevel(damageTeleportDelayMaxMs)
            );

            // Урон может только приблизить телепорт, но никогда не отложить уже
            // запланированный более ранний прыжок.
            if (nextTeleportMs <= 0 || requestedTeleportMs < nextTeleportMs)
            {
                nextTeleportMs = requestedTeleportMs;
            }
        }


        public override bool ContinueExecute(float dt)
        {
            return false;
        }


        // ОБЫЧНЫЙ ТЕЛЕПОРТ
        private bool TryFindRandomTeleportPosition(
            Entity target,
            Vec3d spawn,
            out Vec3d destination)
        {
            Random rand = entity.World.Rand;

            double currentX = entity.Pos.X;
            double currentZ = entity.Pos.Z;

            double minDistanceSq =
                minTeleportDistance *
                minTeleportDistance;

            double arenaRadius =
                Math.Max(
                    1.0,
                    maxDistanceFromSpawn - 0.5
                );

            for (int attempt = 0;
                 attempt < TeleportPositionAttempts;
                 attempt++)
            {
                double angle =
                    rand.NextDouble() *
                    Math.PI *
                    2.0;

                double distance =
                    3.0 +
                    rand.NextDouble() *
                    Math.Max(
                        0.0,
                        range - 3.0
                    );

                double candidateX =
                    target.Pos.X +
                    Math.Cos(angle) *
                    distance;

                double candidateZ =
                    target.Pos.Z +
                    Math.Sin(angle) *
                    distance;


                // Не прижимаем точку к краю арены: это создавало заметные
                // повторяющиеся телепорты по окружности. Просто пробуем другую точку.
                double fromSpawnX = candidateX - spawn.X;
                double fromSpawnZ = candidateZ - spawn.Z;

                if (fromSpawnX * fromSpawnX +
                    fromSpawnZ * fromSpawnZ >
                    arenaRadius * arenaRadius)
                {
                    continue;
                }

                double dx =
                    candidateX - currentX;

                double dz =
                    candidateZ - currentZ;

                if (dx * dx + dz * dz <
                    minDistanceSq)
                {
                    continue;
                }

                if (IsNearRecentTeleport(candidateX, candidateZ))
                {
                    continue;
                }


                if (!TryFindStandingPosition(
                     entity,
                     candidateX,
                     candidateZ,
                     spawn,
                     maxDistanceFromSpawn,
                     out destination,

                     // Боевой телепорт:
                     // верхнего Y-предела нет.
                     true,

                     // Предпочитаем поверхность примерно
                     // на высоте текущей цели.
                     target.Pos.Y))
                {
                    continue;
                }

                return true;
            }

            destination = null;
            return false;
        }


        public static bool TryFindStandingPosition(
              EntityAgent entity,
              double x,
              double z,
              Vec3d spawn,
              double arenaRadius,
              out Vec3d destination,
              bool unlimitedUp = false,
              double preferredY = double.NaN)
        {
            destination = null;

            // Координата должна находиться внутри арены
            double arenaDx =
                x - spawn.X;

            double arenaDz =
                z - spawn.Z;

            if (arenaDx * arenaDx +
                arenaDz * arenaDz >
                arenaRadius * arenaRadius)
            {
                return false;
            }


            IBlockAccessor blockAccessor =
                entity.World.BlockAccessor;

            int blockX =
                (int)Math.Floor(x);

            int blockZ =
                (int)Math.Floor(z);

            double localX =
                x - blockX;

            double localZ =
                z - blockZ;



            int maxY;

            if (unlimitedUp)
            {
                // Не сканируем всю высоту мира для каждой случайной X/Z-точки.
                // Для боевого телепорта достаточно области около уровня арены
                // и текущей высоты цели (если игрок построил платформу выше).
                double referenceY =
                    double.IsNaN(preferredY)
                        ? spawn.Y
                        : Math.Max(spawn.Y, preferredY);

                maxY = Math.Min(
                    blockAccessor.MapSizeY - 2,
                    (int)Math.Ceiling(referenceY + SurfaceSearchAbove)
                );
            }
            else
            {
                maxY =
                    (int)Math.Ceiling(
                        spawn.Y +
                        SurfaceSearchAbove
                    );
            }

            int minY =
                (int)Math.Floor(
                    spawn.Y -
                    SurfaceSearchBelow
                );



            // Если игрок построил платформу над ареной, сначала найдём платформу, а не блок под ней

            Vec3d bestPosition = null;
            double bestVerticalDistance = double.MaxValue;

            BlockPos blockPos = new BlockPos(entity.Pos.Dimension);

            for (int y = maxY;
                 y >= minY;
                 y--)
            {
                blockPos.Set(blockX, y, blockZ);

                Block block =
                    blockAccessor.GetBlock(
                        blockPos,
                        BlockLayersAccess.MostSolid
                    );

                if (block == null ||
                    block.Id == 0)
                {
                    continue;
                }


                Cuboidf[] boxes =
                    block.GetCollisionBoxes(
                        blockAccessor,
                        blockPos
                    );

                if (boxes == null ||
                    boxes.Length == 0)
                {
                    continue;
                }


                double highestSurface =
                    double.MinValue;


                foreach (Cuboidf box in boxes)
                {
                    if (box == null)
                        continue;

                    // Collision box должен реально  находиться под выбранной точкой X/Z
                    if (localX < box.X1 ||
                        localX > box.X2 ||
                        localZ < box.Z1 ||
                        localZ > box.Z2)
                    {
                        continue;
                    }

                    double surfaceY =
                        y + box.Y2;

                    if (surfaceY >
                        highestSurface)
                    {
                        highestSurface =
                            surfaceY;
                    }
                }


                if (highestSurface ==
                    double.MinValue)
                {
                    continue;
                }


                double standingY =
                    highestSurface +
                    SurfaceEpsilon;


                Vec3d candidatePos =
                    new Vec3d(
                        x,
                        standingY,
                        z
                    );



                // Проверяем, помещается ли Гайа целиком


                if (entity.World.CollisionTester.IsColliding(
                    blockAccessor,
                    entity.CollisionBox,
                    candidatePos,
                    false))
                {
                    continue;
                }


                if (!unlimitedUp)
                {
                    destination = candidatePos;
                    return true;
                }


                double verticalDistance;

                if (double.IsNaN(preferredY))
                {
                    verticalDistance = 0;
                }
                else
                {
                    verticalDistance =
                        Math.Abs(
                            candidatePos.Y -
                            preferredY
                        );
                }

                if (bestPosition == null ||
                    verticalDistance < bestVerticalDistance)
                {
                    bestPosition =
                        candidatePos;

                    bestVerticalDistance =
                        verticalDistance;
                }
            }


            if (bestPosition != null)
            {
                destination =
                    bestPosition;

                return true;
            }

            return false;
        }

        public static int CountArenaSupportColumns(
     EntityAgent entity,
     int stopAfter = int.MaxValue)
        {
            Vec3d spawn = GetSpawnPos(entity);

            double arenaRadius =
                EntityGaiaGuardian.ArenaRadius;

            double arenaRadiusSq =
                arenaRadius * arenaRadius;

            int minX =
                (int)Math.Floor(spawn.X - arenaRadius);

            int maxX =
                (int)Math.Ceiling(spawn.X + arenaRadius);

            int minZ =
                (int)Math.Floor(spawn.Z - arenaRadius);

            int maxZ =
                (int)Math.Ceiling(spawn.Z + arenaRadius);

            // Проверяется несущий слой на блок ниже поверхности арены
            int arenaSurfaceY =
                (int)Math.Floor(spawn.Y - SurfaceEpsilon);

            int supportY =
                arenaSurfaceY - 2;

            IBlockAccessor blockAccessor =
                entity.World.BlockAccessor;

            BlockPos blockPos =
                new BlockPos(entity.Pos.Dimension);

            int count = 0;

            for (int bx = minX; bx <= maxX; bx++)
            {
                for (int bz = minZ; bz <= maxZ; bz++)
                {
                    double px = bx + 0.5;
                    double pz = bz + 0.5;

                    double dx = px - spawn.X;
                    double dz = pz - spawn.Z;

                    if (dx * dx + dz * dz > arenaRadiusSq)
                        continue;

                    blockPos.Set(
                        bx,
                        supportY,
                        bz
                    );

                    Block block =
                        blockAccessor.GetBlock(
                            blockPos,
                            BlockLayersAccess.MostSolid
                        );

                    if (block == null ||
                        block.Id == 0)
                    {
                        continue;
                    }

                    Cuboidf[] boxes =
                        block.GetCollisionBoxes(
                            blockAccessor,
                            blockPos
                        );

                    if (boxes == null ||
                        boxes.Length == 0)
                    {
                        continue;
                    }

                    count++;

                    if (count >= stopAfter)
                        return count;
                }
            }

            return count;
        }

        // Поиск опоры на арене

        public static bool TryFindNearestArenaSupport(
             EntityAgent entity,
             double fromX,
             double fromZ,
             double minDistance,
             double arenaRadius,
             out Vec3d destination,
             bool unlimitedUp = false,
             double preferredY = double.NaN)

        {
            destination = null;

            Vec3d spawn =
                GetSpawnPos(entity);

            arenaRadius =
                Math.Min(
                    arenaRadius,
                    EntityGaiaGuardian.ArenaRadius
                );

            double arenaRadiusSq =
                arenaRadius *
                arenaRadius;

            double minDistanceSq =
                minDistance *
                minDistance;

            double bestDistanceSq =
                double.MaxValue;


            int minX =
                (int)Math.Floor(
                    spawn.X -
                    arenaRadius
                );

            int maxX =
                (int)Math.Ceiling(
                    spawn.X +
                    arenaRadius
                );

            int minZ =
                (int)Math.Floor(
                    spawn.Z -
                    arenaRadius
                );

            int maxZ =
                (int)Math.Ceiling(
                    spawn.Z +
                    arenaRadius
                );


            for (int bx = minX;
                 bx <= maxX;
                 bx++)
            {
                for (int bz = minZ;
                     bz <= maxZ;
                     bz++)
                {
                    double px =
                        bx + 0.5;

                    double pz =
                        bz + 0.5;


                    // Внутри арены?

                    double arenaDx =
                        px - spawn.X;

                    double arenaDz =
                        pz - spawn.Z;

                    if (arenaDx * arenaDx +
                        arenaDz * arenaDz >
                        arenaRadiusSq)
                    {
                        continue;
                    }


                    // Достаточно далеко от текущей позиции?

                    double dx =
                        px - fromX;

                    double dz =
                        pz - fromZ;

                    double distSq =
                        dx * dx +
                        dz * dz;

                    if (distSq <
                        minDistanceSq)
                    {
                        continue;
                    }


                    // Уже есть более близкая точка
                    if (distSq >=
                        bestDistanceSq)
                    {
                        continue;
                    }



                    // Есть ли здесь поверхность?


                    if (!TryFindStandingPosition(
                           entity,
                           px,
                           pz,
                           spawn,
                           arenaRadius,
                           out Vec3d candidate,
                           unlimitedUp,
                           preferredY))
                    {
                        continue;
                    }


                    bestDistanceSq =
                        distSq;

                    destination =
                        candidate;
                }
            }


            return destination != null;
        }



        // МЕТОДЫ ДЛЯ EntityGaiaGuardian

        public static bool HasImmediateArenaSupport(
            EntityAgent entity)
        {
            Vec3d spawn =
                GetSpawnPos(entity);

            if (!TryFindStandingPosition(
                entity,
                entity.Pos.X,
                entity.Pos.Z,
                spawn,
                EntityGaiaGuardian.ArenaRadius,
                out Vec3d standingPos))
            {
                return false;
            }


            // Ноги должны находиться практически непосредственно над поверхностью
            double difference =
                entity.Pos.Y -
                standingPos.Y;

            return difference >= -0.1 &&
                   difference <= SupportTolerance;
        }


        public static bool TryEmergencyTeleportToNearestSupport(
     EntityAgent entity,
     out Vec3d destination)
        {
            return TryFindNearestArenaSupport(
                entity,
                entity.Pos.X,
                entity.Pos.Z,
                0,
                EntityGaiaGuardian.ArenaRadius,
                out destination
            );
        }



        private bool IsNearRecentTeleport(double x, double z)
        {
            double minDistanceSq =
                minRecentTeleportDistance *
                minRecentTeleportDistance;

            foreach (Vec3d pos in recentTeleportPositions)
            {
                double dx = x - pos.X;
                double dz = z - pos.Z;

                if (dx * dx + dz * dz < minDistanceSq)
                    return true;
            }

            return false;
        }


        private void RememberTeleport(Vec3d destination)
        {
            recentTeleportPositions.Enqueue(
                new Vec3d(destination.X, destination.Y, destination.Z)
            );

            while (recentTeleportPositions.Count > rememberedTeleportPositions)
            {
                recentTeleportPositions.Dequeue();
            }
        }


        // Перемещение
        private void TeleportTo(
            Vec3d destination)
        {
            RememberTeleport(destination);
            entity.Pos.SetPos(
                destination.X,
                destination.Y,
                destination.Z
            );

            entity.Pos.Motion.X = 0;
            entity.Pos.Motion.Y = 0;
            entity.Pos.Motion.Z = 0;


            entity.World.PlaySoundAt(
                new AssetLocation(
                    "botaniastory",
                    "sounds/gaia_teleport"
                ),
                entity.Pos.X,
                entity.Pos.Y,
                entity.Pos.Z
            );
        }



        // Позиция спавна


        private Vec3d GetSpawnPos()
        {
            return GetSpawnPos(entity);
        }


        private static Vec3d GetSpawnPos(
            EntityAgent entity)
        {
            return new Vec3d(
                entity.WatchedAttributes.GetDouble(
                    "gaiaSpawnPosX",
                    entity.Pos.X
                ),
                entity.WatchedAttributes.GetDouble(
                    "gaiaSpawnPosY",
                    entity.Pos.Y
                ),
                entity.WatchedAttributes.GetDouble(
                    "gaiaSpawnPosZ",
                    entity.Pos.Z
                )
            );
        }
        public static int CountIdealArenaColumns(EntityAgent entity)
        {
            Vec3d spawn = GetSpawnPos(entity);

            double arenaRadius =
                EntityGaiaGuardian.ArenaRadius;

            double arenaRadiusSq =
                arenaRadius * arenaRadius;

            int minX =
                (int)Math.Floor(
                    spawn.X - arenaRadius
                );

            int maxX =
                (int)Math.Ceiling(
                    spawn.X + arenaRadius
                );

            int minZ =
                (int)Math.Floor(
                    spawn.Z - arenaRadius
                );

            int maxZ =
                (int)Math.Ceiling(
                    spawn.Z + arenaRadius
                );

            int count = 0;

            for (int bx = minX; bx <= maxX; bx++)
            {
                for (int bz = minZ; bz <= maxZ; bz++)
                {
                    double px = bx + 0.5;
                    double pz = bz + 0.5;

                    double dx =
                        px - spawn.X;

                    double dz =
                        pz - spawn.Z;

                    if (dx * dx + dz * dz <= arenaRadiusSq)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}