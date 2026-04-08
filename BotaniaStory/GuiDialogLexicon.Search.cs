using botaniastory;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;

namespace botaniastory
{
    public partial class GuiDialogLexicon
    {
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
    }
}