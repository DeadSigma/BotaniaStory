using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace BotaniaStory.util
{
    // Сетевой пакет для синхронизации
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class MeditationTogglePacket
    {
        public bool IsMeditating;
    }

    //  Система для регистрации кнопки и сети
    public class MeditationModSystem : ModSystem
    {
        private ICoreClientAPI capi;
        private IClientNetworkChannel clientChannel;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            // Регистрируем сетевой канал
            api.Network.RegisterChannel("botaniameditation")
                .RegisterMessageType<MeditationTogglePacket>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.GetChannel("botaniameditation");

            capi.Input.RegisterHotKey(
                 "meditationToggle",
                 Lang.Get("botaniastory:hotkey-meditationToggle"), 
                 GlKeys.N,
                 HotkeyType.CharacterControls
             );
            capi.Input.SetHotKeyHandler("meditationToggle", OnMeditationKey);
        }

        private bool OnMeditationKey(KeyCombination t1)
        {
            // Получаем текущее состояние из атрибутов игрока и переключаем его
            bool currentState = capi.World.Player.Entity.Attributes.GetBool("isMeditating", false);
            bool newState = !currentState;

            // Устанавливаем локально
            capi.World.Player.Entity.Attributes.SetBool("isMeditating", newState);

            // Тихо отправляем пакет на сервер
            clientChannel.SendPacket(new MeditationTogglePacket { IsMeditating = newState });

            return true;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Network.GetChannel("botaniameditation")
                .SetMessageHandler<MeditationTogglePacket>(OnServerMeditationToggle);
        }

        private void OnServerMeditationToggle(IServerPlayer fromPlayer, MeditationTogglePacket packet)
        {
            // Обновляем состояние на сервере для конкретного игрока
            fromPlayer.Entity.Attributes.SetBool("isMeditating", packet.IsMeditating);
        }
    }

    // Обновленное поведение
    public class EntityBehaviorPlayerMeditation(Entity entity) : EntityBehavior(entity)
    {
        private const float MeditationThreshold = 10f; // секунд сидения до начала эффекта
        private const float EffectInterval = 10.5f;     // как часто превращаем цветок
        private const float AmbientInterval = 0.25f;   // как часто пускаем "ауру"
        private const int transformRadius = 4;

        private float sitDuration = 0f;
        private float effectTick = 0f;
        private float ambientTick = 0f;

        private readonly string[] flowerColors = [
            "white", "orange", "magenta", "lightblue", "yellow", "lime", "pink", "gray",
            "lightgray", "cyan", "purple", "blue", "brown", "green", "red", "black"
        ];

        public override string PropertyName() => "playermeditation";

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if (entity.World.Side != EnumAppSide.Server) return;

            bool isMeditating = entity.Attributes.GetBool("isMeditating", false);

            // Проверяем, является ли сущность EntityAgent, и получаем доступ к Controls через переменную agent
            if (isMeditating && entity is EntityAgent agent)
            {
                if (agent.Controls.TriesToMove || agent.Controls.Jump)
                {
                    isMeditating = false;
                    entity.Attributes.SetBool("isMeditating", false);

                    // Сброс таймеров
                    sitDuration = 0f;
                    effectTick = 0f;
                    ambientTick = 0f;
                }
            }

            if (!isMeditating)
            {
                sitDuration = 0f;
                effectTick = 0f;
                ambientTick = 0f;
                return;
            }

            sitDuration += deltaTime;

            ambientTick += deltaTime;
            if (ambientTick >= AmbientInterval)
            {
                SpawnAuraParticles(sitDuration >= MeditationThreshold);
                ambientTick = 0f;
            }

            if (sitDuration >= MeditationThreshold)
            {
                effectTick += deltaTime;
                if (effectTick >= EffectInterval)
                {
                    TryTransformNearbyFlower();
                    effectTick = 0f;
                }
            }
        }

        private void TryTransformNearbyFlower()
        {
            BlockPos playerPos = entity.Pos.AsBlockPos;
            IBlockAccessor blockAccessor = entity.World.BlockAccessor;

            for (int i = 0; i < 8; i++)
            {
                int xOffset = entity.World.Rand.Next(-transformRadius, transformRadius + 1);
                int zOffset = entity.World.Rand.Next(-transformRadius, transformRadius + 1);
                int yOffset = entity.World.Rand.Next(-2, 3);

                BlockPos checkPos = playerPos.AddCopy(xOffset, yOffset, zOffset);
                Block targetBlock = blockAccessor.GetBlock(checkPos);

                if (targetBlock?.Code == null) continue;

                if (targetBlock.Code.Domain == "game" &&
                    targetBlock.Code.Path.StartsWith("flower-") &&
                    !targetBlock.Code.Path.Contains("mystical"))
                {
                    string randomColor = flowerColors[entity.World.Rand.Next(flowerColors.Length)];

                    AssetLocation mysticalLoc = new("botaniastory", "mysticalflower-" + randomColor + "-free");
                    Block mysticalFlowerBlock = entity.World.GetBlock(mysticalLoc);

                    if (mysticalFlowerBlock != null)
                    {
                        blockAccessor.SetBlock(mysticalFlowerBlock.BlockId, checkPos);
                        SpawnTransformBurst(checkPos);

                        entity.World.PlaySoundAt(
                            new("game", "sounds/block/plant"),
                            checkPos.X + 0.5, checkPos.Y + 0.5, checkPos.Z + 0.5,
                            null, true, 16, 1f);

                        return;
                    }
                }
            }
        }

        private void SpawnAuraParticles(bool intense)
        {
            double cx = entity.Pos.X;
            double cy = entity.Pos.Y;
            double cz = entity.Pos.Z;
            const float r = 0.85f;

            int min = intense ? 2 : 1;
            int max = intense ? 5 : 2;

            SimpleParticleProperties aura = new(
                min, max,
                ColorUtil.ToRgba(200, 200, 120, 255),
                new Vec3d(cx - r, cy + 0.1, cz - r),
                new Vec3d(cx + r, cy + 1.3, cz + r),
                new Vec3f(-0.05f, 0.15f, -0.05f),
                new Vec3f(0.05f, 0.45f, 0.05f),
                1.2f,
                -0.02f,
                0.1f, 0.3f,
                EnumParticleModel.Quad
            )
            {
                SelfPropelled = true
            };

            entity.World.SpawnParticles(aura);
        }

        private void SpawnTransformBurst(BlockPos pos)
        {
            SimpleParticleProperties burst = new(
                5, 10,
                ColorUtil.ToRgba(255, 150, 255, 150),
                new Vec3d(pos.X, pos.Y, pos.Z),
                new Vec3d(pos.X + 1, pos.Y + 0.5, pos.Z + 1),
                new Vec3f(-0.5f, 0.5f, -0.5f),
                new Vec3f(0.5f, 1f, 0.5f),
                1.5f, -0.05f, 0.2f, 0.4f,
                EnumParticleModel.Quad
            );

            entity.World.SpawnParticles(burst);
        }
    }
}