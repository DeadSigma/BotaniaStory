using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace BotaniaStory.client.renderers
{
    public class PylonParticleSystem : ModSystem
    {
        public static PylonParticleRenderer Renderer;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Renderer = new PylonParticleRenderer(api);
        }

        public override void Dispose()
        {
            Renderer?.Dispose();
            Renderer = null;
        }
    }
}