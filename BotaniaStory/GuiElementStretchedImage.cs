using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace botaniastory
{
    public class GuiElementStretchedImage : GuiElement
    {
        private AssetLocation textureLoc;
        private LoadedTexture tex;

        // НОВЫЙ ПЕРЕКЛЮЧАТЕЛЬ: По умолчанию это обычная картинка
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
            return false; // По-прежнему пропускаем клики сквозь картинки
        }

        // 1. СТАНДАРТНЫЙ МЕТОД: Рисует приветствие и картинки в главах поверх книги
        public override void RenderInteractiveElements(float deltaTime)
        {
            // Рисуем ТОЛЬКО если это НЕ фон
            if (!IsBackground && tex != null && tex.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    tex.TextureId, Bounds.renderX, Bounds.renderY, Bounds.OuterWidth, Bounds.OuterHeight, 50f
                );
            }
        }

        // 2. НАШ СПЕЦИАЛЬНЫЙ МЕТОД: Рисует только саму подложку книги
        public void RenderMyBackground(float deltaTime)
        {
            // Рисуем ТОЛЬКО если мы сказали, что это фон
            if (IsBackground && tex != null && tex.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    tex.TextureId, Bounds.renderX, Bounds.renderY, Bounds.OuterWidth, Bounds.OuterHeight, 10f
                );
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}