using System.Collections.Generic;

namespace BotaniaStory.recipes
{
    public static class CatalystRegistry
    {
        // Хранилища рецептов
        public static Dictionary<string, string> AlchemyRecipes { get; private set; } = new Dictionary<string, string>();
        public static Dictionary<string, int> AlchemyManaCosts { get; private set; } = new Dictionary<string, int>();
        public static Dictionary<string, int> ConjurationRecipes { get; private set; } = new Dictionary<string, int>();

        // Статический конструктор 
        static CatalystRegistry()
        {
            InitializeTestRecipes();
        }

        private static void InitializeTestRecipes()
        {
            RegisterCyclicAlchemyRecipe(10000, "game:stick", "botaniastory:root", "game:plank-veryaged", "game:suportbeam-veryaged");

            ConjurationRecipes["game:stick"] = 10000;
            ConjurationRecipes["game:drygrass"] = 20000;
            ConjurationRecipes["game:clearquartz"] = 20000;
            ConjurationRecipes["game:ore-quartz"] = 20000;
            ConjurationRecipes["game:powder-iron-oxide"] = 20000;
            ConjurationRecipes["game:game:snowball-snow"] = 1000;
            ConjurationRecipes["game:clay-red"] = 20000;
            ConjurationRecipes["game:mortar"] = 20000;
        }

        // Умный циклический конструктор
        public static void RegisterCyclicAlchemyRecipe(int manaCost, params string[] items)
        {
            if (items.Length < 2) return;

            for (int i = 0; i < items.Length; i++)
            {
                string currentItem = items[i];
                // Замыкаем последний элемент на первый
                string nextItem = items[(i + 1) % items.Length];

                AlchemyRecipes[currentItem] = nextItem;
                AlchemyManaCosts[currentItem] = manaCost;
            }
        }
    }
}