using botaniastory;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace botaniastory
{
    public partial class GuiDialogLexicon
    {
        private ItemStack[] GetItemStacks(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;

            var stacks = new List<ItemStack>();
            AssetLocation loc = new AssetLocation(code);

            if (code.Contains("*"))
            {
                foreach (var item in capi.World.Items)
                {
                    if (item.Code != null && WildcardUtil.Match(loc, item.Code))
                        stacks.Add(new ItemStack(item));
                }
                foreach (var block in capi.World.Blocks)
                {
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
            var bookBg = new GuiElementStretchedImage(capi, bounds["Книга_Фон"], new AssetLocation("botaniastory:gui/book.png"));
            bookBg.IsBackground = true;

            var compo = capi.Gui.CreateCompo("lexiconDialog", dialogBounds)
                .AddInteractiveElement(bookBg, "bookBackground")
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
                double yPos = startY + (i * 85 * tabScale);
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

                    var welcomeTextCfg = ui["Приветствие_Текст"];
                    double welcomeTextScale = welcomeTextCfg[4];

                    CairoFont welcomeFont = CairoFont.WhiteSmallText()
                        .WithColor(inkColor)
                        .WithFontSize((float)(GuiStyle.NormalFontSize * bookScale * welcomeTextScale));

                    string welcomeText = Lang.Get(HomePageData.WelcomeTextKey);
                    compo.AddDynamicText(welcomeText, welcomeFont, bounds["Приветствие_Текст"], "homeRightText");

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

                    if (currentChapter.Recipes != null)
                    {
                        for (int i = 0; i < currentChapter.Recipes.Count; i++)
                        {
                            var recipe = currentChapter.Recipes[i];

                            if (recipe.Spread == currentSpread)
                            {
                                if (recipe.RecipeType == "Apothecary" && recipe.ApothecaryIngredients != null)
                                {
                                    string targetUiKey = string.IsNullOrEmpty(recipe.UiKey) ? "Аптекарь_Область" : recipe.UiKey;
                                    string bgUiKey = targetUiKey.Replace("Область", "Фон");

                                    if (!ui.ContainsKey(targetUiKey) || !ui.ContainsKey(bgUiKey)) continue;

                                    var apoCfg = ui[targetUiKey];
                                    double rScale = apoCfg[4] * bookScale;
                                    ElementBounds apoBounds = ElementBounds.Fixed(apoCfg[0] * bookScale, apoCfg[1] * bookScale, 250 * rScale, 250 * rScale);

                                    var bgCfg = ui[bgUiKey];
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

                                    compo.AddInteractiveElement(apothecaryElement, $"apothecaryDisplay_{i}");
                                }
                                else if (recipe.RecipeType == "Alfheim" && recipe.AlfheimInputs != null)
                                {
                                    string targetUiKey = string.IsNullOrEmpty(recipe.UiKey) ? "Альфхейм_Область" : recipe.UiKey;
                                    string bgUiKey = targetUiKey.Replace("Область", "Фон");

                                    if (!ui.ContainsKey(targetUiKey) || !ui.ContainsKey(bgUiKey)) continue;

                                    var alfheimCfg = ui[targetUiKey];
                                    double rScale = alfheimCfg[4] * bookScale;
                                    ElementBounds alfheimBounds = ElementBounds.Fixed(alfheimCfg[0] * bookScale, alfheimCfg[1] * bookScale, 250 * rScale, 250 * rScale);

                                    var bgCfg = ui[bgUiKey];
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
                                else if (recipe.RecipeType == "Grid" && recipe.Grid != null)
                                {
                                    string targetUiKey = string.IsNullOrEmpty(recipe.UiKey) ? "Сетка_Правая" : recipe.UiKey;

                                    if (!bounds.ContainsKey(targetUiKey)) continue;

                                    var gridCfg = ui[targetUiKey];
                                    double rScale = gridCfg[4] * bookScale;
                                    ElementBounds gridBounds = ElementBounds.Fixed(gridCfg[0] * bookScale, gridCfg[1] * bookScale, gridCfg[2] * rScale, gridCfg[3] * rScale);

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
                                else if (recipe.RecipeType == "ManaPool" && recipe.PoolInput != null)
                                {
                                    string targetUiKey = string.IsNullOrEmpty(recipe.UiKey) ? "Бассейн_Область" : recipe.UiKey;
                                    string bgUiKey = targetUiKey.Replace("Область", "Фон");

                                    if (!bounds.ContainsKey(targetUiKey) || !bounds.ContainsKey(bgUiKey)) continue;

                                    var poolCfg = ui[targetUiKey];
                                    double rScale = poolCfg[4] * bookScale;
                                    ElementBounds poolBounds = ElementBounds.Fixed(poolCfg[0] * bookScale, poolCfg[1] * bookScale, poolCfg[2] * rScale, poolCfg[3] * rScale);

                                    var bgCfg = ui[bgUiKey];
                                    double bgScale = bgCfg[4] * bookScale;
                                    ElementBounds bgBounds = ElementBounds.Fixed(bgCfg[0] * bookScale, bgCfg[1] * bookScale, bgCfg[2] * bgScale, bgCfg[3] * bgScale);

                                    ItemStack[] inputStacks = new ItemStack[recipe.PoolInput.Length];
                                    for (int j = 0; j < recipe.PoolInput.Length; j++)
                                    {
                                        var stacks = GetItemStacks(recipe.PoolInput[j]);
                                        if (stacks != null && stacks.Length > 0) inputStacks[j] = stacks[0];
                                    }
                                    if (recipe.PoolInput.Length == 1 && recipe.PoolInput[0].Contains("*")) inputStacks = GetItemStacks(recipe.PoolInput[0]);

                                    ItemStack[] catalystStacks = null;
                                    if (recipe.PoolCatalyst != null)
                                    {
                                        catalystStacks = new ItemStack[recipe.PoolCatalyst.Length];
                                        for (int j = 0; j < recipe.PoolCatalyst.Length; j++)
                                        {
                                            var cStacks = GetItemStacks(recipe.PoolCatalyst[j]);
                                            if (cStacks != null && cStacks.Length > 0) catalystStacks[j] = cStacks[0];
                                        }
                                        if (recipe.PoolCatalyst.Length == 1 && recipe.PoolCatalyst[0].Contains("*")) catalystStacks = GetItemStacks(recipe.PoolCatalyst[0]);
                                    }

                                    ItemStack[] poolStacks = GetItemStacks(recipe.PoolBlock);
                                    ItemStack[] outputs = GetItemStacks(recipe.Output);

                                    var poolElement = new GuiElementManaPoolRecipe(capi, poolBounds, bgBounds, inputStacks, poolStacks, outputs, catalystStacks, rScale);
                                    poolElement.OnSlotClick = OnRecipeItemClicked;

                                    compo.AddInteractiveElement(poolElement, $"manaPoolDisplay_{i}");
                                }
                            }
                        }
                    }

                    if (currentChapter.Images != null)
                    {
                        for (int i = 0; i < currentChapter.Images.Count; i++)
                        {
                            var imgData = currentChapter.Images[i];

                            if (currentSpread == imgData.Spread && bounds.ContainsKey(imgData.UiKey))
                            {
                                var imgElement = new GuiElementStretchedImage(capi, bounds[imgData.UiKey], new AssetLocation(imgData.Path));
                                compo.AddInteractiveElement(imgElement, $"chapterImage_{i}");
                            }
                        }
                    }

                    if (currentChapter.ManaBars != null)
                    {
                        for (int i = 0; i < currentChapter.ManaBars.Count; i++)
                        {
                            var manaData = currentChapter.ManaBars[i];

                            if (currentSpread == manaData.Spread && bounds.ContainsKey(manaData.UiKey))
                            {
                                ElementBounds barBounds = bounds[manaData.UiKey];
                                ElementBounds textBounds = ElementBounds.Fixed(barBounds.fixedX, barBounds.fixedY - (20 * bookScale), barBounds.fixedWidth, 20);
                                CairoFont textFont = CairoFont.WhiteSmallText().WithColor(new double[] { 0.4, 0.4, 0.4, 1 }).WithOrientation(EnumTextOrientation.Center);

                                string localizedManaText = Lang.Get("botaniastory:mana-cost");
                                compo.AddStaticText(localizedManaText, textFont, textBounds, $"manaText_{i}");

                                var manaElement = new GuiElementManaBar(capi, barBounds, manaData.ManaCost, 100000);
                                compo.AddInteractiveElement(manaElement, $"manaBar_{i}");
                            }
                        }
                    }

                    int leftIndex = currentSpread * 2;
                    int rightIndex = leftIndex + 1;

                    string leftStr = leftIndex < currentChapter.Pages.Count ? currentChapter.Pages[leftIndex] : "";
                    string rightStr = rightIndex < currentChapter.Pages.Count ? currentChapter.Pages[rightIndex] : "";

                    compo.AddRichtext(ParseLexiconText(leftStr, leftFont), bounds["Левая_Страница"], "leftPageText");
                    compo.AddRichtext(ParseLexiconText(rightStr, rightFont), bounds["Правая_Страница"], "rightPageText");

                    string bookmarkIcon = currentChapter.IsBookmarked ? "botaniastory:gui/bookmark_on.png" : "botaniastory:gui/bookmark_off.png";
                    var bookmarkBtn = new GuiElementClickableImage(capi, bounds["Кнопка_Закладки"], new AssetLocation(bookmarkIcon), () => AddBookmark());
                    compo.AddInteractiveElement(bookmarkBtn, "btnBookmark");

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

                            var btn = SingleComposer.GetButton("btnVisualize");
                            if (btn != null)
                            {
                                btn.Text = hologramSystem.isActive ? "Отключить" : "Визуализировать";
                            }

                            return true;
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

        private void UpdatePageContent()
        {
            if (currentView != BookView.Reading || currentChapter == null) return;

            var nextBtn = SingleComposer.GetElement("nextButton") as GuiElementClickableImage;
            if (nextBtn != null)
            {
                nextBtn.Enabled = (currentSpread + 1) * 2 < currentChapter.Pages.Count;
            }
        }

        private void SendClick(object data)
        {
            // Ничего не делаем, рецепт только для чтения
        }

        private void OnRecipeItemClicked(ItemStack clickedStack)
        {
            if (clickedStack == null) return;

            string itemCode = clickedStack.Collectible.Code.ToString();
            string domain = clickedStack.Collectible.Code.Domain;

            if (domain == "botaniastory")
            {
                string chapterId = BookDataManager.GetChapterForBlock(itemCode);

                if (chapterId != null)
                {
                    if (currentChapter != null && currentChapter.Id == chapterId) return;
                    OpenSpecificChapter(chapterId);
                }
            }
            else
            {
                OpenVanillaHandbook(clickedStack);
            }
        }

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
    }
}