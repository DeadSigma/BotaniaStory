using System.Collections.Generic;

namespace BotaniaStory.util
{
    // Новый класс для хранения полных данных о рецепте
    public class AlchemyRecipe
    {
        public string OutputCode { get; set; }
        public int OutputAmount { get; set; }
        public int InputAmount { get; set; }
        public int ManaCost { get; set; }

        public AlchemyRecipe(string outputCode, int outputAmount, int inputAmount, int manaCost)
        {
            OutputCode = outputCode;
            OutputAmount = outputAmount;
            InputAmount = inputAmount;
            ManaCost = manaCost;
        }
    }

    public static class CatalystRegistry
    {
        // Заменяем два словаря на один, который хранит объект рецепта
        public static Dictionary<string, AlchemyRecipe> AlchemyRecipes { get; private set; } = new Dictionary<string, AlchemyRecipe>();

        public static Dictionary<string, int> ConjurationRecipes { get; private set; } = new Dictionary<string, int>();

        static CatalystRegistry()
        {
            InitializeTestRecipes();
        }

        private static void InitializeTestRecipes()
        {
            RegisterCyclicAlchemyRecipe(10000, "game:stick", "botaniastory:root", "game:plank-veryaged", "game:supportbeam-veryaged");

            RegisterAlchemyRecipe("game:rot", 3, "game:compost", 1, 5000);
            RegisterAlchemyRecipe("game:hide-raw-small", 1, "game:leather-normal-plain", 1, 5000);
            RegisterAlchemyRecipe("game:hide-raw-medium", 1, "game:leather-normal-plain", 2, 7000);
            RegisterAlchemyRecipe("game:hide-raw-large", 1, "game:leather-normal-plain", 3, 10000);
            RegisterAlchemyRecipe("game:hide-raw-huge", 1, "game:leather-normal-plain", 5, 13000);
            RegisterAlchemyRecipe("game:hide-raw-bear-", 1, "game:leather-normal-plain", 5, 15000);
            RegisterAlchemyRecipe("game:compost", 3, "game:saltpeter", 1, 10000);
            RegisterAlchemyRecipe("game:saltpeter", 3, "game:potash", 1, 10000);
            RegisterAlchemyRecipe("game:vegetable-onion", 6, "game:powder-sulfur", 1, 10000);
            RegisterAlchemyRecipe("game:vegetable-cabbage", 2, "game:powder-sulfur", 1, 10000);
            RegisterAlchemyRecipe("game:potash", 4, "game:stone-halite", 1, 10000);
            RegisterAlchemyRecipe("game:charcoal", 2, "game:ore-bituminouscoal", 1, 10000);


            ConjurationRecipes["game:stick"] = 5000;
            ConjurationRecipes["game:cattailroot"] = 10000;
            ConjurationRecipes["game:drygrass"] = 5000;
            ConjurationRecipes["game:clearquartz"] = 20000;
            ConjurationRecipes["game:ore-quartz"] = 20000;
            ConjurationRecipes["game:powder-iron-oxide"] = 20000;
            ConjurationRecipes["game:game:snowball-snow"] = 1000;
            ConjurationRecipes["game:clay-red"] = 20000;
            ConjurationRecipes["game:ore-lignite"] = 20000;
            ConjurationRecipes["game:ore-bituminouscoal"] = 20000;
            ConjurationRecipes["game:ore-anthracite"] = 20000;
            ConjurationRecipes["game:peatbrick"] = 10000;

        }

        // Новый метод для удобной регистрации одиночных рецептов с количеством
        public static void RegisterAlchemyRecipe(string inputCode, int inputAmount, string outputCode, int outputAmount, int manaCost)
        {
            AlchemyRecipes[inputCode] = new AlchemyRecipe(outputCode, outputAmount, inputAmount, manaCost);
        }

        // Обновляем старый метод, чтобы он работал с новым классом (по умолчанию 1 к 1)
        public static void RegisterCyclicAlchemyRecipe(int manaCost, params string[] items)
        {
            if (items.Length < 2) return;

            for (int i = 0; i < items.Length; i++)
            {
                string currentItem = items[i];
                string nextItem = items[(i + 1) % items.Length];

                // Циклические рецепты остаются 1 к 1
                AlchemyRecipes[currentItem] = new AlchemyRecipe(nextItem, 1, 1, manaCost);
            }
        }
    }
}