using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace BotaniaStory.util
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class MeditationTogglePacket
    {
        public bool IsMeditating;
    }

    public class MeditationModSystem : ModSystem
    {
        private ICoreClientAPI capi;
        private IClientNetworkChannel clientChannel;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

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
            bool currentState = capi.World.Player.Entity.Attributes.GetBool("isMeditating", false);
            bool newState = !currentState;

            capi.World.Player.Entity.Attributes.SetBool("isMeditating", newState);
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
            fromPlayer.Entity.Attributes.SetBool("isMeditating", packet.IsMeditating);
        }
    }

    public class EntityBehaviorPlayerMeditation(Entity entity) : EntityBehavior(entity)
    {
        private const float MeditationThreshold = 10f;
        private const float FlowerInterval = 60f;
        private const float AmbientInterval = 0.25f;
        private const int transformRadius = 4;

        private float sitDuration = 0f;
        private float flowerTick = 0f;
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

            if (isMeditating && entity is EntityAgent agent &&
                (agent.Controls.TriesToMove || agent.Controls.Jump))
            {
                entity.Attributes.SetBool("isMeditating", false);
                ResetMeditationTimers();
                return;
            }

            if (!isMeditating)
            {
                ResetMeditationTimers();
                return;
            }

            sitDuration += deltaTime;
            flowerTick += deltaTime;
            ambientTick += deltaTime;

            if (ambientTick >= AmbientInterval)
            {
                SpawnAuraParticles(sitDuration >= MeditationThreshold);
                ambientTick = 0f;
            }

            if (flowerTick >= FlowerInterval)
            {
                TrySpawnNearbyFlower();
                flowerTick -= FlowerInterval;
            }
        }

        private void ResetMeditationTimers()
        {
            sitDuration = 0f;
            flowerTick = 0f;
            ambientTick = 0f;
        }

        private void TrySpawnNearbyFlower()
        {
            BlockPos playerPos = entity.Pos.AsBlockPos;
            IBlockAccessor blockAccessor = entity.World.BlockAccessor;

            string randomColor = flowerColors[entity.World.Rand.Next(flowerColors.Length)];
            AssetLocation flowerLoc = new("botaniastory", "mysticalflower-" + randomColor + "-free");
            Block flowerBlock = entity.World.GetBlock(flowerLoc);

            if (flowerBlock == null) return;

            List<BlockPos> validPositions = [];

            // Проверяем всю область - одиночная почва тоже будет найдена
            for (int x = -transformRadius; x <= transformRadius; x++)
            {
                for (int z = -transformRadius; z <= transformRadius; z++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        BlockPos groundPos = playerPos.AddCopy(x, y, z);
                        Block groundBlock = blockAccessor.GetBlock(groundPos);

                        if (groundBlock.BlockMaterial != EnumBlockMaterial.Soil || groundBlock.Fertility <= 0) continue;

                        BlockPos flowerPos = groundPos.UpCopy();
                        Block targetBlock = blockAccessor.GetBlock(flowerPos, BlockLayersAccess.Solid);
                        Block fluidBlock = blockAccessor.GetBlock(flowerPos, BlockLayersAccess.Fluid);

                        if (fluidBlock.Id != 0) continue;
                        if (targetBlock.Id != 0) continue;

                        validPositions.Add(flowerPos);
                    }
                }
            }

            if (validPositions.Count == 0) return;

            BlockPos spawnPos = validPositions[entity.World.Rand.Next(validPositions.Count)];
            blockAccessor.SetBlock(flowerBlock.BlockId, spawnPos);
            SpawnTransformBurst(spawnPos);

            entity.World.PlaySoundAt(
                new("game", "sounds/block/plant"),
                spawnPos.X + 0.5, spawnPos.Y + 0.5, spawnPos.Z + 0.5,
                null, true, 16, 1f
            );
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
