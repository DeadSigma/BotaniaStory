using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.Flora.GeneratingFlora
{
    // Абстрактный класс - это значит, что сам по себе он не существует в игре,
    // он нужен только для того, чтобы другие цветы от него наследовались.
    public abstract class BlockEntityGeneratingFlower : BlockEntity
    {
        // Общие переменные
        public int CurrentMana = 0;
        public int MaxMana = 10000;
        public BlockPos LinkedSpreader = null;

        // Общий поиск распространителя (при установке)
        public void FindSpreader()
        {
            int radius = 6;
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos checkPos = Pos.AddCopy(x, y, z);
                        if (Api.World.BlockAccessor.GetBlockEntity(checkPos) is BlockEntityManaSpreader)
                        {
                            LinkedSpreader = checkPos;
                            return;
                        }
                    }
                }
            }
        }

        // Общая функция для проверки связи и передачи маны!
        // Вызывай её в OnServerTick каждого цветка.
        protected void ProcessManaTransfer(ref bool dirty)
        {
            // 1. ПРОВЕРКА СВЯЗИ
            if (LinkedSpreader != null)
            {
                BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(LinkedSpreader);
                if (!(be is BlockEntityManaSpreader))
                {
                    LinkedSpreader = null;
                    dirty = true;
                }
            }

            // 2. ПЕРЕДАЧА МАНЫ
            if (LinkedSpreader != null && CurrentMana > 0)
            {
                BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(LinkedSpreader);
                if (be is BlockEntityManaSpreader spreader)
                {
                    int space = spreader.MaxMana - spreader.CurrentMana;
                    int toMove = Math.Min(CurrentMana, space);

                    if (toMove > 0)
                    {
                        spreader.CurrentMana += toMove;
                        spreader.MarkDirty(true);
                        this.CurrentMana -= toMove;
                        dirty = true;
                    }
                }
            }
        }

        // Общее сохранение
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("mana", CurrentMana);

            if (LinkedSpreader != null)
            {
                tree.SetInt("lx", LinkedSpreader.X);
                tree.SetInt("ly", LinkedSpreader.Y);
                tree.SetInt("lz", LinkedSpreader.Z);
                tree.SetBool("hasSpreader", true);
            }
            else
            {
                tree.SetBool("hasSpreader", false);
            }
        }

        // Общая загрузка
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            CurrentMana = tree.GetInt("mana");

            if (tree.GetBool("hasSpreader"))
            {
                LinkedSpreader = new BlockPos(tree.GetInt("lx"), tree.GetInt("ly"), tree.GetInt("lz"));
            }
            else
            {
                LinkedSpreader = null;
            }
        }
    }
}