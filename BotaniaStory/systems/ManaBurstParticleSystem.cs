using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace BotaniaStory.client.renderers
{
    public class ManaBurstParticleSystem : ModSystem
    {
        public static ManaBurstTrailRenderer Renderer;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Renderer = new ManaBurstTrailRenderer(api);
        }

        public override void Dispose()
        {
            Renderer?.Dispose();
            Renderer = null;
        }
    }
}