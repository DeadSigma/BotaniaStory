using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.blockentity
{
    public class BlockEntityJadedAmaranthus : BlockEntity
    {
        private int flowerSpawnRadius = 4;
        private int manaCostPerFlower = 100;

        // Координаты привязанного бассейна маны
        public BlockPos LinkedPool { get; set; } = null;

        private readonly string[] flowerColors = new string[] {
            "white", "orange", "magenta", "lightblue", "yellow", "lime", "pink", "gray",
            "lightgray", "cyan", "purple", "blue", "brown", "green", "red", "black"
        };

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnTick, 1000); // Оставь 1000 для теста, потом верни 3000
            }
        }

        private void OnTick(float dt)
        {
            // Если бассейн не привязан - ничего не делаем
            if (LinkedPool == null) return;

            // Получаем блок по координатам привязки
            BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(LinkedPool);

            // 1. ИСПРАВЛЕНИЕ: Если бассейна больше нет (сломали), очищаем память цветка!
            if (!(be is BlockEntityManaPool pool))
            {
                LinkedPool = null;
                MarkDirty(true);
                return;
            }

            // Проверяем ману
            if (pool.CurrentMana >= manaCostPerFlower)
            {
                if (TrySpawnMysticalFlower())
                {
                    pool.ConsumeMana(manaCostPerFlower);
                }
            }
        }

        // 2. ИСПРАВЛЕНИЕ: Метод для автоматического поиска бассейна
        public void AutoFindPool()
        {
            int searchRadius = 6; // Радиус поиска 6 блоков
            for (int x = -searchRadius; x <= searchRadius; x++)
            {
                for (int y = -searchRadius; y <= searchRadius; y++)
                {
                    for (int z = -searchRadius; z <= searchRadius; z++)
                    {
                        BlockPos checkPos = Pos.AddCopy(x, y, z);
                        if (Api.World.BlockAccessor.GetBlockEntity(checkPos) is BlockEntityManaPool)
                        {
                            LinkedPool = checkPos.Copy();
                            MarkDirty(true);
                            return; // Привязываемся к первому найденному и выходим
                        }
                    }
                }
            }
        }

        private bool TrySpawnMysticalFlower()
        {
            int xOffset = Api.World.Rand.Next(-flowerSpawnRadius, flowerSpawnRadius + 1);
            int zOffset = Api.World.Rand.Next(-flowerSpawnRadius, flowerSpawnRadius + 1);
            int yOffset = Api.World.Rand.Next(-2, 3);

            BlockPos targetPos = Pos.AddCopy(xOffset, yOffset, zOffset);
            BlockPos belowPos = targetPos.DownCopy();

            Block targetBlock = Api.World.BlockAccessor.GetBlock(targetPos);
            Block belowBlock = Api.World.BlockAccessor.GetBlock(belowPos);

            if (targetBlock?.Code == null || belowBlock?.Code == null) return false;

            if (targetBlock.Replaceable >= 6000 &&
                (belowBlock.Code.Path.Contains("soil") || belowBlock.Code.Path.Contains("grass")))
            {
                string randomColor = flowerColors[Api.World.Rand.Next(flowerColors.Length)];
                AssetLocation flowerLocation = new AssetLocation("botaniastory", "mysticalflower-" + randomColor + "-free");
                Block flowerBlock = Api.World.GetBlock(flowerLocation);

                if (flowerBlock != null)
                {
                    Api.World.BlockAccessor.SetBlock(flowerBlock.BlockId, targetPos);
                    SpawnSpawnParticles(targetPos);

                    // ДОБАВЛЕН ЗВУК ИГРЫ (Обычная посадка растения)
                    Api.World.PlaySoundAt(new AssetLocation("game", "sounds/block/plant"), targetPos.X + 0.5, targetPos.Y + 0.5, targetPos.Z + 0.5, null, true, 16, 1f);

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
            Api.World.SpawnParticles(particles);
        }

        // СОХРАНЕНИЕ ПРИВЯЗКИ ПРИ ВЫХОДЕ ИЗ ИГРЫ
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (LinkedPool != null)
            {
                tree.SetInt("poolX", LinkedPool.X);
                tree.SetInt("poolY", LinkedPool.Y);
                tree.SetInt("poolZ", LinkedPool.Z);
            }
        }

        // ЗАГРУЗКА ПРИВЯЗКИ ПРИ ЗАХОДЕ В ИГРУ
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
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