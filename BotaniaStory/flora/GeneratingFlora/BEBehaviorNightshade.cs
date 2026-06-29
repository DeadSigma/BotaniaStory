using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BEBehaviorNightshade : BEBehaviorGeneratingFlower
    {
        // СТАТИЧНЫЙ СЛОВАРЬ: Считает все Пасклены игроков на сервере (для Soft Cap отдельно от Дневноцветов)
        public static Dictionary<string, int> PlayerShadesCount = new Dictionary<string, int>();

        public string OwnerUID = null;
        public double PlantedTotalDays = 0;

        private float fractionalMana = 0f;

        public BEBehaviorNightshade(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            MaxMana = 10000;

            if (api.Side == EnumAppSide.Server)
            {
                if (OwnerUID != null)
                {
                    if (!PlayerShadesCount.ContainsKey(OwnerUID)) PlayerShadesCount[OwnerUID] = 0;
                    PlayerShadesCount[OwnerUID]++;
                }

                if (PlantedTotalDays == 0)
                {
                    PlantedTotalDays = this.Api.World.Calendar.TotalDays;
                }

                this.Blockentity.RegisterGameTickListener(OnServerTick, 1000);
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (this.Api?.Side == EnumAppSide.Server && OwnerUID != null && PlayerShadesCount.ContainsKey(OwnerUID))
            {
                PlayerShadesCount[OwnerUID]--;
                if (PlayerShadesCount[OwnerUID] <= 0) PlayerShadesCount.Remove(OwnerUID);
            }
        }

        private void OnServerTick(float dt)
        {
            bool dirty = false;
            double currentDays = this.Api.World.Calendar.TotalDays;

            // Старение (Живёт 3 игровых дня)
            double daysAlive = currentDays - PlantedTotalDays;
            if (daysAlive >= 3.0)
            {
                Block currentBlock = this.Api.World.BlockAccessor.GetBlock(this.Blockentity.Pos);
                Block deadBlock = null;

                if (currentBlock != null && currentBlock.Code.Path.Contains("floatingisland"))
                {
                    deadBlock = this.Api.World.GetBlock(new AssetLocation("botaniastory", "floatingisland-deadflower"));
                }
                else
                {
                    deadBlock = this.Api.World.GetBlock(new AssetLocation("botaniastory", "deadflower-free"));
                }

                if (deadBlock != null)
                {
                    this.Api.World.BlockAccessor.SetBlock(deadBlock.BlockId, this.Blockentity.Pos);
                    return;
                }
            }

            // БОНУС ПОЧВЫ
            float soilMult = 1.0f;
            Block downBlock = this.Api.World.BlockAccessor.GetBlock(this.Blockentity.Pos.DownCopy());

            if (downBlock != null)
            {
                string path = downBlock.Code.Path;
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

            // ПРОВЕРКА НОЧИ И ОТКРЫТОГО НЕБА
            int rainY = this.Api.World.BlockAccessor.GetRainMapHeightAt(this.Blockentity.Pos.X, this.Blockentity.Pos.Z);
            if (this.Blockentity.Pos.Y < rainY) return;

            float daylightStrength = this.Api.World.Calendar.GetDayLightStrength(this.Blockentity.Pos.X, this.Blockentity.Pos.Z);

            // Если слишком светло (день), не работаем
            if (daylightStrength > 0.4f) return;

            // Убрал плавное нарастание сумерек. Наступила ночь? Жарим на 100%!
            float darknessMult = 1f;

            // ШТРАФ ЗА КОЛИЧЕСТВО (Проверяем только другие Пасклены в радиусе 6 блоков)
            int nearbyFlowers = 0;
            for (int dx = -6; dx <= 6; dx++)
            {
                for (int dy = -6; dy <= 6; dy++)
                {
                    for (int dz = -6; dz <= 6; dz++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;
                        BlockPos checkPos = this.Blockentity.Pos.AddCopy(dx, dy, dz);

                        if (this.Api.World.BlockAccessor.GetBlockEntity(checkPos)?.GetBehavior<BEBehaviorNightshade>() != null)
                        {
                            nearbyFlowers++;
                        }
                    }
                }
            }
            float efficiency = 1f / (1f + nearbyFlowers * 0.4f);

            // СЕЗОНЫ (Справедливые)
            float seasonMult = 1f;
            int month = this.Api.World.Calendar.Month;

            // Зимой ночи долгие, цветок в своей стихии (бонус 20%)
            if (month == 12 || month == 1 || month == 2) seasonMult = 1.2f;
            // Весна/Осень: норма (100%)
            else if (month >= 3 && month <= 5) seasonMult = 1.0f;
            else if (month >= 9 && month <= 11) seasonMult = 1.0f;
            // Лето: небольшой штраф за теплые светлые ночи (80%)
            else seasonMult = 0.8f;

            // SOFT CAP
            float globalMult = 1f;
            if (OwnerUID != null && PlayerShadesCount.ContainsKey(OwnerUID))
            {
                int totalFlowers = PlayerShadesCount[OwnerUID];
                if (totalFlowers > 8)
                {
                    int extra = totalFlowers - 8;
                    globalMult = (float)Math.Pow(0.9, extra);
                }
            }

            // ФИНАЛЬНЫЙ РАСЧЕТ И ВЫДАЧА МАНЫ
            float baseManaPerSec = 4f;
            float generatedThisSec = baseManaPerSec * darknessMult * efficiency * seasonMult * globalMult * soilMult;

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

            if (dirty) this.Blockentity.MarkDirty(true);
        }

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