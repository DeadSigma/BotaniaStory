using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.entities.ai
{
    public class AiTaskGaiaLightning : AiTaskBase
    {
        // ===== НАСТРОЙКИ БАЛАНСА (можно задавать в JSON сущности) =====

        // Полный цикл: примерно раз в это время бьёт каждого отдельного игрока.
        // Интервал между ударами по РАЗНЫМ игрокам = cooldownMs / число целей.
        private int cooldownMs = 8000;

        private float damage = 1f;

        // Радиус, в котором Гайа замечает игроков
        private float range = 15f;

        // Сколько сплошных блоков молния способна пробить ДЛЯ УРОНА (на сам выстрел не влияет).
        // 2 = урон проходит сквозь стену до 2 блоков; 3+ блока — игрок в безопасности.
        // 0 = урон только при прямой видимости.
        private int wallPenetration = 2;

        // ===============================================================

        private long lastStrikeMs;
        private int rotationIndex = 0;
        private Entity targetEntity; // выбранная цель для текущего удара

        public AiTaskGaiaLightning(EntityAgent entity, JsonObject taskConfig, JsonObject fallbackConfig)
            : base(entity, taskConfig, fallbackConfig)
        {
            if (taskConfig != null)
            {
                cooldownMs = taskConfig["cooldownMs"].AsInt(8000);
                damage = taskConfig["damage"].AsFloat(1f);
                range = taskConfig["range"].AsFloat(15f);
                wallPenetration = taskConfig["wallPenetration"].AsInt(2);
            }
        }

        public override bool ShouldExecute()
        {
            if (entity.WatchedAttributes.GetBool("isLevitating", false)) return false;

            // Цели — все игроки в радиусе. Стены тут НЕ учитываем: Гайа стреляет всегда.
            List<Entity> targets = GetValidTargets();
            if (targets.Count == 0) return false;

            // Чем больше игроков — тем чаще бьёт, чтобы Гайа не простаивала
            int interval = cooldownMs / targets.Count;
            if (interval < 1) interval = 1;

            long now = entity.World.ElapsedMilliseconds;
            if (now - lastStrikeMs < interval) return false;

            // Берём следующего по кругу
            targetEntity = targets[rotationIndex % targets.Count];
            rotationIndex++;

            return true;
        }

        public override void StartExecute()
        {
            lastStrikeMs = entity.World.ElapsedMilliseconds;

            if (targetEntity == null || !targetEntity.Alive) return;
            Entity target = targetEntity;

            // Координаты: из центра босса в центр игрока
            Vec3d startPos = entity.Pos.XYZ.Add(0, entity.SelectionBox.Y2 / 2, 0);
            Vec3d endPos = target.Pos.XYZ.Add(0, target.SelectionBox.Y2 / 2, 0);

            // 1. Визуал молнии — ВСЕГДА, даже сквозь глухую стену
            if (entity.World.Side == EnumAppSide.Server)
            {
                SendLightningPacketToClients(startPos, endPos);
            }

            // 2. Звук разряда от Гайи + звук удара в точке игрока — тоже всегда
            entity.World.PlaySoundAt(new AssetLocation("sounds/weather/lightning-near"), entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, true, 32f, 1f);
            entity.World.PlaySoundAt(new AssetLocation("botaniastory", "sounds/gaia_electric_impact"), endPos.X, endPos.Y, endPos.Z, null, true, 32f, 1f);

            // 3. Урон — только если стена не толще порога wallPenetration
            int wallBlocks = CountBlockingBlocks(startPos, endPos);
            if (wallBlocks <= wallPenetration)
            {
                DamageSource dmgSource = new DamageSource()
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = entity,
                    Type = EnumDamageType.Electricity
                };
                target.ReceiveDamage(dmgSource, damage);
            }
        }

        public override bool ContinueExecute(float dt) => false;

        // Все живые игроки в радиусе (без учёта стен — стрелять можно по всем)
        private List<Entity> GetValidTargets()
        {
            List<Entity> result = new List<Entity>();

            foreach (IPlayer player in entity.World.AllOnlinePlayers)
            {
                Entity pe = player.Entity;
                if (pe == null || !pe.Alive) continue;

                // Не бьём по креативу/наблюдателям
                EnumGameMode mode = player.WorldData != null ? player.WorldData.CurrentGameMode : EnumGameMode.Survival;
                if (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator) continue;

                if (pe.Pos.DistanceTo(entity.Pos) > range) continue;

                result.Add(pe);
            }

            // Стабильный порядок, чтобы ротация была предсказуемой при входе/выходе игроков
            result.Sort((a, b) => a.EntityId.CompareTo(b.EntityId));

            return result;
        }

        // Считает количество сплошных блоков между двумя точками
        private int CountBlockingBlocks(Vec3d start, Vec3d end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double dz = end.Z - start.Z;
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist < 1e-4) return 0;

            int steps = (int)(dist * 4) + 1; // ~4 пробы на блок
            IBlockAccessor ba = entity.World.BlockAccessor;

            int count = 0;
            int lastX = int.MinValue, lastY = int.MinValue, lastZ = int.MinValue;
            BlockPos tmp = new BlockPos();

            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                int bx = (int)Math.Floor(start.X + dx * t);
                int by = (int)Math.Floor(start.Y + dy * t);
                int bz = (int)Math.Floor(start.Z + dz * t);

                // Один и тот же блок не считаем дважды
                if (bx == lastX && by == lastY && bz == lastZ) continue;
                lastX = bx; lastY = by; lastZ = bz;

                tmp.Set(bx, by, bz);
                Block block = ba.GetBlock(tmp);
                if (IsBlocking(block)) count++;
            }

            return count;
        }

        // Что считать "стеной". Здесь — любой блок с коллизией (камень, дерево, заборы, стекло и т.п.).
        // Трава, вода, цветы коллизии не имеют и урон не блокируют.
        private bool IsBlocking(Block block)
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