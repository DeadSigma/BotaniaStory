using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace BotaniaStory.lexicon
{
    public class GuiElementAnvilRecipe : GuiElement
    {
        private ICoreClientAPI capi;
        private ItemStack[] inputs;
        private ItemStack[] outputs;
        private ItemStack[] anvil;
        private double scale;
        
        public Action<ItemStack> OnSlotClick;
        private DummySlot renderSlot; // Фейковый слот для тултипов

        private ElementBounds inputBounds;
        private ElementBounds anvilBounds;
        private ElementBounds outputBounds;

        public GuiElementAnvilRecipe(ICoreClientAPI capi, ElementBounds bounds, ItemStack[] inputs, ItemStack[] outputs, ItemStack[] anvil, double scale) : base(capi, bounds)
        {
            this.capi = capi;
            this.inputs = inputs;
            this.outputs = outputs;
            this.anvil = anvil;
            this.scale = scale;
            this.renderSlot = new DummySlot(null); // Инициализируем слот

            double s = scale;
            double iconSize = 25 * s;
            double padding = 24 * s;

            double outputShift = 10 * s;
            double inputShift = 5 * s;

            // В WithFixedOffset для inputBounds мы теперь вычитаем сдвиг:
            inputBounds = ElementBounds.FixedSize(iconSize, iconSize).WithFixedOffset(-inputShift, 0).WithParent(bounds);
            anvilBounds = ElementBounds.FixedSize(iconSize, iconSize).WithFixedOffset(iconSize + padding, 0).WithParent(bounds);
            outputBounds = ElementBounds.FixedSize(iconSize, iconSize).WithFixedOffset((iconSize * 2) + (padding * 2) + outputShift, 0).WithParent(bounds);

            inputBounds.CalcWorldBounds();
            anvilBounds.CalcWorldBounds();
            outputBounds.CalcWorldBounds();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            base.RenderInteractiveElements(deltaTime);

            int mouseX = capi.Input.MouseX;
            int mouseY = capi.Input.MouseY;
            ItemStack hoveredStack = null;

            double size = inputBounds.InnerWidth;

            // Рендер и проверка наведения для ВХОДНОГО материала
            if (inputs != null && inputs.Length > 0 && inputs[0] != null)
            {
                renderSlot.Itemstack = inputs[0];
                capi.Render.RenderItemstackToGui(renderSlot, 
                    inputBounds.renderX + (size / 2.0), 
                    inputBounds.renderY + (size / 2.0), 
                    100, (float)size, -1);

                double absX = inputBounds.absX + (size / 2.0);
                double absY = inputBounds.absY + (size / 2.0);
                if (CheckMouse(mouseX, mouseY, absX, absY, size)) hoveredStack = inputs[0];
            }

            // Рендер и проверка наведения для НАКОВАЛЬНИ
            if (anvil != null && anvil.Length > 0 && anvil[0] != null)
            {
                renderSlot.Itemstack = anvil[0];
                capi.Render.RenderItemstackToGui(renderSlot, 
                    anvilBounds.renderX + (anvilBounds.InnerWidth / 2.0), 
                    anvilBounds.renderY + (anvilBounds.InnerHeight / 2.0), 
                    100, (float)(anvilBounds.InnerWidth), -1);

                double absX = anvilBounds.absX + (anvilBounds.InnerWidth / 2.0);
                double absY = anvilBounds.absY + (anvilBounds.InnerHeight / 2.0);
                if (CheckMouse(mouseX, mouseY, absX, absY, anvilBounds.InnerWidth)) hoveredStack = anvil[0];
            }

            // Рендер и проверка наведения для РЕЗУЛЬТАТА
            if (outputs != null && outputs.Length > 0 && outputs[0] != null)
            {
                renderSlot.Itemstack = outputs[0];
                capi.Render.RenderItemstackToGui(renderSlot, 
                    outputBounds.renderX + (outputBounds.InnerWidth / 2.0), 
                    outputBounds.renderY + (outputBounds.InnerHeight / 2.0), 
                    100, (float)(outputBounds.InnerWidth), -1);

                double absX = outputBounds.absX + (outputBounds.InnerWidth / 2.0);
                double absY = outputBounds.absY + (outputBounds.InnerHeight / 2.0);
                if (CheckMouse(mouseX, mouseY, absX, absY, outputBounds.InnerWidth)) hoveredStack = outputs[0];
            }

            // === МАГИЯ ТУЛТИПОВ ===
            if (hoveredStack != null)
            {
                renderSlot.Itemstack = hoveredStack;
                capi.World.Player.InventoryManager.CurrentHoveredSlot = renderSlot;
            }
            else if (capi.World.Player.InventoryManager.CurrentHoveredSlot == renderSlot)
            {
                capi.World.Player.InventoryManager.CurrentHoveredSlot = null;
            }
        }

        // === ТОЧНЫЕ КЛИКИ ИЗ ALFHEIM ===
        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            if (args.Handled) return;

            int mX = args.X;
            int mY = args.Y;
            double size = inputBounds.InnerWidth;

            // Клик по материалу
            if (inputs != null && inputs.Length > 0 && CheckMouse(mX, mY, inputBounds.absX + (size / 2.0), inputBounds.absY + (size / 2.0), size))
            {
                OnSlotClick?.Invoke(inputs[0]);
                args.Handled = true; return;
            }
            // Клик по наковальне
            if (anvil != null && anvil.Length > 0 && CheckMouse(mX, mY, anvilBounds.absX + (anvilBounds.InnerWidth / 2.0), anvilBounds.absY + (anvilBounds.InnerHeight / 2.0), anvilBounds.InnerWidth))
            {
                OnSlotClick?.Invoke(anvil[0]);
                args.Handled = true; return;
            }
            // Клик по результату
            if (outputs != null && outputs.Length > 0 && CheckMouse(mX, mY, outputBounds.absX + (outputBounds.InnerWidth / 2.0), outputBounds.absY + (outputBounds.InnerHeight / 2.0), outputBounds.InnerWidth))
            {
                OnSlotClick?.Invoke(outputs[0]);
                args.Handled = true; return;
            }

            base.OnMouseDown(api, args);
        }

        // Метод проверки радиуса из Alfheim
        private bool CheckMouse(int mX, int mY, double tgtX, double tgtY, double size)
        {
            double distSq = (mX - tgtX) * (mX - tgtX) + (mY - tgtY) * (mY - tgtY);
            double hitRadius = size / 2.0;
            return distSq <= (hitRadius * hitRadius);
        }

        // Обязательно очищаем тултип при закрытии/перелистывании страницы
        public override void Dispose()
        {
            if (capi?.World?.Player?.InventoryManager?.CurrentHoveredSlot == renderSlot)
            {
                capi.World.Player.InventoryManager.CurrentHoveredSlot = null;
            }
            base.Dispose();
        }
    }
}