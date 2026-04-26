using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace botaniastory
{
    public class GuiElementAlfheimRecipe : GuiElement
    {
        private ItemStack[] inputStacks;
        private ItemStack[] outputStacks;
        private double slotSize;
        private double scale;
        private DummySlot renderSlot;

        private ElementBounds bgBounds;
        private LoadedTexture bgTexture;

        public Action<ItemStack> OnSlotClick;
        public Action<ItemStack> OpenHandbookFor;

        public GuiElementAlfheimRecipe(ICoreClientAPI capi, ElementBounds bounds, ElementBounds bgBounds, ItemStack[] inputs, ItemStack[] outputs, double scale) : base(capi, bounds)
        {
            this.bgBounds = bgBounds;
            this.inputStacks = inputs;
            this.outputStacks = outputs;
            this.scale = scale;
            this.slotSize = GuiElement.scaled(48) * scale;
            this.renderSlot = new DummySlot(null);

            // Текстура фона с порталом и стрелочками
            this.bgTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory:textures/gui/alfheim_bg.png"), ref bgTexture);
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            Bounds.CalcWorldBounds();
            bgBounds.ParentBounds = Bounds.ParentBounds;
            bgBounds.CalcWorldBounds();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (bgTexture != null && bgTexture.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(bgTexture.TextureId, (float)bgBounds.renderX, (float)bgBounds.renderY, (float)bgBounds.OuterWidth, (float)bgBounds.OuterHeight, 50f);
            }

            int timeIndex = (int)(api.World.ElapsedMilliseconds / 1000);
            int mouseX = api.Input.MouseX;
            int mouseY = api.Input.MouseY;
            ItemStack hoveredStack = null;

            // === 1. ВХОДНОЙ предмет (сверху по центру левой части) ===
            double inX = Bounds.renderX + Bounds.InnerWidth * 0.35;
            double inY = Bounds.renderY + Bounds.InnerHeight * 0.15;

            if (inputStacks != null && inputStacks.Length > 0)
            {
                ItemStack inStack = inputStacks[timeIndex % inputStacks.Length];
                renderSlot.Itemstack = inStack;
                api.Render.RenderItemstackToGui(renderSlot, inX, inY, 100, (float)(slotSize * 0.6f), ColorUtil.WhiteArgb);

                double inAbsX = Bounds.absX + Bounds.InnerWidth * 0.35;
                double inAbsY = Bounds.absY + Bounds.InnerHeight * 0.15;
                if (CheckMouse(mouseX, mouseY, inAbsX, inAbsY, slotSize * 0.6f)) hoveredStack = inStack;
            }

            // === 2.  ВЫХОДНОЙ предмет (справа по центру) ===
            double outX = Bounds.renderX + Bounds.InnerWidth * 1.0;
            double outY = Bounds.renderY + Bounds.InnerHeight * 0.74;

            if (outputStacks != null && outputStacks.Length > 0)
            {
                ItemStack outStack = outputStacks[timeIndex % outputStacks.Length];
                renderSlot.Itemstack = outStack;
                api.Render.RenderItemstackToGui(renderSlot, outX, outY, 100, (float)(slotSize * 0.6f), ColorUtil.WhiteArgb);

                double outAbsX = Bounds.absX + Bounds.InnerWidth * 1.0;
                double outAbsY = Bounds.absY + Bounds.InnerHeight * 0.74;
                if (CheckMouse(mouseX, mouseY, outAbsX, outAbsY, slotSize * 0.6f)) hoveredStack = outStack;
            }

            if (hoveredStack != null)
            {
                renderSlot.Itemstack = hoveredStack;
                api.World.Player.InventoryManager.CurrentHoveredSlot = renderSlot;
            }
            else if (api.World.Player.InventoryManager.CurrentHoveredSlot == renderSlot)
            {
                api.World.Player.InventoryManager.CurrentHoveredSlot = null;
            }
        }

        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            if (args.Handled) return;

            int timeIndex = (int)(api.World.ElapsedMilliseconds / 1000);

            // Проверка клика по ВХОДНОМУ предмету
            double inAbsX = Bounds.absX + Bounds.InnerWidth * 0.35;
            double inAbsY = Bounds.absY + Bounds.InnerHeight * 0.15;
            if (inputStacks != null && inputStacks.Length > 0 && CheckMouse(args.X, args.Y, inAbsX, inAbsY, slotSize * 0.6f))
            {
                ItemStack clickedStack = inputStacks[timeIndex % inputStacks.Length];
                OnSlotClick?.Invoke(clickedStack);
                OpenHandbookFor?.Invoke(clickedStack);
                args.Handled = true; return;
            }

            // Проверка клика по ВЫХОДНОМУ предмету
            double outAbsX = Bounds.absX + Bounds.InnerWidth * 0.85;
            double outAbsY = Bounds.absY + Bounds.InnerHeight * 0.50;
            if (outputStacks != null && outputStacks.Length > 0 && CheckMouse(args.X, args.Y, outAbsX, outAbsY, slotSize * 0.6f))
            {
                ItemStack clickedStack = outputStacks[timeIndex % outputStacks.Length];
                OnSlotClick?.Invoke(clickedStack);
                OpenHandbookFor?.Invoke(clickedStack);
                args.Handled = true; return;
            }

            base.OnMouseDown(api, args);
        }

        private bool CheckMouse(int mX, int mY, double tgtX, double tgtY, double size)
        {
            double distSq = (mX - tgtX) * (mX - tgtX) + (mY - tgtY) * (mY - tgtY);
            double hitRadius = size / 2.0;
            return distSq <= (hitRadius * hitRadius);
        }

        public override void Dispose()
        {
            if (api?.World?.Player?.InventoryManager?.CurrentHoveredSlot == renderSlot)
            {
                api.World.Player.InventoryManager.CurrentHoveredSlot = null;
            }

            base.Dispose();
        }
    }
}