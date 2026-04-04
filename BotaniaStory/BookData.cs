using System.Collections.Generic;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace botaniastory
{
    public enum BookView { Home, CategoryList, Reading }

    public static class HomePageData
    {
        // Ключ перевода для приветственного текста
        public static string WelcomeTextKey = "botaniastory:welcome-text";

        // Путь к картинке
        public static string ImagePath = "botaniastory:textures/gui/welcome_art.png";
    }

    public class BookPageImage
    {
        public string Path { get; set; }      // Путь к самой картинке (png)
        public int Spread { get; set; }       // На каком развороте страниц она находится
        public string UiKey { get; set; }     // Ключ рамки из нашего дебаггера (LexiconUIData)
    }

    public class BookRecipe
    {
        public string RecipeType { get; set; } // Тип крафта: "Grid", "Apothecary" или "Alfheim"
        public int Spread { get; set; }        // На каком развороте страниц он находится

        public string Output { get; set; }     // Результат крафта (общий для всех)

        // Данные для ванильной сетки
        public string[] Grid { get; set; }

        // Данные для Аптекаря
        public string[] ApothecaryIngredients { get; set; }
        public string ApothecaryCenter { get; set; }

        // Данные для Альфхейма
        public string[] AlfheimInputs { get; set; }
    }
    public class BookManaBar
    {
        public int Spread { get; set; }       // На каком развороте она появится
        public int ManaCost { get; set; }     // Количество маны
        public string UiKey { get; set; }     // Ключ координат из дебаггера (чтобы двигать её)
    }
    public class BookChapter
    {

        public List<BookManaBar> ManaBars { get; set; } = new List<BookManaBar>();
        public string Title { get; set; }
        public List<string> Pages { get; set; }
        public bool IsBookmarked { get; set; } = false;
        public string TabItemCode { get; set; }
        public string VisualizeStructure { get; set; }
        public string Id { get; set; }
        public List<BookPageImage> Images { get; set; } = new List<BookPageImage>();
        public List<BookRecipe> Recipes { get; set; } = new List<BookRecipe>();
    }

    public class BookCategory
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string IconPath { get; set; }
        public List<BookChapter> Chapters { get; set; }
    }


    public static class BookDataManager
    {
        // Наше "Оглавление". Ключ - ID категории, Значение - массив ID глав
        private static readonly Dictionary<string, string[]> BookStructure = new Dictionary<string, string[]>
        {
            { "basics_and_mechanics", new[] { "basicsintroduction", "botanialexicon", "apothecary", "mysticalflower", "wandoftheforest", 
                "runicaltar", "terrasteel", "puredaisy", "flowerpouch", "blacklotus" } },

            { "mana_management", new[] { "manamanagmentintroduction", "manapool", "manaspreader", "sparks", "manatablet", "redmanaspreader"
            , "manamirror", "manalenses", "craftmanspark", "manadetector", "manastar", "manaonrails"  
            , "manavoid", "manamachine", "manaprism", "manadistributor", "sparkaugments", "elvenmanalenses"
            , "elvenmanaspreaders", "manafluxfield"} },

            { "generating_flora", new[] {"generatingfloraintroduction", "hydroangeas", "endoflame", "generatingfloraintroduction", "shulkmenot", "gourmaryllis"
            , "munchdew", "dandelifeon", "kekimurus", "rafflowsia", "entropinnyum", "spectrolus", "arcanerose"
            , "thermalily", "entropinnyum"} },

            { "functional_flora", new[] { "ch1", "ch2", "ch3", "ch4", "ch5" } },

            { "natural_apparatus", new[] { "ch1", "ch2", "ch3", "ch4", "ch5" } },

            { "mystical_items", new[] { "ch1", "ch2", "ch3", "ch4", "ch5" } },

            { "trinkets_and_accessories", new[] { "ch1", "ch2", "ch3", "ch4", "ch5" } },

            { "rusted_world_artifacts", new[] { "ch1", "ch2", "ch3", "ch4", "ch5" } },

            { "elfmania", new[] { "ch1", "ch2", "ch3", "ch4", "ch5" } },

            { "misc", new[] { "ch1", "ch2", "ch3", "ch4", "ch5" } },

            { "trials", new[] { "ch1", "ch2", "ch3", "ch4", "ch5" } }
        };


        /// <summary>
        /// //Система shif + ПКМ и переход к информации
        /// </summary>
        private static readonly Dictionary<string, string> ExceptionsMap = new Dictionary<string, string>
    {
        { "botaniastory:mysticalpetal-*", "mysticalflower" }
    };

        // === НОВЫЙ КОД: Метод для поиска главы по блоку ===
        public static string GetChapterForBlock(string blockCode)
        {
            if (string.IsNullOrEmpty(blockCode)) return null;

            // Разделяем код на домен (мод) и путь (название)
            // Например: "botaniastory:mysticalflower-orange" -> Domain: "botaniastory", Path: "mysticalflower-orange"
            AssetLocation loc = new AssetLocation(blockCode);
            string blockDomain = loc.Domain;
            string blockPath = loc.Path;

            // 1. Сначала проверяем исключения
            foreach (var kvp in ExceptionsMap)
            {
                if (WildcardUtil.Match(new AssetLocation(kvp.Key), loc))
                {
                    return kvp.Value;
                }
            }

            // 2. Если исключений нет и это блок из твоего мода — ищем автоматически!
            if (blockDomain == "botaniastory")
            {
                // Пробегаемся по всем категориям и главам в BookStructure
                foreach (var kvp in BookStructure)
                {
                    foreach (string chapterId in kvp.Value)
                    {
                        // Игнорируем временные заглушки (ch1, ch2 и т.д.), чтобы не было случайных совпадений
                        if (chapterId.Length <= 3) continue;

                        // МАГИЯ ЗДЕСЬ: Если название блока содержит ID главы 
                        // (например, "mysticalflower-orange-free" содержит "mysticalflower")
                        if (blockPath.Contains(chapterId))
                        {
                            return chapterId; // Книга кричит: "АГА, нашла!" и отдает ID
                        }
                    }
                }
            }

            return null; // Если ничего не нашли
        }
        public static List<BookCategory> GetTemplateCategories()
        {
            var categories = new List<BookCategory>();
            string[] templateItems = { "game:gear-rusty", "game:flint", "game:stick", "game:clay-blue", "game:clay-blue" };

            int catIndex = 0;
            foreach (var kvp in BookStructure)
            {
                string catId = kvp.Key;        // например: "basics_and_mechanics"
                string[] chapterIds = kvp.Value; // например: ["basicsintroduction", "botanialexicon", ...]

                // Ищем название категории: lexicon:lexicon_category_basics_and_mechanics
                string catLangKey = $"botaniastory:lexicon_category_{catId}";
                string localizedCatName = Lang.Get(catLangKey);

                // Если перевода нет, используем ID как временную заглушку
                if (localizedCatName == catLangKey) localizedCatName = catId.ToUpper();

                var cat = new BookCategory()
                {
                    Id = catId,
                    Name = localizedCatName,
                    IconPath = $"botaniastory:gui/category_icon_{catIndex % 12}.png",
                    Chapters = new List<BookChapter>()
                };

                for (int j = 0; j < chapterIds.Length; j++)
                {
                    string chapId = chapterIds[j]; // например: "basicsintroduction"

                    // Ищем название главы: lexicon:lexicon_basics_and_mechanics_basicsintroduction_title
                    string titleKey = $"botaniastory:lexicon_{catId}_{chapId}_title";
                    string title = Lang.Get(titleKey);

                    // Заглушка, если нет перевода
                    if (title == titleKey) title = $"Глава {j + 1}";

                    var chapter = new BookChapter()
                    {
                        Id = chapId,
                        Title = title,
                        TabItemCode = templateItems[(catIndex + j) % templateItems.Length],
                        Pages = new List<string>()
                    };
                    //////////////////////////////////////////////////////
                    //Создание новых глав
                    /////////////////////////////////////////////////////
                    if (chapId == "basicsintroduction")
                    {
                        
                    }

                    // === НАСТРОЙКА ГЛАВЫ ЛЕКСИКОН БОТАНИЯ ===
                    if (chapId == "botanialexicon")
                    {
                        chapter.TabItemCode = "botaniastory:lexicon-closed";

                        // НОВЫЙ ФОРМАТ ЗАПИСИ:
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            Grid = new string[9] {
                                "game:paper-parchment", null, null,
                                "game:treeseed-*", null, null,
                                null, null, null
                            },
                            Output = "botaniastory:lexicon-closed"
                        });
                    }

                    // === НАСТРОЙКА АПТЕКАРЯ ===
                    else if (chapId == "apothecary")
                    {
                        chapter.TabItemCode = "botaniastory:apothecary-*";
                        chapter.Images.Add(new BookPageImage()
                        {
                            Path = "botaniastory:textures/gui/apothecary.png",
                            Spread = 0,
                            UiKey = "Картинка_Правая"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1, 
                                                Grid = new string[9] {
                             "game:rock-*", "botaniastory:mysticalpetal-*" , "game:rock-*",// Верхний ряд

                             "game:hammer-*" , "game:rock-*", "game:chisel-*" ,            // Средний ряд

                             "game:rock-*", "game:rock-*", "game:rock-*" // Нижний ряд
                         },
                            Output = "botaniastory:apothecary-*"
                        });


						//добавить рецепты цветов в аптекаре
						chapter.Recipes.Add(new BookRecipe()
                         {
						RecipeType = "Apothecary",
						Spread = 2,
						ApothecaryIngredients = new string[]
						{
                            "botaniastory:mysticalpetal-*",
                            "botaniastory:mysticalpetal-*",
                            "botaniastory:mysticalpetal-*",
                            "botaniastory:mysticalpetal-*"
                        },
						ApothecaryCenter = "game:table-normal",
						Output = "botaniastory:mysticalpetal-*"
                        });
						
                    }

                    // === НАСТРОЙКА МИСТИЧЕСКИЕ ЦВЕТЫ ===
                    else if (chapId == "mysticalflower")
                    {
                        // Уточняем маску для иконки вкладки
                        chapter.TabItemCode = "botaniastory:mysticalflower-*-free";

                        chapter.Images.Add(new BookPageImage()
                        {
                            Path = "botaniastory:textures/gui/mysticalflower.png",
                            Spread = 0,
                            UiKey = "Картинка_Левая"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                                               Grid = new string[9] {
                             // Уточняем маску для рецепта в сетке
                             "botaniastory:mysticalflower-*-free", null, null, // Верхний ряд
                             null, null, null,                                 // Средний ряд
                             null, null, null                                  // Нижний ряд
                         },
                            // Лепестки, судя по структуре, не имеют суффикса snow/free, так что тут оставляем как есть
                            Output = "botaniastory:mysticalpetal-*"
                        });
                    }
                    // === НАСТРОЙКА ПОСОХ ЛЕСА ===
                    else if (chapId == "wandoftheforest")
                    {
                        chapter.TabItemCode = "botaniastory:wandoftheforest-*";
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            Grid = new string[9] {
                             null, "botaniastory:mysticalpetal-*", "botaniastory:livingwood_stick",// Верхний ряд
                             null, "botaniastory:livingwood_stick", "botaniastory:mysticalpetal-*",            // Средний ряд
                             "botaniastory:livingwood_stick", null, null // Нижний ряд
                         },
                            Output = "botaniastory:mysticalpetal-*"
                        });
                    }
                    // === НАСТРОЙКА РУНИЧЕСКИЙ АЛТАРЬ ===
                    else if (chapId == "runicaltar")
                    {
                        chapter.TabItemCode = "botaniastory:mysticalflower-pink-free";
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            Grid = new string[9] {
                             null, "botaniastory:mysticalpetal-*", "botaniastory:livingwood_stick",// Верхний ряд
                             null, "botaniastory:mysticalflower-*", "botaniastory:mysticalpetal-*",            // Средний ряд
                             "botaniastory:livingwood_stick", null, null // Нижний ряд
                         },
                            Output = "botaniastory:mysticalpetal-*"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 1,
                            ApothecaryIngredients = new string[]
                              {
                               "botaniastory:mysticalflower-*",
                               "botaniastory:mysticalpetal-*",
                               "botaniastory:mysticalpetal-*",
                               "botaniastory:mysticalpetal-*"
                              },
                            ApothecaryCenter = "botaniastory:mysticalpetal-*",
                            Output = "botaniastory:mysticalpetal-*"
                        });

                    }













                    // === НАСТРОЙКА ГЛАВЫ С НЕСКОЛЬКИМИ КРАФТАМИ (Пример: Альфхейм) ===
                    // Проверяем категорию (например, elfmania) и нужную главу (например, ch1)
                    else if (catId == "elfmania" && chapId == "ch1")
                    {
                        // --- 1. ПЕРВЫЙ РАЗВОРОТ (Spread = 0) ---
                        // Добавляем рецепт Альфхейма на самые первые страницы главы
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Alfheim",
                            Spread = 0, // 0 = первый разворот
                            AlfheimInputs = new string[] { "game:rock-granite" }, // Что кидаем
                            Output = "game:rock-granite"                          // Что получаем
                        });

                        // --- 2. ВТОРОЙ РАЗВОРОТ (Spread = 1) ---
                        // Игрок нажимает "Вперед" и видит рецепт обычной сетки
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1, // 1 = второй разворот (после одного перелистывания)
                            Grid = new string[9] {
                                "botaniastory:mysticalflower-*", null, "game:plank-*",
                                null, "game:stick", null,
                                "game:plank-*", null, "botaniastory:apothecary-*"
                            },
                            Output = "game:table-normal"
                        });

                        // --- 3. ТРЕТИЙ РАЗВОРОТ (Spread = 2) ---
                        // Игрок еще раз нажимает "Вперед" и видит Аптекарь
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 2, // 2 = третий разворот
                            ApothecaryIngredients = new string[]
                            {
                                "game:flower-*",
                                "botaniastory:mysticalflower-*",
                               "botaniastory:apothecary-*"
                            },
                            ApothecaryCenter = "game:seed-*",
                            Output = "botaniastory:mysticalflower-*"
                        });
                    }




                    // Пытаемся загрузить до 5 страниц
                    for (int p = 1; p <= 5; p++)
                    {
                        // Ищем страницы: lexicon:lexicon_basics_and_mechanics_basicsintroduction_p1
                        string pageKey = $"botaniastory:lexicon_{catId}_{chapId}_p{p}";
                        string pageText = Lang.Get(pageKey);

                        if (pageText != pageKey)
                        {
                            chapter.Pages.Add(pageText);
                        }
                        else if (p == 1 && chapter.Pages.Count == 0)
                        {
                            // Заглушка, если страниц вообще нет
                            chapter.Pages.Add($"Текст страницы отсутствует. Добавь ключ в lang:\n{pageKey}");
                        }
                    }

                    cat.Chapters.Add(chapter);
                }
                categories.Add(cat);
                catIndex++;
            }
            return categories;
        }
    }
}