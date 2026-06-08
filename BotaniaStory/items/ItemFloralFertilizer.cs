using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.items
{
    public class ItemFloralFertilizer : Item
    {
        // Цвета строго соответствуют variantgroups "color" из mysticalflower.json
        private readonly string[] colors = new string[]
        {
            "white", "orange", "magenta", "lightblue", "yellow", "lime",
            "pink", "gray", "lightgray", "cyan", "purple", "blue",
            "brown", "green", "red", "black"
        };

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            // Проверка прав на строительство в этом регионе
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return;

            handling = EnumHandHandling.PreventDefault;

            // Логику генерации выполняем только на сервере
            if (api.Side == EnumAppSide.Server)
            {
                bool success = SpawnFlowers(api as ICoreServerAPI, blockSel.Position);

                if (success)
                {
                    // Воспроизводим звук и тратим предмет, если мы не в креативе
                    api.World.PlaySoundAt(new AssetLocation("game", "sounds/block/plant"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);

                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        slot.TakeOut(1);
                        slot.MarkDirty();
                    }

                    // Анимация руки
                    byEntity.AnimManager.StartAnimation("interact");
                }
            }
        }

        private bool SpawnFlowers(ICoreServerAPI sapi, BlockPos center)
        {
            int radius = 4; // Радиус распыления
            int flowersSpawned = 0;
            Random rand = sapi.World.Rand;

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    // Делаем область круглой, а не квадратной
                    if (x * x + z * z > radius * radius) continue;

                    // 20% шанс появления цветка на подходящем блоке
                    if (rand.NextDouble() > 0.2) continue;

                    // Проверяем небольшие перепады высот (от -1 до +1 блока от клика)
                    for (int y = 1; y >= -1; y--)
                    {
                        BlockPos checkPos = center.AddCopy(x, y, z);
                        Block groundBlock = sapi.World.BlockAccessor.GetBlock(checkPos);
                        Block airBlock = sapi.World.BlockAccessor.GetBlock(checkPos.UpCopy());

                        // Проверяем, что под цветком есть почва (Fertility > 0 или это блок земли), а сверху пусто
                        if ((groundBlock.Fertility > 0 || groundBlock.Code.Path.Contains("soil")) && airBlock.Replaceable >= 6000)
                        {
                            string randomColor = colors[rand.Next(colors.Length)];

                            // Собираем код цветка: используем вариант cover "free"
                            AssetLocation flowerCode = new AssetLocation("botaniastory", $"mysticalflower-{randomColor}-free");
                            Block flowerBlock = sapi.World.GetBlock(flowerCode);

                            if (flowerBlock != null)
                            {
                                sapi.World.BlockAccessor.SetBlock(flowerBlock.BlockId, checkPos.UpCopy());
                                flowersSpawned++;
                                break; // Переходим к следующим координатам x, z
                            }
                        }
                    }
                }
            }

            return flowersSpawned > 0;
        }
    }
}