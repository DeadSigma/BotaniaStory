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

        private float fractionalMana = 0f; // Копилка для дробной маны

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            MaxMana = 10000;

            if (api.Side == EnumAppSide.Server)
            {
                if (OwnerUID != null)
                {
                    if (!PlayerBloomsCount.ContainsKey(OwnerUID)) PlayerBloomsCount[OwnerUID] = 0;
                    PlayerBloomsCount[OwnerUID]++;
                }

                if (PlantedTotalDays == 0)
                {
                    PlantedTotalDays = Api.World.Calendar.TotalDays;
                }

                // Тик ровно раз в секунду
                RegisterGameTickListener(OnServerTick, 1000);
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
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
            // 1. СТАРЕНИЕ (Живёт 1 игровой день)
            // ==========================================
            double daysAlive = currentDays - PlantedTotalDays;
            if (daysAlive >= 1.0)
            {
                Block deadBlock = Api.World.GetBlock(new AssetLocation("botaniastory", "deadflower-free"));
                if (deadBlock != null)
                {
                    Api.World.BlockAccessor.SetBlock(deadBlock.BlockId, Pos);
                    return;
                }
            }

            // ==========================================
            // 2. БОНУС ПОЧВЫ (Мгновенный множитель)
            // ==========================================
            float soilMult = 1.0f; // По умолчанию множитель х1 (обычная бедная почва)
            Block downBlock = Api.World.BlockAccessor.GetBlock(Pos.DownCopy());

            if (downBlock != null)
            {
                string path = downBlock.Code.Path;

                // Проверяем, земля это или грядка
                if (path.Contains("soil") || path.Contains("farmland"))
                {
                    if (path.Contains("medium")) soilMult = 1.04f; 
                    else if (path.Contains("high")) soilMult = 1.15f; 
                    else if (path.Contains("terrapreta") || path.Contains("compost")) soilMult = 1.07f; 
                }
                else
                {
                    soilMult = 0.5f;
                }
            }

            // ==========================================
            // 3. ПРОВЕРКА СОЛНЦА, ПОГОДЫ И НОЧИ
            // ==========================================
            int rainY = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z);
            if (Pos.Y < rainY) return;

            float rainfall = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Rainfall;
            if (rainfall > 0.05f) return;

            float daylightStrength = Api.World.Calendar.GetDayLightStrength(Pos.X, Pos.Z);
            if (daylightStrength < 0.4f) return;

            int sunLight = Api.World.BlockAccessor.GetLightLevel(Pos, EnumLightLevelType.OnlySunLight);
            if (sunLight < 15) return;

            float sunlightMult = (sunLight / 22f) * daylightStrength;

            // ==========================================
            // 4. ШТРАФ ЗА КОЛИЧЕСТВО (Радиус 6 блоков)
            // ==========================================
            int nearbyFlowers = 0;
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
            if (month == 12 || month == 1 || month == 2) seasonMult = 0.15f;
            else if (month >= 3 && month <= 5) seasonMult = 0.8f;
            else if (month >= 9 && month <= 11) seasonMult = 0.6f;

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
            float baseManaPerSec = 4f; 

            // НОВОЕ: Умножаем всё на наш soilMult (бонус почвы)
            float generatedThisSec = baseManaPerSec * sunlightMult * efficiency * seasonMult * globalMult * soilMult;

            fractionalMana += generatedThisSec;

            if (fractionalMana >= 1f && CurrentMana < MaxMana)
            {
                int manaToAdd = (int)Math.Floor(fractionalMana);
                CurrentMana += manaToAdd;
                fractionalMana -= manaToAdd;

                if (CurrentMana > MaxMana) CurrentMana = MaxMana;
                dirty = true;
            }

            ProcessManaTransfer(ref dirty);

            if (dirty) MarkDirty(true);
        }

        // ==========================================
        // СОХРАНЕНИЕ ДАННЫХ
        // ==========================================
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (OwnerUID != null) tree.SetString("ownerUID", OwnerUID);
            tree.SetDouble("plantedDays", PlantedTotalDays);
            tree.SetFloat("fracMana", fractionalMana);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            OwnerUID = tree.GetString("ownerUID", null);
            PlantedTotalDays = tree.GetDouble("plantedDays", 0);
            fractionalMana = tree.GetFloat("fracMana", 0f);
        }
    }
}