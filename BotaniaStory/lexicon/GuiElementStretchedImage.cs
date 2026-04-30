using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.lexicon
{
    public class GuiElementStretchedImage : GuiElement
    {
        private AssetLocation textureLoc;
        private LoadedTexture tex;

        public bool IsBackground = false;

        public GuiElementStretchedImage(ICoreClientAPI capi, ElementBounds bounds, AssetLocation textureLoc) : base(capi, bounds)
        {
            this.textureLoc = textureLoc;
            this.tex = new LoadedTexture(capi);
        }

        public override void ComposeElements(Cairo.Context ctx, Cairo.ImageSurface surface)
        {
            Bounds.CalcWorldBounds();
            api.Render.GetOrLoadTexture(textureLoc, ref tex);
        }

        public override bool IsPositionInside(int mouseX, int mouseY)
        {
            return false;
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (!IsBackground && tex != null && tex.TextureId != 0)
            {
                // Включаем стандартное смешивание
                api.Render.GlToggleBlend(true, EnumBlendMode.Standard);

                // Добавили (float) перед каждым параметром из Bounds
                api.Render.Render2DTexture(
                    tex.TextureId,
                    (float)Bounds.renderX,
                    (float)Bounds.renderY,
                    (float)Bounds.OuterWidth,
                    (float)Bounds.OuterHeight,
                    50f
                );
            }
        }

        public void RenderMyBackground(float deltaTime)
        {
            if (IsBackground && tex != null && tex.TextureId != 0)
            {
                // Включаем стандартное смешивание
                api.Render.GlToggleBlend(true, EnumBlendMode.Standard);

                // Добавили (float) перед каждым параметром из Bounds
                api.Render.Render2DTexture(
                    tex.TextureId,
                    (float)Bounds.renderX,
                    (float)Bounds.renderY,
                    (float)Bounds.OuterWidth,
                    (float)Bounds.OuterHeight,
                    10f
                );
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}