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
            bool dirty = false; // Флаг: нужно ли обновить картинку для игрока?

            // 1. ВЫРАБОТКА МАНЫ (Только если светит солнце!)
            int sunLight = Api.World.BlockAccessor.GetLightLevel(Pos, EnumLightLevelType.OnlySunLight);
            if (sunLight > 10)
            {
                if (CurrentMana < MaxMana)
                {
                    CurrentMana += 500; // ТЕСТОВОЕ ЗНАЧЕНИЕ
                    if (CurrentMana > MaxMana) CurrentMana = MaxMana;
                    dirty = true; // Цифры изменились, нужно обновить HUD
                }
            }

            // 2. АВТО-ПРИВЯЗКА (Ищем Распространитель)
            // 2. ПРОВЕРКА СВЯЗИ И АВТО-ПРИВЯЗКА
            if (LinkedSpreader != null)
            {
                // Проверяем, стоит ли еще на этом месте распространитель
                BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(LinkedSpreader);
                if (!(be is BlockEntityManaSpreader))
                {
                    LinkedSpreader = null; // Если там пусто или другой блок - забываем!
                    dirty = true;
                }
            }

            // Если связи нет - пытаемся найти новый
            if (LinkedSpreader == null)
            {
                FindSpreader();
                if (LinkedSpreader != null)
                {
                    dirty = true; // Ура, нашли! Обновляем HUD
                }
            }

            // 3. ПЕРЕДАЧА МАНЫ В РАСПРОСТРАНИТЕЛЬ
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
                        dirty = true; // Мана ушла, обновляем HUD
                    }
                }
                else
                {
                    LinkedSpreader = null; // Распространитель сломали
                    dirty = true; // Обновляем HUD, чтобы убрать галочку
                }
            }

            // Если хоть что-то поменялось — пинаем клиент, чтобы HUD перерисовался!
            if (dirty)
            {
                this.MarkDirty(true);
            }
        }

        // Функция сканирования территории 13x13x13 блоков вокруг цветка
        private void FindSpreader()
        {
            int radius = 3;
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

        // ==========================================
        // СОХРАНЕНИЕ И ЗАГРУЗКА (Синхронизация)
        // ==========================================
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("mana", CurrentMana);

            if (LinkedSpreader != null)
            {
                tree.SetInt("lx", LinkedSpreader.X);
                tree.SetInt("ly", LinkedSpreader.Y);
                tree.SetInt("lz", LinkedSpreader.Z);
                tree.SetBool("hasSpreader", true); // Явно говорим: "Цель есть!"
            }
            else
            {
                tree.SetBool("hasSpreader", false); // Явно говорим: "Цели нет, забудь!"
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            CurrentMana = tree.GetInt("mana");

            // Клиент проверяет рубильник
            if (tree.GetBool("hasSpreader"))
            {
                LinkedSpreader = new BlockPos(tree.GetInt("lx"), tree.GetInt("ly"), tree.GetInt("lz"));
            }
            else
            {
                LinkedSpreader = null; // Стираем старую память принудительно!
            }
        }
    }
}