using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.systems
{
    /// <summary>
    /// Ванильные IngotMoldRenderer и ForgeContentsRenderer выставляют на общем StandardShader
    /// TempGlowMode = 1 и RgbaGlowIn (чёрный у остывшего слитка/незажжённого горна) и не сбрасывают.
    /// PreparedStandardShader() эти два юниформа тоже не трогает.
    /// Всё, что после них рисуется этим шейдером со свечением, уходит в чёрное.
    /// Вызывать сразу после PreparedStandardShader() в каждом своём рендерере.
    /// </summary>
    public static class ShaderSanitizer
    {
        private static readonly Vec4f NoGlow = new Vec4f(0f, 0f, 0f, 0f);

        public static void Sanitize(IStandardShaderProgram prog)
        {
            if (prog == null) return;

            prog.TempGlowMode = 0;
            prog.RgbaGlowIn = NoGlow;
            prog.NormalShaded = 1;
        }
    }

    /// <summary>
    /// Глобальный сброс после всех ванильных рендереров с RenderOrder 0.5 (формы, горны).
    /// Защищает рендереры других модов и всё, что идёт позже в кадре.
    /// Свои рендереры это НЕ спасает - при равном RenderOrder порядок регистрации
    /// не контролируется, поэтому у себя вызывай ShaderSanitizer.Sanitize() локально.
    /// </summary>
    public class StandardShaderSanitizerSystem : ModSystem, IRenderer
    {
        private ICoreClientAPI capi;

        public double RenderOrder => 0.55;
        public int RenderRange => 999;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "botaniastory-shadersanitizer");
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Opaque) return;

            IStandardShaderProgram prog = capi.Render.StandardShader;
            if (prog == null) return;

            prog.Use();
            ShaderSanitizer.Sanitize(prog);
            prog.Stop();
        }

        public override void Dispose()
        {
            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            base.Dispose();
        }
    }
}
