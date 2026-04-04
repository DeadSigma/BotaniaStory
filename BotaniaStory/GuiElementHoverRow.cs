using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace botaniastory
{
    public class GuiElementHoverRow : GuiElementControl
    {
        private Action onClick;
        private DummySlot slot;
        private ItemStack[] iconStacks; // Теперь храним целый массив предметов!
        private LoadedTexture starTexture;

        public float IconScale = 1.0f;
        public double IconOffsetX = 0;
        public double IconOffsetY = 0;

        // Переменные для кастомной звездочки избранного
        public bool IsBookmarked = false;
        public double StarOffsetX = 20;
        public double StarOffsetY = 8;
        public double StarSize = 12;

        // ИСПРАВЛЕНО: Теперь принимаем ItemStack[]
        public GuiElementHoverRow(ICoreClientAPI capi, ElementBounds bounds, ItemStack[] stacks, Action onClick) : base(capi, bounds)
        {
            starTexture = new LoadedTexture(capi);
            // Создаем пустой слот один раз, его содержимое будем менять при рендере
            slot = new DummySlot(null);
            UpdateData(stacks, onClick, false);
        }

        // ИСПРАВЛЕНО: Теперь принимаем ItemStack[]
        public void UpdateData(ItemStack[] stacks, Action onClick, bool isBookmarked)
        {
            this.onClick = onClick;
            this.IsBookmarked = isBookmarked;
            this.iconStacks = stacks;
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            base.ComposeElements(ctx, surface);
            // Загружаем текстуру звездочки
            api.Render.GetOrLoadTexture(new AssetLocation("botaniastory:gui/star.png"), ref starTexture);
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (Bounds.PointInside(api.Input.MouseX, api.Input.MouseY))
            {
                api.Render.RenderRectangle(
                    (float)Bounds.renderX, (float)Bounds.renderY, 50,
                    (float)Bounds.InnerWidth, (float)Bounds.InnerHeight,
                    ColorUtil.ColorFromRgba(150, 150, 150, 60)
                );
            }

            // === МАГИЯ СЛАЙД-ШОУ ===
            if (iconStacks != null && iconStacks.Length > 0)
            {
                // api.World.ElapsedMilliseconds - время игры в миллисекундах. 
                // Делим на 1000 = меняем картинку ровно каждую 1 секунду.
                int index = (int)(api.World.ElapsedMilliseconds / 1000) % iconStacks.Length;
                slot.Itemstack = iconStacks[index];

                double size = Bounds.InnerHeight;
                api.Render.RenderItemstackToGui(
                    slot,
                    Bounds.renderX + (size / 2.0) + IconOffsetX,
                    Bounds.renderY + (size / 2.0) + IconOffsetY,
                    100,
                    (float)(size * 0.45f * IconScale),
                    ColorUtil.WhiteArgb
                );
            }
            // =========================

            // Рисуем кастомную звездочку, если глава в закладках
            if (IsBookmarked && starTexture.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    starTexture.TextureId,
                    Bounds.renderX + StarOffsetX,
                    Bounds.renderY + StarOffsetY,
                    (float)StarSize,
                    (float)StarSize,
                    100
                );
            }
        }

        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
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