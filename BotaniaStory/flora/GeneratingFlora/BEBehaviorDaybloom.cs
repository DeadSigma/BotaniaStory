using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BEBehaviorDaybloom : BEBehaviorGeneratingFlower
    {
        protected override bool IsPassiveFlower => true;

        public static Dictionary<string, int> PlayerBloomsCount = new Dictionary<string, int>();

        public string OwnerUID = null;
        public double PlantedTotalDays = 0;

        private float fractionalMana = 0f;

        public BEBehaviorDaybloom(BlockEntity blockentity) : base(blockentity)
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
                    if (!PlayerBloomsCount.ContainsKey(OwnerUID)) PlayerBloomsCount[OwnerUID] = 0;
                    PlayerBloomsCount[OwnerUID]++;
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

            if (this.Api?.Side == EnumAppSide.Server && OwnerUID != null && PlayerBloomsCount.ContainsKey(OwnerUID))
            {
                PlayerBloomsCount[OwnerUID]--;
                if (PlayerBloomsCount[OwnerUID] <= 0) PlayerBloomsCount.Remove(OwnerUID);
            }
        }

        private void OnServerTick(float dt)
        {
            bool dirty = false;

            if (!IsPassiveDecayPrevented())
            {
                double daysAlive = this.Api.World.Calendar.TotalDays - PlantedTotalDays;
                if (daysAlive >= 3.0)
                {
                    Block currentBlock = this.Api.World.BlockAccessor.GetBlock(this.Blockentity.Pos);
                    Block deadBlock;

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
            }

            RunFlowerWork(ProcessFlowerWork, ref dirty);
            ProcessManaTransfer(ref dirty);

            if (dirty) this.Blockentity.MarkDirty(false);
        }

        private void ProcessFlowerWork(ref bool dirty)
        {
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

            int rainY = this.Api.World.BlockAccessor.GetRainMapHeightAt(this.Blockentity.Pos.X, this.Blockentity.Pos.Z);
            if (this.Blockentity.Pos.Y < rainY) return;

            float rainfall = this.Api.World.BlockAccessor.GetClimateAt(
                this.Blockentity.Pos,
                EnumGetClimateMode.NowValues
            ).Rainfall;

            if (rainfall > 0.05f) return;

            float daylightStrength = this.Api.World.Calendar.GetDayLightStrength(
                this.Blockentity.Pos.X,
                this.Blockentity.Pos.Z
            );

            if (daylightStrength < 0.4f) return;

            int sunLight = this.Api.World.BlockAccessor.GetLightLevel(
                this.Blockentity.Pos,
                EnumLightLevelType.OnlySunLight
            );

            if (sunLight < 15) return;

            float sunlightMult = (sunLight / 22f) * daylightStrength;

            int nearbyFlowers = 0;

            for (int dx = -6; dx <= 6; dx++)
            {
                for (int dy = -6; dy <= 6; dy++)
                {
                    for (int dz = -6; dz <= 6; dz++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;

                        BlockPos checkPos = this.Blockentity.Pos.AddCopy(dx, dy, dz);
                        if (this.Api.World.BlockAccessor.GetBlockEntity(checkPos)?.GetBehavior<BEBehaviorDaybloom>() != null)
                        {
                            nearbyFlowers++;
                        }
                    }
                }
            }

            float efficiency = 1f / (1f + nearbyFlowers * 0.4f);

            float seasonMult = 1f;
            int month = this.Api.World.Calendar.Month;

            if (month == 12 || month == 1 || month == 2) seasonMult = 0.15f;
            else if (month >= 3 && month <= 5) seasonMult = 0.8f;
            else if (month >= 9 && month <= 11) seasonMult = 0.6f;

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

            float baseManaPerSec = 4f;
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
