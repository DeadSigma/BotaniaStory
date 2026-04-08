using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace botaniastory
{
    public class GuiElementManaBar : GuiElement
    {
        private int manaCost;
        private int maxMana;

        private AssetLocation bgTextureLoc;
        private AssetLocation fillTextureLoc;
        private AssetLocation frameTextureLoc;

        private LoadedTexture bgTex;
        private LoadedTexture fillTex;
        private LoadedTexture frameTex;

        // Добавляем специальную границу для "маски" обрезки
        private ElementBounds clipBounds;

        public GuiElementManaBar(ICoreClientAPI capi, ElementBounds bounds, int manaCost, int maxMana) : base(capi, bounds)
        {
            this.manaCost = manaCost;
            this.maxMana = maxMana;

            this.bgTextureLoc = new AssetLocation("botaniastory:textures/gui/manabar_bg.png");
            this.fillTextureLoc = new AssetLocation("botaniastory:textures/gui/manabar_fill.png");
            this.frameTextureLoc = new AssetLocation("botaniastory:textures/gui/manabar_frame.png");

            this.bgTex = new LoadedTexture(capi);
            this.fillTex = new LoadedTexture(capi);
            this.frameTex = new LoadedTexture(capi);
        }

        public override void ComposeElements(Cairo.Context ctx, Cairo.ImageSurface surface)
        {
            // 1. Сначала просчитываем оригинальные границы
            Bounds.CalcWorldBounds();

            api.Render.GetOrLoadTexture(bgTextureLoc, ref bgTex);
            api.Render.GetOrLoadTexture(fillTextureLoc, ref fillTex);
            api.Render.GetOrLoadTexture(frameTextureLoc, ref frameTex);

            // 2. Рассчитываем процент заполнения
            float fillPercentage = (float)manaCost / maxMana;
            if (fillPercentage > 1f) fillPercentage = 1f;

            // ИСПРАВЛЕНО: Создаем новые границы правильно, через фиксированные значения (fixed)
            clipBounds = ElementBounds.Fixed(
                Bounds.fixedX,
                Bounds.fixedY,
                Bounds.fixedWidth * fillPercentage, // Умножаем только ширину
                Bounds.fixedHeight
            );

            // 3. Обязательно передаем родительские границы (книги), чтобы маска была в правильном месте на экране
            clipBounds.ParentBounds = Bounds.ParentBounds;

            // 4. Заставляем движок пересчитать renderX/renderY для нашей маски
            clipBounds.CalcWorldBounds();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            // 1. ЗАДНИЙ ФОН (самый нижний слой, Z: 50)
            if (bgTex != null && bgTex.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    bgTex.TextureId,
                    Bounds.renderX, Bounds.renderY, Bounds.OuterWidth, Bounds.OuterHeight, 50f
                );
            }

            // 2. ПОЛОСКА ЗАЛИВКИ (Средний слой, Z: 51)
            if (fillTex != null && fillTex.TextureId != 0)
            {
                // Включаем маску обрезки! Всё, что за пределами clipBounds, станет невидимым.
                api.Render.PushScissor(clipBounds);

                // ВАЖНО: Рисуем текстуру в ПОЛНУЮ ширину (Bounds.OuterWidth).
                // Благодаря маске текстура не сожмется, а просто обрежется справа.
                api.Render.Render2DTexturePremultipliedAlpha(
                    fillTex.TextureId,
                    Bounds.renderX, Bounds.renderY, Bounds.OuterWidth, Bounds.OuterHeight, 51f
                );

                // Выключаем маску, чтобы она не обрезала остальной интерфейс
                api.Render.PopScissor();
            }

            // 3. ФРЕЙМ / РАМКА (Самый верхний слой, Z: 52)
            if (frameTex != null && frameTex.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    frameTex.TextureId,
                    Bounds.renderX, Bounds.renderY, Bounds.OuterWidth, Bounds.OuterHeight, 52f
                );
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}