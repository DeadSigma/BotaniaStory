using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BlockEntityDaybloom : BlockEntity
    {
        public int CurrentMana = 0;
        public int MaxMana = 10000;
        public BlockPos LinkedSpreader = null;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                // Цветок "думает" 1 раз в секунду
                RegisterGameTickListener(OnServerTick, 1000); 
            }
        }

        private void OnServerTick(float dt)
        {
            // 1. ВЫРАБОТКА МАНЫ (Только если светит солнце!)
            // Проверяем уровень именно солнечного света (от 0 до 15) над цветком
            int sunLight = Api.World.BlockAccessor.GetLightLevel(Pos, EnumLightLevelType.OnlySunLight);
            
            // Если свет больше 10 (день и нет крыши над головой)
            if (sunLight > 10) 
            {
                CurrentMana += 100000; // ТЕСТ: ОГРОМНОЕ КОЛИЧЕСТВО МАНЫ
                if (CurrentMana > MaxMana) CurrentMana = MaxMana;
            }

            // 2. АВТО-ПРИВЯЗКА (Ищем Распространитель)
            if (LinkedSpreader == null)
            {
                FindSpreader();
            }

            // 3. ПЕРЕДАЧА МАНЫ В РАСПРОСТРАНИТЕЛЬ
            if (LinkedSpreader != null && CurrentMana > 0)
            {
                BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(LinkedSpreader);
                if (be is BlockEntityManaSpreader spreader)
                {
                    // Сколько свободного места в распространителе?
                    int space = spreader.MaxMana - spreader.CurrentMana;
                    
                    // Берем либо всю нашу ману, либо сколько влезет
                    int toMove = Math.Min(CurrentMana, space); 
                    
                    if (toMove > 0)
                    {
                        spreader.CurrentMana += toMove;
                        spreader.MarkDirty(true);
                        
                        this.CurrentMana -= toMove;
                    }
                }
                else
                {
                    LinkedSpreader = null; // Если распространитель сломали - отвязываемся
                }
            }
        }

        // Функция сканирования территории 13x13x13 блоков вокруг цветка
        private void FindSpreader()
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
                            return; // Нашли! Прерываем поиск.
                        }
                    }
                }
            }
        }

        // Сохранение и загрузка
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("mana", CurrentMana);
            if (LinkedSpreader != null)
            {
                tree.SetInt("lx", LinkedSpreader.X);
                tree.SetInt("ly", LinkedSpreader.Y);
                tree.SetInt("lz", LinkedSpreader.Z);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            CurrentMana = tree.GetInt("mana");
            if (tree.HasAttribute("lx"))
            {
                LinkedSpreader = new BlockPos(tree.GetInt("lx"), tree.GetInt("ly"), tree.GetInt("lz"));
            }
        }
    }
}