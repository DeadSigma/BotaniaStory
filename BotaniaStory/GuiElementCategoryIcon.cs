using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace botaniastory
{
    public class GuiElementCategoryIcon : GuiElementControl
    {
        private Action onClick;
        private AssetLocation normalTextureLoc;
        private AssetLocation hoverTextureLoc;

        private LoadedTexture normalTexture;
        private LoadedTexture hoverTexture;

        private float hoverProgress = 0f;
        private float animationSpeed = 5f; // Скорость заливки (5 = 0.2 секунды на полную анимацию)

        private ElementBounds scissorBounds;

        public GuiElementCategoryIcon(ICoreClientAPI capi, ElementBounds bounds, AssetLocation normalTex, AssetLocation hoverTex, Action onClick)
            : base(capi, bounds)
        {
            this.onClick = onClick;
            this.normalTextureLoc = normalTex;
            this.hoverTextureLoc = hoverTex;
            this.normalTexture = new LoadedTexture(capi);
            this.hoverTexture = new LoadedTexture(capi);

            // Создаем границы для "обрезки" (Scissor), привязываем к основным границам
            this.scissorBounds = ElementBounds.Fixed(0, 0, bounds.fixedWidth, bounds.fixedHeight);
            bounds.WithChild(scissorBounds);
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            base.ComposeElements(ctx, surface);
            api.Render.GetOrLoadTexture(normalTextureLoc, ref normalTexture);
            api.Render.GetOrLoadTexture(hoverTextureLoc, ref hoverTexture);
            scissorBounds.CalcWorldBounds();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (!Enabled) return;

            // 1. Проверяем наведение мыши прямо в рендере (самый надежный способ в VS)
            int mouseX = api.Input.MouseX;
            int mouseY = api.Input.MouseY;
            bool isHovered = Bounds.PointInside(mouseX, mouseY);

            // 2. Анимируем прогресс заливки от 0.0 до 1.0
            if (isHovered)
            {
                hoverProgress += deltaTime * animationSpeed;
                if (hoverProgress > 1f) hoverProgress = 1f;
            }
            else
            {
                hoverProgress -= deltaTime * animationSpeed;
                if (hoverProgress < 0f) hoverProgress = 0f;
            }

            // 3. Рисуем обычную (чёрную) иконку всегда
            if (normalTexture.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    normalTexture.TextureId,
                    Bounds.renderX, Bounds.renderY, Bounds.InnerWidth, Bounds.InnerHeight, 50
                );
            }

            // 4. Рисуем цветную (hover) иконку поверх, используя обрезку (Scissor)
            if (hoverProgress > 0f && hoverTexture.TextureId != 0)
            {
                // Динамически меняем высоту обрезки в зависимости от прогресса
                scissorBounds.fixedHeight = Bounds.fixedHeight * hoverProgress;
                scissorBounds.CalcWorldBounds(); // Пересчитываем мировые координаты

                // Включаем маску
                api.Render.PushScissor(scissorBounds);

                // Рисуем ЦЕЛУЮ цветную текстуру (Scissor обрежет всё, что выходит за пределы высоты)
                api.Render.Render2DTexturePremultipliedAlpha(
                    hoverTexture.TextureId,
                    Bounds.renderX, Bounds.renderY, Bounds.InnerWidth, Bounds.InnerHeight, 51 // Z-index выше, чтобы перекрыть черную
                );

                // Выключаем маску
                api.Render.PopScissor();
            }
        }

        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
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