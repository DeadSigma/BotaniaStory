using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace BotaniaStory.Blocks
{
    public class BlockEntityEnchantedFarmland : BlockEntityFarmland
    {
        private const int EnchantedFertility = 100;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ForceEnchanted();

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(KeepEnchanted, 3000);
            }
        }

        // Вызывается мотыгой сразу после SetBlock и затирает originalFertility значением из варианта почвы
        public override void OnCreatedFromSoil(Block block, TreeAttribute existingFertilityData = null)
        {
            base.OnCreatedFromSoil(block, existingFertilityData);
            ForceEnchanted();
            MarkDirty(true);
        }

        private void ForceEnchanted()
        {
            for (int i = 0; i < 3; i++)
            {
                originalFertility[i] = EnchantedFertility;
                nutrients[i] = EnchantedFertility;
            }
            moistureLevel = 1f;
        }

        private void KeepEnchanted(float dt)
        {
            bool changed = false;

            for (int i = 0; i < 3; i++)
            {
                if (nutrients[i] < EnchantedFertility)
                {
                    nutrients[i] = EnchantedFertility;
                    changed = true;
                }
            }

            if (moistureLevel < 1f)
            {
                moistureLevel = 1f;
                changed = true;
            }

            if (changed) MarkDirty(true);
        }
    }
}