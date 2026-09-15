using System;
using System.Collections.Generic;
using BotaniaStory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace BotaniaStory.items
{
    public class GuiDialogFilterScroll : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private readonly ItemSlot paperSlot;
        private readonly bool isBlacklist;

        private InventoryGeneric searchInventory;
        private InventoryGeneric selectedInventory;

        private ElementBounds searchClipBounds;
        private ElementBounds selectedClipBounds;

        private float searchScrollValue;
        private float selectedScrollValue;

        private readonly List<ItemStack> selectedStacks = new List<ItemStack>();

        private string initialPatternsText = "";
        private string initialPlayersText = "";

        private struct SearchEntry
        {
            public ItemStack Stack;
            public string NameCache;
            public string CodeCache;
        }

        private static SearchEntry[] cachedEntries;
        private static object cachedWorld;

        private SearchEntry[] searchCache;

        public GuiDialogFilterScroll(ICoreClientAPI capi, ItemSlot slot, bool isBlacklist) : base(capi)
        {
            paperSlot = slot;
            this.isBlacklist = isBlacklist;

            searchInventory = new InventoryGeneric(200, "searchInv-0", capi, null);
            selectedInventory = new InventoryGeneric(100, "selectedInv-0", capi, null);

            BuildSearchCacheIfNeeded();
            LoadSavedFilters();
            SetupDialog();
        }

        private void BuildSearchCacheIfNeeded()
        {
            if (cachedEntries != null && cachedWorld == capi.World)
            {
                searchCache = cachedEntries;
                return;
            }

            int estimatedCapacity = capi.World.Items.Count + capi.World.Blocks.Count;
            var tempList = new List<SearchEntry>(estimatedCapacity);

            foreach (var item in capi.World.Items)
            {
                if (item?.Code == null || item.Id == 0 || item.IsMissing) continue;

                var stack = new ItemStack(item);
                tempList.Add(new SearchEntry
                {
                    Stack = stack,
                    NameCache = stack.GetName(),
                    CodeCache = item.Code.Path
                });
            }

            foreach (var block in capi.World.Blocks)
            {
                if (block?.Code == null || block.Id == 0 || block.IsMissing) continue;

                var stack = new ItemStack(block);
                tempList.Add(new SearchEntry
                {
                    Stack = stack,
                    NameCache = stack.GetName(),
                    CodeCache = block.Code.Path
                });
            }

            cachedEntries = tempList.ToArray();
            cachedWorld = capi.World;
            searchCache = cachedEntries;
        }

        private void LoadSavedFilters()
        {
            var attr = paperSlot?.Itemstack?.Attributes;
            if (attr == null) return;

            if (attr.HasAttribute("filterList"))
            {
                var arr = (attr["filterList"] as StringArrayAttribute)?.value;
                if (arr != null)
                {
                    foreach (var code in arr)
                    {
                        var loc = new AssetLocation(code);
                        var item = capi.World.GetItem(loc);
                        var block = capi.World.GetBlock(loc);

                        ItemStack stack = null;
                        if (item != null) stack = new ItemStack(item);
                        else if (block != null && block.Id != 0) stack = new ItemStack(block);

                        if (stack != null && selectedStacks.Count < selectedInventory.Count)
                        {
                            stack.StackSize = 1;
                            selectedStacks.Add(stack);
                        }
                    }
                }
            }

            var patterns = (attr["filterPatterns"] as StringArrayAttribute)?.value;
            if (patterns == null || patterns.Length == 0) return;

            var itemPatterns = new List<string>();
            var playerNames = new List<string>();

            foreach (var raw in patterns)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (raw.StartsWith(ItemFilterScroll.PlayerPatternPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string name = raw.Substring(ItemFilterScroll.PlayerPatternPrefix.Length).Trim();
                    if (name.Length > 0) playerNames.Add(name);
                }
                else
                {
                    itemPatterns.Add(raw);
                }
            }

            initialPatternsText = string.Join(", ", itemPatterns);
            initialPlayersText = string.Join(", ", playerNames);
        }

        private void SetupDialog()
        {
            string title = isBlacklist
                ? Lang.Get("botaniastory:dialog-filter-blacklist")
                : Lang.Get("botaniastory:dialog-filter-whitelist");

            ElementBounds searchInputBounds = ElementBounds.Fixed(0, 40, 250, 30);

            ElementBounds searchOuterBounds = ElementBounds.Fixed(0, 80, 250, 300);
            searchClipBounds = searchOuterBounds.CopyOffsetedSibling();
            ElementBounds searchGridBounds = ElementBounds.Fixed(0, 0, 250, 300);
            searchClipBounds.WithChildren(searchGridBounds);

            ElementBounds searchScrollbarBounds = searchOuterBounds.RightCopy(5).WithFixedSize(20, 300);

            ElementBounds selectedTitleBounds = ElementBounds.Fixed(290, 45, 250, 30);

            ElementBounds selectedOuterBounds = ElementBounds.Fixed(290, 80, 250, 300);
            selectedClipBounds = selectedOuterBounds.CopyOffsetedSibling();
            ElementBounds selectedGridBounds = ElementBounds.Fixed(0, 0, 250, 300);
            selectedClipBounds.WithChildren(selectedGridBounds);

            ElementBounds selectedScrollbarBounds = selectedOuterBounds.RightCopy(5).WithFixedSize(20, 300);

            ElementBounds patternTitleBounds = ElementBounds.Fixed(580, 35, 290, 30);
            ElementBounds patternInsetBounds = ElementBounds.Fixed(580, 70, 290, 145);
            ElementBounds patternInputBounds = ElementBounds.Fixed(585, 75, 280, 135);

            ElementBounds playerTitleBounds = ElementBounds.Fixed(580, 230, 290, 30);
            ElementBounds playerInsetBounds = ElementBounds.Fixed(580, 265, 290, 115);
            ElementBounds playerInputBounds = ElementBounds.Fixed(585, 270, 280, 105);

            ElementBounds clearBtnBounds = ElementBounds.Fixed(0, 400, 100, 30);
            ElementBounds saveBtnBounds = ElementBounds.Fixed(770, 400, 100, 30);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(
                searchInputBounds, searchClipBounds, searchScrollbarBounds,
                selectedTitleBounds, selectedClipBounds, selectedScrollbarBounds,
                patternTitleBounds, patternInsetBounds, patternInputBounds,
                playerTitleBounds, playerInsetBounds, playerInputBounds,
                clearBtnBounds, saveBtnBounds
            );

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle);

            SingleComposer = capi.Gui
                .CreateCompo("filterscrolldialog", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(title, () => TryClose())
                .AddTextInput(searchInputBounds, OnSearchTextChanged, CairoFont.TextInput(), "searchInput")
                .BeginClip(searchClipBounds)
                    .AddItemSlotGrid(searchInventory, p => { }, 5, searchGridBounds, "searchGrid")
                .EndClip()
                .AddVerticalScrollbar(OnSearchScroll, searchScrollbarBounds, "searchScrollbar")
                .AddRichtext(Lang.Get("botaniastory:dialog-filter-added"), CairoFont.WhiteSmallText(), selectedTitleBounds)
                .BeginClip(selectedClipBounds)
                    .AddItemSlotGrid(selectedInventory, p => { }, 5, selectedGridBounds, "selectedGrid")
                .EndClip()
                .AddVerticalScrollbar(OnSelectedScroll, selectedScrollbarBounds, "selectedScrollbar")
                .AddRichtext(Lang.Get("botaniastory:dialog-filter-patterns"), CairoFont.WhiteSmallText(), patternTitleBounds)
                .AddInset(patternInsetBounds, 3)
                .AddTextArea(patternInputBounds, OnPatternTextChanged, CairoFont.WhiteSmallText(), "patternInput")
                .AddRichtext(Lang.Get("botaniastory:dialog-filter-players"), CairoFont.WhiteSmallText(), playerTitleBounds)
                .AddInset(playerInsetBounds, 3)
                .AddTextArea(playerInputBounds, OnPlayerTextChanged, CairoFont.WhiteSmallText(), "playerInput")
                .AddSmallButton(Lang.Get("botaniastory:dialog-filter-clear"), OnClickClear, clearBtnBounds)
                .AddSmallButton(Lang.Get("botaniastory:dialog-filter-save"), OnClickSave, saveBtnBounds)
                .Compose();

            OnSearchTextChanged("");
            RefreshSelectedGrid();

            var patternArea = SingleComposer.GetTextArea("patternInput");
            if (patternArea != null && initialPatternsText.Length > 0)
                patternArea.SetValue(initialPatternsText);

            var playerArea = SingleComposer.GetTextArea("playerInput");
            if (playerArea != null && initialPlayersText.Length > 0)
                playerArea.SetValue(initialPlayersText);
        }

        private void OnSearchScroll(float value)
        {
            searchScrollValue = value;
            ElementBounds bounds = SingleComposer.GetSlotGrid("searchGrid").Bounds;
            bounds.fixedY = -value;
            bounds.CalcWorldBounds();
        }

        private void OnSelectedScroll(float value)
        {
            selectedScrollValue = value;
            ElementBounds bounds = SingleComposer.GetSlotGrid("selectedGrid").Bounds;
            bounds.fixedY = -value;
            bounds.CalcWorldBounds();
        }

        private void OnPatternTextChanged(string text)
        {
        }

        private void OnPlayerTextChanged(string text)
        {
        }

        private void UpdateSearchScrollbar()
        {
            int active = 0;
            foreach (var slot in searchInventory)
            {
                if (!slot.Empty) active++;
            }

            var scrollbar = SingleComposer.GetScrollbar("searchScrollbar");
            if (scrollbar == null) return;

            int rows = Math.Max(1, (int)Math.Ceiling(active / 5.0));
            scrollbar.SetHeights((float)searchClipBounds.fixedHeight, rows * 50f);
        }

        private void UpdateSelectedScrollbar()
        {
            var scrollbar = SingleComposer.GetScrollbar("selectedScrollbar");
            if (scrollbar == null) return;

            int rows = Math.Max(1, (int)Math.Ceiling(selectedStacks.Count / 5.0));
            scrollbar.SetHeights((float)selectedClipBounds.fixedHeight, rows * 50f);
        }

        public override void OnMouseDown(MouseEvent args)
        {
            try
            {
                var searchGrid = SingleComposer.GetSlotGrid("searchGrid");
                if (searchGrid != null && searchClipBounds.PointInside(args.X, args.Y))
                {
                    if (args.Button == EnumMouseButton.Left)
                    {
                        int idx = CalculateSlotIndex(searchGrid, searchClipBounds, searchScrollValue, args.X, args.Y, 5);
                        if (idx >= 0 && idx < searchInventory.Count && !searchInventory[idx].Empty)
                        {
                            AddSelected(searchInventory[idx].Itemstack);
                            capi.Gui.PlaySound("tick");
                        }
                    }

                    args.Handled = true;
                    return;
                }

                var selectedGrid = SingleComposer.GetSlotGrid("selectedGrid");
                if (selectedGrid != null && selectedClipBounds.PointInside(args.X, args.Y))
                {
                    if (args.Button == EnumMouseButton.Right)
                    {
                        int idx = CalculateSlotIndex(selectedGrid, selectedClipBounds, selectedScrollValue, args.X, args.Y, 5);
                        if (idx >= 0 && idx < selectedStacks.Count)
                        {
                            RemoveSelectedAt(idx);
                            capi.Gui.PlaySound("tick");
                        }
                    }

                    args.Handled = true;
                    return;
                }

                base.OnMouseDown(args);
            }
            catch (Exception e)
            {
                capi.Logger.Error("[FilterScroll] OnMouseDown error: {0}", e);
            }
        }

        private int CalculateSlotIndex(
            GuiElementItemSlotGrid grid,
            ElementBounds clipBounds,
            float scrollValue,
            int mouseX,
            int mouseY,
            int columns)
        {
            double pitch = grid.Bounds.InnerWidth / columns;
            if (pitch < 10) pitch = GuiElement.scaled(50.0);

            double dx = mouseX - clipBounds.absX;
            double dy = mouseY - clipBounds.absY + GuiElement.scaled(scrollValue);

            if (dx < 0 || dy < 0) return -1;

            int col = (int)(dx / pitch);
            int row = (int)(dy / pitch);
            if (col < 0 || col >= columns) return -1;

            return row * columns + col;
        }

        private void OnSearchTextChanged(string text)
        {
            for (int i = 0; i < searchInventory.Count; i++)
            {
                searchInventory[i].Itemstack = null;
                searchInventory[i].MarkDirty();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                UpdateSearchScrollbar();
                return;
            }

            string query = text.Trim();
            int slotIdx = 0;

            for (int i = 0; i < searchCache.Length; i++)
            {
                ref SearchEntry entry = ref searchCache[i];

                bool matchName = entry.NameCache.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchCode = entry.CodeCache.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!matchName && !matchCode) continue;

                searchInventory[slotIdx].Itemstack = entry.Stack.Clone();
                searchInventory[slotIdx].MarkDirty();
                slotIdx++;

                if (slotIdx >= searchInventory.Count) break;
            }

            UpdateSearchScrollbar();
        }

        private void AddSelected(ItemStack stackToAdd)
        {
            if (stackToAdd?.Collectible == null) return;

            foreach (var stack in selectedStacks)
            {
                if (stack?.Collectible != null && stack.Collectible.Code.Equals(stackToAdd.Collectible.Code))
                    return;
            }

            if (selectedStacks.Count >= selectedInventory.Count) return;

            var clone = stackToAdd.Clone();
            clone.StackSize = 1;
            selectedStacks.Add(clone);

            RefreshSelectedGrid();
        }

        private void RemoveSelectedAt(int index)
        {
            if (index < 0 || index >= selectedStacks.Count) return;

            selectedStacks.RemoveAt(index);
            RefreshSelectedGrid();
        }

        private void RefreshSelectedGrid()
        {
            for (int i = 0; i < selectedInventory.Count; i++)
            {
                selectedInventory[i].Itemstack = i < selectedStacks.Count ? selectedStacks[i] : null;
                selectedInventory[i].MarkDirty();
            }

            UpdateSelectedScrollbar();
        }

        private bool OnClickClear()
        {
            selectedStacks.Clear();
            RefreshSelectedGrid();

            SingleComposer.GetTextArea("patternInput")?.SetValue("");
            SingleComposer.GetTextArea("playerInput")?.SetValue("");

            return true;
        }

        private bool OnClickSave()
        {
            var codesToSave = new List<string>();
            foreach (var stack in selectedStacks)
            {
                if (stack?.Collectible != null)
                    codesToSave.Add(stack.Collectible.Code.ToString());
            }

            var patternsToSave = new List<string>(ParseEntries(
                SingleComposer.GetTextArea("patternInput")?.GetText()));

            foreach (var playerName in ParseEntries(SingleComposer.GetTextArea("playerInput")?.GetText()))
            {
                patternsToSave.Add(ItemFilterScroll.PlayerPatternPrefix + playerName);
            }

            capi.Network
                .GetChannel("botanianetwork")
                .SendPacket(new FilterUpdatePacket
                {
                    FilteredItemCodes = codesToSave.ToArray(),
                    FilterPatterns = patternsToSave.ToArray()
                });

            TryClose();
            return true;
        }

        private static string[] ParseEntries(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return result.ToArray();

            string normalized = raw
                .Replace('\r', ',')
                .Replace('\n', ',')
                .Replace(';', ',');

            foreach (var part in normalized.Split(','))
            {
                string value = part.Trim();
                if (value.Length > 0) result.Add(value);
            }

            return result.ToArray();
        }
    }
}
