using Vintagestory.API.Common;

namespace botaniastory
{
    public class lexiconModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            // Регистрируем классы для JSON
            api.RegisterItemClass("ItemLexicon", typeof(ItemLexicon));
        }
    }
}