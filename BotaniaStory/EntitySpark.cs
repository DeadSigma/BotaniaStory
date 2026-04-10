using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory
{
    public class EntitySpark : Entity
    {
        public override bool IsInteractable => false;

        private const int SPARK_RANGE = 12;
        private const int TRANSFER_RATE = 100000;
        private float transferAccumulator = 0f;

        // 1. ДОБАВЛЯЕМ ПОЛЕ ДЛЯ РЕНДЕРЕРА
        private SparkRenderer renderer;

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            if (api is ICoreClientAPI capi)
            {
                // 2. СОХРАНЯЕМ ССЫЛКУ ПРИ РЕГИСТРАЦИИ
                renderer = new SparkRenderer(capi, this);
                capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "spark");
            }
        }

        // 3. ДОБАВЛЯЕМ МЕТОД ОЧИСТКИ (Вызывается, когда сервер убивает сущность)
        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            // Если мы на клиенте — удаляем рендерер из движка игры!
            if (Api is ICoreClientAPI capi && renderer != null)
            {
                capi.Event.UnregisterRenderer(renderer, EnumRenderStage.Opaque);
                renderer.Dispose();
                renderer = null;
            }
        }



        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (Api.Side == EnumAppSide.Server)
            {
                double baseX = WatchedAttributes.GetDouble("baseX", Pos.X);
                double baseY = WatchedAttributes.GetDouble("baseY", Pos.Y);
                double baseZ = WatchedAttributes.GetDouble("baseZ", Pos.Z);

                // Просто удерживаем искру на заданной высоте (1.7), без поиска потолка
                if (Math.Abs(Pos.Y - baseY) > 0.05)
                {
                    Pos.Y = baseY;
                }

                transferAccumulator += dt;
                if (transferAccumulator >= 0.05f)
                {
                    transferAccumulator = 0f;
                    DoManaTransfer(baseX, baseY, baseZ);
                }
            }
        }

        private void DoManaTransfer(double baseX, double baseY, double baseZ)
        {
            BlockPos myPoolPos = new BlockPos((int)Math.Floor(baseX), (int)Math.Floor(baseY) - 1, (int)Math.Floor(baseZ));
            BlockEntityManaPool myPool = Api.World.BlockAccessor.GetBlockEntity(myPoolPos) as BlockEntityManaPool;

            if (myPool == null || myPool.CurrentMana <= 0) return;

            // Читаем, какой дополнитель стоит на НАШЕЙ искре
            string myAugment = WatchedAttributes.GetString("augment", "none");

            Entity[] nearbySparks = Api.World.GetEntitiesAround(Pos.XYZ, SPARK_RANGE, SPARK_RANGE, e => e is EntitySpark && e.EntityId != this.EntityId);
            bool myPoolChanged = false;

            foreach (Entity entity in nearbySparks)
            {
                EntitySpark otherSpark = entity as EntitySpark;
                if (otherSpark == null) continue;

                // Читаем, какой дополнитель стоит на ЧУЖОЙ искре
                string otherAugment = otherSpark.WatchedAttributes.GetString("augment", "none");

                // 1. Изолированные искры не участвуют в обмене маной между бассейнами
                if (myAugment == "isolated" || otherAugment == "isolated") continue;

                // 2. Логика передачи: 
                // Мана течет от нас к ним ТОЛЬКО если у нас Подчиненная руна ИЛИ у них Доминантная руна.
                bool shouldSendMana = (myAugment == "recessive") || (otherAugment == "dominant");

                if (!shouldSendMana) continue; // Если условий нет - пропускаем (обычные искры ничего не делают)

                double otherX = otherSpark.WatchedAttributes.GetDouble("baseX");
                double otherY = otherSpark.WatchedAttributes.GetDouble("baseY");
                double otherZ = otherSpark.WatchedAttributes.GetDouble("baseZ");

                BlockPos otherPoolPos = new BlockPos((int)Math.Floor(otherX), (int)Math.Floor(otherY) - 1, (int)Math.Floor(otherZ));
                BlockEntityManaPool otherPool = Api.World.BlockAccessor.GetBlockEntity(otherPoolPos) as BlockEntityManaPool;

                if (otherPool != null)
                {
                    // Нам больше не нужен порог в 10 000 для "выравнивания". 
                    // Руны качают ману до тех пор, пока есть место.
                    int spaceInOther = otherPool.MaxMana - otherPool.CurrentMana;

                    if (spaceInOther > 0 && myPool.CurrentMana > 0)
                    {
                        // Передаем порциями (скорость TRANSFER_RATE)
                        int amountToTransfer = Math.Min(TRANSFER_RATE / 10, spaceInOther);
                        amountToTransfer = Math.Min(amountToTransfer, myPool.CurrentMana);

                        if (amountToTransfer > 0)
                        {
                            myPool.CurrentMana -= amountToTransfer;
                            otherPool.CurrentMana += amountToTransfer;
                            myPoolChanged = true;
                            otherPool.MarkDirty(true);

                            SpawnTransferParticles(Pos.XYZ, otherSpark.Pos.XYZ, amountToTransfer);
                        }
                    }
                }
            }

            if (myPoolChanged)
            {
                myPool.MarkDirty(true);
            }
        }

        // --- ИДЕАЛЬНЫЙ РУЧЕЕК BOTANIA ---
        private void SpawnTransferParticles(Vec3d start, Vec3d end, int amountTransferred)
        {
            // Если мы на сервере, отправляем пакет игрокам поблизости
            if (Api is ICoreServerAPI sapi)
            {
                Vec3d trueStart = start.AddCopy(0, 0.2, 0);
                Vec3d trueEnd = end.AddCopy(0, 0.2, 0);

                ManaStreamPacket packet = new ManaStreamPacket
                {
                    StartX = trueStart.X,
                    StartY = trueStart.Y,
                    StartZ = trueStart.Z,
                    EndX = trueEnd.X,
                    EndY = trueEnd.Y,
                    EndZ = trueEnd.Z
                };

                // ОПТИМИЗАЦИЯ: Отправляем пакет только тем игрокам, которые находятся ближе 64 блоков
                var channel = sapi.Network.GetChannel("botanianetwork");
                foreach (var player in sapi.World.AllOnlinePlayers)
                {
                    if (player is IServerPlayer serverPlayer)
                    {
                        // Проверяем расстояние от искры до игрока
                        if (serverPlayer.Entity.Pos.DistanceTo(Pos.XYZ) < 64)
                        {
                            channel.SendPacket(packet, serverPlayer);
                        }
                    }
                }
            }
        }
    }
}