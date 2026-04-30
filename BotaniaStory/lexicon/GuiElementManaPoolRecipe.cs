using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.lexicon
{
    public class GuiElementManaPoolRecipe : GuiElement
    {
        private LoadedTexture bgTex;
        private AssetLocation bgTextureLoc = new AssetLocation("botaniastory:textures/gui/manapool_arrows.png");

        private ItemStack[] inputStacks;
        private ItemStack[] poolStacks;
        private ItemStack[] outputStacks;
        private ItemStack[] catalystStacks;

        private ElementBounds inputBounds;
        private ElementBounds poolBounds;
        private ElementBounds outputBounds;
        private ElementBounds catalystBounds;
        private ElementBounds bgBounds;

        // ДОБАВЛЕНО: Слот-пустышка для отображения тултипов (как в сетке крафта)
        private DummySlot renderSlot;

        public Action<ItemStack> OnSlotClick;

        public GuiElementManaPoolRecipe(ICoreClientAPI capi, ElementBounds bounds, ElementBounds bgBounds, ItemStack[] input, ItemStack[] pool, ItemStack[] output, ItemStack[] catalyst, double scale) : base(capi, bounds)
        {
            this.bgBounds = bgBounds;
            this.inputStacks = input;
            this.poolStacks = pool;
            this.outputStacks = output;
            this.catalystStacks = catalyst;
            this.bgTex = new LoadedTexture(capi);

            this.renderSlot = new DummySlot(null); // Инициализация

            double slotSize = 48 * scale;

            // --- ВХОДНОЙ ПРЕДМЕТ (Слиток слева сверху) ---
            // 20 * scale  -X (Горизонталь). Увеличение числа = сдвиг вправо
            // 30 * scale  -Y (Вертикаль). Увеличение числа = сдвиг вниз
            inputBounds = ElementBounds.Fixed(0 * scale, 55 * scale, slotSize, slotSize).WithParent(bounds);


            // --- КАТАЛИЗАТОР (Перо слева снизу) ---
            // 20 * scale  -> X (Горизонталь). Увеличение числа = сдвиг вправо
            // 120 * scale -> Y (Вертикаль). Увеличение числа = сдвиг вниз
            catalystBounds = ElementBounds.Fixed(0 * scale, 120 * scale, slotSize, slotSize).WithParent(bounds);


            // --- БАССЕЙН МАНЫ (Центр) ---
            // 100 * scale -> X (Горизонталь). Увеличение числа = сдвиг вправо
            // 75 * scale  -> Y (Вертикаль). Увеличение числа = сдвиг вниз
            poolBounds = ElementBounds.Fixed(100 * scale, 75 * scale, slotSize, slotSize).WithParent(bounds);


            // --- РЕЗУЛЬТАТ КРАФТА (Розовый цветок справа) ---
            // 180 * scale -> X (Горизонталь). Увеличение числа = сдвиг вправо
            // 75 * scale  -> Y (Вертикаль). Увеличение числа = сдвиг вниз
            outputBounds = ElementBounds.Fixed(205 * scale, 80 * scale, slotSize, slotSize).WithParent(bounds);
        }

        public override void ComposeElements(Cairo.Context ctx, Cairo.ImageSurface surface)
        {
            bgBounds.ParentBounds = Bounds.ParentBounds;
            Bounds.CalcWorldBounds();
            bgBounds.CalcWorldBounds();

            inputBounds.CalcWorldBounds();
            catalystBounds.CalcWorldBounds();
            poolBounds.CalcWorldBounds();
            outputBounds.CalcWorldBounds();

            api.Render.GetOrLoadTexture(bgTextureLoc, ref bgTex);
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (bgTex != null && bgTex.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(bgTex.TextureId, bgBounds.renderX, bgBounds.renderY, bgBounds.OuterWidth, bgBounds.OuterHeight, 50f);
            }

            ItemStack hoveredStack = null;
            int mouseX = api.Input.MouseX;
            int mouseY = api.Input.MouseY;

            // Рисуем предметы и сразу проверяем наведение
            hoveredStack = RenderStack(inputStacks, inputBounds, mouseX, mouseY) ?? hoveredStack;
            if (catalystStacks != null) hoveredStack = RenderStack(catalystStacks, catalystBounds, mouseX, mouseY) ?? hoveredStack;
            hoveredStack = RenderStack(poolStacks, poolBounds, mouseX, mouseY) ?? hoveredStack;
            hoveredStack = RenderStack(outputStacks, outputBounds, mouseX, mouseY) ?? hoveredStack;

            // --- МАГИЯ ТУЛТИПА ---
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

        // Обновленный метод отрисовки, который теперь возвращает предмет, если на него навели мышь
        private ItemStack RenderStack(ItemStack[] stacks, ElementBounds slotBounds, int mouseX, int mouseY)
        {
            if (stacks == null || stacks.Length == 0) return null;
            int index = (int)((api.World.ElapsedMilliseconds / 1000) % stacks.Length);
            ItemStack stack = stacks[index];

            if (stack == null) return null;

            renderSlot.Itemstack = stack;
            api.Render.RenderItemstackToGui(
                renderSlot,
                slotBounds.renderX + slotBounds.OuterWidth / 2,
                slotBounds.renderY + slotBounds.OuterHeight / 2,
                100, (float)(slotBounds.OuterWidth / 2), ColorUtil.WhiteArgb
            );

            // Ручная проверка абсолютных координат (как в тултипах сетки крафта)
            if (mouseX >= slotBounds.absX && mouseX <= slotBounds.absX + slotBounds.OuterWidth &&
                mouseY >= slotBounds.absY && mouseY <= slotBounds.absY + slotBounds.OuterHeight)
            {
                return stack;
            }
            return null;
        }

        // ИСПОЛЬЗУЕМ СТАНДАРТНЫЙ OnMouseDown (как в Аптекаре)
        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            if (args.Handled) return;

            if (CheckClick(inputStacks, inputBounds, args)) return;
            if (catalystStacks != null && CheckClick(catalystStacks, catalystBounds, args)) return;
            if (CheckClick(poolStacks, poolBounds, args)) return;
            if (CheckClick(outputStacks, outputBounds, args)) return;

            base.OnMouseDown(api, args);
        }

        private bool CheckClick(ItemStack[] stacks, ElementBounds slotBounds, MouseEvent args)
        {
            if (stacks == null || stacks.Length == 0) return false;

            // Ручная проверка попадания мыши в рамки
            if (args.X >= slotBounds.absX && args.X <= slotBounds.absX + slotBounds.OuterWidth &&
                args.Y >= slotBounds.absY && args.Y <= slotBounds.absY + slotBounds.OuterHeight)
            {
                int index = (int)((api.World.ElapsedMilliseconds / 1000) % stacks.Length);
                api.Gui.PlaySound("tick"); // Добавили звук клика!
                OnSlotClick?.Invoke(stacks[index]);
                args.Handled = true;
                return true;
            }
            return false;
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