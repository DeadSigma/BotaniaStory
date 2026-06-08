using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent; 

namespace BotaniaStory.blockentity
{
    public class BEBehaviorAgricarnation : BlockEntityBehavior, ILinkableToPool
    {
        private int actionRadius = 12;

        public bool CheckedOnPlacement { get; set; } = false;
        public BlockPos LinkedPool { get; set; } = null;

        // Кэш для хранения координат найденных грядок
        private List<BlockPos> cachedFarmlands = new List<BlockPos>();

        public BEBehaviorAgricarnation(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            if (api.Side == EnumAppSide.Server)
            {
                if (!CheckedOnPlacement && LinkedPool == null)
                {
                    AutoFindPool();
                    CheckedOnPlacement = true;
                    this.Blockentity.MarkDirty(true);
                }

                // 1. Медленный тик (раз в 10 секунд) для обновления списка грядок вокруг
                this.Blockentity.RegisterGameTickListener(ScanForFarmland, 60000);

                // 2. Быстрый тик (раз в 2 секунды) для отлова свежепосаженных семян
                // 2 секунды — идеальный баланс: игрок едва успеет заметить паузу, а сервер не напрягается
                this.Blockentity.RegisterGameTickListener(OnTick, 10000);
            }
        }

        private void ScanForFarmland(float dt)
        {
            cachedFarmlands.Clear();
            BlockPos myPos = this.Blockentity.Pos;

            for (int x = -actionRadius; x <= actionRadius; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    for (int z = -actionRadius; z <= actionRadius; z++)
                    {
                        BlockPos checkPos = myPos.AddCopy(x, y, z);

                        if (this.Api.World.BlockAccessor.GetBlockEntity(checkPos) is BlockEntityFarmland)
                        {
                            cachedFarmlands.Add(checkPos);
                        }
                    }
                }
            }
        }

        private void OnTick(float dt)
        {
            if (LinkedPool == null) return;

            BlockEntity be = this.Api.World.BlockAccessor.GetBlockEntity(LinkedPool);

            if (!(be is BlockEntityManaPool pool))
            {
                LinkedPool = null;
                AutoFindPool();
                this.Blockentity.MarkDirty(true);
                return;
            }

            if (cachedFarmlands.Count == 0) return;

            bool effectTriggered = false;
            double currentHours = this.Api.World.Calendar.TotalHours;

            foreach (BlockPos farmPos in cachedFarmlands)
            {
                BlockEntityFarmland beFarm = this.Api.World.BlockAccessor.GetBlockEntity(farmPos) as BlockEntityFarmland;

                if (beFarm != null)
                {
                    BlockPos cropPos = farmPos.UpCopy();
                    Block blockAbove = this.Api.World.BlockAccessor.GetBlock(cropPos);

                    if (blockAbove is BlockCrop crop && crop.CurrentCropStage == 1)
                    {
                        // 1. Узнаем, сколько всего стадий у этого растения
                        // Узнаем общее количество стадий
                        int totalStages = crop.CropProps.GrowthStages;

                        // Считаем количество для пропуска через наш новый конфиг:
                        int stagesToSkip = (int)System.Math.Round(totalStages * BotaniaStoryModSystem.ServerConfig.AgricarnationSkipPercentage);

                        if (stagesToSkip < 1) stagesToSkip = 1;
                        if (stagesToSkip >= totalStages) stagesToSkip = totalStages - 1;

                        // Считаем ману через конфиг:
                        int finalManaCost = BotaniaStoryModSystem.ServerConfig.AgricarnationManaCostPerStage * stagesToSkip;

                        // Проверяем, хватает ли маны
                        if (pool.CurrentMana < finalManaCost) break;

                        // Применяем рост
                        for (int i = 0; i < stagesToSkip; i++)
                        {
                            beFarm.TryGrowCrop(currentHours + (this.Api.World.Calendar.HoursPerDay * (i + 1)));
                        }

                        // Списываем ману
                        pool.ConsumeMana(finalManaCost);
                        effectTriggered = true;

                        SpawnBoostParticles(cropPos);
                    }
                }
            }

            if (effectTriggered)
            {
                this.Blockentity.MarkDirty(true);
            }
        }

        private void SpawnBoostParticles(BlockPos pos)
        {
            // Зеленые магические частицы, как от костной муки
            SimpleParticleProperties particles = new SimpleParticleProperties(
                10, 15, ColorUtil.ToRgba(255, 100, 255, 100),
                new Vec3d(pos.X, pos.Y, pos.Z), new Vec3d(pos.X + 1, pos.Y + 0.5, pos.Z + 1),
                new Vec3f(-0.5f, 0.5f, -0.5f), new Vec3f(0.5f, 1f, 0.5f),
                1.5f, -0.05f, 0.2f, 0.4f, EnumParticleModel.Quad
            );
            this.Api.World.SpawnParticles(particles);
        }

        public void AutoFindPool()
        {
            int searchRadius = 6;
            for (int x = -searchRadius; x <= searchRadius; x++)
            {
                for (int y = -searchRadius; y <= searchRadius; y++)
                {
                    for (int z = -searchRadius; z <= searchRadius; z++)
                    {
                        BlockPos checkPos = this.Blockentity.Pos.AddCopy(x, y, z);
                        if (this.Api.World.BlockAccessor.GetBlockEntity(checkPos) is BlockEntityManaPool)
                        {
                            LinkedPool = checkPos.Copy();
                            this.Blockentity.MarkDirty(true);
                            return;
                        }
                    }
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("checkedOnPlacement", CheckedOnPlacement);
            if (LinkedPool != null)
            {
                tree.SetInt("poolX", LinkedPool.X);
                tree.SetInt("poolY", LinkedPool.Y);
                tree.SetInt("poolZ", LinkedPool.Z);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            CheckedOnPlacement = tree.GetBool("checkedOnPlacement", false);
            if (tree.HasAttribute("poolX"))
            {
                LinkedPool = new BlockPos(tree.GetInt("poolX"), tree.GetInt("poolY"), tree.GetInt("poolZ"));
            }
            else
            {
                LinkedPool = null;
            }
        }
    }
}