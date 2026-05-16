using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace BotaniaStory.blockentity
{
    public class BlockEntityMechanicalDropper : BlockEntityGenericContainer
    {
        private BEBehaviorMPConsumer mechBehavior;
        private float timeAccumulator = 0f;

        public float DropIntervalSeconds = 5.0f;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            mechBehavior = GetBehavior<BEBehaviorMPConsumer>();

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnDropperTick, 100);
            }
        }

        // Проверяем, есть ли вплотную к выбрасывателю песочные часы
        private bool HasAdjacentHourglass()
        {
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                BlockPos adjPos = Pos.AddCopy(facing);
                if (Api.World.BlockAccessor.GetBlockEntity(adjPos) is BlockEntityHourglass)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnDropperTick(float dt)
        {
            float speed = mechBehavior?.Network?.Speed ?? 0f;

            // Если энергии нет, ничего не делаем
            if (Math.Abs(speed) <= 0.01f) return;

            // Если рядом стоят часы, мы игнорируем внутренний таймер
            // (таймер не накапливается, автоматический сброс не происходит)
            if (HasAdjacentHourglass()) return;

            timeAccumulator += dt;

            if (timeAccumulator >= DropIntervalSeconds)
            {
                timeAccumulator = 0f;
                TryDropItem();
            }
        }

        // Вызывается часами, когда заканчивается песок
        public void DoDropFromHourglass()
        {
            float speed = mechBehavior?.Network?.Speed ?? 0f;

            // Выбрасываем только если подключена энергия
            // Если механизм не крутится, сигнал от часов игнорируется.
            if (Math.Abs(speed) <= 0.01f) return;

            TryDropItem();
        }

        public void TryDropItem()
        {
            var slot = Inventory.FirstOrDefault(s => !s.Empty);
            if (slot == null) return;

            ItemStack stackToDrop = slot.TakeOut(1);
            slot.MarkDirty();

            BlockFacing facing = BlockFacing.DOWN;
            if (Block.Variant.ContainsKey("facing"))
            {
                facing = BlockFacing.FromCode(Block.Variant["facing"]);
            }

            Vec3d spawnPos = Pos.ToVec3d().Add(0.5, 0.5, 0.5) + (facing.Normalf.ToVec3d() * 0.6);
            Vec3d velocity = facing.Normalf.ToVec3d() * 0.05;

            Api.World.SpawnItemEntity(stackToDrop, spawnPos, velocity);
            if (Api.Side == EnumAppSide.Server)
            {
                var sapi = Api as Vintagestory.API.Server.ICoreServerAPI;
                var channel = sapi.Network.GetChannel("botanianetwork");

                // Можно добавить проверку на null у channel, если он не всегда инициализирован?
                channel?.BroadcastPacket(new PlayManaSoundPacket()
                {
                    Position = Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                    SoundName = "mechanical_dropper"
                });
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            return false;
        }
    }
}