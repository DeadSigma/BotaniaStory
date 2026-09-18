using BotaniaStory.lexicon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace botaniastory
{
    public partial class GuiDialogLexicon
    {
        private class ChapterSearchEntry
        {
            public BookChapter Chapter;
            public string Title;
            public List<string> Links = new List<string>();
            public string Text;
        }

        private List<ChapterSearchEntry> searchIndex = new List<ChapterSearchEntry>();

        private void OnSearchTextChanged(string text)
        {
            searchQuery = text;
            UpdateSearchResults();
        }

        private void BuildSearchIndex()
        {
            searchIndex.Clear();

            foreach (var chapter in categories.SelectMany(c => c.Chapters))
            {
                var entry = new ChapterSearchEntry
                {
                    Chapter = chapter,
                    Title = NormalizeSearchText(chapter.Title)
                };

                var fullText = new StringBuilder();

                foreach (string page in chapter.Pages)
                {
                    if (string.IsNullOrEmpty(page)) continue;

                    foreach (Match match in Regex.Matches(
                        page,
                        @"<link=([^>]+)>(.*?)</link>",
                        RegexOptions.Singleline))
                    {
                        string target = match.Groups[1].Value.Trim();

                        if (target == "nothing") continue;

                        string linkText = StripSearchMarkup(match.Groups[2].Value);
                        linkText = NormalizeSearchText(linkText);

                        if (!string.IsNullOrEmpty(linkText))
                        {
                            entry.Links.Add(linkText);
                        }
                    }

                    fullText.Append(' ');
                    fullText.Append(StripSearchMarkup(page));
                }

                entry.Text = NormalizeSearchText(fullText.ToString());

                searchIndex.Add(entry);
            }
        }

        private List<BookChapter> SearchChapters(string query)
        {
            string normalizedQuery = NormalizeSearchText(query);

            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return new List<BookChapter>();
            }

            return searchIndex
                .Select(entry => new
                {
                    entry.Chapter,
                    Score = GetSearchScore(entry, normalizedQuery)
                })
                .Where(x => x.Score > 0)
                .GroupBy(x => x.Chapter.Id)
                .Select(group => group
                    .OrderByDescending(x => x.Score)
                    .First())
                .OrderByDescending(x => x.Score)
                .Select(x => x.Chapter)
                .ToList();
        }

        private int GetSearchScore(ChapterSearchEntry entry, string query)
        {
            if (entry.Title == query)
            {
                return 1000;
            }

            if (entry.Title.Contains(query))
            {
                return 900;
            }

            if (WordsMatch(query, entry.Title))
            {
                return 800;
            }

            foreach (string link in entry.Links)
            {
                if (link == query)
                {
                    return 750;
                }

                if (link.Contains(query))
                {
                    return 700;
                }

                if (WordsMatch(query, link))
                {
                    return 650;
                }
            }

            if (entry.Text.Contains(query))
            {
                return 400;
            }

            if (WordsMatch(query, entry.Text))
            {
                return 250;
            }

            return 0;
        }

        private bool WordsMatch(string query, string text)
        {
            string[] queryWords = SplitSearchWords(query);
            string[] textWords = SplitSearchWords(text);

            if (queryWords.Length == 0 || textWords.Length == 0)
            {
                return false;
            }

            foreach (string queryWord in queryWords)
            {
                bool found = false;

                foreach (string textWord in textWords)
                {
                    if (SearchWordsMatch(queryWord, textWord))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private bool SearchWordsMatch(string queryWord, string textWord)
        {
            if (queryWord == textWord)
            {
                return true;
            }

            if (queryWord.Length >= 3 && textWord.Contains(queryWord))
            {
                return true;
            }

            int minLength = Math.Min(queryWord.Length, textWord.Length);
            int maxLength = Math.Max(queryWord.Length, textWord.Length);

            if (minLength < 4)
            {
                return false;
            }

            int commonPrefix = 0;

            while (commonPrefix < minLength &&
                   queryWord[commonPrefix] == textWord[commonPrefix])
            {
                commonPrefix++;
            }

            int requiredPrefix = Math.Max(4, minLength - 3);

            if (commonPrefix >= requiredPrefix && maxLength - minLength <= 4)
            {
                return true;
            }

            int maxDistance;

            if (maxLength <= 5)
            {
                maxDistance = 1;
            }
            else if (maxLength <= 9)
            {
                maxDistance = 2;
            }
            else
            {
                maxDistance = 3;
            }

            return GetLevenshteinDistance(queryWord, textWord, maxDistance) <= maxDistance;
        }

        private int GetLevenshteinDistance(string a, string b, int maxDistance)
        {
            if (Math.Abs(a.Length - b.Length) > maxDistance)
            {
                return maxDistance + 1;
            }

            int[] previous = new int[b.Length + 1];
            int[] current = new int[b.Length + 1];

            for (int j = 0; j <= b.Length; j++)
            {
                previous[j] = j;
            }

            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                int rowMin = current[0];

                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;

                    current[j] = Math.Min(
                        Math.Min(
                            current[j - 1] + 1,
                            previous[j] + 1),
                        previous[j - 1] + cost
                    );

                    if (current[j] < rowMin)
                    {
                        rowMin = current[j];
                    }
                }

                if (rowMin > maxDistance)
                {
                    return maxDistance + 1;
                }

                var temp = previous;
                previous = current;
                current = temp;
            }

            return previous[b.Length];
        }

        private string[] SplitSearchWords(string text)
        {
            return text.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries
            );
        }

        private string StripSearchMarkup(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            return Regex.Replace(text, @"<[^>]+>", "");
        }

        private string NormalizeSearchText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            text = text
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var result = new StringBuilder(text.Length);

            foreach (char c in text)
            {
                UnicodeCategory category =
                    CharUnicodeInfo.GetUnicodeCategory(c);

                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(c))
                {
                    result.Append(c);
                }
                else
                {
                    result.Append(' ');
                }
            }

            return Regex.Replace(
                result.ToString(),
                @"\s+",
                " "
            ).Trim();
        }

        private void UpdateSearchResults()
        {
            if (!isSearchOpen) return;

            var results = SearchChapters(searchQuery);

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

                if (i < results.Count &&
                    i < maxTotal &&
                    !string.IsNullOrEmpty(searchQuery))
                {
                    var ch = results[i];

                    bool rightPage = i >= maxLeft;
                    int pageIndex = rightPage ? i - maxLeft : i;

                    double baseX = rightPage
                        ? listCoords[2]
                        : listCoords[0];

                    double baseY = rightPage
                        ? listCoords[3]
                        : listCoords[1];

                    double yOffset =
                        baseY + pageIndex * listStep[1];

                    row.Bounds.fixedWidth =
                        300 * bookScale * listScale;

                    row.Bounds.fixedHeight =
                        25 * bookScale * listScale;

                    row.Bounds.fixedX =
                        baseX * bookScale;

                    row.Bounds.fixedY =
                        yOffset * bookScale;

                    row.Bounds.CalcWorldBounds();

                    row.IconOffsetX =
                        ui["Список_Глав_Иконки"][0] * bookScale;

                    row.IconOffsetY =
                        ui["Список_Глав_Иконки"][1] * bookScale;

                    row.IconScale =
                        (float)ui["Список_Глав_Иконки"][4];

                    var starCfg = ui["Звезда_Избранного"];

                    row.StarOffsetX =
                        starCfg[0] * bookScale;

                    row.StarOffsetY =
                        starCfg[1] * bookScale;

                    row.StarSize =
                        starCfg[2] * bookScale * starCfg[4];

                    BookChapter capturedCh = ch;

                    row.UpdateData(
                        GetItemStacks(ch.TabItemCode) ??
                        new ItemStack[0],
                        () => OpenChapter(capturedCh),
                        ch.IsBookmarked
                    );

                    text.Bounds.fixedWidth =
                        260 * bookScale * listScale;

                    text.Bounds.fixedHeight =
                        25 * bookScale * listScale;

                    text.Bounds.fixedX =
                        (baseX + listStep[0]) * bookScale;

                    text.Bounds.fixedY =
                        (yOffset + 4) * bookScale;

                    text.Bounds.CalcWorldBounds();

                    text.SetNewText(ch.Title);
                }
                else
                {
                    text.SetNewText(" ");

                    row.UpdateData(
                        new ItemStack[0],
                        null,
                        false
                    );

                    row.Bounds.fixedX = -9999;
                    row.Bounds.fixedY = -9999;
                    row.Bounds.CalcWorldBounds();
                }
            }
        }
    }
}