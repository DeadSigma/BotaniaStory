using BotaniaStory;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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
        public string RecipeType { get; set; }
        public int Spread { get; set; }      

        public string Output { get; set; }    

        public string[] Grid { get; set; }
        public string UiKey { get; set; }
        public string[] ApothecaryIngredients { get; set; }
        public string ApothecaryCenter { get; set; }
        public string[] AlfheimInputs { get; set; }
        public string[] PoolInput { get; set; }   
        public string PoolBlock { get; set; }      
        public string[] PoolCatalyst { get; set; }
        public string AnvilInput { get; set; }
        public string AnvilBlock { get; set; }
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
        // "Оглавление". Ключ - ID категории, Значение - массив ID глав
        private static readonly Dictionary<string, string[]> BookStructure = new Dictionary<string, string[]>
        {
            { "basics_and_mechanics", new[] { "basicsintroduction", "botanialexicon", "apothecary", "mysticalflower", "wandoftheforest",
                "puredaisy", "runicaltar" } },

            { "mana_management", new[] { "manaintroduction", "manaspreader", "manapool", "manatablet", "spark", "sparkaugment"} },

            { "generating_flora", new[] {"generatingfloraintroduction", "daybloom", "endoflame" } },

            { "functional_flora", new[] { "puredaisy"} },

            { "natural_apparatus", new[] { "ch1" } },

            { "mystical_items", new[] { "wandofbinding", "manasteelgear" } },

            { "trinkets_and_accessories", new[] { "ch1" } },

            { "rusted_world_artifacts", new[] { "ch1" } },

            { "elfmania", new[] { "ch1" } },

            { "misc", new[] { "livingwood_firewood", "livingwood_stick", "livingrock", "livingrock_slab" } },

            { "trials", new[] { "ch1" } }
        };


        /// <summary>
        /// //Система shif + ПКМ и переход к информации
        /// </summary>
        private static readonly Dictionary<string, string> ExceptionsMap = new Dictionary<string, string>
    {
        { "botaniastory:mysticalpetal-*", "mysticalflower" },
        { "botaniastory:livingwood_stick", "puredaisy" },
        { "botaniastory:livingwood", "puredaisy" },
        { "botaniastory:livingwood_firewood", "puredaisy" },
        { "botaniastory:livingrock", "puredaisy" }
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

                // Ищем название категории: botaniastory:lexicon_category_basics_and_mechanics
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

                    // Ищем название главы: botaniastory:lexicon_basics_and_mechanics_basicsintroduction_title
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
                            UiKey = "Сетка_Правая_Верхняя",
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
                            UiKey = "Сетка_Правая_Верхняя",
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
                            UiKey = "Аптекарь_Область_Правая",
                            ApothecaryIngredients = new string[]
                        {
                            "botaniastory:mysticalpetal-*",
                            "botaniastory:mysticalpetal-*",
                            "botaniastory:mysticalpetal-*",
                            "botaniastory:mysticalpetal-*"
                        },
                            ApothecaryCenter = "botaniastory:apothecary-*",
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
                            Path = "botaniastory:textures/gui/mysticalflowers.png",
                            Spread = 0,
                            UiKey = "Картинка_Левая"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
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
                            UiKey = "Сетка_Правая_Верхняя",
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
                            UiKey = "Сетка_Правая_Верхняя",
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

                    // === НАСТРОЙКА ГЛАВЫ РАСПРОСТРАНИТЕЛЬ МАНЫ===
                    else if (chapId == "manaspreader")
                    {
                        chapter.TabItemCode = "botaniastory:manaspreader";

                        chapter.Images.Add(new BookPageImage()
                        {
                            Path = "botaniastory:textures/gui/manaspreader.png",
                            Spread = 0,
                            UiKey = "Картинка_Правая"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                                 "botaniastory:livingwood", "botaniastory:livingwood", "botaniastory:livingwood",
                                 "game:ingot-copper", "botaniastory:mysticalpetal-*", null,
                                 "botaniastory:livingwood", "botaniastory:livingwood", "botaniastory:livingwood"},
                            Output = "botaniastory:manaspreader"
                        });
                    }

                    // === НАСТРОЙКА PUREDAISY ===
                    else if (chapId == "puredaisy")
                    {
                        chapter.TabItemCode = "botaniastory:puredaisy-free";

                        chapter.Images.Add(new BookPageImage()
                        {
                            Path = "botaniastory:textures/gui/puredaisy.png",
                            Spread = 0,
                            UiKey = "Картинка_Правая"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1, // Тот же разворот
                            UiKey = "Сетка_Левая_Верхняя", // <--- СЛЕВА
                            Grid = new string[9] {
                                "game:axe-*", "botaniastory:livingwood", null,
                                null, null, null,
                                null, null, null
                            },
                            Output = "botaniastory:livingwood_firewood"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                                "game:axe-*", "botaniastory:livingwood_firewood", null,
                                null, null, null,
                                null, null, null
                            },
                            Output = "botaniastory:livingwood_stick"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 2,
                            UiKey = "Аптекарь_Область_Левая",
                            ApothecaryIngredients = new string[]
                        {
                            "botaniastory:mysticalpetal-white",
                            "botaniastory:mysticalpetal-white",
                            "botaniastory:mysticalpetal-white",
                            "botaniastory:mysticalpetal-white"
                        },
                            ApothecaryCenter = "botaniastory:apothecary-*",
                            Output = "botaniastory:puredaisy-free"
                        });
                    }
                    // === НАСТРОЙКА БАССЕЙН МАНЫ ===
                    else if (chapId == "manapool")
                    {
                        chapter.TabItemCode = "botaniastory:manapool-normal";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                               "game:chisel-*", null,"game:hammer-*",// Верхний ряд

                              "botaniastory:livingrock_slab", null, "botaniastory:livingrock_slab",// Средний ряд
                        
                              "botaniastory:livingrock_slab", "botaniastory:livingrock_slab", "botaniastory:livingrock_slab" // Нижний ряд
                          },
                            Output = "botaniastory:manapool_diluted"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                               "game:chisel-*", null,"game:hammer-*",// Верхний ряд


                              "botaniastory:livingrock", null, "botaniastory:livingrock",// Средний ряд
                        
                              "botaniastory:livingrock", "botaniastory:livingrock", "botaniastory:livingrock" // Нижний ряд
                          },
                            Output = "botaniastory:manapool-normal"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 1,

                            UiKey = "Бассейн_Область_Правая_Верхняя",

                            PoolInput = new string[] { "game:ingot-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[] { "botaniastory:mysticalpetal-white" },

                            Output = "botaniastory:mysticalflower-*"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 1,
                            ManaCost = 50000,
                            UiKey = "Полоска_Маны_Правая_Верхняя"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 1,

                            UiKey = "Бассейн_Область_Правая_Нижняя",

                            PoolInput = new string[] { "game:ingot-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[] { "botaniastory:mysticalpetal-white" },

                            Output = "botaniastory:mysticalflower-*"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 1,
                            ManaCost = 50000,
                            UiKey = "Полоска_Маны_Правая_Нижняя"
                        });

                        ////page 2

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 2,

                            UiKey = "Бассейн_Область_Правая_Верхняя",

                            PoolInput = new string[] { "game:ingot-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[] { "botaniastory:mysticalpetal-white" },

                            Output = "botaniastory:mysticalflower-*"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 2,
                            ManaCost = 50000,
                            UiKey = "Полоска_Маны_Правая_Верхняя"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 2,

                            UiKey = "Бассейн_Область_Правая_Нижняя",

                            PoolInput = new string[] { "game:ingot-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[] { "botaniastory:mysticalpetal-white" },

                            Output = "botaniastory:mysticalflower-*"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 2,
                            ManaCost = 50000,
                            UiKey = "Полоска_Маны_Правая_Нижняя"
                        });

                        ////////

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 2,

                            UiKey = "Бассейн_Область_Левая_Нижняя",

                            PoolInput = new string[] { "game:ingot-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[] { "botaniastory:mysticalpetal-white" },

                            Output = "botaniastory:mysticalflower-*"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 2,
                            ManaCost = 50000,
                            UiKey = "Полоска_Маны_Левая_Нижняя"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 2,

                            UiKey = "Бассейн_Область_Левая_Верхняя",

                            PoolInput = new string[] { "game:ingot-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[] { "botaniastory:mysticalpetal-white" },

                            Output = "botaniastory:mysticalflower-*"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 2,
                            ManaCost = 50000,
                            UiKey = "Полоска_Маны_Левая_Верхняя"
                        });

                        ///page 3


                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 3,

                            UiKey = "Бассейн_Область_Левая_Верхняя",

                            PoolInput = new string[] { "game:ingot-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[] { "botaniastory:mysticalpetal-white" },

                            Output = "botaniastory:mysticalflower-*"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 3,
                            ManaCost = 50000,
                            UiKey = "Полоска_Маны_Левая_Верхняя"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 3,

                            UiKey = "Бассейн_Область_Левая_Нижняя",

                            PoolInput = new string[] { "game:ingot-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[] { "botaniastory:mysticalpetal-white" },

                            Output = "botaniastory:mysticalflower-*"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 3,
                            ManaCost = 50000,
                            UiKey = "Полоска_Маны_Левая_Нижняя"
                        });

                    }

                    else if (chapId == "manatablet")
                    {
                        chapter.TabItemCode = "botaniastory:manatablet";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                                  "botaniastory:livingrock", "botaniastory:livingrock", "botaniastory:livingrock",
                                  "botaniastory:livingrock", "botaniastory:manaitem-manaquartz", "botaniastory:livingrock",
                                  "botaniastory:livingrock", "botaniastory:livingrock", "botaniastory:livingrock"},
                            Output = "botaniastory:manatablet"
                        });

                    }

                    // === НАСТРОЙКА ГЛАВЫ ИСКРЫ ===
                    else if (chapId == "spark")
                    {
                        chapter.TabItemCode = "botaniastory:spark";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                             null, "botaniastory:mysticalpetal-*", null,
                             "game:blastingpowder", "game:nugget-nativegold", "game:blastingpowder",
                             null, "botaniastory:mysticalpetal-*", null},
                            Output = "botaniastory:spark"
                        });
                    }

                    // === НАСТРОЙКА ГЛАВЫ ДОПОЛНИТЕЛИ ИСКР ===
                    else if (chapId == "sparkaugment")
                    {
                        chapter.TabItemCode = "botaniastory:sparkaugment-*";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Левая_Верхняя",
                            Grid = new string[9] {
                             null, "botaniastory:mysticalpetal-*", null,
                             "game:blastingpowder", "game:nugget-nativegold", "game:blastingpowder",
                             null, "botaniastory:mysticalpetal-*", null},
                            Output = "botaniastory:sparkaugment-isolated"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                             null, "botaniastory:mysticalpetal-*", null,
                             "game:blastingpowder", "game:nugget-nativegold", "game:blastingpowder",
                             null, "botaniastory:mysticalpetal-*", null},
                            Output = "botaniastory:sparkaugment-dominant"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                             null, "botaniastory:mysticalpetal-*", null,
                             "game:blastingpowder", "game:nugget-nativegold", "game:blastingpowder",
                             null, "botaniastory:mysticalpetal-*", null},
                            Output = "botaniastory:sparkaugment-dispersive"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                             null, "botaniastory:mysticalpetal-*", null,
                             "game:blastingpowder", "game:nugget-nativegold", "game:blastingpowder",
                             null, "botaniastory:mysticalpetal-*", null},
                            Output = "botaniastory:sparkaugment-recessive"
                        });
                    }

                    // === НАСТРОЙКА ГЛАВЫ ДНЕВНОЦВЕТ ===
                    else if (chapId == "daybloom")
                    {
                        chapter.TabItemCode = "botaniastory:daybloom-free";
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 1,
                            UiKey = "Аптекарь_Область_Левая",
                            ApothecaryIngredients = new string[]
                            {
                                 "botaniastory:mysticalpetal-yellow",
                                 "botaniastory:mysticalpetal-orange",
                                 "botaniastory:mysticalpetal-lightblue",
                                 "botaniastory:mysticalpetal-yellow"
                            },
                            ApothecaryCenter = "botaniastory:apothecary-*",
                            Output = "botaniastory:daybloom-free"
                        });
                    }

                    // === НАСТРОЙКА ГЛАВЫ ЭНДОПЛАМЯ ===
                    else if (chapId == "endoflame")
                    {
                        chapter.TabItemCode = "botaniastory:endoflame-free";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 0,
                            UiKey = "Аптекарь_Область_Правая",
                            ApothecaryIngredients = new string[]
                            {
                                 "botaniastory:mysticalpetal-brown",
                                 "botaniastory:mysticalpetal-red",
                                 "botaniastory:mysticalpetal-lightgray",
                                 "botaniastory:mysticalpetal-brown"
                            },
                            ApothecaryCenter = "botaniastory:apothecary-*",

                            Output = "botaniastory:endoflame-free"
                        });
                    }

                    // === НАСТРОЙКА ГЛАВЫ ЖЕЗЛ СВЯЗЫВАНИЯ ===
                    else if (chapId == "wandofbinding")
                    {
                        chapter.TabItemCode = "botaniastory:wandofbinding";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                              null, null, "botaniastory:livingwood_stick",
                              null, "botaniastory:livingwood_stick", null,
                              "botaniastory:livingwood_stick", null, null},
                            Output = "botaniastory:wandofbinding"
                        });

                    }

                    // === НАСТРОЙКА ГЛАВЫ ЖИЗНЕДРОВА ===
                    else if (chapId == "livingwood_firewood")
                    {
                        chapter.TabItemCode = "botaniastory:livingwood_firewood";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                              "game:axe-*", "botaniastory:livingwood", null,
                              null, null, null,
                              null, null, null},
                            Output = "botaniastory:livingwood_firewood"
                        });
                    }

                    // === НАСТРОЙКА ГЛАВЫ ЖИЗНЕПАЛКИ ===
                    else if (chapId == "livingwood_stick")
                    {
                        chapter.TabItemCode = "botaniastory:livingwood_stick";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                              "game:axe-*", "botaniastory:livingwood_firewood", null,
                              null, null, null,
                              null, null, null},
                            Output = "botaniastory:livingwood_stick"
                        });
                    }

                    // === НАСТРОЙКА ГЛАВЫ ЖИЗНЕКАМЕНЬ ===
                    else if (chapId == "livingrock")
                    {
                        chapter.TabItemCode = "botaniastory:livingrock";

                        chapter.Images.Add(new BookPageImage()
                        {
                            Path = "botaniastory:textures/gui/puredaisy.png",
                            Spread = 0,
                            UiKey = "Картинка_Правая"
                        });

                    }

                    // === НАСТРОЙКА ГЛАВЫ ПЛИТА ИЗ ЖИЗНЕКАМНЯ ===
                    else if (chapId == "livingrock_slab")
                    {
                        chapter.TabItemCode = "botaniastory:livingrock_slab";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                               "game:chisel-*", "game:hammer-*",null,// Верхний ряд

                              "botaniastory:livingrock", null, null,// Средний ряд
                        
                              null, null, null // Нижний ряд
                          },
                            Output = "botaniastory:livingrock_slab"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                               "game:chisel-*", null,"game:hammer-*",// Верхний ряд

                              "botaniastory:livingrock_slab", null, "botaniastory:livingrock_slab",// Средний ряд
                        
                              "botaniastory:livingrock_slab", "botaniastory:livingrock_slab", "botaniastory:livingrock_slab" // Нижний ряд
                          },
                            Output = "botaniastory:manapool_diluted"
                        });
                    }

                    // === НАСТРОЙКА ГЛАВЫ ПРЕДМЕТЫ ИЗ МАНАСТАЛИ ===
                    else if (chapId == "manasteelgear")
                    {
                        ////////////////////////////////////////////////
                        chapter.TabItemCode = "botaniastory:pickaxe-manasteel";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 0,
                            UiKey = "Кузня_Манасталь",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "game:metalchain-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Манашлем",
                            Grid = new string[9] {
                              null, "game:metalchain-manasteel", null,
                              "game:metalchain-manasteel", null, "game:metalchain-manasteel",
                              null, null, null},
                            Output = "botaniastory:armor-chain-manasteel-head"
                        });
                        ////////////////////////////////////////////////


                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Левая_Верхняя",
                            Grid = new string[9] {
                              "game:metalchain-manasteel", "game:armor-body-jerkin-leather", "game:metalchain-manasteel",
                              "game:metalchain-manasteel@2", "game:metalchain-manasteel@2", "game:metalchain-manasteel@2",
                              "game:metalchain-manasteel", "game:metalchain-manasteel", "game:metalchain-manasteel"},
                            Output = "botaniastory:armor-chain-manasteel-body"
                        });
                        ////////////////////////////////////////////////


                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                              "game:metalchain-manasteel", "game:metalchain-manasteel@2", "game:metalchain-manasteel",
                              "game:metalchain-manasteel", "game:armor-legs-jerkin-leather", "game:metalchain-manasteel",
                              null, null, null},
                            Output = "botaniastory:armor-chain-manasteel-legs"
                        });
                        ////////////////////////////////////////////////
                        ///
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 1,
                            UiKey = "Кузня_Правая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:pickaxehead-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:pickaxehead-manasteel", null, null,
                                 "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:pickaxe-manasteel"
                        });
                      //////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 2,
                            UiKey = "Кузня_Левая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:axehead-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 2,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:axehead-manasteel", null, null,
                                  "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:axe-manasteel"
                        });
                        //////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 2,
                            UiKey = "Кузня_Правая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:shovelhead-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 2,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:shovelhead-manasteel", null, null,
                                  "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:shovel-manasteel"
                        });
                        //////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 3,
                            UiKey = "Кузня_Левая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:cleaverhead-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 3,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:cleaverhead-manasteel", null, null,
                                 "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:cleaver-manasteel"
                        });
                        //////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 3,
                            UiKey = "Кузня_Правая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:falxhead-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 3,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:falxhead-manasteel", null, null,
                                 "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:falx-manasteel"
                        });
                        //////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 4,
                            UiKey = "Кузня_Левая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:hammerhead-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 4,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:hammerhead-manasteel", null, null,
                                 "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:hammer-manasteel"
                        });
                        //////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 4,
                            UiKey = "Кузня_Правая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:hoehead-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 4,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:hoehead-manasteel", null, null,
                                 "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:hoe-manasteel"
                        });
                        //////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 5,
                            UiKey = "Кузня_Левая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:knifeblade-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 5,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:knifeblade-manasteel", null, null,
                                 "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:knife-manasteel"
                        });
                        //////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 5,
                            UiKey = "Кузня_Правая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:prospectingpickhead-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 5,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:prospectingpickhead-manasteel", null, null,
                                 "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:prospectingpick-manasteel"
                        });
                        //////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 6,
                            UiKey = "Кузня_Левая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:sawblade-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 6,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:sawblade-manasteel", null, null,
                                 "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:saw-manasteel"
                        });
                        //////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 6,
                            UiKey = "Кузня_Правая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:scythehead-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 6,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:scythehead-manasteel", null, null,
                                 "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:scythe-manasteel"
                        });
                        //////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 7,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:spearhead-manasteel", null, null,
                                 "game:stick", null, null,
                                 null, null, null},
                            Output = "botaniastory:spear-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 7,
                            UiKey = "Кузня_Левая_Верхняя",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:spearhead-manasteel"
                        });
                        //////////////////////////////
                        ///
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 7,
                            UiKey = "Кузня_Правая_Зубило",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:chisel-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 7,
                            UiKey = "Кузня_Правая_Ключ",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:wrench-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 7,
                            UiKey = "Кузня_Правая_Клещи",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:tongs-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 7,
                            UiKey = "Кузня_Правая_Монтировка",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:crowbar-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Anvil",
                            Spread = 7,
                            UiKey = "Кузня_Правая_Ножницы",
                            AnvilInput = "game:ingot-manasteel",
                            AnvilBlock = "game:anvil-*",
                            Output = "botaniastory:shears-manasteel"
                        });


                    }



                    // Пытаемся загрузить до 30 страниц
                    for (int p = 1; p <= 30; p++)
                    {
                        // Ищем страницы: botaniastory:lexicon_basics_and_mechanics_basicsintroduction_p1
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