using Cairo;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config; // Обязательно для Lang.Get()
using Vintagestory.API.MathTools;

namespace botaniastory
{
    // --- ДАННЫЕ СТРОКИ ---
    public class StructureTrackerItemData
    {
        public Block Block;
        public int RequiredCount;
        public int PlacedCount;
    }

    // --- КАСТОМНЫЙ ЭЛЕМЕНТ: ИКОНКА БЕЗ РАМКИ ---
    public class GuiElementItemIcon : GuiElement
    {
        private ItemStack stack;
        private DummySlot dummySlot;

        public GuiElementItemIcon(ICoreClientAPI capi, ElementBounds bounds, ItemStack stack) : base(capi, bounds)
        {
            this.stack = stack;
            this.dummySlot = new DummySlot(stack);
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            // Рассчитываем координаты, но фон не рисуем!
            Bounds.CalcWorldBounds();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (stack == null) return;

            // Отрисовываем сам предмет (без подложки инвентаря)
            api.Render.RenderItemstackToGui(
                dummySlot,
                Bounds.renderX + Bounds.InnerWidth / 2,
                Bounds.renderY + Bounds.InnerHeight / 2,
                50, // z-index
                (float)Bounds.InnerWidth * 0.8f,
                ColorUtil.WhiteArgb
            );

        }
    }

    // --- РАСШИРЕНИЕ ДЛЯ GUI COMPOSER ---
    public static class GuiComposerItemIconExtension
    {
        // Добавляем удобный метод, чтобы вызывать его как compo.AddItemIcon()
        public static GuiComposer AddItemIcon(this GuiComposer compo, ItemStack stack, ElementBounds bounds, string key = null)
        {
            if (!compo.Composed)
            {
                compo.AddInteractiveElement(new GuiElementItemIcon(compo.Api, bounds, stack), key);
            }
            return compo;
        }
    }

    // --- ОСНОВНОЙ ХУД ---
    public class GuiDialogStructureTracker : HudElement
    {
        public override double DrawOrder => 0.2;

        public GuiDialogStructureTracker(ICoreClientAPI capi) : base(capi) { }

        public void Rebuild(List<StructureTrackerItemData> items, string structureName)
        {
            SingleComposer?.Dispose();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.RightBottom)
                .WithFixedAlignmentOffset(-20, -50);

            GuiComposer compo = capi.Gui.CreateCompo("structuretracker", dialogBounds);

            // Изменили EnumTextOrientation.Right на Center для центрирования
            CairoFont headerFont = CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center);
            CairoFont normalFont = CairoFont.WhiteSmallText();
            CairoFont completeFont = CairoFont.WhiteSmallText().WithColor(new double[] { 0.2, 1.0, 0.2, 1.0 });

            ElementBounds headerBounds = ElementBounds.Fixed(0, 0, 300, 30);

            // --- ЛОКАЛИЗАЦИЯ ЗАГОЛОВКА ---
            // Пытаемся перевести и само название структуры, если оно есть в lang файле
            string translatedStructureName = Lang.GetMatching("botaniastory:structure-" + structureName);

            // Если перевода нет, используем техническое имя (terraaltar)
            if (translatedStructureName == "botaniastory:structure-" + structureName)
            {
                translatedStructureName = structureName;
            }

            // Формируем заголовок через lang-файл
            string titleText = Lang.Get("botaniastory:hud-structure-title", translatedStructureName);
            compo.AddStaticText(titleText, headerFont, headerBounds);

            int yOffset = 40;

            // Фейковый инвентарь (DummyInventory) больше не нужен! Удалили его.

            for (int i = 0; i < items.Count; i++)
            {
                StructureTrackerItemData item = items[i];
                bool isComplete = item.PlacedCount >= item.RequiredCount;
                CairoFont currentFont = isComplete ? completeFont : normalFont;

                // Чуть уменьшили размер иконки для аккуратности
                ElementBounds iconBounds = ElementBounds.Fixed(10, yOffset, 30, 30);
                ElementBounds textBounds = ElementBounds.Fixed(50, yOffset + 5, 250, 30);

                // Используем НОВЫЙ метод отрисовки иконки
                compo.AddItemIcon(new ItemStack(item.Block), iconBounds, "icon_" + i);

                // Имя блока уже локализовано движком через GetPlacedBlockName
                string blockName = item.Block.GetPlacedBlockName(capi.World, null);

                compo.AddStaticText($"{blockName}: {item.PlacedCount} / {item.RequiredCount}", currentFont, textBounds);

                yOffset += 40;
            }

            SingleComposer = compo.Compose();
        }

        public override bool TryOpen()
        {
            if (IsOpened()) return false;
            return base.TryOpen();
        }
    }
}