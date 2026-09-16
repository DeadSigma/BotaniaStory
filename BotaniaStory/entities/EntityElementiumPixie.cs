using System;
using System.Collections.Generic;
using BotaniaStory.items;
using BotaniaStory.systems;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.entities
{
    public class EntityElementiumPixie : EntityAgent
    {
        private const float EnemyRange = 12f;
        private const float MaxCombatTargetDistance = 20f;
        private const float AttackRange = 1.45f;
        private const float AttackDamage = 2f;
        private const float HealAmount = 0.05f;

        private const float AttackInterval = 1f;
        private const float HealInterval = 1f;

        private const float IdleSpeed = 1.55f;
        private const float DartSpeed = 2.25f;
        private const float ReturnSpeed = 3.5f;
        private const float CombatSpeed = 4.1f;
        private const float Steering = 7.5f;

        private const float MaxOwnerDistance = 6.5f;
        private const float SeparationRadius = 0.85f;
        private const long DutyGraceMs = 5000;

        private float attackTimer;
        private float healTimer;
        private float wanderTimer;
        private float combatRetargetTimer;
        private float retreatTimer;

        private double velocityX;
        private double velocityY;
        private double velocityZ;

        private double bobPhase;
        private double swayPhase;
        private double personalityOffset;

        private float currentIdleSpeed = IdleSpeed;
        private bool aiInitialized;

        private Vec3d wanderTarget;
        private Vec3d combatTarget;

        public string OwnerUid =>
            WatchedAttributes.GetString("ownerUid");

        private long TargetId
        {
            get => WatchedAttributes.GetLong("targetId");
            set => WatchedAttributes.SetLong("targetId", value);
        }

        public override bool StoreWithChunk => false;

        public override bool AlwaysActive
        {
            get => true;
            set { }
        }

        public override bool ShouldReceiveDamage(
            DamageSource damageSource,
            float damage)
        {
            return false;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (World.Side != EnumAppSide.Server) return;

            EnsureAiInitialized();

            EntityPlayer owner =
                World.PlayerByUid(OwnerUid)?.Entity;

            if (owner == null ||
                !owner.Alive ||
                !ItemManaArmor.HasFullElementiumSet(owner))
            {
                RemovePixie();
                return;
            }

            dt = Math.Min(dt, 0.1f);

            attackTimer += dt;
            healTimer += dt;
            bobPhase += dt * 2.7;
            swayPhase += dt * 1.8;

            Entity target = GetCurrentTarget(owner);

            List<Entity> enemies =
                ElementiumPixieTargeting.FindEnemies(
                    World,
                    owner,
                    EnemyRange
                );

            if (target == null && enemies.Count > 0)
            {
                target = enemies[0];
                TargetId = target.EntityId;
            }

            if (target != null && target.Alive)
            {
                KeepAliveWhileBusy();
                UpdateCombat(owner, target, dt);
                healTimer = 0;
                return;
            }

            TargetId = 0;

            if (enemies.Count > 0)
            {
                TargetId = enemies[0].EntityId;
                KeepAliveWhileBusy();
                UpdateCombat(owner, enemies[0], dt);
                healTimer = 0;
                return;
            }

            if (NeedsHealing(owner))
            {
                KeepAliveWhileBusy();
                UpdateHealing(owner, dt);
                return;
            }

            long expireAt =
                WatchedAttributes.GetLong("expireAt");

            if (expireAt > 0 &&
                World.ElapsedMilliseconds >= expireAt)
            {
                RemovePixie();
                return;
            }

            UpdateHealing(owner, dt);
        }

        private void EnsureAiInitialized()
        {
            if (aiInitialized) return;

            aiInitialized = true;
            personalityOffset = World.Rand.NextDouble() * GameMath.TWOPI;
            bobPhase = World.Rand.NextDouble() * GameMath.TWOPI;
            swayPhase = World.Rand.NextDouble() * GameMath.TWOPI;
            wanderTimer = -1;
            combatRetargetTimer = -1;
        }

        private void KeepAliveWhileBusy()
        {
            WatchedAttributes.SetLong(
                "expireAt",
                World.ElapsedMilliseconds + DutyGraceMs
            );
        }

        private static bool NeedsHealing(EntityPlayer owner)
        {
            ITreeAttribute health =
                owner.WatchedAttributes.GetTreeAttribute("health");

            if (health == null) return false;

            float current =
                health.GetFloat("currenthealth");

            float max =
                health.GetFloat("maxhealth");

            return current < max - 0.001f;
        }

        private Entity GetCurrentTarget(EntityPlayer owner)
        {
            long targetId = TargetId;
            if (targetId == 0) return null;

            Entity target = World.GetEntityById(targetId);
            if (target == null || !target.Alive) return null;

            double maxRangeSq =
                MaxCombatTargetDistance * MaxCombatTargetDistance;

            if (ElementiumPixieTargeting.DistanceSq(
                    owner.Pos.XYZ,
                    target.Pos.XYZ) > maxRangeSq)
            {
                return null;
            }

            return target;
        }

        private void UpdateCombat(
            EntityPlayer owner,
            Entity target,
            float dt)
        {
            wanderTimer = -1;
            combatRetargetTimer -= dt;

            Vec3d targetCenter = new Vec3d(
                target.Pos.X,
                target.Pos.Y + 0.8,
                target.Pos.Z
            );

            if (retreatTimer > 0)
            {
                retreatTimer -= dt;

                if (combatRetargetTimer <= 0)
                {
                    SetRetreatTarget(targetCenter);
                    combatRetargetTimer =
                        0.18f + (float)World.Rand.NextDouble() * 0.18f;
                }
            }
            else if (combatTarget == null ||
                     combatRetargetTimer <= 0)
            {
                SetCombatTarget(targetCenter);
                combatRetargetTimer =
                    0.32f + (float)World.Rand.NextDouble() * 0.5f;
            }

            Vec3d desired =
                combatTarget?.Clone() ?? targetCenter;

            desired.Y +=
                Math.Sin(bobPhase * 2.2 + personalityOffset) * 0.12;

            Vec3d separation = GetSeparation(owner);
            desired.Add(
                separation.X * 0.8,
                separation.Y * 0.35,
                separation.Z * 0.8
            );

            SteerToward(
                desired,
                retreatTimer > 0 ? DartSpeed + 1.2f : CombatSpeed,
                dt
            );

            double distanceSq =
                ElementiumPixieTargeting.DistanceSq(
                    Pos.XYZ,
                    targetCenter
                );

            if (retreatTimer > 0 ||
                distanceSq > AttackRange * AttackRange ||
                attackTimer < AttackInterval)
            {
                return;
            }

            attackTimer = 0;

            target.ReceiveDamage(
                new DamageSource
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = this,
                    CauseEntity = owner,
                    Type = EnumDamageType.PiercingAttack,
                    DamageTier = 1,
                    KnockbackStrength = 0.15f
                },
                AttackDamage
            );

            StopAnimation("attack");
            StartAnimation("attack");
            PlayEntitySound("attack");

            retreatTimer =
                0.28f + (float)World.Rand.NextDouble() * 0.28f;

            SetRetreatTarget(targetCenter);
            combatRetargetTimer = 0.12f;
        }

        private void SetCombatTarget(Vec3d center)
        {
            double angle =
                World.Rand.NextDouble() * GameMath.TWOPI;

            double radius =
                0.65 + World.Rand.NextDouble() * 0.75;

            combatTarget = new Vec3d(
                center.X + Math.Cos(angle) * radius,
                center.Y - 0.15 + World.Rand.NextDouble() * 0.75,
                center.Z + Math.Sin(angle) * radius
            );
        }

        private void SetRetreatTarget(Vec3d center)
        {
            double dx = Pos.X - center.X;
            double dz = Pos.Z - center.Z;

            double length =
                Math.Sqrt(dx * dx + dz * dz);

            if (length < 0.05)
            {
                double angle =
                    World.Rand.NextDouble() * GameMath.TWOPI;

                dx = Math.Cos(angle);
                dz = Math.Sin(angle);
                length = 1;
            }

            dx /= length;
            dz /= length;

            double side =
                World.Rand.NextDouble() * 1.2 - 0.6;

            combatTarget = new Vec3d(
                Pos.X + dx * 1.3 - dz * side,
                Pos.Y + 0.25 + World.Rand.NextDouble() * 0.45,
                Pos.Z + dz * 1.3 + dx * side
            );
        }

        private void UpdateHealing(
            EntityPlayer owner,
            float dt)
        {
            combatTarget = null;
            retreatTimer = 0;
            wanderTimer -= dt;

            double ownerDistSq =
                ElementiumPixieTargeting.DistanceSq(
                    Pos.XYZ,
                    owner.Pos.XYZ
                );

            if (ownerDistSq >
                MaxOwnerDistance * MaxOwnerDistance)
            {
                wanderTarget = new Vec3d(
                    owner.Pos.X,
                    owner.Pos.Y + 1.45,
                    owner.Pos.Z
                );

                currentIdleSpeed = ReturnSpeed;
                wanderTimer = 0.25f;
            }
            else if (wanderTarget == null ||
                     wanderTimer <= 0 ||
                     ElementiumPixieTargeting.DistanceSq(
                         Pos.XYZ,
                         wanderTarget) < 0.12)
            {
                PickWanderTarget(owner);
            }

            Vec3d desired = wanderTarget.Clone();

            desired.X +=
                Math.Sin(swayPhase + personalityOffset) * 0.18;

            desired.Y +=
                Math.Sin(bobPhase + personalityOffset) * 0.16;

            desired.Z +=
                Math.Cos(swayPhase * 0.83 + personalityOffset) * 0.18;

            Vec3d separation = GetSeparation(owner);

            desired.Add(
                separation.X,
                separation.Y * 0.45,
                separation.Z
            );

            SteerToward(
                desired,
                currentIdleSpeed,
                dt
            );

            if (healTimer < HealInterval) return;
            healTimer = 0;

            ITreeAttribute health =
                owner.WatchedAttributes.GetTreeAttribute("health");

            if (health == null) return;

            float current =
                health.GetFloat("currenthealth");

            float max =
                health.GetFloat("maxhealth");

            if (current >= max - 0.001f) return;

            owner.ReceiveDamage(
                new DamageSource
                {
                    Source = EnumDamageSource.Internal,
                    SourceEntity = this,
                    Type = EnumDamageType.Heal,
                    KnockbackStrength = 0
                },
                HealAmount
            );

            PlayEntitySound("heal");
        }

        private void PickWanderTarget(EntityPlayer owner)
        {
            double angle =
                World.Rand.NextDouble() * GameMath.TWOPI;

            bool dart =
                World.Rand.NextDouble() < 0.16;

            double radius = dart
                ? 2.2 + World.Rand.NextDouble() * 1.1
                : 0.7 + World.Rand.NextDouble() * 1.8;

            double height =
                0.85 + World.Rand.NextDouble() * 1.25;

            double lead =
                0.25 + World.Rand.NextDouble() * 0.35;

            wanderTarget = new Vec3d(
                owner.Pos.X +
                    Math.Cos(angle) * radius +
                    owner.Pos.Motion.X * lead,
                owner.Pos.Y + height,
                owner.Pos.Z +
                    Math.Sin(angle) * radius +
                    owner.Pos.Motion.Z * lead
            );

            currentIdleSpeed =
                dart ? DartSpeed : IdleSpeed;

            wanderTimer = dart
                ? 0.45f + (float)World.Rand.NextDouble() * 0.55f
                : 0.75f + (float)World.Rand.NextDouble() * 1.25f;
        }

        private Vec3d GetSeparation(EntityPlayer owner)
        {
            Vec3d force = new Vec3d();

            Entity[] nearby = World.GetEntitiesAround(
                Pos.XYZ,
                SeparationRadius,
                SeparationRadius,
                candidate =>
                    candidate is EntityElementiumPixie pixie &&
                    candidate.EntityId != EntityId &&
                    pixie.OwnerUid == owner.PlayerUID
            );

            if (nearby == null || nearby.Length == 0)
            {
                return force;
            }

            foreach (Entity other in nearby)
            {
                double dx = Pos.X - other.Pos.X;
                double dy = Pos.Y - other.Pos.Y;
                double dz = Pos.Z - other.Pos.Z;

                double distSq =
                    dx * dx + dy * dy + dz * dz;

                if (distSq < 0.0001) continue;

                double strength =
                    Math.Min(1.5, 0.22 / distSq);

                force.X += dx * strength;
                force.Y += dy * strength;
                force.Z += dz * strength;
            }

            return force;
        }

        private void SteerToward(
            Vec3d desired,
            float speed,
            float dt)
        {
            double dx = desired.X - Pos.X;
            double dy = desired.Y - Pos.Y;
            double dz = desired.Z - Pos.Z;

            double distance =
                Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (distance < 0.025)
            {
                velocityX *= 0.86;
                velocityY *= 0.86;
                velocityZ *= 0.86;
                return;
            }

            double desiredSpeed =
                Math.Min(speed, Math.Max(0.35, distance * 2.2));

            double targetVx =
                dx / distance * desiredSpeed;

            double targetVy =
                dy / distance * desiredSpeed;

            double targetVz =
                dz / distance * desiredSpeed;

            double response =
                1.0 - Math.Exp(-Steering * dt);

            velocityX +=
                (targetVx - velocityX) * response;

            velocityY +=
                (targetVy - velocityY) * response;

            velocityZ +=
                (targetVz - velocityZ) * response;

            Pos.X += velocityX * dt;
            Pos.Y += velocityY * dt;
            Pos.Z += velocityZ * dt;

            double horizontalSq =
                velocityX * velocityX +
                velocityZ * velocityZ;

            if (horizontalSq > 0.0025)
            {
                Pos.Yaw =
                    (float)Math.Atan2(
                        velocityX,
                        velocityZ
                    );
            }
        }

        private void RemovePixie()
        {
            if (World is not IServerWorldAccessor serverWorld)
            {
                return;
            }

            serverWorld.DespawnEntity(
                this,
                new EntityDespawnData
                {
                    Reason = EnumDespawnReason.Removed
                }
            );
        }
    }
}
