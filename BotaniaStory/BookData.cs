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
        public string UiKey { get; set; }     // Ключ рамки дебаггера (LexiconUIData)
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
        public int VisualizeSpread { get; set; }
        public string VisualizeUiKey { get; set; }
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
        private static readonly Dictionary<string, string[]> BookStructure = new Dictionary<string, string[]>
        {
            { "basics_and_mechanics", new[] { "basicsintroduction", "botanialexicon", "apothecary", "mysticalflower", "wandoftheforest", "terrasteel",
                "puredaisy", "runicaltar", "rune", "pylon" } },

            { "mana_management", new[] { "manaintroduction", "manaspreader", "manapool", "manatablet", "spark", "sparkaugment" } },

            { "generating_flora", new[] {"generatingfloraintroduction", "daybloom", "endoflame" } },

            { "functional_flora", new[] { "puredaisy"} },

            { "natural_apparatus", new[] { "ch1" } },

            { "mystical_items", new[] { "wandofbinding", "manaitem" } },

            { "trinkets_and_accessories", new[] { "trinkets" } },

            { "rusted_world_artifacts", new[] { "ch1" } },

            { "elfmania", new[] { "alfheimgates", "elfresources" } },

            { "misc", new[] { "livingwood_stuff",  "livingrock_stuff", "managlass" } },

            { "trials", new[] { "ch1" } }
        };



        //Система shif + ПКМ и переход к информации

        private static readonly Dictionary<string, string> ExceptionsMap = new Dictionary<string, string>
    {
        { "botaniastory:mysticalpetal-*", "mysticalflower" },
        { "botaniastory:livingwood_stick", "puredaisy" },
        { "botaniastory:livingwood-*", "puredaisy" },
        { "botaniastory:livingwood_firewood", "puredaisy" },
        { "botaniastory:livingrock", "puredaisy" },
        { "botaniastory:rune-*", "runicaltar" },
        { "botaniastory:manaitem-*", "manapool" },
        { "game:ingot-manasteel*", "manapool" }
    };

        //  Метод для поиска главы по блоку ===
        public static string GetChapterForBlock(string blockCode)
        {
            if (string.IsNullOrEmpty(blockCode)) return null;

            // Разделяем код на домен (мод) и путь (название)
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

            // 2. Если исключений нет и это блок из  мода — ищем автоматически!
            if (blockDomain == "botaniastory")
            {
                // Пробегаемся по всем категориям и главам в BookStructure
                foreach (var kvp in BookStructure)
                {
                    foreach (string chapterId in kvp.Value)
                    {
                        // Игнорируем временные заглушки (ch1, ch2 и т.д.), чтобы не было случайных совпадений
                        if (chapterId.Length <= 3) continue;

                        //  Если название блока содержит ID главы 
                        // (например, "mysticalflower-orange-free" содержит "mysticalflower")
                        if (blockPath.Contains(chapterId))
                        {
                            return chapterId; 
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
                string catId = kvp.Key;      
                string[] chapterIds = kvp.Value;

                // Ищем название категории
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
                    string chapId = chapterIds[j]; 


                    string titleKey = $"botaniastory:lexicon_{catId}_{chapId}_title";
                    string title = Lang.Get(titleKey);

                    // Заглушка, если нет перевода
                    if (title == titleKey) title = $"Kapitel {j + 1}";

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


                    // === НАСТРОЙКА ГЛАВЫ ВВЕДЕНИЕ В ОСНОВЫ МАНЫ ===
                    if (chapId == "basicsintroduction")
                    {
                        chapter.TabItemCode = "botaniastory:checkmark";
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
                        chapter.TabItemCode = "botaniastory:runicaltar";
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                            "botaniastory:livingrock", "botaniastory:livingrock", "botaniastory:livingrock", // Сверху 3 камня
                            "botaniastory:livingrock", "botaniastory:manaitem-managear, botaniastory:manaitem-manaquartz", "botaniastory:livingrock", // Бока + Ядро в центре
                            null,                      null,                      null                       // Нижний ряд пустой
                        },
                            Output = "botaniastory:runicaltar"
                        });



                    }

                    // === НАСТРОЙКА ГЛАВЫ ТЕРРАСТАЛЬ ===
                    else if (chapId == "terrasteel")
                    {
                        chapter.TabItemCode = "game:ingot-terrasteel";

                        chapter.VisualizeStructure = "terraaltar";
                        chapter.VisualizeSpread = 0;

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                                  "game:rockpolished-andesite", "game:rockpolished-andesite", "game:rockpolished-andesite",
                                  "botaniastory:rune-water", "game:metalblock-new-riveted-manasteel", "botaniastory:rune-fire",
                                  "botaniastory:rune-earth", "botaniastory:rune-mana", "botaniastory:rune-air"},
                            Output = "botaniastory:terrestrialplate"
                        });

                        chapter.Images.Add(new BookPageImage()
                        {
                            Path = "botaniastory:textures/gui/terraplatform.png",
                            Spread = 0,
                            UiKey = "Картинка_Правая_Платформа"
                        });
                    }



                    // === НАСТРОЙКА ГЛАВЫ ВВЕДЕНИЕ В УПРАВЛЕНИЕ МАНОЙ ===
                    else if (chapId == "manaintroduction")
                    {
                        chapter.TabItemCode = "botaniastory:checkmark";
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
                                 "botaniastory:livingwood-*", "botaniastory:livingwood-*", "botaniastory:livingwood-*",
                                 "game:ingot-copper", "botaniastory:mysticalpetal-*", null,
                                 "botaniastory:livingwood-*", "botaniastory:livingwood-*", "botaniastory:livingwood-*"},
                            Output = "botaniastory:manaspreader"
                        });
                    }

                    // === НАСТРОЙКА ЧИСТАЯ МАРГАРИТКА ===
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
                                "game:axe-*", "botaniastory:livingwood-*", null,
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

                              "botaniastory:livingrock_slab-down-free", null, "botaniastory:livingrock_slab-down-free",// Средний ряд
                        
                              "botaniastory:livingrock_slab-down-free", "botaniastory:livingrock_slab-down-free", "botaniastory:livingrock_slab-down-free" // Нижний ряд
                          },
                            Output = "botaniastory:manapool-diluted"
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
                        ////////////////////////////////////////////
                        // Разворот 1 (Spread 1) - Правая страница
                        ////////////////////////////////////////////

                        // 1. Мана-слиток
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 1,
                            UiKey = "Бассейн_Область_Правая_Верхняя",
                            PoolInput = new string[] { "game:ingot-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[0],
                            Output = "game:ingot-manasteel"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 1,
                            ManaCost = 25000,
                            UiKey = "Полоска_Маны_Правая_Верхняя"
                        });

                        // 2. Мана-кварц
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 1,
                            UiKey = "Бассейн_Область_Правая_Нижняя",
                            PoolInput = new string[] { "game:clearquartz" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[0],
                            Output = "botaniastory:manaitem-manaquartz"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 1,
                            ManaCost = 25000,
                            UiKey = "Полоска_Маны_Правая_Нижняя"
                        });

                        ////////////////////////////////////////////
                        // Разворот 2 (Spread 2) - Полностью заполненный
                        ////////////////////////////////////////////

                        // 3. Мана-шестерня
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 2,
                            UiKey = "Бассейн_Область_Левая_Верхняя",
                            PoolInput = new string[] { "game:gear-rusty" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[0],
                            Output = "botaniastory:manaitem-managear"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 2,
                            ManaCost = 30000,
                            UiKey = "Полоска_Маны_Левая_Верхняя"
                        });

                        // 4. Мана-нить
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 2,
                            UiKey = "Бассейн_Область_Левая_Нижняя",
                            PoolInput = new string[] { "game:flaxfibers" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[0],
                            Output = "botaniastory:manaitem-manaflax"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 2,
                            ManaCost = 10000,
                            UiKey = "Полоска_Маны_Левая_Нижняя"
                        });

                        // 5. Мана-порошок
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 2,
                            UiKey = "Бассейн_Область_Правая_Верхняя",
                            PoolInput = new string[] { "game:powder-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[0],
                            Output = "botaniastory:manaitem-manapowder"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 2,
                            ManaCost = 10000,
                            UiKey = "Полоска_Маны_Правая_Верхняя"
                        });

                        // 6. Манастекло (В самом конце разворота)
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 2,
                            UiKey = "Бассейн_Область_Правая_Нижняя",
                            PoolInput = new string[] { "game:glass-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[0],
                            Output = "botaniastory:managlass"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 2,
                            ManaCost = 5000,
                            UiKey = "Полоска_Маны_Правая_Нижняя"
                        });


                    }
                    // === НАСТРОЙКА ГЛАВЫ ПЛАНШЕТ МАНЫ ===
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
                        ////////////////////////////////
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
                                  null, null, null,
                                  "game:ingot-manasteel", "botaniastory:rune-air", "game:stick",
                                  null, null, null},
                            Output = "botaniastory:sparkaugment-isolated"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                              null, null, null,
                              "game:ingot-manasteel", "botaniastory:rune-fire", "game:stick",
                              null, null, null},
                            Output = "botaniastory:sparkaugment-dominant"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                              null, null, null,
                              "game:ingot-manasteel", "botaniastory:rune-water", "game:stick",
                              null, null, null},
                            Output = "botaniastory:sparkaugment-dispersive"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                              null, null, null,
                              "game:ingot-manasteel", "botaniastory:rune-earth", "game:stick",
                              null, null, null},
                            Output = "botaniastory:sparkaugment-recessive"
                        });
                    }

                    // === НАСТРОЙКА ГЛАВЫ ВВЕДЕНИЕ В ГЕНЕРИРУЮЩУЮ ФЛОРУ ===
                    else if (chapId == "generatingfloraintroduction")
                    {
                        chapter.TabItemCode = "botaniastory:checkmark";
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

                    // === НАСТРОЙКА ГЛАВЫ ПОСОХ/ЖЕЗЛ СВЯЗЫВАНИЯ ===
                    else if (chapId == "wandofbinding")
                    {
                        chapter.TabItemCode = "botaniastory:wandofbinding";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                              null, null, "game:ingot-manasteel",
                              null, "botaniastory:livingwood_stick", null,
                              "botaniastory:livingwood_stick", null, null},
                            Output = "botaniastory:wandofbinding"
                        });

                    }

                    // === НАСТРОЙКА ГЛАВЫ ПРЕДМЕТЫ ИЗ ЖИЗНЕДЕРЕВА ===
                    else if (chapId == "livingwood_stuff")
                    {
                        chapter.TabItemCode = "botaniastory:livingwood-normal";

                        chapter.Images.Add(new BookPageImage()
                        {
                            Path = "botaniastory:textures/gui/puredaisy.png",
                            Spread = 0,
                            UiKey = "Картинка_Левая"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                              "game:axe-*", "botaniastory:livingwood-normal", null,
                              null, null, null,
                              null, null, null},
                            Output = "botaniastory:livingwood_firewood"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                              "game:axe-*", "botaniastory:livingwood_firewood", null,
                              null, null, null,
                              null, null, null},
                            Output = "botaniastory:livingwood_stick"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Левая_Верхняя",
                            Grid = new string[9] {
                                  "botaniastory:livingwood-normal", "game:candle", null,
                                  null, null, null,
                                  null, null, null},
                            Output = "botaniastory:glimmering-livingwood"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                            "game:saw-*", "botaniastory:livingwood-normal", null,
                            null, null, null,
                            null, null, null
                        },
                            Output = "botaniastory:livingwood_plank-normal"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                            "botaniastory:livingwood_plank-normal", "botaniastory:livingwood_plank-normal", null,
                            "botaniastory:livingwood_plank-normal", "botaniastory:livingwood_plank-normal", null,
                            null, null, null
                        },
                            Output = "botaniastory:livingwood_planks-normal-ud"
                        });

                        ////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                            "botaniastory:livingwood_plank-normal", "botaniastory:livingwood_plank-normal", null,
                            null, null, null,
                            null, null, null
                        },
                            Output = "botaniastory:livingwood_plankslab-normal-down-free"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 2,
                            UiKey = "Сетка_Левая_Верхняя",
                            Grid = new string[9] {
                            "botaniastory:livingwood_plank-normal", null, null,
                            "botaniastory:livingwood_plank-normal", "botaniastory:livingwood_plank-normal", null,
                            null, null, null
                        },
                            Output = "botaniastory:livingwood_plankstairs-normal-up-north-free"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 2,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                            "botaniastory:livingwood-normal", null, null,
                            null, null, null,
                            null, null, null
                        },
                            Output = "botaniastory:livingwood-aged"
                        });

                        ///////////////////////////////////////////////////////////////////////////////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 2,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                            "game:saw-*", "botaniastory:livingwood-aged", null,
                            null, null, null,
                            null, null, null
                            },
                            Output = "botaniastory:livingwood_plank-aged"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 2,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                            "botaniastory:livingwood_plank-aged", "botaniastory:livingwood_plank-aged", null,
                            "botaniastory:livingwood_plank-aged", "botaniastory:livingwood_plank-aged", null,
                            null, null, null
                        },
                            Output = "botaniastory:livingwood_planks-aged-ud"
                        });

                        /////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 3,
                            UiKey = "Сетка_Левая_Верхняя",
                            Grid = new string[9] {
                            "botaniastory:livingwood_plank-aged", "botaniastory:livingwood_plank-aged", null,
                            null, null, null,
                            null, null, null
                        },
                            Output = "botaniastory:livingwood_plankslab-aged-down-free"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 3,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                            "botaniastory:livingwood_plank-aged", null, null,
                            "botaniastory:livingwood_plank-aged", "botaniastory:livingwood_plank-aged", null,
                            null, null, null
                        },
                            Output = "botaniastory:livingwood_plankstairs-aged-up-north-free"
                        });

                    }

                    // === НАСТРОЙКА ГЛАВЫ ПРЕДМЕТЫ ИЗ ЖИЗНЕКАМНЯ ===
                    else if (chapId == "livingrock_stuff")
                    {
                        chapter.TabItemCode = "botaniastory:livingrock";

                        chapter.Images.Add(new BookPageImage()
                        {
                            Path = "botaniastory:textures/gui/puredaisy.png",
                            Spread = 0,
                            UiKey = "Картинка_Левая"
                        });

                        /////////////Полублоки из жизнекамня 
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
                            Output = "botaniastory:livingrock_slab-down-free"
                        });
                        /////////////Полублоки из жизнекамня 
                        /////////////Кирпичи
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                             "game:chisel-*", null, null,
                             "game:hammer-*", null, null,
                             "botaniastory:livingrock", null, null},
                            Output = "botaniastory:livingrock_brick"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Левая_Верхняя",
                            Grid = new string[9] {
                             "botaniastory:livingrock_brick", "botaniastory:livingrock_brick", "botaniastory:livingrock_brick",
                             "botaniastory:livingrock_brick", "game:mortar", "botaniastory:livingrock_brick",
                             "botaniastory:livingrock_brick", "botaniastory:livingrock_brick", "botaniastory:livingrock_brick" },
                            Output = "botaniastory:livingrock_bricks"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                             "game:chisel-*", null, null,
                             "game:hammer-*", null, null,
                             "botaniastory:livingrock_bricks", null, null},
                            Output = "botaniastory:livingrock_crackedbricks"
                        });
                        /////////////Кирпичи
                    }
                   
                    // === НАСТРОЙКА ГЛАВЫ МАНАСТЕКЛО ===
                    else if (chapId == "managlass")
                    {
                        chapter.TabItemCode = "botaniastory:managlass";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "ManaPool",
                            Spread = 0,
                            UiKey = "Бассейн_Область_Правая_Верхняя",
                            PoolInput = new string[] { "game:glass-*" },
                            PoolBlock = "botaniastory:manapool-creative",
                            PoolCatalyst = new string[0],
                            Output = "botaniastory:managlass"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 0,
                            ManaCost = 5000,
                            UiKey = "Полоска_Маны_Правая_Верхняя"
                        });


                    }


                    // === НАСТРОЙКА ГЛАВЫ РУНЫ ===
                    else if (chapId == "rune")
                    {
                        chapter.TabItemCode = "botaniastory:rune-*";

                        // ==========================================
                        //  ТИР 1: БАЗОВЫЕ РУНЫ (Элементы)
                        // ==========================================

                        // Руна Воды
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 0,
                            UiKey = "Аптекарь_Область_Правая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-water",
                            ApothecaryIngredients = new string[] {
                                  "botaniastory:manaitem-manapowder",
                                  "game:ingot-manasteel",
                                  "game:bone",             // Костная мука -> Кость
                                  "game:cattailtops",      // Сахарный тростник -> Верхушки рогоза
                                  "game:cattailroot"       // Удочка -> Корень рогоза (символ воды)
                              }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 0,
                            ManaCost = 5200,
                            UiKey = "Полоска_Маны_Алтарь_Правая_Нижняя"
                        });

                        // Руна Огня
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 1,
                            UiKey = "Аптекарь_Область_Левая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-fire",
                            ApothecaryIngredients = new string[] {
                                 "botaniastory:manaitem-manapowder",
                                 "game:ingot-manasteel",
                                 "game:mushroom-flyagaric-normal", // Адский нарост -> Мухомор
                                 "game:burnedbrick-*",        // Адский кирпич -> Обожженный кирпич
                                 "game:powder-sulfur"              // Порох -> Сера
                             }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 1,
                            ManaCost = 5200,
                            UiKey = "Полоска_Маны_Алтарь_Левая_Нижняя"
                        });

                        // Руна Земли
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 1,
                            UiKey = "Аптекарь_Область_Правая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-earth",
                            ApothecaryIngredients = new string[] {
                                  "botaniastory:manaitem-manapowder",
                                  "game:ingot-manasteel",
                                  "game:rock-granite",           // Камень -> Гранит
                                  "game:ore-bituminouscoal",        // Угольный блок -> Уголь
                                  "game:mushroom-almondmushroom-normal"  // Гриб -> Обычный гриб
                              }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 1,
                            ManaCost = 5200,
                            UiKey = "Полоска_Маны_Алтарь_Правая_Нижняя"
                        });

                        // Руна Воздуха
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 2,
                            UiKey = "Аптекарь_Область_Левая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-air",
                            ApothecaryIngredients = new string[] {
                                  "botaniastory:manaitem-manapowder",
                                  "game:ingot-manasteel",
                                  "botaniastory:manaitem-manaflax", // Нить -> Мананить
                                  "game:feather",                   // Перо
                                  "game:cloth-plain"                // Ковер -> Льняная ткань
                              }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 2,
                            ManaCost = 5200,
                            UiKey = "Полоска_Маны_Алтарь_Левая_Нижняя"
                        });

                        // Руна Маны
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 2,
                            UiKey = "Аптекарь_Область_Правая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-mana",
                            ApothecaryIngredients = new string[] {
                                 "game:ingot-manasteel",
                                 "game:ingot-manasteel",
                                 "game:ingot-manasteel",
                                 "game:ingot-manasteel",
                                 "game:ingot-manasteel",
                                 "botaniastory:manaitem-manaquartz" // Жемчуг маны -> Манакварц
                             }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 2,
                            ManaCost = 5200,
                            UiKey = "Полоска_Маны_Алтарь_Правая_Нижняя"
                        });

                        // ==========================================
                        // ТИР 2: РУНЫ СЕЗОНОВ
                        // ==========================================

                        // Руна Весны
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 3,
                            UiKey = "Аптекарь_Область_Левая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-spring",
                            ApothecaryIngredients = new string[] {
                                "botaniastory:rune-water",
                                "botaniastory:rune-fire",
                                "game:treeseed-oak",
                                "game:treeseed-oak",
                                "game:treeseed-oak",
                                "game:hay-normal-ud"
                            }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 3,
                            ManaCost = 8000,
                            UiKey = "Полоска_Маны_Алтарь_Левая_Нижняя"
                        });

                        // Руна Лета
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 3,
                            UiKey = "Аптекарь_Область_Правая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-summer",
                            ApothecaryIngredients = new string[] {
                                  "botaniastory:rune-earth",
                                  "botaniastory:rune-air",
                                  "game:sand-*",   // Песок
                                  "game:fat",           // Слизь -> Жир
                                  "game:fruit-cherry"   // Арбуз -> Вишня (или другая ягода)
                              }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 3,
                            ManaCost = 8000,
                            UiKey = "Полоска_Маны_Алтарь_Правая_Нижняя"
                        });

                        // Руна Осени
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 4,
                            UiKey = "Аптекарь_Область_Левая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-autumn",
                            ApothecaryIngredients = new string[] {
                                 "botaniastory:rune-fire",
                                 "botaniastory:rune-air",
                                 "game:treeseed-oak", // Листва
                                 "game:treeseed-oak",
                                 "game:treeseed-oak",
                                 "game:butterfly-dead-*",    // Паучий глаз -> Мертвая бабочка
                                 "game:pumpkin-fruit-4"     // Тыква
                             }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 4,
                            ManaCost = 8000,
                            UiKey = "Полоска_Маны_Алтарь_Левая_Нижняя"
                        });

                        // Руна Зимы
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 4,
                            UiKey = "Аптекарь_Область_Правая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-winter",
                            ApothecaryIngredients = new string[] {
                                 "botaniastory:rune-water",
                                 "botaniastory:rune-earth",
                                 "game:snowblock",
                                 "game:cloth-plain", // Шерсть -> Ткань
                                 "game:dough-*"    // Торт -> Тесто
                             }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 4,
                            ManaCost = 8000,
                            UiKey = "Полоска_Маны_Алтарь_Правая_Нижняя"
                        });

                        // ==========================================
                        // ТИР 3: РУНЫ ГРЕХОВ
                        // ==========================================

                        // Руна Похоти (Lust)
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 5,
                            UiKey = "Аптекарь_Область_Левая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-lust",
                            ApothecaryIngredients = new string[] {
                                  "botaniastory:manaitem-managear", // Манаалмаз -> Манашестерня
                                  "botaniastory:rune-summer",
                                  "botaniastory:rune-spring",
                                  "game:clearquartz", // Алмаз -> Чистый кварц
                                  "game:clearquartz"
                              }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 5,
                            ManaCost = 12000,
                            UiKey = "Полоска_Маны_Алтарь_Левая_Нижняя"
                        });

                        // Руна Обжорства (Gluttony)
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 5,
                            UiKey = "Аптекарь_Область_Правая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-gluttony",
                            ApothecaryIngredients = new string[] {
                                      "botaniastory:manaitem-managear",
                                      "botaniastory:rune-winter",
                                      "botaniastory:rune-autumn",
                                      "game:clearquartz", // Слеза гаста -> Чистый кварц
                                      "game:clearquartz"
                                  }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 5,
                            ManaCost = 12000,
                            UiKey = "Полоска_Маны_Алтарь_Правая_Нижняя"
                        });

                        // Руна Жадности (Greed)
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 6,
                            UiKey = "Аптекарь_Область_Левая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-greed",
                            ApothecaryIngredients = new string[] {
                             "botaniastory:manaitem-managear",
                             "botaniastory:rune-spring",
                             "botaniastory:rune-water",
                             "game:fat", // Слизь -> Жир
                             "game:fat"
                         }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 6,
                            ManaCost = 12000,
                            UiKey = "Полоска_Маны_Алтарь_Левая_Нижняя"
                        });

                        // Руна Лени (Sloth)
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 6,
                            UiKey = "Аптекарь_Область_Правая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-sloth",
                            ApothecaryIngredients = new string[] {
                                  "botaniastory:manaitem-managear",
                                  "botaniastory:rune-autumn",
                                  "botaniastory:rune-air",
                                  "game:ore-bituminouscoal", // Уголь
                                  "game:ore-bituminouscoal"
                              }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 6,
                            ManaCost = 12000,
                            UiKey = "Полоска_Маны_Алтарь_Правая_Нижняя"
                        });

                        // Руна Гнева (Wrath)
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 7,
                            UiKey = "Аптекарь_Область_Левая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-wrath",
                            ApothecaryIngredients = new string[] {
                              "botaniastory:manaitem-managear",
                              "botaniastory:rune-winter",
                              "botaniastory:rune-earth",
                              "game:powder-sulfur", // Огненный порошок -> Сера
                              "game:powder-sulfur"
                          }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 7,
                            ManaCost = 12000,
                            UiKey = "Полоска_Маны_Алтарь_Левая_Нижняя"
                        });

                        // Руна Зависти (Envy)
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 7,
                            UiKey = "Аптекарь_Область_Правая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-envy",
                            ApothecaryIngredients = new string[] {
                              "botaniastory:manaitem-managear",
                              "botaniastory:rune-winter",
                              "botaniastory:rune-water",
                              "game:butterfly-dead-*", // Паучий глаз -> Мертвая бабочка
                              "game:butterfly-dead-*"
                          }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 7,
                            ManaCost = 12000,
                            UiKey = "Полоска_Маны_Алтарь_Правая_Нижняя"
                        });

                        // Руна Гордыни (Pride)
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Apothecary",
                            Spread = 8,
                            UiKey = "Аптекарь_Область_Левая",
                            ApothecaryCenter = "botaniastory:runicaltar",
                            Output = "botaniastory:rune-pride",
                            ApothecaryIngredients = new string[] {
                                 "botaniastory:manaitem-managear",
                                 "botaniastory:rune-summer",
                                 "botaniastory:rune-fire",
                                 "game:ingot-gold", // Золотой слиток
                                 "game:ingot-gold"
                             }
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 8,
                            ManaCost = 12000,
                            UiKey = "Полоска_Маны_Алтарь_Левая_Нижняя"
                        });

                    }

                    // === НАСТРОЙКА ГЛАВЫ ПРЕДМЕТЫ ИЗ МАНАСТАЛИ ===
                    else if (chapId == "manaitem")
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
                            Output = "botaniastory:cleaverblade-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 3,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:cleaverblade-manasteel", null, null,
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
                            Output = "botaniastory:falxblade-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 3,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:falxblade-manasteel", null, null,
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
                            Output = "botaniastory:scytheblade-manasteel"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 6,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                                 "botaniastory:scytheblade-manasteel", null, null,
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

                    // === НАСТРОЙКА ГЛАВЫ АКСЕССУАРЫ ===
                    else if (chapId == "trinkets")
                    {
                        chapter.TabItemCode = "botaniastory:checkmark";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                                 null, null, null,
                                 null, null, null,
                                 null, null, null},
                            Output = "botaniastory:checkmark"
                        });
                    }

                    // === НАСТРОЙКА ГЛАВЫ ПИЛОНЫ ===
                    else if (chapId == "pylon")
                    {
                        chapter.TabItemCode = "botaniastory:pylon-mana";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 1,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                                 null, "game:ingot-terrasteel", null,
                                 "game:metalbit-terrasteel", "botaniastory:pylon-mana", "game:metalbit-terrasteel",
                                 null, "game:gear-temporal", null },
                            Output = "botaniastory:pylon-gaia"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                                 null, "game:ingot-gold", null,
                                 "game:ingot-manasteel", "botaniastory:manaitem-managear", "game:ingot-manasteel",
                                 null, "game:ingot-gold", null},
                            Output = "botaniastory:pylon-natura"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Левая_Нижняя",
                            Grid = new string[9] {
                                null, "game:metalbit-terrasteel", null,
                                "game:metalbit-terrasteel", "botaniastory:pylon-mana", "game:metalbit-terrasteel",
                                null, "game:gear-temporal", null },
                            Output = "botaniastory:pylon-mana"
                        });

                    }

                    // === НАСТРОЙКА ГЛАВЫ ВРАТА АЛЬФХЕЙМ ===
                    else if (chapId == "alfheimgates")
                    {
                        chapter.TabItemCode = "botaniastory:pylon-natura";

                        chapter.VisualizeStructure = "alfheimgates";
                        chapter.VisualizeSpread = 1;
                        chapter.VisualizeUiKey = "Кнопка_Визуализации_Альфхейм";

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Нижняя",
                            Grid = new string[9] {
                             "botaniastory:livingwood-normal", "game:metalbit-terrasteel",  "botaniastory:livingwood-normal",
                             "botaniastory:livingwood-normal", "game:metalbit-terrasteel",  "botaniastory:livingwood-normal",
                             "botaniastory:livingwood-normal", "game:metalbit-terrasteel",  "botaniastory:livingwood-normal"},
                            Output = "botaniastory:alfheimcore-off"
                        });

                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Grid",
                            Spread = 0,
                            UiKey = "Сетка_Правая_Верхняя",
                            Grid = new string[9] {
                                 null, "game:ingot-gold", null,
                                 "game:ingot-manasteel", "botaniastory:manaitem-managear", "game:ingot-manasteel",
                                 null, "game:ingot-gold", null},
                            Output = "botaniastory:pylon-natura"
                        });

                        chapter.Images.Add(new BookPageImage()
                        {
                            Path = "botaniastory:textures/gui/alfheimgates.png",
                            Spread = 1,
                            UiKey = "Картинка_Левая_Альфхейм"
                        });
                    }

                    // === НАСТРОЙКА ГЛАВЫ РЕСУРСЫ АЛЬФХЕЙМА ===
                    else if (chapId == "elfresources")
                    {
                        chapter.TabItemCode = "botaniastory:elvenglass-1";

                      
                      
                        //////////////////////////////////////////////
                        //0
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Alfheim",
                            Spread = 0,
                            UiKey = "Альфхейм_Область_Правая",
                            AlfheimInputs = new string[] { "game:ingot-manasteel" },
                            Output = "game:ingot-elementium"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 0,               
                            ManaCost = 1000,
                            UiKey = "Полоска_Маны_Правая_Альфхейм"
                        });
                            /////////////////////////1
                            chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Alfheim",
                            Spread = 1,
                            UiKey = "Альфхейм_Область_Левая",
                            AlfheimInputs = new string[] { "botaniastory:manaitem-managear" },
                            Output = "botaniastory:dragonstone"
                        });

                    chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 1,
                            ManaCost = 1000,
                            UiKey = "Полоска_Маны_Левая_Альфхейм"
                        });
                            //////////
                            chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Alfheim",
                            Spread = 1,
                            UiKey = "Альфхейм_Область_Правая",
                            AlfheimInputs = new string[] { "botaniastory:managlass" },
                            Output = "botaniastory:elvenglass-0"
                        });

                        chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 1,
                            ManaCost = 1000,
                            UiKey = "Полоска_Маны_Правая_Альфхейм"
                        });
                        /////////
                        chapter.Recipes.Add(new BookRecipe()
                        {
                            RecipeType = "Alfheim",
                            Spread = 2,
                            UiKey = "Альфхейм_Область_Левая",
                            AlfheimInputs = new string[] { "botaniastory:manaitem-manaquartz" },
                            Output = "botaniastory:fairydust"
                        });
                             /////
                             chapter.ManaBars.Add(new BookManaBar()
                        {
                            Spread = 2,
                            ManaCost = 1000,
                            UiKey = "Полоска_Маны_Левая_Альфхейм"
                        });
                    }



                    // Пытаемся загрузить до 30 страниц
                    for (int p = 1; p <= 30; p++)
                    {
                        // Ищем страницы
                        string pageKey = $"botaniastory:lexicon_{catId}_{chapId}_p{p}";
                        string pageText = Lang.Get(pageKey);

                        if (pageText != pageKey)
                        {
                            chapter.Pages.Add(pageText);
                        }
                        else if (p == 1 && chapter.Pages.Count == 0)
                        {
                            // Заглушка, если страниц вообще нет
                            chapter.Pages.Add($"Der Seitentext fehlt. Schlüssel in der lang-Datei:\n{pageKey}");
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