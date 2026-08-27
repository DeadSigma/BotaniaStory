using BotaniaStory.blockentity;
using BotaniaStory.client.renderers;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory.entities
{
    public class EntityManaBurst : Entity
    {

        public int ManaPayload = 0;
        public BlockPos SourcePos = null;

        private Vec3d startPos = null;
        private float aliveSeconds = 0f;

        // позиция прошлого тика, из нее строится сплошной луч
        private Vec3d prevTickPos = null;

        // постоянная для конкретной искры добавка к толщине, в оригинале sin() от сида
        private float burstJitter = float.NaN;

        private float colorR = 0.125f, colorG = 1f, colorB = 0.125f;
        private bool colorRead = false;

        private bool impactSpawned = false;

        // доля пути на которой искра держит полную толщину
        private const float GraceFraction = 0.80f;

        // потолок жизни искры, страховка от застрявших сущностей
        private const float MaxLifeSeconds = 6f;

        public static bool IsManaPermeable(Block block)
        {
            if (block?.Code == null) return false;

            string path = block.Code.Path;

            // managlass точным совпадением, elvenglass по началу названия
            return path == "managlass" || path.StartsWith("elvenglass");
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            aliveSeconds += dt;

            if (startPos == null)
            {
                if (WatchedAttributes.HasAttribute("startX"))
                {
                    startPos = new Vec3d(
                        WatchedAttributes.GetDouble("startX"),
                        WatchedAttributes.GetDouble("startY"),
                        WatchedAttributes.GetDouble("startZ")
                    );
                }
                else
                {
                    startPos = Pos.XYZ.Clone();
                }
            }

            if (Api.Side == EnumAppSide.Client) TickClient(dt);
            else TickServer(dt);
        }

        // если клиент сильно разошелся с сервером - подтягиваем, мелкий рассинхрон игнорируем
        public override void OnReceivedServerPos(bool isTeleport)
        {
            if (isTeleport)
            {
                Pos.SetFrom(Pos);
                prevTickPos = null;
                return;
            }

            // prevTickPos не сбрасываем - следующий тик закрасит рывок частицами
            if (Pos.SquareDistanceTo(Pos.XYZ) > 4.0)
            {
                Pos.SetFrom(Pos);
            }
        }

        // хлопок при попадании, клиенту хватает факта деспавна
        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            if (Api?.Side == EnumAppSide.Client && !impactSpawned)
            {
                impactSpawned = true;

                double maxDistance = WatchedAttributes.GetDouble("maxDist", 8.0);
                double traveled = startPos == null ? 0 : startPos.DistanceTo(Pos.XYZ);

                // искра, которая просто выдохлась на излете, не хлопает
                if (traveled < maxDistance * 0.97)
                {
                    ReadColor();
                    ManaBurstParticleSystem.Renderer?.SpawnImpact(Pos.XYZ, SizeRatio(traveled, maxDistance), colorR, colorG, colorB);
                }
            }

            base.OnEntityDespawn(despawn);
        }

        private void ReadColor()
        {
            if (colorRead) return;
            colorRead = true;

            int color = WatchedAttributes.GetInt("burstColor", 0x20FF20);
            colorR = ((color >> 16) & 0xFF) / 255f;
            colorG = ((color >> 8) & 0xFF) / 255f;
            colorB = (color & 0xFF) / 255f;
        }

        // толщина луча: полная до GraceFraction пути, дальше сходит на нет
        private float SizeRatio(double traveled, double maxDistance)
        {
            float t = (float)(traveled / maxDistance);
            if (t <= GraceFraction) return 1f;

            float ratio = 1f - (t - GraceFraction) / (1f - GraceFraction);
            return ratio < 0f ? 0f : ratio;
        }

        // клиент сам двигает искру между серверными пакетами и рисует луч
        private void TickClient(float dt)
        {
            if (!Alive) return;
            if (aliveSeconds > MaxLifeSeconds) return;

            // скорость приходит в блоках в секунду
            double mx = WatchedAttributes.GetDouble("motionX", 0);
            double my = WatchedAttributes.GetDouble("motionY", 0);
            double mz = WatchedAttributes.GetDouble("motionZ", 0);

            if (mx == 0 && my == 0 && mz == 0) return;

            if (float.IsNaN(burstJitter))
            {
                burstJitter = (float)Math.Sin(EntityId % 9001) * 0.4f;
            }

            ReadColor();

            if (prevTickPos == null) prevTickPos = Pos.XYZ.Clone();
            else prevTickPos.Set(Pos.X, Pos.Y, Pos.Z);

            Pos.Motion.Set(mx, my, mz);
            Pos.X += mx * dt;
            Pos.Y += my * dt;
            Pos.Z += mz * dt;

            double maxDistance = WatchedAttributes.GetDouble("maxDist", 8.0);
            double traveled = startPos.DistanceTo(Pos.XYZ);
            if (traveled >= maxDistance) return;

            ManaBurstParticleSystem.Renderer?.SpawnBeam(
                prevTickPos,
                Pos.XYZ,
                Pos.Motion,
                SizeRatio(traveled, maxDistance),
                burstJitter,
                colorR, colorG, colorB
            );
        }

        private void TickServer(float dt)
        {
            double maxDistance = WatchedAttributes.GetDouble("maxDist", 8.0);

            // двигаем именно Pos, иначе клиент не получит коррекцию позиции
            Pos.Motion.Set(Pos.Motion);
            Pos.X += Pos.Motion.X * dt;
            Pos.Y += Pos.Motion.Y * dt;
            Pos.Z += Pos.Motion.Z * dt;
            Pos.SetFrom(Pos);

            double traveled = startPos.DistanceTo(Pos.XYZ);

            if (traveled >= maxDistance || aliveSeconds > MaxLifeSeconds)
            {
                Die(EnumDespawnReason.Removed);
                return;
            }

            BlockPos currentPos = Pos.AsBlockPos;
            if (SourcePos != null && currentPos.Equals(SourcePos)) return;

            Block block = Api.World.BlockAccessor.GetBlock(currentPos);

            // чанк не загружен - дальше лететь некуда
            if (block == null)
            {
                Die(EnumDespawnReason.Removed);
                return;
            }

            if (block.Id == 0 || block.MatterState == EnumMatterState.Liquid) return;

            // манастекло и эльфийское стекло пропускают искру насквозь
            if (IsManaPermeable(block)) return;

            BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(currentPos);

            if (be is IManaReceiver receiver)
            {
                float lifeRatio = Math.Max(0f, 1f - (float)(traveled / maxDistance));

                int finalMana = (int)(ManaPayload * lifeRatio);
                if (finalMana < 1) finalMana = 1;

                receiver.ReceiveMana(finalMana);
                be.MarkDirty(false);

                Die(EnumDespawnReason.Removed);
                return;
            }

            // обычный твердый блок - разбиваемся
            if (block.CollisionBoxes != null && block.CollisionBoxes.Length > 0)
            {
                Die(EnumDespawnReason.Removed);
            }
        }

        private void TickServer()
        {
            double maxDistance = WatchedAttributes.GetDouble("maxDist", 8.0);

            // двигаем именно Pos, иначе клиент не получит коррекцию позиции
            Pos.Motion.Set(Pos.Motion);
            Pos.X += Pos.Motion.X;
            Pos.Y += Pos.Motion.Y;
            Pos.Z += Pos.Motion.Z;
            Pos.SetFrom(Pos);

            double traveled = startPos.DistanceTo(Pos.XYZ);

            if (traveled >= maxDistance || aliveSeconds > MaxLifeSeconds)
            {
                Die(EnumDespawnReason.Removed);
                return;
            }

            BlockPos currentPos = Pos.AsBlockPos;
            if (SourcePos != null && currentPos.Equals(SourcePos)) return;

            Block block = Api.World.BlockAccessor.GetBlock(currentPos);

            // чанк не загружен - дальше лететь некуда
            if (block == null)
            {
                Die(EnumDespawnReason.Removed);
                return;
            }

            if (block.Id == 0 || block.MatterState == EnumMatterState.Liquid) return;

            // манастекло и эльфийское стекло пропускают искру насквозь
            if (IsManaPermeable(block)) return;

            BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(currentPos);

            if (be is IManaReceiver receiver)
            {
                float lifeRatio = Math.Max(0f, 1f - (float)(traveled / maxDistance));

                int finalMana = (int)(ManaPayload * lifeRatio);
                if (finalMana < 1) finalMana = 1;

                receiver.ReceiveMana(finalMana);
                be.MarkDirty(false);

                Die(EnumDespawnReason.Removed);
                return;
            }

            // обычный твердый блок - разбиваемся
            if (block.CollisionBoxes != null && block.CollisionBoxes.Length > 0)
            {
                Die(EnumDespawnReason.Removed);
            }
        }
    }
}