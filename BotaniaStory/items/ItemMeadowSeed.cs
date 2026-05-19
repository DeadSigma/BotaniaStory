using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ItemMeadowSeed : Item
    {
        private readonly string[] tallGrassCodes = new string[]
        {
            "tallgrass-veryshort-free",
            "tallgrass-short-free",
            "tallgrass-mediumshort-free",
            "tallgrass-medium-free"
        };

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            IWorldAccessor world = api.World;
            BlockPos pos = blockSel.Position;
            Block clickedBlock = world.BlockAccessor.GetBlock(pos);

            if (!clickedBlock.Code.Path.Contains("soil") && !clickedBlock.Code.Path.Contains("peat"))
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            // Блокируем стандартное действие для клиента и сервера
            handling = EnumHandHandling.PreventDefault;

            string seedType = this.Variant["type"];

            if (world.Side == EnumAppSide.Server)
            {
                bool success = false;

                if (seedType == "normal") success = ApplyNormalMeadow(world, pos, clickedBlock);
                if (seedType == "peat") success = ApplyPeatMeadow(world, pos);
                if (seedType == "medium") success = ApplyMediumMeadow(world, pos);

                if (success)
                {
                    // Тратим семечко
                    if (byEntity is EntityPlayer player && player.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        slot.TakeOut(1);
                        slot.MarkDirty();
                    }

                    // Воспроизводим звук
                    world.PlaySoundAt(new AssetLocation("game:sounds/block/plant"), pos.X + 0.5, pos.Y + 1, pos.Z + 0.5, null);
                }
            }
        }

        private bool ApplyNormalMeadow(IWorldAccessor world, BlockPos centerPos, Block clickedBlock)
        {
            bool changed = false;

            // 1. Пытаемся сохранить оригинальное плодородие центрального блока
            string fertility = "low"; // по умолчанию
            if (clickedBlock.Code.Path.Contains("-medium-")) fertility = "medium";
            if (clickedBlock.Code.Path.Contains("-high-")) fertility = "high";
            if (clickedBlock.Code.Path.Contains("-compost-")) fertility = "compost";

            Block grassSoil = world.GetBlock(new AssetLocation("game", $"soil-{fertility}-normal"));

            // Заменяем центральный блок, если он отличается
            if (grassSoil != null && clickedBlock.Id != grassSoil.Id)
            {
                world.BlockAccessor.SetBlock(grassSoil.Id, centerPos);
                world.BlockAccessor.MarkBlockDirty(centerPos); // Принудительно обновляем клиенты
                changed = true;
            }

            // 2. Создаем траву вокруг
            int radius = 3;
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    // Делаем область круглой
                    if (x * x + z * z > radius * radius + 1) continue;

                    BlockPos targetPos = centerPos.AddCopy(x, 0, z);
                    Block targetBlock = world.BlockAccessor.GetBlock(targetPos);

                    BlockPos abovePos = targetPos.UpCopy();
                    Block blockAbove = world.BlockAccessor.GetBlock(abovePos);

                    // Проверяем, что это земля, а над ней пусто
                    if (targetBlock.Code.Path.Contains("soil") && blockAbove.Replaceable >= 6000)
                    {
                        // Вероятность 60%, что на этом блоке что-то вырастет (пятнистое зарастание)
                        if (world.Rand.NextDouble() > 0.6) continue;

                        // Если земля голая, превращаем её в заросшую
                        if (targetBlock.Code.Path.EndsWith("-none"))
                        {
                            string targetFertility = targetBlock.Code.Path.Split('-')[1];
                            Block newSoil = world.GetBlock(new AssetLocation("game", $"soil-{targetFertility}-normal"));
                            if (newSoil != null)
                            {
                                world.BlockAccessor.SetBlock(newSoil.Id, targetPos);
                                world.BlockAccessor.MarkBlockDirty(targetPos);
                                changed = true;
                            }
                        }

                        // Сажаем случайную высокую траву из списка
                        string randomGrass = tallGrassCodes[world.Rand.Next(tallGrassCodes.Length)];
                        Block tallGrassBlock = world.GetBlock(new AssetLocation("game", randomGrass));

                        if (tallGrassBlock != null)
                        {
                            world.BlockAccessor.SetBlock(tallGrassBlock.Id, abovePos);
                            world.BlockAccessor.MarkBlockDirty(abovePos);
                            changed = true;
                        }
                    }
                }
            }
            return changed;
        }

        private bool ApplyPeatMeadow(IWorldAccessor world, BlockPos pos)
        {
            Block peatBlock = world.GetBlock(new AssetLocation("game", "peat-verysparse"));
            if (peatBlock != null)
            {
                world.BlockAccessor.SetBlock(peatBlock.Id, pos);
                world.BlockAccessor.MarkBlockDirty(pos);
                return true;
            }
            return false;
        }

        private bool ApplyMediumMeadow(IWorldAccessor world, BlockPos pos)
        {
            Block mediumGrassSoil = world.GetBlock(new AssetLocation("game", "soil-medium-normal"));
            if (mediumGrassSoil != null)
            {
                world.BlockAccessor.SetBlock(mediumGrassSoil.Id, pos);
                world.BlockAccessor.MarkBlockDirty(pos);
                return true;
            }
            return false;
        }
    }
}