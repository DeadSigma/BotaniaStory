using BotaniaStory.blockentity;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.Flora.GeneratingFlora
{
    // Абстрактный класс поведения для всех генерирующих цветов
    public abstract class BEBehaviorGeneratingFlower : BlockEntityBehavior, ILinkableToSpreader
    {
        // Общие переменные
        public int CurrentMana = 0;
        public int MaxMana = 10000;

        // ВАЖНО: Это теперь свойство (Property), чтобы соответствовать интерфейсу ILinkableToSpreader
        public BlockPos LinkedSpreader { get; set; } = null;

        public BEBehaviorGeneratingFlower(BlockEntity blockentity) : base(blockentity) { }

        // Добавляем обязательный метод Initialize для поведения
        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
        }

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
                        // ВАЖНО: Используем this.Blockentity.Pos
                        BlockPos checkPos = this.Blockentity.Pos.AddCopy(x, y, z);

                        // ВАЖНО: Используем this.Api
                        if (this.Api.World.BlockAccessor.GetBlockEntity(checkPos) is BlockEntityManaSpreader)
                        {
                            LinkedSpreader = checkPos.Copy(); // Копируем позицию для безопасности
                            return;
                        }
                    }
                }
            }
        }

        // Общая функция для проверки связи и передачи маны!
        protected void ProcessManaTransfer(ref bool dirty)
        {
            // 1. ПРОВЕРКА СВЯЗИ
            if (LinkedSpreader != null)
            {
                // ВАЖНО: Используем this.Api
                BlockEntity be = this.Api.World.BlockAccessor.GetBlockEntity(LinkedSpreader);
                if (!(be is BlockEntityManaSpreader))
                {
                    LinkedSpreader = null;
                    dirty = true;
                }
            }

            // 2. ПЕРЕДАЧА МАНЫ
            if (LinkedSpreader != null && CurrentMana > 0)
            {
                BlockEntity be = this.Api.World.BlockAccessor.GetBlockEntity(LinkedSpreader);
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