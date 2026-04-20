using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace BotaniaStory
{
    public class BlockEntityPylon : BlockEntity
    {
        private PylonRenderer renderer;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Рендерер запускаем только на стороне клиента (для отрисовки графики)
            if (api is ICoreClientAPI capi)
            {
                renderer = new PylonRenderer(capi, Pos);
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            renderer?.Dispose(); // Очищаем память, если блок сломали
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            renderer?.Dispose(); // Очищаем память, если чанк выгрузился
        }
    }
}