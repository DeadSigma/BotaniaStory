using BotaniaStory;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BEBehaviorHydroangeas : BEBehaviorGeneratingFlower
    {
        public int DigestTicksLeft = 0;

        // Время посадки цветка 
        public double PlantedTotalDays = -1;

        public BEBehaviorHydroangeas(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            MaxMana = 150;

            // Если время посадки еще не задано, берем текущее время календаря
            if (PlantedTotalDays < 0 && api.World != null)
            {
                PlantedTotalDays = api.World.Calendar.TotalDays;
            }

            if (api.Side == EnumAppSide.Server)
                this.Blockentity.RegisterGameTickListener(OnServerTick, 100);
            else
                this.Blockentity.RegisterGameTickListener(OnClientTick, 100);
        }

        private void OnServerTick(float dt)
        {
            bool dirty = false;
            double currentDays = this.Api.World.Calendar.TotalDays;

            // СТАРЕНИЕ
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
                    return; // Прерываем работу тика, так как цветок умер
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

            // Оптимизированные тайминги
            int manaPerCycle = 7;
            int ticksPerCycle = 6;

            // Генерация маны
            if (DigestTicksLeft > 0)
            {
                DigestTicksLeft--;
                dirty = true;

                if (CurrentMana < MaxMana)
                {
                    if (DigestTicksLeft % ticksPerCycle == 0)
                    {
                        // Применяем множитель и округляем до ближайшего целого ( 7 * 1.15 = 8.05 -> 8 маны)
                        CurrentMana += (int)Math.Round(manaPerCycle * soilMult);
                    }

                    if (CurrentMana > MaxMana) CurrentMana = MaxMana;
                }
            }
            // Поиск воды
            else if (CurrentMana <= MaxMana - 40)
            {
                BlockPos myPos = this.Blockentity.Pos;

                BlockPos[] offsets = {
                    myPos.AddCopy(1, 0, 0), myPos.AddCopy(-1, 0, 0),
                    myPos.AddCopy(0, 0, 1), myPos.AddCopy(0, 0, -1)
                };

                foreach (BlockPos checkPos in offsets)
                {
                    Block block = this.Api.World.BlockAccessor.GetBlock(checkPos);

                    if (block.LiquidCode == "water" && block.LiquidLevel == 7)
                    {
                        this.Api.World.BlockAccessor.SetBlock(0, checkPos);
                        DigestTicksLeft = 80;

                        ICoreServerAPI sapi = this.Api as ICoreServerAPI;
                        if (sapi != null)
                        {
                            sapi.World.PlaySoundAt(new AssetLocation("botaniastory:sounds/hydroangeas"), myPos.X + 0.5, myPos.Y + 0.5, myPos.Z + 0.5, null, true, 32, 1f);

                        }

                        dirty = true;
                        break;
                    }
                }
            }

            // Проверка подачи маны
            ProcessManaTransfer(ref dirty);

            if (dirty) this.Blockentity.MarkDirty(true);
        }

        // Партиклы воды
        private void OnClientTick(float dt)
        {
            if (DigestTicksLeft > 0 && this.Api.World.Rand.NextDouble() < 0.1)
            {
                SimpleParticleProperties waterParticles = new SimpleParticleProperties(
                    1, 2, ColorUtil.ToRgba(255, 60, 150, 255),
                    new Vec3d(this.Blockentity.Pos.X + 0.35, this.Blockentity.Pos.Y + 0.1, this.Blockentity.Pos.Z + 0.35),
                    new Vec3d(this.Blockentity.Pos.X + 0.65, this.Blockentity.Pos.Y + 0.4, this.Blockentity.Pos.Z + 0.65),
                    new Vec3f(-0.1f, 0.5f, -0.1f),
                    new Vec3f(0.1f, 1.0f, 0.1f),
                    0.8f, 0f, 0.4f, 1f, EnumParticleModel.Cube
                );

                this.Api.World.SpawnParticles(waterParticles);
            }
        }

        // Сохранение и загрузка данных

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("digest", DigestTicksLeft);
            tree.SetDouble("plantedDays", PlantedTotalDays); // Сохраняем возраст
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            DigestTicksLeft = tree.GetInt("digest");

            // Если атрибут существует (не старый блок), загружаем его
            if (tree.HasAttribute("plantedDays"))
            {
                PlantedTotalDays = tree.GetDouble("plantedDays");
            }
            else
            {
                // Заглушка для обратной совместимости, если водогортензия была поставлена до этого обновления
                PlantedTotalDays = worldForResolving.Calendar.TotalDays;
            }
        }
    }
}