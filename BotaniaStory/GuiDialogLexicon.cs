using botaniastory;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace botaniastory
{
    public partial class GuiDialogLexicon : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private BookView currentView = BookView.Home;
        private BookCategory currentCategory = null;
        private BookChapter currentChapter = null;
        private int currentSpread = 0;

        private string searchQuery = "";
        private bool isSearchOpen = false;

        public float bookScale = 1.0f;
        public LexiconConfig config;

        private GuiDialogLexiconSettings settingsDialog;
        private UIDebugger debuggerDialog;

        private List<BookCategory> categories;
        private GuiElementHoverRow[] searchRows = new GuiElementHoverRow[28];

        public Dictionary<string, double[]> ui = LexiconUIData.GetDefaultUI();

        private Stack<HistoryState> history = new Stack<HistoryState>();
        public ItemSlot ActiveBookSlot;

        private class HistoryState
        {
            public BookCategory Category;
            public BookChapter Chapter;
            public int Spread;
        }

        public GuiDialogLexicon(ICoreClientAPI capi) : base(capi)
        {
            config = capi.LoadModConfig<LexiconConfig>("lexicon_client.json") ?? new LexiconConfig();
            bookScale = config.BookScale;

            if (config.CustomUI != null)
            {
                foreach (var kvp in config.CustomUI)
                {
                    if (ui.ContainsKey(kvp.Key)) ui[kvp.Key] = kvp.Value;
                }
            }

            categories = BookDataManager.GetTemplateCategories();
            SetupDialog();
        }

        private void PlayLexiconSound(string soundName)
        {
            float volume = config.Volume / 100f;
            if (volume <= 0f) return;

            var pos = capi.World.Player.Entity.Pos;
            AssetLocation loc = new AssetLocation("botaniastory", "sounds/" + soundName);
            capi.World.PlaySoundAt(loc, pos.X, pos.Y, pos.Z, null, true, 32, volume);
        }

        public void SaveConfig() { capi.StoreModConfig(config, "lexicon_client.json"); }
        public void UpdateScale(float newScale) { bookScale = newScale; config.BookScale = newScale; SaveConfig(); RecomposeDialog(); }
        private void RecomposeDialog() { SingleComposer?.Dispose(); SetupDialog(); }
        public void RecomposeFromDebugger() { RecomposeDialog(); }

        public override void OnRenderGUI(float deltaTime)
        {
            var bg = SingleComposer?.GetElement("bookBackground") as GuiElementStretchedImage;
            if (bg != null)
            {
                bg.RenderMyBackground(deltaTime);
            }
            base.OnRenderGUI(deltaTime);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            PlayLexiconSound("lexiconopen");

            var bookmarkedIds = categories
                .SelectMany(c => c.Chapters)
                .Where(ch => ch.IsBookmarked)
                .Select(ch => ch.Id)
                .ToList();

            categories = BookDataManager.GetTemplateCategories();

            foreach (var chapter in categories.SelectMany(c => c.Chapters))
            {
                if (bookmarkedIds.Contains(chapter.Id))
                {
                    chapter.IsBookmarked = true;
                }
            }

            if (currentCategory != null) currentCategory = categories.FirstOrDefault(c => c.Id == currentCategory.Id);
            if (currentChapter != null && currentCategory != null) currentChapter = currentCategory.Chapters.FirstOrDefault(c => c.Id == currentChapter.Id);

            if (currentCategory == null && currentChapter == null) currentView = BookView.Home;

            RecomposeDialog();
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            settingsDialog?.TryClose();
            debuggerDialog?.TryClose();
            PlayLexiconSound("lexiconclose");

            if (ActiveBookSlot != null && ActiveBookSlot.Itemstack != null)
            {
                string currentPath = ActiveBookSlot.Itemstack.Item.Code.Path;
                if (currentPath.EndsWith("open"))
                {
                    string newPath = currentPath.Replace("open", "closed");
                    Item newBookItem = capi.World.GetItem(new AssetLocation("botaniastory", newPath));
                    if (newBookItem != null)
                    {
                        ActiveBookSlot.Itemstack = new ItemStack(newBookItem);
                        ActiveBookSlot.MarkDirty();
                    }
                }
            }
        }
    }
}