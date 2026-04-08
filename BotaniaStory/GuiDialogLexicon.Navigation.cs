using botaniastory;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace botaniastory
{
    public partial class GuiDialogLexicon
    {
        private bool OnClickPrev()
        {
            PlayLexiconSound("lexiconpage");

            if (currentView == BookView.Reading)
            {
                if (currentSpread > 0)
                {
                    currentSpread--;
                    RecomposeDialog();
                }
                else
                {
                    if (history.Count > 0)
                    {
                        var prevState = history.Pop();
                        currentCategory = prevState.Category;
                        currentChapter = prevState.Chapter;
                        currentSpread = prevState.Spread;
                        currentView = BookView.Reading;
                        RecomposeDialog();
                    }
                    else
                    {
                        currentView = BookView.CategoryList;
                        RecomposeDialog();
                    }
                }
            }
            else if (currentView == BookView.CategoryList)
            {
                if (history.Count > 0)
                {
                    var prevState = history.Pop();
                    currentCategory = prevState.Category;
                    currentChapter = prevState.Chapter;
                    currentSpread = prevState.Spread;
                    currentView = BookView.Reading;
                    RecomposeDialog();
                }
                else
                {
                    GoHome(false);
                }
            }
            return true;
        }

        private bool OnClickNext()
        {
            PlayLexiconSound("lexiconpage");

            if (currentView == BookView.Reading && currentChapter != null && (currentSpread + 1) * 2 < currentChapter.Pages.Count)
            {
                currentSpread++; RecomposeDialog();
            }
            return true;
        }

        public void OpenSpecificCategory(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId)) return;

            // ЗАЩИТА: Если мы УЖЕ находимся в списке глав этой категории, ничего не делаем!
            if (currentView == BookView.CategoryList && currentCategory != null && currentCategory.Id == categoryId) return;

            var targetCategory = categories.FirstOrDefault(c => c.Id == categoryId);

            if (targetCategory != null)
            {
                if (currentView == BookView.Reading && currentChapter != null)
                {
                    history.Push(new HistoryState
                    {
                        Category = currentCategory,
                        Chapter = currentChapter,
                        Spread = currentSpread
                    });
                }

                currentCategory = targetCategory;
                currentView = BookView.CategoryList;
                isSearchOpen = false;

                PlayLexiconSound("lexiconpage");
                RecomposeDialog();
            }
        }

        public void OpenSpecificChapter(string chapterId)
        {
            if (string.IsNullOrEmpty(chapterId)) return;

            // ЗАЩИТА: Если мы УЖЕ читаем эту главу, отменяем переход и не засоряем историю!
            if (currentView == BookView.Reading && currentChapter != null && currentChapter.Id == chapterId) return;

            var targetChapter = categories
                .SelectMany(c => c.Chapters)
                .FirstOrDefault(ch => ch.Id == chapterId);

            if (targetChapter != null)
            {
                if (currentView == BookView.Reading && currentChapter != null)
                {
                    history.Push(new HistoryState
                    {
                        Category = currentCategory,
                        Chapter = currentChapter,
                        Spread = currentSpread
                    });
                }

                var targetCategory = categories.FirstOrDefault(c => c.Chapters.Contains(targetChapter));
                if (targetCategory != null) currentCategory = targetCategory;

                currentChapter = targetChapter;
                currentSpread = 0;
                currentView = BookView.Reading;
                isSearchOpen = false;

                PlayLexiconSound("lexiconpage");
                RecomposeDialog();
            }
        }

        private bool GoHome(bool playSound = true)
        {
            history.Clear();
            currentView = BookView.Home;
            if (playSound) PlayLexiconSound("lexiconpage");
            isSearchOpen = false;
            searchQuery = "";
            RecomposeDialog();
            return true;
        }

        private bool ToggleSearch() { history.Clear(); isSearchOpen = !isSearchOpen; PlayLexiconSound("lexiconpage"); if (!isSearchOpen) searchQuery = ""; RecomposeDialog(); return true; }
        private bool OpenCategory(BookCategory category) { history.Clear(); currentCategory = category; currentView = BookView.CategoryList; isSearchOpen = false; PlayLexiconSound("lexiconpage"); RecomposeDialog(); return true; }
        private bool OpenChapter(BookChapter chapter) { history.Clear(); currentChapter = chapter; currentSpread = 0; currentView = BookView.Reading; isSearchOpen = false; PlayLexiconSound("lexiconpage"); RecomposeDialog(); return true; }
        private void AddBookmark() { if (currentChapter != null && !currentChapter.IsBookmarked) { currentChapter.IsBookmarked = true; capi.Gui.PlaySound("tick"); RecomposeDialog(); } }
        private void RemoveBookmark(BookChapter ch) { if (ch != null && ch.IsBookmarked) { ch.IsBookmarked = false; RecomposeDialog(); } }

        private bool OnToggleSettings()
        {
            if (settingsDialog == null || !settingsDialog.IsOpened())
            {
                settingsDialog = new GuiDialogLexiconSettings(capi, config, this);
                settingsDialog.TryOpen();
            }
            else { settingsDialog.TryClose(); }
            return true;
        }

        private bool OnToggleDebugger()
        {
            if (debuggerDialog == null || !debuggerDialog.IsOpened())
            {
                debuggerDialog = new UIDebugger(capi, this);
                debuggerDialog.TryOpen();
            }
            else { debuggerDialog.TryClose(); }
            return true;
        }
        // --- НОВЫЕ МЕТОДЫ УПРАВЛЕНИЯ МЫШЬЮ ---

        // 1. Обработка колёсика мыши
        // 1. Обработка колёсика мыши (Улучшенная и безопасная)
        public override void OnMouseWheel(MouseWheelEventArgs args)
        {
            // СНАЧАЛА ПЕРЕХВАТЫВАЕМ КОЛЁСИКО (до того, как базовый интерфейс его поглотит)
            if (config.MouseWheelPaging && currentView == BookView.Reading)
            {
                if (args.delta > 0) // Колесико вверх (от себя) -> назад
                {
                    // ПРОВЕРКА: Листаем назад ТОЛЬКО если мы не на первой странице
                    if (currentSpread > 0)
                    {
                        currentSpread--;
                        PlayLexiconSound("lexiconpage");
                        RecomposeDialog();
                    }
                    args.SetHandled(); // Поглощаем событие в любом случае, чтобы хотбар не крутился
                    return;
                }
                else if (args.delta < 0) // Колесико вниз (на себя) -> вперед
                {
                    // ПРОВЕРКА: Листаем вперед ТОЛЬКО если есть еще страницы
                    if (currentChapter != null && (currentSpread + 1) * 2 < currentChapter.Pages.Count)
                    {
                        currentSpread++;
                        PlayLexiconSound("lexiconpage");
                        RecomposeDialog();
                    }
                    args.SetHandled(); // Поглощаем событие
                    return;
                }
            }

            // Если опция выключена или мы не в режиме чтения — отдаем управление движку
            base.OnMouseWheel(args);
        }

        // 2. Обработка ПКМ (Правая кнопка мыши)
        public override void OnMouseDown(MouseEvent args)
        {
            // СНАЧАЛА ПЕРЕХВАТЫВАЕМ ПКМ (иначе игра проигнорирует клик или использует предмет в руке)
            if (config.RightClickBack && args.Button == EnumMouseButton.Right && currentView != BookView.Home)
            {
                OnClickPrev(); // Возвращаемся на шаг назад
                args.Handled = true; // Забираем клик себе
                return;
            }

            // Если опция выключена или нажата другая кнопка — отдаем управление движку
            base.OnMouseDown(args);
        }
    }

}