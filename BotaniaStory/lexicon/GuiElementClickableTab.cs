using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.lexicon
{
    public class GuiElementClickableTab : GuiElementClickableImage
    {
        private DummySlot slot;
        private Action onRightClick;

        // Новые переменные для Дебаггера
        public float IconScale = 1.0f;
        public double IconOffsetX = 0;
        public double IconOffsetY = 0;

        public GuiElementClickableTab(ICoreClientAPI capi, ElementBounds bounds, AssetLocation textureLocation, ItemStack stack, Action onLeftClick, Action onRightClick)
            : base(capi, bounds, textureLocation, onLeftClick)
        {
            this.onRightClick = onRightClick;
            if (stack != null) this.slot = new DummySlot(stack);
        }

        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (!Bounds.PointInside(args.X, args.Y)) return;

            if (args.Button == EnumMouseButton.Right)
            {
                
                onRightClick?.Invoke();
                args.Handled = true;
            }
            else if (args.Button == EnumMouseButton.Left)
            {
                base.OnMouseDownOnElement(api, args);
            }
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            base.RenderInteractiveElements(deltaTime);

            if (slot != null && slot.Itemstack != null)
            {
                double size = Bounds.InnerHeight;
                api.Render.RenderItemstackToGui(
                    slot,
                    Bounds.renderX + (Bounds.InnerWidth / 2.0) + IconOffsetX,
                    Bounds.renderY + (Bounds.InnerHeight / 2.0) + IconOffsetY,
                    100,
                    (float)(Bounds.InnerWidth * 0.40 * IconScale),
                    ColorUtil.WhiteArgb
                );
            }
        }
    }
}