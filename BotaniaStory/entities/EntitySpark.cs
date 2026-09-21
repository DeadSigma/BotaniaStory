using BotaniaStory.blockentity;
using BotaniaStory.client.renderers;
using BotaniaStory.network;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.entities
{
    public class EntitySpark : Entity
    {
        public override bool IsInteractable => false;

        private const int SPARK_RANGE = 12;
        private const int TRANSFER_RATE = 14000;

        private float transferAccumulator = 0f;
        private bool isDespawning = false;
        private SparkRenderer renderer;

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            if (api is ICoreServerAPI)
            {
                // Искра без опорных координат удаляется
                if (!WatchedAttributes.HasAttribute("baseX"))
                {
                    Die();
                    return;
                }
            }

            if (api is ICoreClientAPI capi)
            {
                renderer = new SparkRenderer(capi, this);
                capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "spark");
            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            // Рендерер удаляется вместе с искрой
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

            if (Api.Side != EnumAppSide.Server) return;

            double baseX = WatchedAttributes.GetDouble("baseX", Pos.X);
            double baseY = WatchedAttributes.GetDouble("baseY", Pos.Y);
            double baseZ = WatchedAttributes.GetDouble("baseZ", Pos.Z);

            double targetY = baseY;

            BlockPos checkPos = new BlockPos(
                (int)Math.Floor(baseX),
                (int)Math.Floor(targetY),
                (int)Math.Floor(baseZ)
            );

            // Искра поднимается над перекрывающими блоками
            for (int i = 0; i < 10; i++)
            {
                Block blockHere = Api.World.BlockAccessor.GetBlock(checkPos);
                BlockEntity beHere = Api.World.BlockAccessor.GetBlockEntity(checkPos);

                bool isSolid =
                    blockHere.CollisionBoxes != null &&
                    blockHere.CollisionBoxes.Length > 0;

                if (!isSolid || beHere is BlockEntityManaPool)
                {
                    break;
                }

                targetY += 1.0;
                checkPos.Y += 1;
            }

            if (Math.Abs(Pos.Y - targetY) > 0.01)
            {
                Pos.Y += (targetY - Pos.Y) * (dt * 5f);
                Pos.SetFrom(Pos);
            }

            transferAccumulator += dt;

            if (transferAccumulator < 0.05f) return;

            transferAccumulator = 0f;

            BlockPos anchorPos = new BlockPos(
                (int)Math.Floor(baseX),
                (int)Math.Floor(baseY) - 1,
                (int)Math.Floor(baseZ)
            );

            BlockEntity anchorBE =
                Api.World.BlockAccessor.GetBlockEntity(anchorPos);

            // Искра удаляется после потери опоры
            if (!(anchorBE is BlockEntityManaPool) &&
                !(anchorBE is BlockEntityTerrestrialPlate))
            {
                if (Alive && !isDespawning)
                {
                    isDespawning = true;

                    Item itemSpark =
                        Api.World.GetItem(
                            new AssetLocation("botaniastory", "spark")
                        );

                    if (itemSpark != null)
                    {
                        Api.World.SpawnItemEntity(
                            new ItemStack(itemSpark),
                            Pos.XYZ
                        );
                    }

                    Die();
                }

                return;
            }

            DoManaTransfer(baseX, baseY, baseZ);
        }

        private void DoManaTransfer(double baseX, double baseY, double baseZ)
        {
            BlockPos myPoolPos = new BlockPos(
                (int)Math.Floor(baseX),
                (int)Math.Floor(baseY) - 1,
                (int)Math.Floor(baseZ)
            );

            BlockEntityManaPool myPool =
                Api.World.BlockAccessor.GetBlockEntity(myPoolPos)
                as BlockEntityManaPool;

            if (myPool == null || myPool.CurrentMana <= 0) return;

            string myAugment =
                WatchedAttributes.GetString("augment", "none");

            Entity[] nearbySparks =
                Api.World.GetEntitiesAround(
                    Pos.XYZ,
                    SPARK_RANGE,
                    SPARK_RANGE,
                    e => e is EntitySpark &&
                         e.EntityId != EntityId
                );

            bool myPoolChanged = false;

            foreach (Entity entity in nearbySparks)
            {
                if (!(entity is EntitySpark otherSpark)) continue;

                string otherAugment =
                    otherSpark.WatchedAttributes.GetString(
                        "augment",
                        "none"
                    );

                // Изолированные искры исключаются из передачи
                if (myAugment == "isolated" ||
                    otherAugment == "isolated")
                {
                    continue;
                }

                // Одинаковые управляющие дополнители между собой не работают
                if (
                    (myAugment == "recessive" &&
                     otherAugment == "recessive") ||
                    (myAugment == "dominant" &&
                     otherAugment == "dominant")
                )
                {
                    continue;
                }

                bool shouldSendMana =
                    myAugment == "recessive" ||
                    otherAugment == "dominant";

                if (!shouldSendMana) continue;

                double otherX =
                    otherSpark.WatchedAttributes.GetDouble("baseX");

                double otherY =
                    otherSpark.WatchedAttributes.GetDouble("baseY");

                double otherZ =
                    otherSpark.WatchedAttributes.GetDouble("baseZ");

                BlockPos otherReceiverPos = new BlockPos(
                    (int)Math.Floor(otherX),
                    (int)Math.Floor(otherY) - 1,
                    (int)Math.Floor(otherZ)
                );

                BlockEntity otherBE =
                    Api.World.BlockAccessor.GetBlockEntity(
                        otherReceiverPos
                    );

                if (!(otherBE is IManaReceiver receiver)) continue;

                int spaceInOther = receiver.GetAvailableSpace();

                if (spaceInOther <= 0 || myPool.CurrentMana <= 0)
                {
                    continue;
                }

                int amountToTransfer =
                    Math.Min(
                        TRANSFER_RATE / 10,
                        spaceInOther
                    );

                amountToTransfer =
                    Math.Min(
                        amountToTransfer,
                        myPool.CurrentMana
                    );

                if (amountToTransfer <= 0) continue;

                myPool.CurrentMana -= amountToTransfer;
                receiver.ReceiveMana(amountToTransfer);

                myPoolChanged = true;
                otherBE.MarkDirty(false);

                SpawnTransferParticles(
                    Pos.XYZ,
                    otherSpark.Pos.XYZ,
                    amountToTransfer
                );
            }

            if (myPoolChanged)
            {
                myPool.MarkDirty(false);
            }
        }

        private void SpawnTransferParticles(
            Vec3d start,
            Vec3d end,
            int amountTransferred
        )
        {
            if (!(Api is ICoreServerAPI sapi)) return;

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

            var channel =
                sapi.Network.GetChannel("botanianetwork");

            // Поток отправляется только ближайшим игрокам
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (!(player is IServerPlayer serverPlayer)) continue;

                if (serverPlayer.Entity.Pos.DistanceTo(Pos.XYZ) < 64)
                {
                    channel.SendPacket(packet, serverPlayer);
                }
            }
        }
    }
}