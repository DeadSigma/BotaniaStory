using Vintagestory.API.Common;

namespace BotaniaStory
{
    public class ItemWingedTiara : Item
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        // Здесь в будущем можно добавить кастомные текстуры крыльев, 
        // рендер на игроке (через EntityBehavior) или звуки экипировки.
    }
}