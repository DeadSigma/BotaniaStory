using Vintagestory.API.Common;

namespace BotaniaStory.Flora.GeneratingFlora
{
    // НАСЛЕДУЕМСЯ от BlockEntityGeneratingFlower
    public class BlockEntityDaybloom : BlockEntityGeneratingFlower
    {
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            MaxMana = 10000; // Настраиваем максимум для Дневноцвета

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 1000); 
            }
        }

        private void OnServerTick(float dt)
        {
            bool dirty = false;

            // 1. ВЫРАБОТКА МАНЫ (Только от солнца)
            int sunLight = Api.World.BlockAccessor.GetLightLevel(Pos, EnumLightLevelType.OnlySunLight);
            if (sunLight > 10)
            {
                if (CurrentMana < MaxMana)
                {
                    CurrentMana += 500;
                    if (CurrentMana > MaxMana) CurrentMana = MaxMana;
                    dirty = true; 
                }
            }

            // 2. ПРОВЕРКА И ПЕРЕДАЧА МАНЫ (Магия родительского класса!)
            ProcessManaTransfer(ref dirty);

            if (dirty)
            {
                this.MarkDirty(true);
            }
        }
    }
}