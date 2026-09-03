using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.util
{
    public class AiTaskGaiaLightning : AiTaskBase
    {

        private readonly int cooldownMs = 8000;
        private int rageCooldownMs = 1000;
        private readonly float damage = 1f;
        private readonly int wallPenetration = 2;


        private long lastStrikeMs;
        private int rotationIndex = 0;
        private EntityPlayer targetEntity;

        public AiTaskGaiaLightning(EntityAgent entity, JsonObject taskConfig, JsonObject fallbackConfig)
            : base(entity, taskConfig, fallbackConfig)
        {
            if (taskConfig != null)
            {
                cooldownMs = taskConfig["cooldownMs"].AsInt(8000);
                rageCooldownMs = taskConfig["rageCooldownMs"].AsInt(1000);
                damage = taskConfig["damage"].AsFloat(1f);
                wallPenetration = taskConfig["wallPenetration"].AsInt(2);
            }
        }

        public override bool ShouldExecute()
        {
            // Во время левитации молнии не стреляют
            if (entity.WatchedAttributes.GetBool("isLevitating", false))
                return false;

            List<EntityPlayer> targets = GetValidTargets();

            if (targets.Count == 0)
                return false;


           
            // РЕЖИМ ЯРОСТИ

            bool rage =
                entity.WatchedAttributes.GetBool(
                    "gaiaRageMode",
                    false
                );

            int activeCooldown =
                rage
                    ? rageCooldownMs
                    : cooldownMs;


            // При нескольких игроках Гайа распределяет  выстрелы между ними.
            int interval =
                activeCooldown / targets.Count;

            if (interval < 1)
                interval = 1;


            long now =
                entity.World.ElapsedMilliseconds;

            if (now - lastStrikeMs < interval)
                return false;


            targetEntity =
                targets[
                    rotationIndex %
                    targets.Count
                ];

            rotationIndex++;

            return true;
        }

        public override void StartExecute()
        {
            lastStrikeMs = entity.World.ElapsedMilliseconds;

            if (targetEntity == null || !targetEntity.Alive) return;
            var target = targetEntity;

            Vec3d startPos = entity.Pos.XYZ.Add(0, entity.SelectionBox.Y2 / 2, 0);
            Vec3d endPos = target.Pos.XYZ.Add(0, target.SelectionBox.Y2 / 2, 0);

            if (entity.World.Side == EnumAppSide.Server)
            {
                SendLightningPacketToClients(startPos, endPos);
            }

            entity.World.PlaySoundAt(new AssetLocation("sounds/weather/lightning-near"), entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, true, 32f, 1f);
            entity.World.PlaySoundAt(new AssetLocation("botaniastory", "sounds/gaia_electric_impact"), endPos.X, endPos.Y, endPos.Z, null, true, 32f, 1f);

            int wallBlocks = CountBlockingBlocks(startPos, endPos);
            if (wallBlocks <= wallPenetration)
            {
                DamageSource dmgSource = new()
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = entity,
                    Type = EnumDamageType.BluntAttack,
                    DamageTier = 3
                };
                target.ReceiveDamage(dmgSource, damage);
            }
        }

        public override bool ContinueExecute(float dt) => false;

        private List<EntityPlayer> GetValidTargets()
        {
            List<EntityPlayer> result = [];

            foreach (IPlayer player in entity.World.AllOnlinePlayers)
            {
                EntityPlayer pe = player.Entity;

                if (pe == null || !pe.Alive)
                    continue;

                EnumGameMode mode =
                    player.WorldData != null
                        ? player.WorldData.CurrentGameMode
                        : EnumGameMode.Survival;

                if (mode == EnumGameMode.Creative ||
                    mode == EnumGameMode.Spectator)
                {
                    continue;
                }



                if (pe.Pos.Dimension != entity.Pos.Dimension)
                    continue;


                result.Add(pe);
            }


            // Стабильный порядок целей, чтобы rotationIndex работал предсказуемо
            result.Sort(
                (a, b) =>
                    a.EntityId.CompareTo(b.EntityId)
            );

            return result;
        }

        private int CountBlockingBlocks(Vec3d start, Vec3d end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double dz = end.Z - start.Z;
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist < 1e-4) return 0;

            int steps = (int)(dist * 4) + 1;
            IBlockAccessor ba = entity.World.BlockAccessor;

            int count = 0;
            int lastX = int.MinValue, lastY = int.MinValue, lastZ = int.MinValue;

            BlockPos tmp = new(entity.Pos.Dimension);

            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                int bx = (int)Math.Floor(start.X + dx * t);
                int by = (int)Math.Floor(start.Y + dy * t);
                int bz = (int)Math.Floor(start.Z + dz * t);

                if (bx == lastX && by == lastY && bz == lastZ) continue;
                lastX = bx; lastY = by; lastZ = bz;

                tmp.Set(bx, by, bz);
                Block block = ba.GetBlock(tmp);
                if (IsBlocking(block)) count++;
            }

            return count;
        }

        private static bool IsBlocking(Block block)
        {
            if (block == null || block.Id == 0) return false;
            return block.CollisionBoxes != null && block.CollisionBoxes.Length > 0;
        }

        private void SendLightningPacketToClients(Vec3d startPos, Vec3d endPos)
        {
            var serverApi = entity.World.Api as ICoreServerAPI;
            serverApi.Network.GetChannel("botanianetwork")
                .BroadcastPacket(new GaiaLightningPacket()
                {
                    StartPos = startPos,
                    EndPos = endPos
                });
        }
    }
}