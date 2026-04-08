using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BlockEntityDaybloom : BlockEntityGeneratingFlower
    {
        // СТАТИЧНЫЙ СЛОВАРЬ: Считает все Дневноцветы игроков на сервере (для Soft Cap)
        public static Dictionary<string, int> PlayerBloomsCount = new Dictionary<string, int>();

        public string OwnerUID = null; // Кто посадил цветок
        public double PlantedTotalDays = 0; // День посадки
        public float CurrentFertility = 100f; // Плодородность почвы (%)
        private double lastDayUpdate = 0; // Для отслеживания смены дней

        private float fractionalMana = 0f; // Копилка для дробной маны

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            MaxMana = 10000; // Твой максимум из файла

            if (api.Side == EnumAppSide.Server)
            {
                // Записываем цветок в глобальную статистику
                if (OwnerUID != null)
                {
                    if (!PlayerBloomsCount.ContainsKey(OwnerUID)) PlayerBloomsCount[OwnerUID] = 0;
                    PlayerBloomsCount[OwnerUID]++;
                }

                // Запоминаем день посадки, если это первый раз
                if (PlantedTotalDays == 0)
                {
                    PlantedTotalDays = Api.World.Calendar.TotalDays;
                    lastDayUpdate = Api.World.Calendar.TotalDays;
                }

                // Тик ровно раз в секунду (1000 мс) - супер оптимизация!
                RegisterGameTickListener(OnServerTick, 1000);
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            // Убираем цветок из статистики при разрушении
            if (Api?.Side == EnumAppSide.Server && OwnerUID != null && PlayerBloomsCount.ContainsKey(OwnerUID))
            {
                PlayerBloomsCount[OwnerUID]--;
                if (PlayerBloomsCount[OwnerUID] <= 0) PlayerBloomsCount.Remove(OwnerUID);
            }
        }

        private void OnServerTick(float dt)
        {
            bool dirty = false;
            double currentDays = Api.World.Calendar.TotalDays;

            // ==========================================
            // 1. СТАРЕНИЕ (Теперь живёт 1 игровой день)
            // ==========================================
            double daysAlive = currentDays - PlantedTotalDays;
            if (daysAlive >= 1.0)
            {
                // Ищем наш новый мертвый цветок (добавляем -free, так как у нас есть варианты снега)
                Block deadBlock = Api.World.GetBlock(new AssetLocation("botaniastory", "deadflower-free"));
                if (deadBlock != null)
                {
                    Api.World.BlockAccessor.SetBlock(deadBlock.BlockId, Pos);
                    return; // Полностью прекращаем работу этого цветка
                }
            }

            // ==========================================
            // 2. ПОЧВА (Падение на 2% каждый новый день)
            // ==========================================
            if (currentDays - lastDayUpdate >= 1.0)
            {
                lastDayUpdate = currentDays;
                CurrentFertility -= 2f;
                dirty = true;
            }

            // Проверка: Что под нами? (Должна быть почва)
            Block downBlock = Api.World.BlockAccessor.GetBlock(Pos.DownCopy());
            if (downBlock == null || (!downBlock.Code.Path.Contains("soil") && !downBlock.Code.Path.Contains("farmland")))
            {
                CurrentFertility -= 5f; // На камне или досках умирает очень быстро
            }

            // Если плодородия меньше 30% - ману не даём
            if (CurrentFertility < 30f) return;

            // ==========================================
            // 3. ПРОВЕРКА СОЛНЦА, ПОГОДЫ И НОЧИ
            // ==========================================
            // Небо открыто? (Блок дождя ниже или равен Y цветка)
            int rainY = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z);
            if (Pos.Y < rainY) return; // Под крышей или в пещере

            // Идет ли дождь?
            float rainfall = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Rainfall;
            if (rainfall > 0.05f) return; // В дождь не работаем

            // НОВОЕ: Проверка времени суток (ночью вернет 0, днем около 1.0)
            float daylightStrength = Api.World.Calendar.GetDayLightStrength(Pos.X, Pos.Z);
            if (daylightStrength < 0.4f) return; // Если солнца меньше 40% (вечер/ночь) - цветок спит!

            // Уровень солнечного света (чтобы не работал в глухой тени стен днем)
            int sunLight = Api.World.BlockAccessor.GetLightLevel(Pos, EnumLightLevelType.OnlySunLight);
            if (sunLight < 15) return;

            // Множитель теперь учитывает и тень, и время суток
            float sunlightMult = (sunLight / 22f) * daylightStrength;

            // ==========================================
            // 4. ШТРАФ ЗА КОЛИЧЕСТВО (Радиус 6 блоков)
            // ==========================================
            int nearbyFlowers = 0;
            // Простая проверка куба 13x13x13 вокруг цветка
            for (int dx = -6; dx <= 6; dx++)
            {
                for (int dy = -6; dy <= 6; dy++)
                {
                    for (int dz = -6; dz <= 6; dz++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;
                        BlockPos checkPos = Pos.AddCopy(dx, dy, dz);
                        if (Api.World.BlockAccessor.GetBlockEntity(checkPos) is BlockEntityDaybloom)
                        {
                            nearbyFlowers++;
                        }
                    }
                }
            }
            float efficiency = 1f / (1f + nearbyFlowers * 0.4f);

            // ==========================================
            // 5. СЕЗОНЫ
            // ==========================================
            float seasonMult = 1f;
            int month = Api.World.Calendar.Month;
            if (month == 12 || month == 1 || month == 2) seasonMult = 0.15f; // Зима (15%)
            else if (month >= 3 && month <= 5) seasonMult = 0.8f;            // Весна (80%)
            else if (month >= 9 && month <= 11) seasonMult = 0.6f;           // Осень (60%)

            // ==========================================
            // 6. SOFT CAP (Глобальный штраф на игрока > 8 цветов)
            // ==========================================
            float globalMult = 1f;
            if (OwnerUID != null && PlayerBloomsCount.ContainsKey(OwnerUID))
            {
                int totalFlowers = PlayerBloomsCount[OwnerUID];
                if (totalFlowers > 8)
                {
                    int extra = totalFlowers - 8;
                    globalMult = (float)Math.Pow(0.9, extra);
                }
            }

            // ==========================================
            // ФИНАЛЬНЫЙ РАСЧЕТ И ВЫДАЧА МАНЫ
            // ==========================================
            // Базово дает 0.25 маны в секунду (как ты просил в формуле)
            float baseManaPerSec = 100.00f;
            float generatedThisSec = baseManaPerSec * sunlightMult * efficiency * seasonMult * globalMult;

            // Копим дробную ману
            fractionalMana += generatedThisSec;

            // Как только накопилась хотя бы 1 целая единица маны - добавляем в CurrentMana
            if (fractionalMana >= 1f && CurrentMana < MaxMana)
            {
                int manaToAdd = (int)Math.Floor(fractionalMana);
                CurrentMana += manaToAdd;
                fractionalMana -= manaToAdd;

                if (CurrentMana > MaxMana) CurrentMana = MaxMana;
                dirty = true;
            }

            // Передача маны (из родительского класса)
            ProcessManaTransfer(ref dirty);

            if (dirty) MarkDirty(true);
        }

        // ==========================================
        // СОХРАНЕНИЕ ДАННЫХ ПРИ ПЕРЕЗАХОДЕ
        // ==========================================
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (OwnerUID != null) tree.SetString("ownerUID", OwnerUID);
            tree.SetDouble("plantedDays", PlantedTotalDays);
            tree.SetFloat("fertility", CurrentFertility);
            tree.SetDouble("lastDayUpdate", lastDayUpdate);
            tree.SetFloat("fracMana", fractionalMana);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            OwnerUID = tree.GetString("ownerUID", null);
            PlantedTotalDays = tree.GetDouble("plantedDays", 0);
            CurrentFertility = tree.GetFloat("fertility", 100f);
            lastDayUpdate = tree.GetDouble("lastDayUpdate", 0);
            fractionalMana = tree.GetFloat("fracMana", 0f);
        }
    }
}