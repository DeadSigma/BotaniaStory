using Cairo;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace botaniastory
{
    public class GuiElementRecipeGrid : GuiElement
    {
        private ItemStack[][] inputStacks;
        private ItemStack[] outputStacks;
        private double slotSize;
        private double padding;
        private double scale;
        private DummySlot renderSlot;

        private int maxVariants = 1;

        public Action<ItemStack> OnSlotClick;

        public GuiElementRecipeGrid(ICoreClientAPI capi, ElementBounds bounds, ItemStack[][] inputs, ItemStack[] outputs, double scale) : base(capi, bounds)
        {
            this.inputStacks = inputs;
            this.outputStacks = outputs;
            this.scale = scale;

            this.slotSize = GuiElement.scaled(48) * scale;
            this.padding = GuiElement.scaled(4) * scale;
            this.renderSlot = new DummySlot(null);

            this.inputStacks = new ItemStack[9][];
            for (int i = 0; i < 9; i++)
            {
                if (inputs[i] != null && inputs[i].Length > 0)
                {
                    // Сортируем по алфавиту названия (Path), чтобы варианты всегда совпадали
                    this.inputStacks[i] = inputs[i].OrderBy(s => s.Collectible.Code.Path).ToArray();
                    if (this.inputStacks[i].Length > maxVariants) maxVariants = this.inputStacks[i].Length;
                }
            }

            if (outputs != null && outputs.Length > 0)
            {
                this.outputStacks = outputs.OrderBy(s => s.Collectible.Code.Path).ToArray();
                if (this.outputStacks.Length > maxVariants) maxVariants = this.outputStacks.Length;
            }
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            Bounds.CalcWorldBounds();

            for (int i = 0; i < 9; i++)
            {
                int col = i % 3;
                int row = i / 3;
                double x = Bounds.drawX + col * (slotSize + padding);
                double y = Bounds.drawY + row * (slotSize + padding);

                ctx.SetSourceRGBA(0, 0, 0, 0.4);
                ctx.Rectangle(x, y, slotSize, slotSize);
                ctx.Fill();
            }

            double eqX = Bounds.drawX + 3.1 * (slotSize + padding);
            double eqY = Bounds.drawY + 1.6 * (slotSize + padding);

            ctx.SetSourceRGBA(1, 1, 1, 1);
            ctx.SelectFontFace("sans-serif", FontSlant.Normal, FontWeight.Bold);
            ctx.SetFontSize(GuiElement.scaled(28) * scale);
            ctx.MoveTo(eqX, eqY);
            ctx.ShowText("=");

            double outX = Bounds.drawX + 3.8 * (slotSize + padding);
            double outY = Bounds.drawY + 1 * (slotSize + padding);
            ctx.SetSourceRGBA(0, 0, 0, 0.4);
            ctx.Rectangle(outX, outY, slotSize, slotSize);
            ctx.Fill();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            int timeIndex = (int)(api.World.ElapsedMilliseconds / 1000);

            int masterIndex = timeIndex % maxVariants; // <--- ЕДИНЫЙ ИНДЕКС

            int mouseX = api.Input.MouseX;
            int mouseY = api.Input.MouseY;

            // Сюда будем сохранять предмет, если мышь над ним
            ItemStack hoveredStack = null;

            for (int i = 0; i < 9; i++)
            {
                if (inputStacks[i] != null && inputStacks[i].Length > 0)
                {
                    int col = i % 3;
                    int row = i / 3;

                    // Координаты для отрисовки
                    double rX = Bounds.renderX + col * (slotSize + padding);
                    double rY = Bounds.renderY + row * (slotSize + padding);

                    // Абсолютные координаты для проверки мыши (как в твоём OnMouseDown)
                    double aX = Bounds.absX + col * (slotSize + padding);
                    double aY = Bounds.absY + row * (slotSize + padding);




                    ItemStack stackToDraw = inputStacks[i][masterIndex % inputStacks[i].Length];
                    renderSlot.Itemstack = stackToDraw;

                    api.Render.RenderItemstackToGui(
                        renderSlot,
                        rX + slotSize / 2,
                        rY + slotSize / 2,
                        100,
                        (float)(slotSize * 0.5f),
                        ColorUtil.WhiteArgb
                    );

                    // Если мышь над предметом сетки — запоминаем его
                    if (mouseX >= aX && mouseX <= aX + slotSize && mouseY >= aY && mouseY <= aY + slotSize)
                    {
                        hoveredStack = stackToDraw;
                    }
                }
            }

            if (outputStacks != null && outputStacks.Length > 0)
            {
                double outRX = Bounds.renderX + 3.8 * (slotSize + padding);
                double outRY = Bounds.renderY + 1 * (slotSize + padding);

                double outAX = Bounds.absX + 3.8 * (slotSize + padding);
                double outAY = Bounds.absY + 1 * (slotSize + padding);

                ItemStack outStack = outputStacks[timeIndex % outputStacks.Length];
                renderSlot.Itemstack = outStack;

                api.Render.RenderItemstackToGui(
                        renderSlot,
                        outRX + slotSize / 2,
                        outRY + slotSize / 2,
                        100,
                        (float)(slotSize * 0.5f),
                        ColorUtil.WhiteArgb
                    );

                // Если мышь над результатом — запоминаем его
                if (mouseX >= outAX && mouseX <= outAX + slotSize && mouseY >= outAY && mouseY <= outAY + slotSize)
                {
                    hoveredStack = outStack;
                }
            }

            // --- МАГИЯ ТУЛТИПА ---
            if (hoveredStack != null)
            {
                // Подсовываем наш предмет в качестве "текущего активного слота"
                renderSlot.Itemstack = hoveredStack;
                api.World.Player.InventoryManager.CurrentHoveredSlot = renderSlot;
            }
            else if (api.World.Player.InventoryManager.CurrentHoveredSlot == renderSlot)
            {
                // Если мышь ушла, очищаем, чтобы тултип не зависал на экране
                api.World.Player.InventoryManager.CurrentHoveredSlot = null;
            }
        }

        // ИСПРАВЛЕНО: Теперь используем OnMouseDownOnElement. 
        // Клик будет работать СТРОГО внутри рецепта!
        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            // Если клик уже перехвачен кем-то другим поверх нас — игнорируем
            if (args.Handled) return;

            int timeIndex = (int)(api.World.ElapsedMilliseconds / 1000);

            int masterIndex = timeIndex % maxVariants; // <--- ЕДИНЫЙ ИНДЕКС

            double baseX = Bounds.absX;
            double baseY = Bounds.absY;

            // Проверка сетки 3x3
            for (int i = 0; i < 9; i++)
            {
                if (inputStacks[i] != null && inputStacks[i].Length > 0)
                {
                    int col = i % 3;
                    int row = i / 3;
                    double slotX = baseX + col * (slotSize + padding);
                    double slotY = baseY + row * (slotSize + padding);

                    if (args.X >= slotX && args.X <= slotX + slotSize && args.Y >= slotY && args.Y <= slotY + slotSize)
                    {
                        ItemStack clickedStack = inputStacks[i][masterIndex % inputStacks[i].Length];
                        OnSlotClick?.Invoke(clickedStack);

                        args.Handled = true; // Говорим игре, что клик успешно поглощён
                        return;
                    }
                }
            }

            // Проверка слота результата
            if (outputStacks != null && outputStacks.Length > 0)
            {
                double outX = baseX + 3.8 * (slotSize + padding);
                double outY = baseY + 1 * (slotSize + padding);

                if (args.X >= outX && args.X <= outX + slotSize && args.Y >= outY && args.Y <= outY + slotSize)
                {
                    ItemStack clickedStack = outputStacks[timeIndex % outputStacks.Length];
                    OnSlotClick?.Invoke(clickedStack);

                    args.Handled = true; // Говорим игре, что клик успешно поглощён
                    return;
                }
            }

            // Если мы кликнули МИМО слотов (например, по фону страницы),
            // отдаём клик обратно игре, чтобы работало перелистывание книги!
            base.OnMouseDown(api, args);



        }
        public override void Dispose()
        {
            // Если при удалении интерфейса наш слот всё ещё висит в игре как "наведенный" — сбрасываем его!
            if (api?.World?.Player?.InventoryManager?.CurrentHoveredSlot == renderSlot)
            {
                api.World.Player.InventoryManager.CurrentHoveredSlot = null;
            }
            base.Dispose();
        }
    }

}