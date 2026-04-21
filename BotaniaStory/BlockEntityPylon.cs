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

            if (api is ICoreClientAPI capi)
            {
                EnumPylonType currentType = EnumPylonType.Mana;

                if (this.Block.Code.Path.Contains("natura"))
                {
                    currentType = EnumPylonType.Natura;
                }
                else if (this.Block.Code.Path.Contains("gaia"))
                {
                    currentType = EnumPylonType.Gaia;
                }

                renderer = new PylonRenderer(capi, Pos, currentType);
            }
        }

        // === ТОТ САМЫЙ МЕТОД ===
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessellator)
        {
            // Возвращаем true, чтобы движок НЕ генерировал стандартную JSON-модель в самом мире.
            // При этом в инвентаре и в руках JSON-модель будет отображаться корректно!
            return true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            renderer?.Dispose();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            renderer?.Dispose();
        }
    }
}