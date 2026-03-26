using BotaniaStory.Flora.GeneratingFlora;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent; // Добавили доступ к ванильным растениям

namespace BotaniaStory
{
    public class BotaniaStoryModSystem : ModSystem
    {

        private BotaniaWandHud wandHud;

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            wandHud = new BotaniaWandHud(api);
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("BlockMysticalFlower", typeof(BlockMysticalFlower));
            api.RegisterBlockClass("BlockApothecary", typeof(BlockApothecary));
            api.RegisterBlockEntityClass("ApothecaryEntity", typeof(BlockEntityApothecary)); // ДОБАВИТЬ ЭТУ СТРОКУ
            api.Logger.Notification("Мод Botania Story успешно загружен! Магия начинается...");
            api.RegisterBlockClass("BlockPureDaisy", typeof(BlockPureDaisy));
            api.RegisterBlockEntityClass("PureDaisyEntity", typeof(BlockEntityPureDaisy));
            api.RegisterBlockClass("BlockManaPool", typeof(BlockManaPool));
            api.RegisterBlockEntityClass("ManaPoolEntity", typeof(BlockEntityManaPool));
            api.RegisterBlockClass("ManaSpreader", typeof(ManaSpreader));
            api.RegisterBlockEntityClass("ManaSpreaderEntity", typeof(BlockEntityManaSpreader));
            api.RegisterItemClass("ItemWandOfTheForest", typeof(ItemWandOfTheForest));
            api.RegisterBlockClass("BlockDaybloom", typeof(BlockDaybloom));
            api.RegisterBlockEntityClass("DaybloomEntity", typeof(BlockEntityDaybloom));
            api.RegisterEntity("EntityManaBurst", typeof(EntityManaBurst));
            api.RegisterBlockClass("BlockEndoflame", typeof(BlockEndoflame));
            api.RegisterBlockEntityClass("EndoflameEntity", typeof(BlockEntityEndoflame));
            api.RegisterItemClass("ItemWandOfBinding", typeof(ItemWandOfBinding));
        }
    }

    // Теперь мы наследуемся от BlockPlant, а не от Block!
    public class BlockMysticalFlower : BlockPlant
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }
    }
}