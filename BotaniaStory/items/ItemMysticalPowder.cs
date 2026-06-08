using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config; // Обязательно для работы Lang.Get

namespace BotaniaStory
{
    public class ItemMysticalPowder : Item
    {
        private bool isAlchemyLoaded;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            isAlchemyLoaded = api.ModLoader.IsModEnabled("alchemy");
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            // Базовое описание из itemdesc-mysticalpowder-*
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (!isAlchemyLoaded)
            {
                dsc.AppendLine();

                // Берем текст из lang-файла с указанием домена твоего мода
                dsc.AppendLine(Lang.Get("botaniastory:alchemy-missing-warning"));
            }
        }
    }
}