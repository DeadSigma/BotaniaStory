using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.blockentity
{
    public class BEBehaviorWitheredAmaranthus : BlockEntityBehavior, ILinkableToPool
    {
        private int flowerSpawnRadius = 4;
        private int manaCostPerFlower = 100;
        public bool CheckedOnPlacement { get; set; } = false;

        // Координаты привязанного бассейна маны
        public BlockPos LinkedPool { get; set; } = null;

        // Массив с названиями ванильных цветов Vintage Story
        private readonly string[] vanillaFlowers = new string[] {
         "catmint", "cornflower", "forgetmenot", "lilyofthevalley",
         "edelweiss", "heather", "orangemallow", "wilddaisy",
         "westerngorse", "cowparsley", "goldenpoppy", "woad",
         "bluebell", "daffodil", "ghostpipe", "mugwort",
         "lupine-blue", "lupine-orange", "lupine-purple", "lupine-red", "lupine-white"
        };

        // Обязательный конструктор
        public BEBehaviorWitheredAmaranthus(BlockEntity blockentity) : base(blockentity)
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

                this.Blockentity.RegisterGameTickListener(OnTick, 1000);
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

            if (pool.CurrentMana >= manaCostPerFlower)
            {
                if (TrySpawnVanillaFlower()) 
                {
                    pool.ConsumeMana(manaCostPerFlower);
                }
            }
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

        private bool TrySpawnVanillaFlower()
        {
            int xOffset = this.Api.World.Rand.Next(-flowerSpawnRadius, flowerSpawnRadius + 1);
            int zOffset = this.Api.World.Rand.Next(-flowerSpawnRadius, flowerSpawnRadius + 1);
            int yOffset = this.Api.World.Rand.Next(-2, 3);

            BlockPos targetPos = this.Blockentity.Pos.AddCopy(xOffset, yOffset, zOffset);
            BlockPos belowPos = targetPos.DownCopy();

            Block targetBlock = this.Api.World.BlockAccessor.GetBlock(targetPos);
            Block belowBlock = this.Api.World.BlockAccessor.GetBlock(belowPos);

            if (targetBlock?.Code == null || belowBlock?.Code == null) return false;

            if (targetBlock.Replaceable >= 6000 &&
                (belowBlock.Code.Path.Contains("soil") || belowBlock.Code.Path.Contains("grass")))
            {
                // Выбираем случайный ванильный цветок из массива
                string randomFlower = vanillaFlowers[this.Api.World.Rand.Next(vanillaFlowers.Length)];

                // Формируем путь к стандартному ассету. Домен меняется на "game"
                AssetLocation flowerLocation = new AssetLocation("game", "flower-" + randomFlower + "-free");
                Block flowerBlock = this.Api.World.GetBlock(flowerLocation);

                if (flowerBlock != null)
                {
                    this.Api.World.BlockAccessor.SetBlock(flowerBlock.BlockId, targetPos);
                    SpawnSpawnParticles(targetPos);

                    this.Api.World.PlaySoundAt(new AssetLocation("game", "sounds/block/plant"), targetPos.X + 0.5, targetPos.Y + 0.5, targetPos.Z + 0.5, null, true, 16, 1f);

                    return true;
                }
            }
            return false;
        }

        private void SpawnSpawnParticles(BlockPos pos)
        {
            SimpleParticleProperties particles = new SimpleParticleProperties(
                5, 10, ColorUtil.ToRgba(255, 150, 255, 150),
                new Vec3d(pos.X, pos.Y, pos.Z), new Vec3d(pos.X + 1, pos.Y + 0.5, pos.Z + 1),
                new Vec3f(-0.5f, 0.5f, -0.5f), new Vec3f(0.5f, 1f, 0.5f),
                1.5f, -0.05f, 0.2f, 0.4f, EnumParticleModel.Quad
            );
            this.Api.World.SpawnParticles(particles);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("checkedOnPlacement", CheckedOnPlacement); // Сохраняем флаг

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
            CheckedOnPlacement = tree.GetBool("checkedOnPlacement", false); // Загружаем флаг

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