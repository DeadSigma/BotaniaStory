using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace botaniastory
{
    public class GuiElementClickableImage : GuiElementControl
    {
        private Action onClick;
        private AssetLocation textureLocation;
        private LoadedTexture texture;

        // Добавляем свойство, которое будет включать и выключать кнопку


        public GuiElementClickableImage(ICoreClientAPI capi, ElementBounds bounds, AssetLocation textureLocation, Action onClick)
            : base(capi, bounds)
        {
            this.onClick = onClick;
            this.textureLocation = textureLocation;
            this.texture = new LoadedTexture(capi);
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            base.ComposeElements(ctx, surface);
            api.Render.GetOrLoadTexture(textureLocation, ref texture);
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            // Если кнопка отключена, просто не рисуем её
            if (!Enabled) return;

            if (texture.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    texture.TextureId,
                    Bounds.renderX,
                    Bounds.renderY,
                    Bounds.InnerWidth,
                    Bounds.InnerHeight,
                    50
                );
            }
        }

        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            // Если кнопка отключена, игнорируем клики
            if (!Enabled) return;

            base.OnMouseDownOnElement(api, args);
            if (Bounds.PointInside(args.X, args.Y))
            {
                
                onClick?.Invoke();
                args.Handled = true;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}