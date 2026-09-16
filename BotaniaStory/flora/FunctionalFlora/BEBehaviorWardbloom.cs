using System;
using System.Collections.Generic;
using BotaniaStory.client.renderers;
using BotaniaStory.items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory.blockentity
{
    public class BEBehaviorWardbloom : BlockEntityBehavior, ILinkableToPool
    {
        public const float MinBarrierRadius = 8f;
        public const float MaxBarrierRadius = 250f;
        public const float BarrierThickness = 1.15f;
        public const float UndergroundRenderDepth = 10f;
        public const double BarrierBottomY = -5.0;
        public const float PushStrength = 0.42f;
        public const int TickIntervalMs = 150;

        // Расход маны рассчитывается для радиуса 250 за игровые сутки
        public const int MaxManaPerDay = 2000000;

        private static readonly float[] RadiusSteps =
        {
            8f, 12f, 16f, 24f, 32f, 48f,
            64f, 96f, 128f, 160f, 200f, 250f
        };

        private const string SpawnBypassAttribute = "botaniastory:wardbloomSpawnBypass";

        private static readonly HashSet<BEBehaviorWardbloom> ActiveServerBarriers = new HashSet<BEBehaviorWardbloom>();
        private static ICoreServerAPI hookedServerApi;
        private static ModSystemRifts hookedRiftSystem;
        private static int spawnBypassDepth;

        public InventoryGeneric FilterInventory;
        public BlockPos LinkedPool { get; set; }
        public bool Active { get; private set; }
        public int ParticleMode { get; private set; }
        public float BarrierRadius { get; private set; } = MinBarrierRadius;
        public string BarrierColor { get; private set; } = "pink";

        private long tickListenerId;
        private WardbloomBarrierRenderer renderer;
        private double lastGameDays;
        private bool hasStoredGameTime;
        private double manaAccumulator;
        public BEBehaviorWardbloom(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (FilterInventory == null)
            {
                FilterInventory = new InventoryGeneric(1, "wardbloom-filter-" + Pos, api);
            }
            else
            {
                FilterInventory.Api = api;
            }

            if (!hasStoredGameTime && api.World.Calendar != null)
            {
                lastGameDays = api.World.Calendar.ElapsedDays;
            }

            if (api.Side == EnumAppSide.Server)
            {
                EnsureServerHooks(api as ICoreServerAPI);
                if (Active) ActiveServerBarriers.Add(this);
                tickListenerId = Blockentity.RegisterGameTickListener(OnTick, TickIntervalMs);
            }
            else if (api is ICoreClientAPI capi)
            {
                renderer = new WardbloomBarrierRenderer(capi, this);
            }
        }

        private void OnTick(float dt)
        {
            double currentGameDays = Api.World.Calendar?.ElapsedDays ?? lastGameDays;
            double elapsedGameDays = currentGameDays - lastGameDays;
            lastGameDays = currentGameDays;

            if (elapsedGameDays < 0)
            {
                elapsedGameDays = 0;
            }

            BlockEntityManaPool pool = GetLinkedPool();
            if (pool == null || pool.CurrentMana <= 0)
            {
                manaAccumulator = 0;
                SetActive(false);
                return;
            }

            manaAccumulator += elapsedGameDays * GetManaPerDay();

            int manaToConsume = (int)Math.Floor(manaAccumulator);
            if (manaToConsume > 0)
            {
                if (pool.CurrentMana < manaToConsume)
                {
                    pool.ConsumeMana(pool.CurrentMana);
                    manaAccumulator = 0;
                    SetActive(false);
                    return;
                }

                pool.ConsumeMana(manaToConsume);
                manaAccumulator -= manaToConsume;
            }

            SetActive(true);
        }

        public int GetManaPerDay()
        {
            double radiusRatio = BarrierRadius / MaxBarrierRadius;
            return Math.Max(1, (int)Math.Round(MaxManaPerDay * radiusRatio * radiusRatio));
        }

        public bool StepRadius(int direction)
        {
            if (direction == 0) return false;

            int currentIndex = FindClosestRadiusStep();
            int nextIndex = GameMath.Clamp(currentIndex + Math.Sign(direction), 0, RadiusSteps.Length - 1);
            if (nextIndex == currentIndex) return false;

            BarrierRadius = RadiusSteps[nextIndex];
            Blockentity.MarkDirty(true);
            return true;
        }

        private int FindClosestRadiusStep()
        {
            int bestIndex = 0;
            float bestDistance = Math.Abs(BarrierRadius - RadiusSteps[0]);

            for (int i = 1; i < RadiusSteps.Length; i++)
            {
                float distance = Math.Abs(BarrierRadius - RadiusSteps[i]);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestIndex = i;
            }

            return bestIndex;
        }

        public void CycleParticleMode()
        {
            ParticleMode = (ParticleMode + 1) % 4;
            Blockentity.MarkDirty(true);
        }

        public float GetParticleDensityMultiplier()
        {
            switch (ParticleMode)
            {
                case 1: return 0.55f;
                case 2: return 0.25f;
                case 3: return 0f;
                default: return 1f;
            }
        }

        public bool TrySetBarrierColor(ItemStack stack)
        {
            string path = stack?.Collectible?.Code?.Path;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("mysticalpowder-", StringComparison.OrdinalIgnoreCase))
                return false;

            string color = path.Substring("mysticalpowder-".Length).ToLowerInvariant();
            if (!IsValidBarrierColor(color)) return false;

            BarrierColor = color;
            Blockentity.MarkDirty(true);
            return true;
        }

        private static bool IsValidBarrierColor(string color)
        {
            switch (color)
            {
                case "white":
                case "orange":
                case "magenta":
                case "lightblue":
                case "yellow":
                case "lime":
                case "pink":
                case "gray":
                case "lightgray":
                case "cyan":
                case "purple":
                case "blue":
                case "brown":
                case "green":
                case "red":
                case "black":
                    return true;
                default:
                    return false;
            }
        }

        public bool HandleInteract(IPlayer byPlayer)
        {
            if (byPlayer == null) return false;

            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!activeSlot.Empty && TrySetBarrierColor(activeSlot.Itemstack))
            {
                if (Api.Side == EnumAppSide.Server)
                {
                    activeSlot.TakeOut(1);
                    activeSlot.MarkDirty();
                    Blockentity.MarkDirty(true);
                }

                return true;
            }

            if (!activeSlot.Empty && activeSlot.Itemstack?.Item is ItemFilterScroll filter && !filter.IsBlacklist)
            {
                if (Api.Side == EnumAppSide.Server && FilterInventory[0].Empty)
                {
                    FilterInventory[0].Itemstack = activeSlot.TakeOut(1);
                    FilterInventory[0].MarkDirty();
                    activeSlot.MarkDirty();
                    Blockentity.MarkDirty(false);
                }

                return true;
            }

            if (activeSlot.Empty && !FilterInventory[0].Empty)
            {
                if (Api.Side == EnumAppSide.Server)
                {
                    ItemStack filterStack = FilterInventory[0].TakeOut(1);
                    if (!byPlayer.InventoryManager.TryGiveItemstack(filterStack))
                    {
                        Api.World.SpawnItemEntity(filterStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }

                    FilterInventory[0].MarkDirty();
                    Blockentity.MarkDirty(false);
                }

                return true;
            }

            return false;
        }

        private BlockEntityManaPool GetLinkedPool()
        {
            if (LinkedPool == null) return null;
            return Api.World.BlockAccessor.GetBlockEntity(LinkedPool) as BlockEntityManaPool;
        }

        private static void EnsureServerHooks(ICoreServerAPI sapi)
        {
            if (sapi == null || ReferenceEquals(hookedServerApi, sapi)) return;

            if (hookedServerApi != null)
            {
                hookedServerApi.Event.OnEntityLoaded -= OnServerEntityAvailable;
                hookedServerApi.Event.OnEntitySpawn -= OnServerEntityAvailable;
                hookedServerApi.Event.OnTrySpawnEntity -= OnServerTrySpawnEntity;
            }

            if (hookedRiftSystem != null)
            {
                hookedRiftSystem.OnTrySpawnRift -= OnServerTrySpawnRift;
            }

            ActiveServerBarriers.Clear();
            hookedServerApi = sapi;
            hookedServerApi.Event.OnEntityLoaded += OnServerEntityAvailable;
            hookedServerApi.Event.OnEntitySpawn += OnServerEntityAvailable;
            hookedServerApi.Event.OnTrySpawnEntity += OnServerTrySpawnEntity;

            hookedRiftSystem = sapi.ModLoader.GetModSystem<ModSystemRifts>();
            if (hookedRiftSystem != null)
            {
                hookedRiftSystem.OnTrySpawnRift += OnServerTrySpawnRift;
            }

            foreach (Entity entity in hookedServerApi.World.LoadedEntities.Values)
            {
                AttachEntityProtection(entity);
            }
        }

        private static bool OnServerTrySpawnEntity(
            IBlockAccessor blockAccessor,
            ref EntityProperties properties,
            Vec3d spawnPosition,
            long herdId)
        {
            if (spawnBypassDepth > 0) return true;
            if (!IsHostileEntity(properties)) return true;
            if (spawnPosition == null) return true;

            return !IsInsideAnyActiveBarrier(
                spawnPosition.X,
                spawnPosition.Y,
                spawnPosition.Z);
        }

        private static void OnServerTrySpawnRift(BlockPos pos, ref EnumHandling handling)
        {
            if (pos == null) return;

            if (IsInsideAnyActiveBarrier(
                pos.X + 0.5,
                pos.Y + 0.5,
                pos.Z + 0.5))
            {
                handling = EnumHandling.PreventDefault;
            }
        }

        private static void OnServerEntityAvailable(Entity entity)
        {
            if (ShouldRejectSpawnedHostile(entity))
            {
                entity.Die(EnumDespawnReason.Removed);
                return;
            }

            AttachEntityProtection(entity);
        }

        internal static void SpawnEntityIgnoringSpawnProtection(IWorldAccessor world, Entity entity)
        {
            if (world == null || entity == null) return;

            // Сущность помечается как разрешённая для спавна внутри барьера
            entity.WatchedAttributes.SetBool(SpawnBypassAttribute, true);

            spawnBypassDepth++;
            try
            {
                world.SpawnEntity(entity);
            }
            finally
            {
                spawnBypassDepth--;
            }
        }

        private static bool ShouldRejectSpawnedHostile(Entity entity)
        {
            if (entity == null || !entity.Alive) return false;
            if (entity.WatchedAttributes.GetBool(SpawnBypassAttribute, false)) return false;
            if (entity.WatchedAttributes.GetLong("spawnedByGaia", 0) != 0) return false;
            if (!IsHostileEntity(entity.Properties)) return false;

            Vec3d center = GetEntityCenter(entity);
            return IsInsideAnyActiveBarrier(center.X, center.Y, center.Z);
        }

        private static bool IsHostileEntity(EntityProperties properties)
        {
            SpawnConditions spawnConditions = properties?.Server?.SpawnConditions;
            if (spawnConditions == null) return false;

            return string.Equals(
                       spawnConditions.Runtime?.Group,
                       "hostile",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       spawnConditions.Worldgen?.Group,
                       "hostile",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInsideAnyActiveBarrier(double x, double y, double z)
        {
            if (ActiveServerBarriers.Count == 0) return false;

            foreach (BEBehaviorWardbloom barrier in ActiveServerBarriers)
            {
                if (barrier.ContainsProtectedPoint(x, y, z)) return true;
            }

            return false;
        }

        private bool ContainsProtectedPoint(double x, double y, double z)
        {
            if (!Active || y < BarrierBottomY) return false;

            double centerX = Pos.X + 0.5;
            double centerY = Pos.Y + 0.15;
            double centerZ = Pos.Z + 0.5;
            double dx = x - centerX;
            double dz = z - centerZ;
            double radiusSq = BarrierRadius * BarrierRadius;

            if (y < centerY)
            {
                return dx * dx + dz * dz < radiusSq;
            }

            double dy = y - centerY;
            return dx * dx + dy * dy + dz * dz < radiusSq;
        }

        private static void AttachEntityProtection(Entity entity)
        {
            if (entity == null) return;

            bool changed = false;

            if (entity is EntityAgent && entity.GetBehavior<EntityBehaviorWardbloomProtection>() == null)
            {
                // Защита сущности добавляется один раз
                entity.SidedProperties.Behaviors.Insert(0, new EntityBehaviorWardbloomProtection(entity));
                changed = true;
            }

            if (entity is IProjectile && entity.GetBehavior<EntityBehaviorWardbloomProjectileProtection>() == null)
            {
                entity.SidedProperties.Behaviors.Add(new EntityBehaviorWardbloomProjectileProtection(entity));
                changed = true;
            }

            if (changed)
            {
                entity.CacheServerBehaviors();
            }
        }

        internal static bool TryBlockAgentMovement(
            Entity entity,
            Vec3d previousCenter,
            Vec3d currentCenter,
            out BEBehaviorWardbloom blockingBarrier)
        {
            blockingBarrier = null;

            if (entity == null || ActiveServerBarriers.Count == 0) return false;

            foreach (BEBehaviorWardbloom barrier in ActiveServerBarriers)
            {
                if (!barrier.BlocksAgentMovement(entity, previousCenter, currentCenter)) continue;

                blockingBarrier = barrier;
                return true;
            }

            return false;
        }

        internal static bool ShouldRemoveProjectile(Entity entity, IProjectile projectile, Vec3d previousPos)
        {
            if (entity == null || projectile == null || ActiveServerBarriers.Count == 0) return false;

            Vec3d currentPos = entity.Pos.XYZ;

            foreach (BEBehaviorWardbloom barrier in ActiveServerBarriers)
            {
                if (barrier.BlocksProjectile(entity, projectile, previousPos, currentPos)) return true;
            }

            return false;
        }

        internal static bool ShouldBlockDamage(Entity target, DamageSource damageSource)
        {
            if (target == null || damageSource == null || damageSource.Type == EnumDamageType.Heal) return false;

            if (ActiveServerBarriers.Count == 0) return false;

            foreach (BEBehaviorWardbloom barrier in ActiveServerBarriers)
            {
                if (barrier.BlocksDamage(target, damageSource)) return true;
            }

            return false;
        }

        private bool BlocksAgentMovement(Entity entity, Vec3d previousCenter, Vec3d currentCenter)
        {
            if (!Active || entity == null || !entity.Alive) return false;
            if (IsPlayerWhitelisted(entity)) return false;

            if (!SegmentMayReachBarrier(previousCenter, currentCenter)) return false;

            int previousSide = GetBarrierRegionSide(previousCenter);
            int currentSide = GetBarrierRegionSide(currentCenter);

            if (previousSide == 0 || currentSide == 0) return false;
            if (previousSide != currentSide) return true;

            return SegmentCrossesBarrier(previousCenter, currentCenter);
        }

        internal void StopCrossingMotion(Entity entity, Vec3d safePos)
        {
            if (entity == null) return;

            Vec3d safeCenter = GetEntityCenterAt(entity, safePos);
            if (!TryGetShellData(safeCenter, out double shellDistance, out double nx, out double ny, out double nz))
                return;

            double side = shellDistance >= 0 ? 1.0 : -1.0;
            double currentNormalSpeed =
                entity.Pos.Motion.X * nx +
                entity.Pos.Motion.Y * ny +
                entity.Pos.Motion.Z * nz;

            const double minimumAwaySpeed = 0.08;
            double targetNormalSpeed = side * minimumAwaySpeed;

            if (side > 0 && currentNormalSpeed >= targetNormalSpeed) return;
            if (side < 0 && currentNormalSpeed <= targetNormalSpeed) return;

            double correction = targetNormalSpeed - currentNormalSpeed;
            entity.Pos.Motion.X += nx * correction;
            entity.Pos.Motion.Y += ny * correction;
            entity.Pos.Motion.Z += nz * correction;
        }

        private bool BlocksDamage(Entity target, DamageSource damageSource)
        {
            if (!Active || target == null || !target.Alive) return false;

            Entity causeEntity = ResolveDamageCause(damageSource);
            if (causeEntity == target) return false;
            if (causeEntity != null && IsPlayerWhitelisted(causeEntity)) return false;

            Vec3d targetPoint = GetEntityCenter(target);
            int targetSide = GetBarrierRegionSide(targetPoint);
            if (targetSide == 0) return false;

            Vec3d sourcePoint = ResolveDamageSourcePoint(damageSource, causeEntity);
            if (sourcePoint == null) return false;

            int sourceSide = GetBarrierRegionSide(sourcePoint);

            // Урон блокируется при атаке через разные стороны барьера
            if (sourceSide != 0 && sourceSide != targetSide)
            {
                return true;
            }

            if (!SegmentMayReachBarrier(sourcePoint, targetPoint)) return false;
            return SegmentCrossesBarrier(sourcePoint, targetPoint);
        }

        private static Entity ResolveDamageCause(DamageSource damageSource)
        {
            if (damageSource.CauseEntity != null)
            {
                return damageSource.CauseEntity;
            }

            if (damageSource.SourceEntity is IProjectile projectile && projectile.FiredBy != null)
            {
                return projectile.FiredBy;
            }

            return damageSource.GetCauseEntity();
        }

        private static Vec3d ResolveDamageSourcePoint(DamageSource damageSource, Entity causeEntity)
        {
            if (causeEntity != null)
            {
                return GetEntityCenter(causeEntity);
            }

            if (damageSource.SourcePos != null)
            {
                return damageSource.SourcePos;
            }

            if (damageSource.SourceEntity != null)
            {
                return GetEntityCenter(damageSource.SourceEntity);
            }

            return null;
        }

        private bool BlocksProjectile(Entity entity, IProjectile projectile, Vec3d previousPos, Vec3d currentPos)
        {
            if (!Active || entity == null || !entity.Alive || projectile.Stuck) return false;
            if (projectile.FiredBy != null && IsPlayerWhitelisted(projectile.FiredBy)) return false;

            if (TryGetShellData(currentPos, out double shellDistance, out _, out _, out _) &&
                Math.Abs(shellDistance) <= BarrierThickness)
            {
                return true;
            }

            if (previousPos != null &&
                SegmentMayReachBarrier(previousPos, currentPos) &&
                SegmentCrossesBarrier(previousPos, currentPos))
            {
                return true;
            }

            if (projectile.FiredBy == null) return false;

            int sourceSide = GetBarrierRegionSide(GetEntityCenter(projectile.FiredBy));
            int projectileSide = GetBarrierRegionSide(currentPos);

            return sourceSide != 0 && projectileSide != 0 && sourceSide != projectileSide;
        }

        private bool SegmentMayReachBarrier(Vec3d from, Vec3d to)
        {
            double centerX = Pos.X + 0.5;
            double centerY = Pos.Y + 0.15;
            double centerZ = Pos.Z + 0.5;
            double radius = BarrierRadius + BarrierThickness;

            double minX = Math.Min(from.X, to.X);
            double maxX = Math.Max(from.X, to.X);
            if (maxX < centerX - radius || minX > centerX + radius) return false;

            double minZ = Math.Min(from.Z, to.Z);
            double maxZ = Math.Max(from.Z, to.Z);
            if (maxZ < centerZ - radius || minZ > centerZ + radius) return false;

            double minY = Math.Min(from.Y, to.Y);
            double maxY = Math.Max(from.Y, to.Y);
            if (maxY < BarrierBottomY || minY > centerY + radius) return false;

            return true;
        }

        private bool SegmentCrossesBarrier(Vec3d from, Vec3d to)
        {
            double centerX = Pos.X + 0.5;
            double centerY = Pos.Y + 0.15;
            double centerZ = Pos.Z + 0.5;

            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double dz = to.Z - from.Z;

            double fx = from.X - centerX;
            double fy = from.Y - centerY;
            double fz = from.Z - centerZ;

            double radiusSq = BarrierRadius * BarrierRadius;

            double cylinderA = dx * dx + dz * dz;
            double cylinderB = 2.0 * (fx * dx + fz * dz);
            double cylinderC = fx * fx + fz * fz - radiusSq;

            if (TryGetSegmentRoots(cylinderA, cylinderB, cylinderC, out double cylinderT1, out double cylinderT2))
            {
                if (IsCylinderIntersection(from.Y + dy * cylinderT1, cylinderT1, centerY)) return true;
                if (IsCylinderIntersection(from.Y + dy * cylinderT2, cylinderT2, centerY)) return true;
            }

            double sphereA = dx * dx + dy * dy + dz * dz;
            double sphereB = 2.0 * (fx * dx + fy * dy + fz * dz);
            double sphereC = fx * fx + fy * fy + fz * fz - radiusSq;

            if (!TryGetSegmentRoots(sphereA, sphereB, sphereC, out double sphereT1, out double sphereT2))
                return false;

            if (IsDomeIntersection(from.Y + dy * sphereT1, sphereT1, centerY)) return true;
            return IsDomeIntersection(from.Y + dy * sphereT2, sphereT2, centerY);
        }

        private static bool TryGetSegmentRoots(
            double a,
            double b,
            double c,
            out double t1,
            out double t2)
        {
            t1 = 0;
            t2 = 0;

            if (a < 0.0000001) return false;

            double discriminant = b * b - 4.0 * a * c;
            if (discriminant < 0) return false;

            double sqrt = Math.Sqrt(discriminant);
            double inverse = 0.5 / a;

            t1 = (-b - sqrt) * inverse;
            t2 = (-b + sqrt) * inverse;
            return true;
        }

        private static bool IsCylinderIntersection(double y, double t, double centerY)
        {
            return t >= 0.0 && t <= 1.0 && y >= BarrierBottomY && y <= centerY;
        }

        private static bool IsDomeIntersection(double y, double t, double centerY)
        {
            return t >= 0.0 && t <= 1.0 && y >= centerY;
        }

        public void ApplyBarrierToEntity(Entity entity)
        {
            if (!Active || entity == null || !entity.Alive) return;
            if (IsPlayerWhitelisted(entity)) return;

            Vec3d point = GetEntityCenter(entity);
            if (!TryGetShellData(point, out double shellDistance, out double nx, out double ny, out double nz)) return;

            double absShellDistance = Math.Abs(shellDistance);
            if (absShellDistance > BarrierThickness) return;

            double side = shellDistance >= 0 ? 1.0 : -1.0;
            double strength = PushStrength * (1.0 - absShellDistance / BarrierThickness) + 0.08;
            double targetNormalSpeed = side * strength;
            double currentNormalSpeed =
                entity.Pos.Motion.X * nx +
                entity.Pos.Motion.Y * ny +
                entity.Pos.Motion.Z * nz;

            double correction = targetNormalSpeed - currentNormalSpeed;

            if (side > 0 && correction < 0) correction = 0;
            if (side < 0 && correction > 0) correction = 0;

            entity.Pos.Motion.X += nx * correction;
            entity.Pos.Motion.Y += ny * correction;
            entity.Pos.Motion.Z += nz * correction;
        }

        private bool TryGetShellData(
            Vec3d point,
            out double shellDistance,
            out double nx,
            out double ny,
            out double nz)
        {
            double centerX = Pos.X + 0.5;
            double centerY = Pos.Y + 0.15;
            double centerZ = Pos.Z + 0.5;
            double dx = point.X - centerX;
            double dy = point.Y - centerY;
            double dz = point.Z - centerZ;

            shellDistance = 0;
            nx = 0;
            ny = 0;
            nz = 0;

            if (point.Y < BarrierBottomY) return false;

            if (dy < 0)
            {
                double horizontalDistSq = dx * dx + dz * dz;
                if (horizontalDistSq < 0.0001) return false;

                double horizontalDist = Math.Sqrt(horizontalDistSq);
                shellDistance = horizontalDist - BarrierRadius;
                nx = dx / horizontalDist;
                nz = dz / horizontalDist;
                return true;
            }

            double distSq = dx * dx + dy * dy + dz * dz;
            if (distSq < 0.0001) return false;

            double dist = Math.Sqrt(distSq);
            shellDistance = dist - BarrierRadius;
            nx = dx / dist;
            ny = dy / dist;
            nz = dz / dist;
            return true;
        }

        private int GetBarrierRegionSide(Vec3d point)
        {
            double centerX = Pos.X + 0.5;
            double centerY = Pos.Y + 0.15;
            double centerZ = Pos.Z + 0.5;
            double dx = point.X - centerX;
            double dy = point.Y - centerY;
            double dz = point.Z - centerZ;

            if (point.Y < BarrierBottomY) return 0;

            if (dy < 0)
            {
                double horizontalDistSq = dx * dx + dz * dz;
                double radiusSq = BarrierRadius * BarrierRadius;
                return horizontalDistSq < radiusSq ? -1 : 1;
            }

            double distSq = dx * dx + dy * dy + dz * dz;
            double domeRadiusSq = BarrierRadius * BarrierRadius;
            return distSq < domeRadiusSq ? -1 : 1;
        }

        private static Vec3d GetEntityCenter(Entity entity)
        {
            return GetEntityCenterAt(entity, entity.Pos.XYZ);
        }

        private static Vec3d GetEntityCenterAt(Entity entity, Vec3d position)
        {
            double centerOffsetY = 0.5;
            if (entity.OriginCollisionBox != null)
            {
                centerOffsetY = (entity.OriginCollisionBox.Y1 + entity.OriginCollisionBox.Y2) * 0.5;
            }

            return new Vec3d(
                position.X,
                position.Y + centerOffsetY,
                position.Z);
        }

        public bool IsPlayerWhitelisted(Entity entity)
        {
            if (!(entity is EntityPlayer player)) return false;

            ItemStack filterStack = FilterInventory?[0]?.Itemstack;
            if (!(filterStack?.Item is ItemFilterScroll filter)) return false;
            if (filter.IsBlacklist) return false;

            return filter.AllowsPlayer(filterStack, player.Player?.PlayerName);
        }

        public Vec3d GetBarrierCenter()
        {
            return Pos.ToVec3d().Add(0.5, 0.15, 0.5);
        }

        private void SetActive(bool value)
        {
            if (Active == value) return;

            Active = value;

            if (Api?.Side == EnumAppSide.Server)
            {
                if (value)
                {
                    ActiveServerBarriers.Add(this);
                }
                else
                {
                    ActiveServerBarriers.Remove(this);
                }
            }

            Blockentity.MarkDirty(false);
        }

        public void AutoFindPool()
        {
            const int searchRadius = 6;

            for (int x = -searchRadius; x <= searchRadius; x++)
            {
                for (int y = -searchRadius; y <= searchRadius; y++)
                {
                    for (int z = -searchRadius; z <= searchRadius; z++)
                    {
                        BlockPos checkPos = Pos.AddCopy(x, y, z);
                        if (!(Api.World.BlockAccessor.GetBlockEntity(checkPos) is BlockEntityManaPool)) continue;

                        LinkedPool = checkPos.Copy();
                        Blockentity.MarkDirty(false);
                        return;
                    }
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("barrierActive", Active);
            tree.SetInt("barrierParticleMode", ParticleMode);
            tree.SetFloat("barrierRadius", BarrierRadius);
            tree.SetString("barrierColor", BarrierColor);
            tree.SetDouble("barrierLastGameDays", lastGameDays);

            if (LinkedPool != null)
            {
                tree.SetInt("poolX", LinkedPool.X);
                tree.SetInt("poolY", LinkedPool.Y);
                tree.SetInt("poolZ", LinkedPool.Z);
            }

            ITreeAttribute filterTree = new TreeAttribute();
            FilterInventory?.ToTreeAttributes(filterTree);
            tree["barrierFilterInv"] = filterTree;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            Active = tree.GetBool("barrierActive", false);

            if (tree.HasAttribute("barrierParticleMode"))
            {
                ParticleMode = GameMath.Clamp(tree.GetInt("barrierParticleMode"), 0, 3);
            }
            else
            {
                ParticleMode = tree.GetBool("barrierParticles", true) ? 0 : 3;
            }

            BarrierRadius = GameMath.Clamp(
                tree.GetFloat("barrierRadius", MinBarrierRadius),
                MinBarrierRadius,
                MaxBarrierRadius);

            string savedColor = tree.GetString("barrierColor", "pink");
            BarrierColor = IsValidBarrierColor(savedColor) ? savedColor : "pink";

            if (tree.HasAttribute("barrierLastGameDays"))
            {
                lastGameDays = tree.GetDouble("barrierLastGameDays");
                hasStoredGameTime = true;
            }

            if (tree.HasAttribute("poolX"))
            {
                LinkedPool = new BlockPos(
                    tree.GetInt("poolX"),
                    tree.GetInt("poolY"),
                    tree.GetInt("poolZ"));
            }
            else
            {
                LinkedPool = null;
            }

            if (FilterInventory == null)
            {
                FilterInventory = new InventoryGeneric(
                    1,
                    "wardbloom-filter-" + Pos,
                    worldForResolving.Api);
            }

            ITreeAttribute filterTree = tree.GetTreeAttribute("barrierFilterInv");
            if (filterTree != null)
            {
                FilterInventory.FromTreeAttributes(filterTree);
                FilterInventory.ResolveBlocksOrItems();
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (tickListenerId != 0)
                Blockentity.UnregisterGameTickListener(tickListenerId);

            if (Api.Side == EnumAppSide.Server)
            {
                ActiveServerBarriers.Remove(this);

                if (FilterInventory != null)
                {
                    FilterInventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }

            renderer?.Dispose();
            renderer = null;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api.Side == EnumAppSide.Server)
            {
                ActiveServerBarriers.Remove(this);
            }

            renderer?.Dispose();
            renderer = null;
        }
    }

    internal sealed class EntityBehaviorWardbloomProtection : EntityBehavior
    {
        private readonly Vec3d previousPos;
        private readonly Vec3d currentPos;
        private readonly Vec3d previousCenter;
        private readonly Vec3d currentCenter;

        public EntityBehaviorWardbloomProtection(Entity entity) : base(entity)
        {
            previousPos = new Vec3d(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            currentPos = new Vec3d();
            previousCenter = new Vec3d();
            currentCenter = new Vec3d();
        }

        public override string PropertyName()
        {
            return "wardbloomdamageprotection";
        }

        public override void OnGameTick(float deltaTime)
        {
            if (entity.World.Side != EnumAppSide.Server || !entity.Alive) return;

            double currentX = entity.Pos.X;
            double currentY = entity.Pos.Y;
            double currentZ = entity.Pos.Z;

            double dx = currentX - previousPos.X;
            double dy = currentY - previousPos.Y;
            double dz = currentZ - previousPos.Z;

            if (dx * dx + dy * dy + dz * dz < 0.000001) return;

            currentPos.Set(currentX, currentY, currentZ);

            double centerOffsetY = 0.5;
            if (entity.OriginCollisionBox != null)
            {
                centerOffsetY = (entity.OriginCollisionBox.Y1 + entity.OriginCollisionBox.Y2) * 0.5;
            }

            previousCenter.Set(previousPos.X, previousPos.Y + centerOffsetY, previousPos.Z);
            currentCenter.Set(currentX, currentY + centerOffsetY, currentZ);

            if (BEBehaviorWardbloom.TryBlockAgentMovement(
                entity,
                previousCenter,
                currentCenter,
                out BEBehaviorWardbloom blockingBarrier))
            {
                // Сущность возвращается на последнюю безопасную сторону барьера
                Vec3d safePos = new Vec3d(previousPos.X, previousPos.Y, previousPos.Z);
                entity.TeleportTo(safePos);
                blockingBarrier.StopCrossingMotion(entity, safePos);
                return;
            }

            previousPos.Set(currentX, currentY, currentZ);
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if (damage <= 0f) return;
            if (!BEBehaviorWardbloom.ShouldBlockDamage(entity, damageSource)) return;

            damage = 0f;
        }
    }


    internal sealed class EntityBehaviorWardbloomProjectileProtection : EntityBehavior
    {
        private readonly IProjectile projectile;
        private Vec3d previousPos;

        public EntityBehaviorWardbloomProjectileProtection(Entity entity) : base(entity)
        {
            projectile = entity as IProjectile;
            previousPos = new Vec3d(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
        }

        public override string PropertyName()
        {
            return "wardbloomprojectileprotection";
        }

        public override void OnGameTick(float deltaTime)
        {
            if (entity.World.Side != EnumAppSide.Server || !entity.Alive || projectile == null) return;

            Vec3d currentPos = entity.Pos.XYZ;

            if (BEBehaviorWardbloom.ShouldRemoveProjectile(entity, projectile, previousPos))
            {
                entity.Die(EnumDespawnReason.Removed);
                return;
            }

            previousPos.Set(currentPos.X, currentPos.Y, currentPos.Z);
        }
    }
}
