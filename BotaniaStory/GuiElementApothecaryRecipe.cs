using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace botaniastory
{
    public class GuiElementApothecaryRecipe : GuiElement
    {
        private ItemStack[][] ingredients;
        private ItemStack[] outputStacks;
        private ItemStack[] centralStacks;
        private double slotSize;
        private double scale;
        private DummySlot renderSlot;

        // --- НОВЫЕ ПЕРЕМЕННЫЕ ДЛЯ ФОНА ---
        private ElementBounds bgBounds;
        private LoadedTexture bgTexture;

        public Action<ItemStack> OnSlotClick;
        public Action<ItemStack> OpenHandbookFor;

        // В конструктор добавлен bgBounds
        public GuiElementApothecaryRecipe(ICoreClientAPI capi, ElementBounds bounds, ElementBounds bgBounds, ItemStack[][] ingredients, ItemStack[] outputs, ItemStack[] central, double scale) : base(capi, bounds)
        {
            this.bgBounds = bgBounds;
            this.ingredients = ingredients;
            this.outputStacks = outputs;
            this.centralStacks = central;
            this.scale = scale;
            this.slotSize = GuiElement.scaled(48) * scale;
            this.renderSlot = new DummySlot(null);

            // Загружаем картинку в память для ручной отрисовки
            this.bgTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory:textures/gui/apothecary_bg.png"), ref bgTexture);
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            Bounds.CalcWorldBounds();

            // Назначаем фону того же родителя (окно книги), что и у основного элемента
            bgBounds.ParentBounds = Bounds.ParentBounds;

            bgBounds.CalcWorldBounds();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            // --- МАГИЯ МАСШТАБИРОВАНИЯ ФОНА ---
            // Рисуем текстуру вручную, заставляя её растягиваться до размеров bgBounds
            if (bgTexture != null && bgTexture.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(bgTexture.TextureId, (float)bgBounds.renderX, (float)bgBounds.renderY, (float)bgBounds.OuterWidth, (float)bgBounds.OuterHeight, 50f);
            }

            int timeIndex = (int)(api.World.ElapsedMilliseconds / 1000);
            double continuousTime = api.World.ElapsedMilliseconds / 1000.0;

            int mouseX = api.Input.MouseX;
            int mouseY = api.Input.MouseY;
            ItemStack hoveredStack = null;

            double centerX = Bounds.renderX + Bounds.InnerWidth / 2;
            double centerY = Bounds.renderY + Bounds.InnerHeight / 2;
            double radius = Bounds.InnerWidth * 0.35;

            // 1. Рисуем лепестки
            if (ingredients != null)
            {
                double speed = 0.5;
                for (int i = 0; i < ingredients.Length; i++)
                {
                    if (ingredients[i] == null || ingredients[i].Length == 0) continue;

                    double angle = (continuousTime * speed) + (i * (Math.PI * 2) / ingredients.Length);
                    double rX = centerX + Math.Cos(angle) * radius;
                    double rY = centerY + Math.Sin(angle) * radius;

                    ItemStack stackToDraw = ingredients[i][timeIndex % ingredients[i].Length];
                    renderSlot.Itemstack = stackToDraw;

                    api.Render.RenderItemstackToGui(renderSlot, rX, rY, 100, (float)(slotSize * 0.4f), ColorUtil.WhiteArgb);

                    double aX = Bounds.absX + (rX - Bounds.renderX);
                    double aY = Bounds.absY + (rY - Bounds.renderY);
                    if (CheckMouse(mouseX, mouseY, aX, aY, slotSize * 0.5f)) hoveredStack = stackToDraw;
                }
            }

            // 2. Рисуем центральный блок
            if (centralStacks != null && centralStacks.Length > 0)
            {
                ItemStack cStack = centralStacks[timeIndex % centralStacks.Length];
                renderSlot.Itemstack = cStack;
                api.Render.RenderItemstackToGui(renderSlot, centerX, centerY, 100, (float)(slotSize * 0.6f), ColorUtil.WhiteArgb);

                double cAbsX = Bounds.absX + Bounds.InnerWidth / 2;
                double cAbsY = Bounds.absY + Bounds.InnerHeight / 2;
                if (CheckMouse(mouseX, mouseY, cAbsX, cAbsY, slotSize * 0.5f)) hoveredStack = cStack;
            }

            // 3. Рисуем результат
            if (outputStacks != null && outputStacks.Length > 0)
            {
                double outX = Bounds.renderX + Bounds.InnerWidth * 0.94;
                double outY = Bounds.renderY + Bounds.InnerHeight * 0.09;

                ItemStack outStack = outputStacks[timeIndex % outputStacks.Length];
                renderSlot.Itemstack = outStack;
                api.Render.RenderItemstackToGui(renderSlot, outX, outY, 100, (float)(slotSize * 0.6f), ColorUtil.WhiteArgb);

                double outAbsX = Bounds.absX + Bounds.InnerWidth * 0.95;
                double outAbsY = Bounds.absY + Bounds.InnerHeight * 0.05;
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
            double continuousTime = api.World.ElapsedMilliseconds / 1000.0;

            double centerX = Bounds.absX + Bounds.InnerWidth / 2;
            double centerY = Bounds.absY + Bounds.InnerHeight / 2;
            double radius = Bounds.InnerWidth * 0.35;

            if (ingredients != null)
            {
                for (int i = 0; i < ingredients.Length; i++)
                {
                    if (ingredients[i] == null || ingredients[i].Length == 0) continue;
                    double angle = (continuousTime * 0.5) + (i * (Math.PI * 2) / ingredients.Length);
                    double x = centerX + Math.Cos(angle) * radius;
                    double y = centerY + Math.Sin(angle) * radius;

                    if (CheckMouse(args.X, args.Y, x, y, slotSize * 0.4f))
                    {
                        ItemStack clickedStack = ingredients[i][timeIndex % ingredients[i].Length];
                        OnSlotClick?.Invoke(clickedStack);
                        OpenHandbookFor?.Invoke(clickedStack);
                        args.Handled = true; return;
                    }
                }
            }

            if (centralStacks != null && centralStacks.Length > 0 && CheckMouse(args.X, args.Y, centerX, centerY, slotSize * 0.6f))
            {
                ItemStack clickedStack = centralStacks[timeIndex % centralStacks.Length];
                OnSlotClick?.Invoke(clickedStack);
                OpenHandbookFor?.Invoke(clickedStack);
                args.Handled = true; return;
            }

            if (outputStacks != null && outputStacks.Length > 0)
            {
                double outX = Bounds.absX + Bounds.InnerWidth * 0.95;
                double outY = Bounds.absY + Bounds.InnerHeight * 0.05;
                if (CheckMouse(args.X, args.Y, outX, outY, slotSize * 0.6f))
                {
                    ItemStack clickedStack = outputStacks[timeIndex % outputStacks.Length];
                    OnSlotClick?.Invoke(clickedStack);
                    OpenHandbookFor?.Invoke(clickedStack);
                    args.Handled = true; return;
                }
            }

            base.OnMouseDown(api, args);
        }

        private bool CheckMouse(int mX, int mY, double tgtX, double tgtY, double size)
        {
            double distSq = (mX - tgtX) * (mX - tgtX) + (mY - tgtY) * (mY - tgtY);
            double hitRadius = size / 2.0;
            return distSq <= (hitRadius * hitRadius);
        }

        // ОЧИЩАЕМ ПАМЯТЬ ОТ ТЕКСТУРЫ
        public override void Dispose()
        {
            base.Dispose();
        }
    }
}