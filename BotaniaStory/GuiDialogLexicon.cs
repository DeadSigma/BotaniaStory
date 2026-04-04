using botaniastory;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace botaniastory
{
    public class GuiDialogLexicon : GuiDialog
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

        private ItemStack[] GetItemStacks(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;

            var stacks = new List<ItemStack>();
            AssetLocation loc = new AssetLocation(code);

            // Если в коде есть *, ищем все подходящие варианты (для слайдшоу)
            if (code.Contains("*"))
            {
                foreach (var item in capi.World.Items)
                {
                    if (item.Code != null && WildcardUtil.Match(loc, item.Code))
                        stacks.Add(new ItemStack(item));
                }
                foreach (var block in capi.World.Blocks)
                {
                    // ИСПРАВЛЕНО: теперь проверяем block.Code
                    if (block.Code != null && WildcardUtil.Match(loc, block.Code))
                        stacks.Add(new ItemStack(block));
                }
            }
            else
            {
                Item item = capi.World.GetItem(loc);
                if (item != null) stacks.Add(new ItemStack(item));
                Block block = capi.World.GetBlock(loc);
                if (block != null) stacks.Add(new ItemStack(block));
            }

            return stacks.Count > 0 ? stacks.ToArray() : null;
        }

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0.0);

            var bounds = new Dictionary<string, ElementBounds>();
            foreach (var kvp in ui)
            {
                double s = kvp.Value[4];
                bounds[kvp.Key] = ElementBounds.Fixed(kvp.Value[0] * bookScale, kvp.Value[1] * bookScale, kvp.Value[2] * bookScale * s, kvp.Value[3] * bookScale * s);
            }

            var gearImage = new GuiElementClickableImage(capi, bounds["Шестеренка"], new AssetLocation("botaniastory:gui/gear.png"), () => OnToggleSettings());
            var tabGreen = new GuiElementClickableImage(capi, bounds["Вкладка_Зеленая"], new AssetLocation("botaniastory:gui/tab_green.png"), () => GoHome());
            var tabPurple = new GuiElementClickableImage(capi, bounds["Вкладка_Фиолетовая"], new AssetLocation("botaniastory:gui/tab_purple.png"), () => ToggleSearch());

            double[] inkColor = new double[] { 0.1, 0.1, 0.1, 1 };
            CairoFont bookFont = CairoFont.WhiteSmallText().WithColor(inkColor).WithFontSize((float)(GuiStyle.SmallFontSize * bookScale));

            var titleCfg = ui["Заголовок_Левый"];
            CairoFont titleFont = CairoFont.WhiteSmallText().WithColor(inkColor).WithFontSize((float)(GuiStyle.NormalFontSize * bookScale * titleCfg[4])).WithWeight(Cairo.FontWeight.Bold).WithOrientation(EnumTextOrientation.Center);
            // Создаем наш растягивающийся фон через твой класс
            var bookBg = new GuiElementStretchedImage(capi, bounds["Книга_Фон"], new AssetLocation("botaniastory:gui/book.png"));
            bookBg.IsBackground = true; 

            var compo = capi.Gui.CreateCompo("lexiconDialog", dialogBounds)
                .AddInteractiveElement(bookBg, "bookBackground") // Вставляем фон
                .AddInteractiveElement(gearImage, "btnSettingsIcon")
                .AddButton(" [ D ] ", OnToggleDebugger, bounds["Кнопка_Дебаггера"], CairoFont.WhiteSmallText(), EnumButtonStyle.Small)
                .AddInteractiveElement(tabGreen, "tabHome")
                .AddInteractiveElement(tabPurple, "tabSearch");

            if (currentView != BookView.Home)
            {
                var prevBtnImage = new GuiElementClickableImage(capi, bounds["Кнопка_Назад"], new AssetLocation("botaniastory:gui/btn_prev.png"), () => OnClickPrev());
                compo.AddInteractiveElement(prevBtnImage, "prevButton");
            }

            if (currentView == BookView.Reading)
            {
                var nextBtnImage = new GuiElementClickableImage(capi, bounds["Кнопка_Вперед"], new AssetLocation("botaniastory:gui/btn_next.png"), () => OnClickNext());
                compo.AddInteractiveElement(nextBtnImage, "nextButton");
            }

            // --- ЗАКЛАДКИ ---
            var bookmarkedChapters = categories.SelectMany(c => c.Chapters).Where(ch => ch.IsBookmarked).ToList();

            var tabCfg = ui["Закладки_Сбоку"];
            var tabItemCfg = ui["Закладки_Сбоку_Предмет"];
            double startY = tabCfg[1];
            double tabScale = tabCfg[4];

            for (int i = 0; i < bookmarkedChapters.Count; i++)
            {
                var ch = bookmarkedChapters[i];
                double yPos = startY + (i * 85 * tabScale); // Отступ масштабируется
                if (i >= 6) break;

                ElementBounds tabBounds = ElementBounds.Fixed(tabCfg[0] * bookScale, yPos * bookScale, tabCfg[2] * bookScale * tabScale, tabCfg[3] * bookScale * tabScale);

                var bookmarkTab = new GuiElementClickableTab(capi, tabBounds, new AssetLocation("botaniastory:gui/tab_empty.png"), GetItemStacks(ch.TabItemCode)?[0],
                    () => OpenChapter(ch), () => RemoveBookmark(ch))
                {
                    IconOffsetX = tabItemCfg[0] * bookScale,
                    IconOffsetY = tabItemCfg[1] * bookScale,
                    IconScale = (float)tabItemCfg[4]
                };
                compo.AddInteractiveElement(bookmarkTab, $"bookmarkTab_{i}");
            }

            var listCoords = ui["Список_Глав_Координаты"];
            var listStep = ui["Список_Глав_Шаг"];
            double listScale = listStep[4];
            CairoFont listFont = CairoFont.WhiteSmallText().WithColor(inkColor).WithFontSize((float)(GuiStyle.SmallFontSize * bookScale * listScale));

            // --- ПОИСК ---
            if (isSearchOpen)
            {
                compo.AddTextInput(bounds["Строка_Поиска"], OnSearchTextChanged, bookFont, "searchBar");
                ElementBounds titleBounds = ElementBounds.Fixed(titleCfg[0] * bookScale, titleCfg[1] * bookScale, titleCfg[2] * bookScale * titleCfg[4], titleCfg[3] * bookScale * titleCfg[4]);
                compo.AddDynamicText(Lang.Get("botaniastory:search-results"), titleFont, titleBounds, "searchTitle");

                var starCfg = ui["Звезда_Избранного"];

                for (int i = 0; i < 28; i++)
                {
                    ElementBounds rowBounds = ElementBounds.Fixed(-9999, -9999, 300 * bookScale * listScale, 25 * bookScale * listScale);

                    var hoverRow = new GuiElementHoverRow(capi, rowBounds, null, null)
                    {
                        IconOffsetX = ui["Список_Глав_Иконки"][0] * bookScale,
                        IconOffsetY = ui["Список_Глав_Иконки"][1] * bookScale,
                        IconScale = (float)ui["Список_Глав_Иконки"][4],
                        StarOffsetX = starCfg[0] * bookScale,
                        StarOffsetY = starCfg[1] * bookScale,
                        StarSize = starCfg[2] * bookScale * starCfg[4]
                    };
                    searchRows[i] = hoverRow;
                    compo.AddInteractiveElement(hoverRow, $"search_row_{i}");

                    ElementBounds textBounds = ElementBounds.Fixed(-9999, -9999, 260 * bookScale * listScale, 25 * bookScale * listScale);
                    compo.AddDynamicText("", listFont, textBounds, $"search_text_{i}");
                }
            }
            // --- ОТРИСОВКА СОДЕРЖИМОГО ---
            else
            {
                ElementBounds titleBounds = ElementBounds.Fixed(titleCfg[0] * bookScale, titleCfg[1] * bookScale, titleCfg[2] * bookScale * titleCfg[4], titleCfg[3] * bookScale * titleCfg[4]);

                if (currentView == BookView.Home)
                {
                    compo.AddDynamicText(Lang.Get("botaniastory:categories-title"), titleFont, titleBounds, "homeTitle");

                    // 1. Рисуем текст приветствия, используя ЕГО СОБСТВЕННЫЕ настройки
                    var welcomeTextCfg = ui["Приветствие_Текст"];
                    double welcomeTextScale = welcomeTextCfg[4];

                    CairoFont welcomeFont = CairoFont.WhiteSmallText()
                        .WithColor(inkColor)
                        // Умножаем на наш отдельный масштаб из дебаггера
                        .WithFontSize((float)(GuiStyle.NormalFontSize * bookScale * welcomeTextScale));

                    string welcomeText = Lang.Get(HomePageData.WelcomeTextKey);
                    // Используем новые границы bounds["Приветствие_Текст"]
                    compo.AddDynamicText(welcomeText, welcomeFont, bounds["Приветствие_Текст"], "homeRightText");

                    // 2. Рисуем картинку, беря путь и координаты из HomePageData



                    var welcomeImg = new GuiElementStretchedImage(capi, bounds["Приветствие_Картинка"], new AssetLocation(HomePageData.ImagePath));
                    compo.AddInteractiveElement(welcomeImg, "welcome_art_img");


                    var gridStart = ui["Кат_Сетка_Иконки"];
                    var gridStep = ui["Кат_Сетка_Шаг"];
                    var textOffset = ui["Кат_Сетка_Текст"];

                    int columns = (int)System.Math.Max(1, gridStep[2]);
                    double iconScale = gridStart[4];
                    double textScale = textOffset[4];

                    CairoFont centeredFont = CairoFont.WhiteSmallText()
                        .WithColor(inkColor)
                        .WithFontSize((float)(GuiStyle.SmallFontSize * bookScale * textScale))
                        .WithOrientation(EnumTextOrientation.Center);

                    for (int i = 0; i < categories.Count; i++)
                    {
                        var cat = categories[i];
                        double x = gridStart[0] + (i % columns) * gridStep[0];
                        double y = gridStart[1] + (i / columns) * gridStep[1];

                        ElementBounds iconBounds = ElementBounds.Fixed(x * bookScale, y * bookScale, gridStart[2] * bookScale * iconScale, gridStart[3] * bookScale * iconScale);

                        double textX = x + (gridStart[2] / 2.0) - (textOffset[2] / 2.0) + textOffset[0];
                        double textY = y + textOffset[1];
                        ElementBounds textBounds = ElementBounds.Fixed(textX * bookScale, textY * bookScale, textOffset[2] * bookScale * textScale, textOffset[3] * bookScale * textScale);

                        var catImg = new GuiElementClickableImage(capi, iconBounds, new AssetLocation(cat.IconPath), () => OpenCategory(cat));
                        compo.AddInteractiveElement(catImg, $"catIcon_{i}");
                        compo.AddStaticText(cat.Name, centeredFont, textBounds, $"catText_{i}");
                    }
                }
                else if (currentView == BookView.CategoryList && currentCategory != null)
                {
                    compo.AddDynamicText(currentCategory.Name.ToUpper(), titleFont, titleBounds, "catListTitle");

                    int maxOnLeft = (int)listStep[2];
                    double rowHeight = listStep[1];
                    var iconCfg = ui["Список_Глав_Иконки"];

                    var starCfg = ui["Звезда_Избранного"];

                    for (int i = 0; i < currentCategory.Chapters.Count; i++)
                    {
                        var ch = currentCategory.Chapters[i];
                        bool rightPage = i >= maxOnLeft;
                        int pageIndex = rightPage ? i - maxOnLeft : i;

                        if (rightPage && pageIndex >= listStep[3]) break;

                        double baseX = rightPage ? listCoords[2] : listCoords[0];
                        double baseY = rightPage ? listCoords[3] : listCoords[1];
                        double yOffset = baseY + (pageIndex * rowHeight);

                        ElementBounds rowBounds = ElementBounds.Fixed(baseX * bookScale, yOffset * bookScale, 300 * bookScale * listScale, 25 * bookScale * listScale);

                        var hoverRow = new GuiElementHoverRow(capi, rowBounds, GetItemStacks(ch.TabItemCode), () => OpenChapter(ch))
                        {
                            IconOffsetX = ui["Список_Глав_Иконки"][0] * bookScale,
                            IconOffsetY = ui["Список_Глав_Иконки"][1] * bookScale,
                            IconScale = (float)ui["Список_Глав_Иконки"][4],
                            StarOffsetX = starCfg[0] * bookScale,
                            StarOffsetY = starCfg[1] * bookScale,
                            StarSize = starCfg[2] * bookScale * starCfg[4]
                        };
                        hoverRow.IsBookmarked = ch.IsBookmarked;
                        compo.AddInteractiveElement(hoverRow, $"chapHover_{i}");

                        ElementBounds textBounds = ElementBounds.Fixed((baseX + listStep[0]) * bookScale, (yOffset + 4) * bookScale, 260 * bookScale * listScale, 25 * bookScale * listScale);
                        compo.AddStaticText(ch.Title, listFont, textBounds, $"chapText_{i}");
                    }
                }
                else if (currentView == BookView.Reading && currentChapter != null)
                {
                    double leftScale = ui["Левая_Страница"][4];
                    double rightScale = ui["Правая_Страница"][4];

                    CairoFont leftFont = CairoFont.WhiteSmallText().WithColor(inkColor).WithFontSize((float)(GuiStyle.SmallFontSize * bookScale * leftScale));
                    CairoFont rightFont = CairoFont.WhiteSmallText().WithColor(inkColor).WithFontSize((float)(GuiStyle.SmallFontSize * bookScale * rightScale));

                    // --- НОВАЯ СИСТЕМА: ПЕРЕБИРАЕМ ВСЕ РЕЦЕПТЫ В ГЛАВЕ ---
                    if (currentChapter.Recipes != null)
                    {
                        // Проходим по каждому рецепту
                        for (int i = 0; i < currentChapter.Recipes.Count; i++)
                        {
                            var recipe = currentChapter.Recipes[i];

                            // Проверяем, на этом ли развороте должен быть рецепт
                            if (recipe.Spread == currentSpread)
                            {
                                // --- 1. АПТЕКАРЬ ---
                                if (recipe.RecipeType == "Apothecary" && recipe.ApothecaryIngredients != null)
                                {
                                    var apoCfg = ui["Аптекарь_Область"];
                                    double rScale = apoCfg[4] * bookScale;
                                    ElementBounds apoBounds = ElementBounds.Fixed(apoCfg[0] * bookScale, apoCfg[1] * bookScale, 250 * rScale, 250 * rScale);

                                    var bgCfg = ui["Аптекарь_Фон"];
                                    double apoBgScale = bgCfg[4] * bookScale;
                                    ElementBounds bgBounds = ElementBounds.Fixed(bgCfg[0] * bookScale, bgCfg[1] * bookScale, bgCfg[2] * apoBgScale, bgCfg[3] * apoBgScale);

                                    ItemStack[][] inputs = new ItemStack[recipe.ApothecaryIngredients.Length][];
                                    for (int j = 0; j < recipe.ApothecaryIngredients.Length; j++)
                                    {
                                        inputs[j] = GetItemStacks(recipe.ApothecaryIngredients[j]);
                                    }

                                    ItemStack[] outputs = GetItemStacks(recipe.Output);
                                    ItemStack[] center = GetItemStacks(recipe.ApothecaryCenter);

                                    var apothecaryElement = new GuiElementApothecaryRecipe(capi, apoBounds, bgBounds, inputs, outputs, center, rScale);
                                    apothecaryElement.OnSlotClick = OnRecipeItemClicked;


                                    // Добавляем индекс [i] в имя, чтобы игра не путалась, если рецептов на странице два
                                    compo.AddInteractiveElement(apothecaryElement, $"apothecaryDisplay_{i}");
                                }

                                // --- 2. АЛЬФХЕЙМ ---
                                else if (recipe.RecipeType == "Alfheim" && recipe.AlfheimInputs != null)
                                {
                                    var alfheimCfg = ui["Альфхейм_Область"];
                                    double rScale = alfheimCfg[4] * bookScale;
                                    ElementBounds alfheimBounds = ElementBounds.Fixed(alfheimCfg[0] * bookScale, alfheimCfg[1] * bookScale, 250 * rScale, 250 * rScale);

                                    var bgCfg = ui["Альфхейм_Фон"];
                                    double alfheimBgScale = bgCfg[4] * bookScale;
                                    ElementBounds bgBounds = ElementBounds.Fixed(bgCfg[0] * bookScale, bgCfg[1] * bookScale, bgCfg[2] * alfheimBgScale, bgCfg[3] * alfheimBgScale);

                                    ItemStack[] inputs = new ItemStack[recipe.AlfheimInputs.Length];
                                    for (int j = 0; j < recipe.AlfheimInputs.Length; j++)
                                    {
                                        var stacks = GetItemStacks(recipe.AlfheimInputs[j]);
                                        if (stacks != null && stacks.Length > 0) inputs[j] = stacks[0];
                                    }

                                    if (recipe.AlfheimInputs.Length == 1 && recipe.AlfheimInputs[0].Contains("*"))
                                    {
                                        inputs = GetItemStacks(recipe.AlfheimInputs[0]);
                                    }

                                    ItemStack[] outputs = GetItemStacks(recipe.Output);

                                    var alfheimElement = new GuiElementAlfheimRecipe(capi, alfheimBounds, bgBounds, inputs, outputs, rScale);
                                    alfheimElement.OnSlotClick = OnRecipeItemClicked;
                                    alfheimElement.OpenHandbookFor = (stack) => {
                                        if (stack == null) return; capi.SendChatMessage($".hb {stack.Collectible.Code}");
                                    };

                                    compo.AddInteractiveElement(alfheimElement, $"alfheimDisplay_{i}");
                                }

                                // --- 3. СЕТКА ---
                                else if (recipe.RecipeType == "Grid" && recipe.Grid != null)
                                {
                                    var gridCfg = ui["Рецепт_Сетка"];
                                    double rScale = gridCfg[4] * bookScale;
                                    ElementBounds gridBounds = ElementBounds.Fixed(gridCfg[0] * bookScale, gridCfg[1] * bookScale, 350 * rScale, 200 * rScale);

                                    ItemStack[][] inputs = new ItemStack[9][];
                                    for (int j = 0; j < 9; j++)
                                    {
                                        inputs[j] = GetItemStacks(recipe.Grid[j]);
                                    }

                                    ItemStack[] outputs = GetItemStacks(recipe.Output);

                                    var recipeElement = new GuiElementRecipeGrid(capi, gridBounds, inputs, outputs, rScale);
                                    recipeElement.OnSlotClick = OnRecipeItemClicked;

                                    compo.AddInteractiveElement(recipeElement, $"recipeGridDisplay_{i}");
                                }
                            }
                        }
                    }
                    // ==============================================================

                    // --- УНИВЕРСАЛЬНЫЙ БЛОК: КАРТИНКИ НА СТРАНИЦАХ ---
                    // Проходимся по всем картинкам, которые есть в этой главе
                    if (currentChapter.Images != null)
                    {
                        for (int i = 0; i < currentChapter.Images.Count; i++)
                        {
                            var imgData = currentChapter.Images[i];

                            // Если картинка предназначена для текущего разворота и её шаблон есть в словаре
                            if (currentSpread == imgData.Spread && bounds.ContainsKey(imgData.UiKey))
                            {
                                var imgElement = new GuiElementStretchedImage(capi, bounds[imgData.UiKey], new AssetLocation(imgData.Path));
                                // Добавляем картинку, давая ей уникальное имя с индексом "i"
                                compo.AddInteractiveElement(imgElement, $"chapterImage_{i}");
                            }
                        }
                    }
                    // --- УНИВЕРСАЛЬНЫЙ БЛОК: ПОЛОСКИ МАНЫ НА СТРАНИЦАХ ---
                    if (currentChapter.ManaBars != null)
                    {
                        for (int i = 0; i < currentChapter.ManaBars.Count; i++)
                        {
                            var manaData = currentChapter.ManaBars[i];

                            if (currentSpread == manaData.Spread && bounds.ContainsKey(manaData.UiKey))
                            {
                                ElementBounds barBounds = bounds[manaData.UiKey];

                                // 1. Поднимаем текст еще на 5 пикселей вверх (меняем 15 на 20)
                                ElementBounds textBounds = ElementBounds.Fixed(barBounds.fixedX, barBounds.fixedY - (20 * bookScale), barBounds.fixedWidth, 20);
                                CairoFont textFont = CairoFont.WhiteSmallText().WithColor(new double[] { 0.4, 0.4, 0.4, 1 }).WithOrientation(EnumTextOrientation.Center);

                                // 2. Берем текст из lang-файла
                                string localizedManaText = Lang.Get("botaniastory:mana-cost");
                                compo.AddStaticText(localizedManaText, textFont, textBounds, $"manaText_{i}");

                                // 3. Сама полоска
                                var manaElement = new GuiElementManaBar(capi, barBounds, manaData.ManaCost, 100000);
                                compo.AddInteractiveElement(manaElement, $"manaBar_{i}");
                            }
                        }
                    }

                    // --- 2. А УЖЕ ЗАТЕМ ТЕКСТ (С ПОДДЕРЖКОЙ ССЫЛОК) ---
                    int leftIndex = currentSpread * 2;
                    int rightIndex = leftIndex + 1;

                    // Берем текст нужных страниц из памяти
                    string leftStr = leftIndex < currentChapter.Pages.Count ? currentChapter.Pages[leftIndex] : "";
                    string rightStr = rightIndex < currentChapter.Pages.Count ? currentChapter.Pages[rightIndex] : "";

                    // Пропускаем текст через парсер и добавляем как RichText
                    compo.AddRichtext(ParseLexiconText(leftStr, leftFont), bounds["Левая_Страница"], "leftPageText");
                    compo.AddRichtext(ParseLexiconText(rightStr, rightFont), bounds["Правая_Страница"], "rightPageText");


                    // Кнопка закладки
                    string bookmarkIcon = currentChapter.IsBookmarked ? "botaniastory:gui/bookmark_on.png" : "botaniastory:gui/bookmark_off.png";
                    var bookmarkBtn = new GuiElementClickableImage(capi, bounds["Кнопка_Закладки"], new AssetLocation(bookmarkIcon), () => AddBookmark());
                    compo.AddInteractiveElement(bookmarkBtn, "btnBookmark");

                    // --- КНОПКА ВИЗУАЛИЗАЦИИ ---
                    if (!string.IsNullOrEmpty(currentChapter.VisualizeStructure))
                    {
                        var btnCfg = ui["Кнопка_Визуализации"];
                        double bScale = btnCfg[4] * bookScale;

                        ElementBounds btnBounds = ElementBounds.Fixed(btnCfg[0] * bookScale, btnCfg[1] * bookScale, btnCfg[2] * bScale, btnCfg[3] * bScale);

                        var hologramSystem = capi.ModLoader.GetModSystem<LexiconHologramSystem>();
                        string buttonText = hologramSystem.isActive ? "Отключить" : "Визуализировать";

                        compo.AddButton(buttonText, () =>
                        {
                            if (hologramSystem.isActive)
                            {
                                hologramSystem.StopVisualization();
                            }
                            else
                            {
                                hologramSystem.StartVisualization(currentChapter.VisualizeStructure);
                            }

                            // ИСПРАВЛЕНИЕ: Мы убрали RecomposeDialog()!
                            // Теперь мы просто берем саму кнопку и напрямую меняем на ней текст.
                            // Это гарантирует, что кнопка отработает анимацию клика и не сломает GUI.
                            var btn = SingleComposer.GetButton("btnVisualize");
                            if (btn != null)
                            {
                                btn.Text = hologramSystem.isActive ? "Отключить" : "Визуализировать";
                            }

                            return true; // Возвращаем true, чтобы игра поняла, что клик успешно прошел
                        }, btnBounds, CairoFont.WhiteSmallText(), EnumButtonStyle.Normal, "btnVisualize");
                    }

                }
            }

            SingleComposer = compo.Compose();

            if (isSearchOpen)
            {
                SingleComposer.GetTextInput("searchBar")?.SetPlaceHolderText(Lang.Get("botaniastory:search-placeholder"));
                UpdateSearchResults();
            }

            UpdatePageContent();
        }

        private void OnSearchTextChanged(string text)
        {
            searchQuery = text;
            UpdateSearchResults();
        }

        private void UpdateSearchResults()
        {
            if (!isSearchOpen) return;

            var results = string.IsNullOrEmpty(searchQuery) ? new List<BookChapter>() :
                categories.SelectMany(c => c.Chapters).Where(ch => ch.Title.ToLower().Contains(searchQuery.ToLower())).ToList();

            var listCoords = ui["Список_Глав_Координаты"];
            var listStep = ui["Список_Глав_Шаг"];
            int maxLeft = (int)listStep[2];
            int maxRight = (int)listStep[3];
            int maxTotal = maxLeft + maxRight;
            double listScale = listStep[4];

            for (int i = 0; i < 28; i++)
            {
                var row = searchRows[i];
                var text = SingleComposer.GetDynamicText($"search_text_{i}");
                if (row == null || text == null) continue;

                if (i < results.Count && i < maxTotal && !string.IsNullOrEmpty(searchQuery))
                {
                    var ch = results[i];
                    bool rightPage = i >= maxLeft;
                    int pageIndex = rightPage ? i - maxLeft : i;

                    double baseX = rightPage ? listCoords[2] : listCoords[0];
                    double baseY = rightPage ? listCoords[3] : listCoords[1];
                    double yOffset = baseY + (pageIndex * listStep[1]);

                    row.Bounds.fixedWidth = 300 * bookScale * listScale;
                    row.Bounds.fixedHeight = 25 * bookScale * listScale;
                    row.Bounds.fixedX = baseX * bookScale;
                    row.Bounds.fixedY = yOffset * bookScale;
                    row.Bounds.CalcWorldBounds();

                    row.IconOffsetX = ui["Список_Глав_Иконки"][0] * bookScale;
                    row.IconOffsetY = ui["Список_Глав_Иконки"][1] * bookScale;
                    row.IconScale = (float)ui["Список_Глав_Иконки"][4];

                    var starCfg = ui["Звезда_Избранного"];
                    row.StarOffsetX = starCfg[0] * bookScale;
                    row.StarOffsetY = starCfg[1] * bookScale;
                    row.StarSize = starCfg[2] * bookScale * starCfg[4];

                    BookChapter capturedCh = ch;
                    row.UpdateData(GetItemStacks(ch.TabItemCode), () => OpenChapter(capturedCh), ch.IsBookmarked);

                    text.Bounds.fixedWidth = 260 * bookScale * listScale;
                    text.Bounds.fixedHeight = 25 * bookScale * listScale;
                    text.Bounds.fixedX = (baseX + listStep[0]) * bookScale;
                    text.Bounds.fixedY = (yOffset + 4) * bookScale;
                    text.Bounds.CalcWorldBounds();

                    text.SetNewText(ch.Title);
                }
                else
                {
                    text.SetNewText("");
                    row.UpdateData(null, null, false);
                    row.Bounds.fixedX = -9999;
                    row.Bounds.fixedY = -9999;
                    row.Bounds.CalcWorldBounds();
                }
            }
        }

        private void OnRecipeItemClicked(ItemStack clickedStack)
        {
            if (clickedStack == null) return;

            string itemCode = clickedStack.Collectible.Code.ToString();
            string domain = clickedStack.Collectible.Code.Domain;

            // 1. Если предмет из твоего мода — ищем соответствующую главу Лексикона
            if (domain == "botaniastory")
            {
                string chapterId = BookDataManager.GetChapterForBlock(itemCode);

                if (chapterId != null)
                {
                    // Защита от перезагрузки: если мы уже в этой главе, ничего не делаем
                    if (currentChapter != null && currentChapter.Id == chapterId) return;

                    OpenSpecificChapter(chapterId);
                }
            }
            // 2. Если предмет ванильный (game) или из другого мода — открываем ванильный Handbook
            else
            {
                OpenVanillaHandbook(clickedStack);
            }
        }

        // Перенесенный метод для открытия ванильной книги
        private void OpenVanillaHandbook(ItemStack stack)
        {
            if (stack == null) return;

            var handbookSys = capi.ModLoader.Systems.FirstOrDefault(s => s.GetType().Name == "ModSystemSurvivalHandbook");
            if (handbookSys != null)
            {
                var dialogField = handbookSys.GetType().GetField("dialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (dialogField != null)
                {
                    var dialog = dialogField.GetValue(handbookSys);
                    if (dialog != null)
                    {
                        var guiDialog = dialog as Vintagestory.API.Client.GuiDialog;
                        if (guiDialog != null && !guiDialog.IsOpened())
                        {
                            guiDialog.TryOpen();
                        }

                        string itemType = stack.Class == EnumItemClass.Item ? "item" : "block";
                        string pageCode = $"{itemType}-{stack.Collectible.Code.ToShortString()}";

                        var openMethod = dialog.GetType().GetMethod("OpenDetailPageFor", new Type[] { typeof(string) });
                        openMethod?.Invoke(dialog, new object[] { pageCode });
                    }
                }
            }
        }
        private void PlayLexiconSound(string soundName)
        {
            // Переводим 0-100 в 0.0-1.0
            float volume = config.Volume / 100f;

            // Если игрок выкрутил громкость на 0, просто ничего не делаем (экономим ресурсы)
            if (volume <= 0f) return;

            var pos = capi.World.Player.Entity.Pos;
            AssetLocation loc = new AssetLocation("botaniastory", "sounds/" + soundName);

            // Последний параметр - это наша громкость!
            capi.World.PlaySoundAt(loc, pos.X, pos.Y, pos.Z, null, true, 32, volume);
        }
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
                        currentView = BookView.Reading; // Убеждаемся, что возвращаемся к чтению
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
                // ПРОВЕРЯЕМ ИСТОРИЮ: Если мы пришли сюда по ссылке из главы — возвращаемся в главу!
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
                    // Если истории нет, просто выходим на главный экран
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

        private void UpdatePageContent()
        {
            if (currentView != BookView.Reading || currentChapter == null) return;

            var nextBtn = SingleComposer.GetElement("nextButton") as GuiElementClickableImage;

            // Отключаем/включаем кнопку "Вперед" в зависимости от количества страниц
            if (nextBtn != null)
            {
                nextBtn.Enabled = (currentSpread + 1) * 2 < currentChapter.Pages.Count;
            }
        }

        // --- ЗАГЛУШКА ДЛЯ КЛИКОВ ПО СЛОТАМ РЕЦЕПТА ---
        private void SendClick(object data)
        {
            // Ничего не делаем, рецепт только для чтения
        }

        // === ПАРСЕР С ПОДДЕРЖКОЙ ВАНИЛЬНЫХ ССЫЛОК ===
        private RichTextComponentBase[] ParseLexiconText(string rawText, CairoFont baseFont)
        {
            if (string.IsNullOrEmpty(rawText)) return new RichTextComponentBase[0];

            var components = new List<RichTextComponentBase>();

            string[] parts = rawText.Split(new string[] { "<link=" }, System.StringSplitOptions.None);

            if (!string.IsNullOrEmpty(parts[0]))
            {
                components.Add(new RichTextComponent(capi, parts[0], baseFont));
            }

            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                int closeBracket = part.IndexOf('>');
                int closeTag = part.IndexOf("</link>");

                if (closeBracket != -1 && closeTag != -1 && closeBracket < closeTag)
                {
                    string targetId = part.Substring(0, closeBracket).Trim();
                    string linkText = part.Substring(closeBracket + 1, closeTag - closeBracket - 1);
                    string remainingText = part.Substring(closeTag + 7);

                    CairoFont linkFont = baseFont.Clone().WithColor(new double[] { 0.1, 0.5, 0.1, 1 }).WithWeight(Cairo.FontWeight.Bold);

                    var linkComp = new LinkTextComponent(capi, linkText, linkFont, (comp) => {

                        // === 1. РЕЖИМ ПРЯМОГО ПОИСКА ПО ТЕКСТУ (Ваниль) ===
                        if (targetId.StartsWith("search:"))
                        {
                            string searchQuery = targetId.Substring(7);

                            var handbookSys = capi.ModLoader.Systems.FirstOrDefault(s => s.GetType().Name == "ModSystemSurvivalHandbook");
                            if (handbookSys != null)
                            {
                                var dialogField = handbookSys.GetType().GetField("dialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (dialogField != null)
                                {
                                    var dialog = dialogField.GetValue(handbookSys);
                                    if (dialog != null)
                                    {
                                        dynamic dynDialog = dialog;
                                        dynDialog.TryOpen();
                                        dynDialog.Search(searchQuery);
                                        return;
                                    }
                                }
                            }
                        }
                        // === 2. РЕЖИМ КАТЕГОРИИ ИЗ LEXICON ===
                        // (Должен стоять ДО проверки на двоеточие!)
                        else if (targetId.StartsWith("category:"))
                        {
                            string catId = targetId.Substring(9); // Отрезаем префикс "category:"
                            OpenSpecificCategory(catId);
                        }
                        // === 3. РЕЖИМ КОНКРЕТНОГО ВАНИЛЬНОГО ПРЕДМЕТА/БЛОКА ===
                        else if (targetId.Contains(":"))
                        {
                            string cleanId = targetId.Replace("handbook://item-", "")
                                                     .Replace("handbook://block-", "")
                                                     .Replace("handbooksearch://", "");

                            AssetLocation loc = new AssetLocation(cleanId);

                            if (loc.Domain == "botaniastory")
                            {
                                string chapterId = BookDataManager.GetChapterForBlock(cleanId);
                                if (chapterId != null)
                                {
                                    OpenSpecificChapter(chapterId);
                                }
                                return; // Обязательно прерываем, чтобы не открылся ванильный UI!
                            }

                            var handbookSys = capi.ModLoader.Systems.FirstOrDefault(s => s.GetType().Name == "ModSystemSurvivalHandbook");
                            if (handbookSys != null)
                            {
                                var dialogField = handbookSys.GetType().GetField("dialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (dialogField != null)
                                {
                                    var dialog = dialogField.GetValue(handbookSys);
                                    if (dialog != null)
                                    {
                                        dynamic dynDialog = dialog;
                                        dynDialog.TryOpen();

                                        bool pageOpened = false;

                                        if (capi.World.GetItem(loc) != null) pageOpened = dynDialog.OpenDetailPageFor($"item-{loc.Path}");
                                        else if (capi.World.GetBlock(loc) != null) pageOpened = dynDialog.OpenDetailPageFor($"block-{loc.Path}");

                                        if (!pageOpened)
                                        {
                                            string searchWord = loc.Path;
                                            Item item = capi.World.GetItem(loc);
                                            if (item != null) searchWord = new ItemStack(item).GetName();
                                            else
                                            {
                                                Block block = capi.World.GetBlock(loc);
                                                if (block != null) searchWord = new ItemStack(block).GetName();
                                            }
                                            dynDialog.Search(searchWord);
                                        }
                                        return;
                                    }
                                }
                            }
                        }
                        // === 4. ВНУТРЕННИЕ ССЫЛКИ НА ГЛАВЫ LEXICON ===
                        else
                        {
                            OpenSpecificChapter(targetId);
                        }
                    });

                    linkComp.Href = targetId;

                    components.Add(linkComp);

                    if (!string.IsNullOrEmpty(remainingText))
                    {
                        components.Add(new RichTextComponent(capi, remainingText, baseFont));
                    }
                }
                else
                {
                    components.Add(new RichTextComponent(capi, "<link=" + part, baseFont));
                }
            }

            return components.ToArray();
        }
        // ==========================================

        // === НОВЫЙ МЕТОД ДЛЯ БЫСТРОГО ПЕРЕХОДА К КАТЕГОРИИ ===
        // === НОВЫЙ МЕТОД ДЛЯ БЫСТРОГО ПЕРЕХОДА К КАТЕГОРИИ ===
        public void OpenSpecificCategory(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId)) return;

            // Ищем категорию по её ID
            var targetCategory = categories.FirstOrDefault(c => c.Id == categoryId);

            if (targetCategory != null)
            {
                // Запоминаем текущую страницу для кнопки "Назад", если мы переходим из режима чтения
                if (currentView == BookView.Reading && currentChapter != null)
                {
                    history.Push(new HistoryState
                    {
                        Category = currentCategory,
                        Chapter = currentChapter,
                        Spread = currentSpread
                    });
                }

                // Открываем список глав этой категории
                currentCategory = targetCategory;
                currentView = BookView.CategoryList;
                isSearchOpen = false;

                // Звук перелистывания (если ты не добавил его в сам клик по ссылке)
                PlayLexiconSound("lexiconpage"); 

                // === ИСПРАВЛЕНИЕ: Вот эта строчка всё починит! ===
                RecomposeDialog();
            }
        }
        // ====================================================

        // === НОВЫЙ МЕТОД ДЛЯ БЫСТРОГО ПЕРЕХОДА ===
        public void OpenSpecificChapter(string chapterId)
        {
            if (string.IsNullOrEmpty(chapterId)) return;

            var targetChapter = categories
                .SelectMany(c => c.Chapters)
                .FirstOrDefault(ch => ch.Id == chapterId);

            if (targetChapter != null)
            {
                // 1. Запоминаем текущую страницу перед прыжком по ссылке
                if (currentView == BookView.Reading && currentChapter != null)
                {
                    history.Push(new HistoryState
                    {
                        Category = currentCategory,
                        Chapter = currentChapter,
                        Spread = currentSpread
                    });
                }

                // 2. Находим категорию новой главы (чтобы при выходе из нее попасть в правильный список)
                var targetCategory = categories.FirstOrDefault(c => c.Chapters.Contains(targetChapter));
                if (targetCategory != null) currentCategory = targetCategory;

                // 3. Открываем новую главу напрямую
                currentChapter = targetChapter;
                currentSpread = 0;
                currentView = BookView.Reading;
                isSearchOpen = false;

                PlayLexiconSound("lexiconpage");

                RecomposeDialog();
            }
        }
        // ==========================================

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

        public void SaveConfig() { capi.StoreModConfig(config, "lexicon_client.json"); }
        public void UpdateScale(float newScale) { bookScale = newScale; config.BookScale = newScale; SaveConfig(); RecomposeDialog(); }
        private void RecomposeDialog() { SingleComposer?.Dispose(); SetupDialog(); }
        public void RecomposeFromDebugger() { RecomposeDialog(); }
        public override void OnRenderGUI(float deltaTime)
        {
            // 1. Вытаскиваем наш фон из собранного интерфейса
            var bg = SingleComposer?.GetElement("bookBackground") as GuiElementStretchedImage;

            // 2. ПРИНУДИТЕЛЬНО РИСУЕМ ФОН САМЫМ ПЕРВЫМ (до любых расчетов глубины)
            if (bg != null)
            {
                bg.RenderMyBackground(deltaTime);
            }

            // 3. Запускаем стандартную отрисовку: теперь слоты, текст и кнопки 
            // лягут ровненько поверх уже нарисованной книги!
            base.OnRenderGUI(deltaTime);
        }
        public override void OnGuiOpened()
        {
            base.OnGuiOpened();

            // === ЗВУК ОТКРЫТИЯ КНИГИ ===
            PlayLexiconSound("lexiconopen");

            // 1. Сохраняем наши закладки перед обновлением (чтобы они не пропали)
            var bookmarkedIds = categories
                .SelectMany(c => c.Chapters)
                .Where(ch => ch.IsBookmarked)
                .Select(ch => ch.Id)
                .ToList();

            // 2. ЗАНОВО считываем все переводы из файлов (Вот она, магия обновления!)
            categories = BookDataManager.GetTemplateCategories();

            // 3. Восстанавливаем закладки на новых "свежих" страницах
            foreach (var chapter in categories.SelectMany(c => c.Chapters))
            {
                if (bookmarkedIds.Contains(chapter.Id))
                {
                    chapter.IsBookmarked = true;
                }
            }

            // 4. Восстанавливаем открытую категорию, если игрок был в ней
            if (currentCategory != null)
            {
                currentCategory = categories.FirstOrDefault(c => c.Id == currentCategory.Id);
            }

            // 5. Восстанавливаем открытую главу
            if (currentChapter != null && currentCategory != null)
            {
                currentChapter = currentCategory.Chapters.FirstOrDefault(c => c.Id == currentChapter.Id);
            }

            // Если что-то пошло не так (например, ты удалил главу из кода), кидаем на главную
            if (currentCategory == null && currentChapter == null)
            {
                currentView = BookView.Home;
            }

            // 6. Перерисовываем весь интерфейс с новым текстом
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
                        ActiveBookSlot.MarkDirty(); // Говорим игре обновить визуал в руке
                    }
                }
            }
        }
    }
    public class GuiElementStretchedImage : GuiElement
    {
        private AssetLocation textureLoc;
        private LoadedTexture tex;

        // НОВЫЙ ПЕРЕКЛЮЧАТЕЛЬ: По умолчанию это обычная картинка
        public bool IsBackground = false;

        public GuiElementStretchedImage(ICoreClientAPI capi, ElementBounds bounds, AssetLocation textureLoc) : base(capi, bounds)
        {
            this.textureLoc = textureLoc;
            this.tex = new LoadedTexture(capi);
        }

        public override void ComposeElements(Cairo.Context ctx, Cairo.ImageSurface surface)
        {
            Bounds.CalcWorldBounds();
            api.Render.GetOrLoadTexture(textureLoc, ref tex);
        }

        public override bool IsPositionInside(int mouseX, int mouseY)
        {
            return false; // По-прежнему пропускаем клики сквозь картинки
        }

        // 1. СТАНДАРТНЫЙ МЕТОД: Рисует приветствие и картинки в главах поверх книги
        public override void RenderInteractiveElements(float deltaTime)
        {
            // Рисуем ТОЛЬКО если это НЕ фон
            if (!IsBackground && tex != null && tex.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    tex.TextureId, Bounds.renderX, Bounds.renderY, Bounds.OuterWidth, Bounds.OuterHeight, 50f
                );
            }
        }

        // 2. НАШ СПЕЦИАЛЬНЫЙ МЕТОД: Рисует только саму подложку книги
        public void RenderMyBackground(float deltaTime)
        {
            // Рисуем ТОЛЬКО если мы сказали, что это фон
            if (IsBackground && tex != null && tex.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    tex.TextureId, Bounds.renderX, Bounds.renderY, Bounds.OuterWidth, Bounds.OuterHeight, 10f
                );
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }

    public class GuiElementManaBar : GuiElement
    {
        private int manaCost;
        private int maxMana;

        private AssetLocation bgTextureLoc;
        private AssetLocation fillTextureLoc;
        private AssetLocation frameTextureLoc;

        private LoadedTexture bgTex;
        private LoadedTexture fillTex;
        private LoadedTexture frameTex;

        // Добавляем специальную границу для "маски" обрезки
        private ElementBounds clipBounds;

        public GuiElementManaBar(ICoreClientAPI capi, ElementBounds bounds, int manaCost, int maxMana) : base(capi, bounds)
        {
            this.manaCost = manaCost;
            this.maxMana = maxMana;

            this.bgTextureLoc = new AssetLocation("botaniastory:textures/gui/manabar_bg.png");
            this.fillTextureLoc = new AssetLocation("botaniastory:textures/gui/manabar_fill.png");
            this.frameTextureLoc = new AssetLocation("botaniastory:textures/gui/manabar_frame.png");

            this.bgTex = new LoadedTexture(capi);
            this.fillTex = new LoadedTexture(capi);
            this.frameTex = new LoadedTexture(capi);
        }

        public override void ComposeElements(Cairo.Context ctx, Cairo.ImageSurface surface)
        {
            // 1. Сначала просчитываем оригинальные границы
            Bounds.CalcWorldBounds();

            api.Render.GetOrLoadTexture(bgTextureLoc, ref bgTex);
            api.Render.GetOrLoadTexture(fillTextureLoc, ref fillTex);
            api.Render.GetOrLoadTexture(frameTextureLoc, ref frameTex);

            // 2. Рассчитываем процент заполнения
            float fillPercentage = (float)manaCost / maxMana;
            if (fillPercentage > 1f) fillPercentage = 1f;

            // ИСПРАВЛЕНО: Создаем новые границы правильно, через фиксированные значения (fixed)
            clipBounds = ElementBounds.Fixed(
                Bounds.fixedX,
                Bounds.fixedY,
                Bounds.fixedWidth * fillPercentage, // Умножаем только ширину
                Bounds.fixedHeight
            );

            // 3. Обязательно передаем родительские границы (книги), чтобы маска была в правильном месте на экране
            clipBounds.ParentBounds = Bounds.ParentBounds;

            // 4. Заставляем движок пересчитать renderX/renderY для нашей маски
            clipBounds.CalcWorldBounds();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            // 1. ЗАДНИЙ ФОН (самый нижний слой, Z: 50)
            if (bgTex != null && bgTex.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    bgTex.TextureId,
                    Bounds.renderX, Bounds.renderY, Bounds.OuterWidth, Bounds.OuterHeight, 50f
                );
            }

            // 2. ПОЛОСКА ЗАЛИВКИ (Средний слой, Z: 51)
            if (fillTex != null && fillTex.TextureId != 0)
            {
                // Включаем маску обрезки! Всё, что за пределами clipBounds, станет невидимым.
                api.Render.PushScissor(clipBounds);

                // ВАЖНО: Рисуем текстуру в ПОЛНУЮ ширину (Bounds.OuterWidth).
                // Благодаря маске текстура не сожмется, а просто обрежется справа.
                api.Render.Render2DTexturePremultipliedAlpha(
                    fillTex.TextureId,
                    Bounds.renderX, Bounds.renderY, Bounds.OuterWidth, Bounds.OuterHeight, 51f
                );

                // Выключаем маску, чтобы она не обрезала остальной интерфейс
                api.Render.PopScissor();
            }

            // 3. ФРЕЙМ / РАМКА (Самый верхний слой, Z: 52)
            if (frameTex != null && frameTex.TextureId != 0)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    frameTex.TextureId,
                    Bounds.renderX, Bounds.renderY, Bounds.OuterWidth, Bounds.OuterHeight, 52f
                );
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }

}