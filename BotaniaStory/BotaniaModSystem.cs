using Vintagestory.API.Common;
using Vintagestory.GameContent; // Добавили доступ к ванильным растениям

namespace BotaniaStory
{
    public class BotaniaStoryModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("BlockMysticalFlower", typeof(BlockMysticalFlower));
            api.RegisterBlockClass("BlockApothecary", typeof(BlockApothecary));
            api.Logger.Notification("Мод Botania Story успешно загружен! Магия начинается...");
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