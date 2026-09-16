using System;
using System.Collections.Generic;
using System.Linq;
using BotaniaStory.entities;
using BotaniaStory.items;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory.systems
{
    public class EntityBehaviorElementiumPixies : EntityBehavior
    {
        private const string PixieCode = "botaniastory:elementiumpixie";
        private const float EnemyRange = 12f;
        private const float PassiveCheckInterval = 0.5f;
        private const long PixieLifetimeMs = 15000;

        private float passiveCheckTimer;

        public EntityBehaviorElementiumPixies(Entity entity)
            : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "elementiumpixies";
        }

        public override void OnGameTick(float deltaTime)
        {
            if (entity.World.Side != EnumAppSide.Server) return;
            if (entity is not EntityPlayer player || !player.Alive) return;

            passiveCheckTimer += deltaTime;
            if (passiveCheckTimer < PassiveCheckInterval) return;

            passiveCheckTimer = 0;

            if (!ItemManaArmor.HasFullElementiumSet(player)) return;
            if (!NeedsHealing(player)) return;

            EnsurePixies(player);
        }

        public override void OnEntityReceiveDamage(
            DamageSource damageSource,
            ref float damage)
        {
            if (entity.World.Side != EnumAppSide.Server) return;
            if (damage <= 0 || damageSource?.Type == EnumDamageType.Heal) return;
            if (entity is not EntityPlayer player) return;
            if (!ItemManaArmor.HasFullElementiumSet(player)) return;

            EnsurePixies(
                player,
                damageSource?.GetCauseEntity()
            );
        }

        private void EnsurePixies(
            EntityPlayer player,
            Entity forcedEnemy = null)
        {
            List<Entity> enemies =
                ElementiumPixieTargeting.FindEnemies(
                    entity.World,
                    player,
                    EnemyRange,
                    forcedEnemy
                );

            int wanted =
                Math.Min(3, Math.Max(1, enemies.Count));

            List<EntityElementiumPixie> active =
                ElementiumPixieTargeting.FindOwnedPixies(
                    entity.World,
                    player,
                    EnemyRange + 8f
                );

            long expireAt =
                entity.World.ElapsedMilliseconds +
                PixieLifetimeMs;

            for (int i = 0; i < active.Count; i++)
            {
                EntityElementiumPixie pixie = active[i];

                pixie.WatchedAttributes.SetLong(
                    "expireAt",
                    expireAt
                );

                if (enemies.Count == 0) continue;

                long targetId =
                    pixie.WatchedAttributes.GetLong("targetId");

                Entity currentTarget =
                    targetId == 0
                        ? null
                        : entity.World.GetEntityById(targetId);

                if (currentTarget != null &&
                    currentTarget.Alive)
                {
                    continue;
                }

                pixie.WatchedAttributes.SetLong(
                    "targetId",
                    enemies[i % enemies.Count].EntityId
                );
            }

            int toSpawn =
                Math.Max(0, wanted - active.Count);

            for (int i = 0; i < toSpawn; i++)
            {
                long targetId = 0;

                if (enemies.Count > 0)
                {
                    targetId =
                        enemies[
                            (active.Count + i) %
                            enemies.Count
                        ].EntityId;
                }

                SpawnPixie(
                    player,
                    targetId,
                    expireAt,
                    active.Count + i
                );
            }
        }

        private static bool NeedsHealing(
            EntityPlayer player)
        {
            var health =
                player.WatchedAttributes
                    .GetTreeAttribute("health");

            if (health == null) return false;

            float current =
                health.GetFloat("currenthealth");

            float max =
                health.GetFloat("maxhealth");

            return current < max - 0.001f;
        }

        private void SpawnPixie(
            EntityPlayer owner,
            long targetId,
            long expireAt,
            int index)
        {
            EntityProperties type =
                entity.World.GetEntityType(
                    new AssetLocation(PixieCode)
                );

            if (type == null)
            {
                entity.World.Logger.Error(
                    "[BotaniaStory] Entity type not found: {0}",
                    PixieCode
                );
                return;
            }

            Entity created =
                entity.World.ClassRegistry.CreateEntity(type);

            if (created is not EntityElementiumPixie pixie)
            {
                return;
            }

            double angle =
                index * GameMath.TWOPI / 3.0 +
                entity.World.Rand.NextDouble() * 0.5;

            pixie.Pos.SetPos(
                owner.Pos.X + Math.Cos(angle) * 0.8,
                owner.Pos.Y + 1.35,
                owner.Pos.Z + Math.Sin(angle) * 0.8
            );

            pixie.WatchedAttributes.SetString(
                "ownerUid",
                owner.PlayerUID
            );

            pixie.WatchedAttributes.SetLong(
                "targetId",
                targetId
            );

            pixie.WatchedAttributes.SetLong(
                "expireAt",
                expireAt
            );

            entity.World.SpawnEntity(pixie);
        }
    }

    public static class ElementiumPixieTargeting
    {
        private static readonly string[] HostilePrefixes =
        {
            "drifter",
            "shiver",
            "bowtorn",
            "locust",
            "bell",
            "eidolon"
        };

        public static List<Entity> FindEnemies(
            IWorldAccessor world,
            EntityPlayer owner,
            float range,
            Entity forcedEnemy = null)
        {
            List<Entity> result =
                new List<Entity>();

            if (IsForcedEnemy(owner, forcedEnemy))
            {
                result.Add(forcedEnemy);
            }

            Entity[] nearby =
                world.GetEntitiesAround(
                    owner.Pos.XYZ,
                    range,
                    range,
                    candidate =>
                        IsGenericEnemy(
                            owner,
                            candidate
                        )
                );

            if (nearby != null)
            {
                foreach (Entity candidate in nearby)
                {
                    if (result.Any(
                        e =>
                            e.EntityId ==
                            candidate.EntityId))
                    {
                        continue;
                    }

                    result.Add(candidate);
                }
            }

            return result
                .OrderBy(
                    candidate =>
                        DistanceSq(
                            owner.Pos.XYZ,
                            candidate.Pos.XYZ
                        )
                )
                .ToList();
        }

        public static List<EntityElementiumPixie>
            FindOwnedPixies(
                IWorldAccessor world,
                EntityPlayer owner,
                float range)
        {
            Entity[] nearby =
                world.GetEntitiesAround(
                    owner.Pos.XYZ,
                    range,
                    range,
                    candidate =>
                        candidate is
                            EntityElementiumPixie pixie &&
                        pixie.Alive &&
                        pixie.OwnerUid ==
                            owner.PlayerUID
                );

            if (nearby == null)
            {
                return new List<EntityElementiumPixie>();
            }

            return nearby
                .OfType<EntityElementiumPixie>()
                .ToList();
        }

        public static bool IsGenericEnemy(
            EntityPlayer owner,
            Entity candidate)
        {
            if (candidate == null ||
                !candidate.Alive)
            {
                return false;
            }

            if (candidate.EntityId ==
                owner.EntityId)
            {
                return false;
            }

            if (candidate is EntityElementiumPixie)
            {
                return false;
            }

            if (candidate is EntityPlayer)
            {
                return false;
            }

            if (candidate is not EntityAgent)
            {
                return false;
            }

            if (candidate.Properties?
                    .Attributes?["hostile"]
                    .AsBool(false) == true)
            {
                return true;
            }

            string path =
                candidate.Code?.Path;

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            foreach (string prefix in HostilePrefixes)
            {
                if (path.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsForcedEnemy(
            EntityPlayer owner,
            Entity candidate)
        {
            if (candidate == null ||
                !candidate.Alive)
            {
                return false;
            }

            if (candidate.EntityId ==
                owner.EntityId)
            {
                return false;
            }

            if (candidate is EntityElementiumPixie)
            {
                return false;
            }

            return candidate is EntityAgent;
        }

        public static double DistanceSq(
            Vec3d a,
            Vec3d b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;

            return dx * dx +
                   dy * dy +
                   dz * dz;
        }
    }
}
