using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BlockEntityRunicAltar : BlockEntity, IManaReceiver
    {
        public InventoryGeneric inventory;

        public int MaxBufferMana = 12000;
        public int CurrentMana = 0;
        public int TargetMana = 0;
        public bool HasLivingrock = false;
        private bool soundFullPlayed = false;
        private float lightningTimer = 0;
        private string currentRecipeResult = null;
        public string LastCraftedRecipe = null;
        public long LastCraftTime = 0;
        private RunicAltarRenderer renderer;

        private Dictionary<string, (int mana, List<string> items)> runeRecipes = new Dictionary<string, (int, List<string>)>
{
    // === Базовые руны (Стихии и Мана) - 5200 маны ===
    { "rune-water", (5200, new List<string> { "manaitem-manapowder", "ingot-manasteel", "bone", "cattailtops", "cattailroot" }) },
    { "rune-fire", (5200, new List<string> { "manaitem-manapowder", "ingot-manasteel", "mushroom-flyagaric-normal", "burnedbrick-*", "powder-sulfur" }) },
    { "rune-earth", (5200, new List<string> { "manaitem-manapowder", "ingot-manasteel", "rock-granite", "ore-bituminouscoal", "mushroom-almondmushroom-normal" }) },
    { "rune-air", (5200, new List<string> { "manaitem-manapowder", "ingot-manasteel", "manaitem-manaflax", "feather", "cloth-plain" }) },
    { "rune-mana", (5200, new List<string> { "ingot-manasteel", "ingot-manasteel", "ingot-manasteel", "ingot-manasteel", "ingot-manasteel", "manaitem-manaquartz" }) },

    // === Руны Сезонов - 8000 маны ===
    { "rune-spring", (8000, new List<string> { "rune-water", "rune-fire", "treeseed-oak", "treeseed-oak", "treeseed-oak", "hay-normal-ud" }) },
    { "rune-summer", (8000, new List<string> { "rune-earth", "rune-air", "sand-*", "fat", "fruit-cherry" }) },
    { "rune-autumn", (8000, new List<string> { "rune-fire", "rune-air", "treeseed-oak", "treeseed-oak", "treeseed-oak", "butterfly-dead-*", "pumpkin-fruit-4" }) },
    { "rune-winter", (8000, new List<string> { "rune-water", "rune-earth", "snowblock", "cloth-plain", "dough-" }) },

    // === Руны Смертных Грехов - 12000 маны ===
    { "rune-lust", (12000, new List<string> { "manaitem-managear", "rune-summer", "rune-spring", "clearquartz", "clearquartz" }) },
    { "rune-gluttony", (12000, new List<string> { "manaitem-managear", "rune-winter", "rune-autumn", "clearquartz", "clearquartz" }) },
    { "rune-greed", (12000, new List<string> { "manaitem-managear", "rune-spring", "rune-water", "fat", "fat" }) },
    { "rune-sloth", (12000, new List<string> { "manaitem-managear", "rune-autumn", "rune-air", "ore-bituminouscoal", "ore-bituminouscoal" }) },
    { "rune-wrath", (12000, new List<string> { "manaitem-managear", "rune-winter", "rune-earth", "powder-sulfur", "powder-sulfur" }) },
    { "rune-envy", (12000, new List<string> { "manaitem-managear", "rune-winter", "rune-water", "butterfly-dead-*", "butterfly-dead-*" }) },
    { "rune-pride", (12000, new List<string> { "manaitem-managear", "rune-summer", "rune-fire", "ingot-gold", "ingot-gold" }) }
};

        public BlockEntityRunicAltar()
        {
            // Стандартная инициализация
            inventory = new InventoryGeneric(16, "runicaltar-inv", null);


        }
        public bool IsFull()
        {
            return CurrentMana >= MaxBufferMana;
        }

        public int GetAvailableSpace()
        {
            return MaxBufferMana - CurrentMana;
        }
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inventory.LateInitialize("runicaltar-inv-" + Pos.ToString(), api);

            if (api is ICoreClientAPI capi)
            {
                renderer = new RunicAltarRenderer(Pos, capi, this);
                capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "runicaltar-render");
                renderer.UpdateMeshes();
                RegisterGameTickListener(SpawnIdleParticles, 50);
            }
        }

        private void PlayAltarSound(string soundName)
        {
            if (Api.Side == EnumAppSide.Server)
            {
                var sapi = Api as Vintagestory.API.Server.ICoreServerAPI;
                if (sapi != null)
                {
                    var channel = sapi.Network.GetChannel("botanianetwork");
                    channel.BroadcastPacket(new PlayManaSoundPacket()
                    {
                        Position = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5),
                        SoundName = soundName
                    });
                }
            }
        }

       
        // БЕЗОПАСНОЕ СОХРАНЕНИЕ И ЗАГРУЗКА 

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("currentMana", CurrentMana);
            tree.SetInt("targetMana", TargetMana);
            tree.SetBool("hasLivingrock", HasLivingrock);
            if (currentRecipeResult != null) tree.SetString("recipe", currentRecipeResult);

            // Сохраняем инвентарь стандартным методом
            inventory.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            try
            {
                CurrentMana = tree.GetInt("currentMana");
                TargetMana = tree.GetInt("targetMana");
                HasLivingrock = tree.GetBool("hasLivingrock");
                currentRecipeResult = tree.GetString("recipe", null);

                // Пытаемся загрузить предметы
                inventory.FromTreeAttributes(tree);
                if (worldForResolving != null)
                {
                    inventory.AfterBlocksLoaded(worldForResolving);
                }
            }
            catch (Exception ex)
            {
                // Если произойдет ошибка, алтарь НЕ удалится из мира!
                // Мы увидим точную причину в логах (server-main.txt или client-main.txt)
                Api?.Logger.Error($"[BotaniaStory] Сбой загрузки алтаря по координатам {Pos}: {ex}");
            }

            if (Api?.Side == EnumAppSide.Client)
            {
                renderer?.UpdateMeshes();
            }
        }

        // ========================================================
        // ЛОГИКА ПРЕДМЕТОВ
        // ========================================================
        public bool TryAddItem(ItemSlot slot, IPlayer player)
        {
            if (slot.Itemstack.Collectible.Code.Path.Contains("livingrock"))
            {
                if (HasLivingrock) return false;
                HasLivingrock = true;
                slot.TakeOut(1);
                slot.MarkDirty();
                UpdateState(player);
                return true;
            }

            string path = slot.Itemstack.Collectible.Code.Path;

            if (!path.StartsWith("rune-") &&
                !path.StartsWith("manaitem-") &&
                !path.StartsWith("ingot-") &&
                !path.StartsWith("cattail") &&
                !path.StartsWith("mushroom-") &&
                !path.StartsWith("burnedbrick-") &&
                !path.StartsWith("sand-") &&
                !path.StartsWith("butterfly-dead-") &&
                !path.StartsWith("dough-") &&
                path != "bone" &&
                path != "powder-sulfur" &&
                path != "rock-granite" &&
                path != "ore-bituminouscoal" &&
                path != "feather" &&
                path != "cloth-plain" &&
                path != "treeseed-oak" &&
                path != "hay-normal-ud" &&
                path != "fat" &&
                path != "fruit-cherry" &&
                path != "treeseed-oak" &&
                path != "pumpkin-fruit-4" &&
                path != "snowblock" &&
                path != "clearquartz")
            {
                return false;
            }

            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].Empty)
                {
                    inventory[i].Itemstack = slot.TakeOut(1);
                    slot.MarkDirty();
                    inventory[i].MarkDirty();
                    CheckRecipe();
                    UpdateState(player);
                    return true;
                }
            }
            return false;
        }

        public bool TryTakeItem(IPlayer player)
        {
            if (HasLivingrock)
            {
                GiveOrDropItem(player, new ItemStack(Api.World.GetBlock(new AssetLocation("botaniastory:livingrock"))));
                HasLivingrock = false;
                UpdateState(player);
                return true;
            }

            for (int i = inventory.Count - 1; i >= 0; i--)
            {
                if (!inventory[i].Empty)
                {
                    GiveOrDropItem(player, inventory[i].TakeOut(1));
                    inventory[i].MarkDirty();
                    CheckRecipe();
                    UpdateState(player);
                    return true;
                }
            }
            return false;
        }

        public bool TryAutoCraft(IPlayer player)
        {
            // 1. Проверяем базовые условия: был ли рецепт и прошло ли меньше 20 секунд (20000 мс)
            if (LastCraftedRecipe == null) return false;
            long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (currentTime - LastCraftTime > 20000) return false;

            // 2. Автокрафт работает ТОЛЬКО если алтарь полностью пуст
            for (int i = 0; i < inventory.Count; i++)
            {
                if (!inventory[i].Empty) return false;
            }

            // 3. Достаем рецепт и пытаемся выложить предметы
            if (runeRecipes.TryGetValue(LastCraftedRecipe, out var recipe))
            {
                // Сначала симулируем: есть ли у игрока ВСЕ нужные предметы?
                if (CheckAndConsumePlayerItems(player, recipe.items, true))
                {
                    // Если есть — физически забираем предметы в алтарь
                    CheckAndConsumePlayerItems(player, recipe.items, false);

                    // Заставляем алтарь проверить рецепт и посчитать ману
                    CheckRecipe();
                    UpdateState(player);

                    // Обновляем таймер, чтобы игрок мог "спамить" автокрафт несколько раз подряд
                    LastCraftTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    return true;
                }
            }
            return false;
        }

        private bool CheckAndConsumePlayerItems(IPlayer player, List<string> requirements, bool simulate)
        {
            List<string> remainingItems = new List<string>(requirements);
            Dictionary<ItemSlot, int> itemsToTake = new Dictionary<ItemSlot, int>();

            // Ищем предметы по всем открытым инвентарям игрока (хотбар + рюкзаки)
            foreach (var inv in player.InventoryManager.OpenedInventories)
            {
                foreach (var slot in inv)
                {
                    if (slot.Empty) continue;
                    string path = slot.Itemstack.Collectible.Code.Path;
                    int available = slot.StackSize;

                    // Пытаемся применить этот стак к оставшимся требованиям рецепта
                    for (int i = remainingItems.Count - 1; i >= 0; i--)
                    {
                        if (available <= 0) break;

                        string req = remainingItems[i];

                        // Умная проверка: если требование заканчивается на "*", проверяем начало строки. Иначе обычный Contains.
                        bool isMatch = req.EndsWith("*") ? path.StartsWith(req.TrimEnd('*')) : path.Contains(req);

                        if (isMatch)
                        {
                            remainingItems.RemoveAt(i);
                            available--;

                            if (itemsToTake.ContainsKey(slot)) itemsToTake[slot]++;
                            else itemsToTake[slot] = 1;
                        }
                    }
                }
            }

            // Если список требований не опустел — предметов не хватает
            if (remainingItems.Count > 0) return false;

            // Если это была лишь проверка (simulate = true), останавливаемся и рапортуем об успехе
            if (simulate) return true;

            // --- ФИЗИЧЕСКИЙ ПЕРЕНОС ПРЕДМЕТОВ НА АЛТАРЬ ---
            int altarSlotIndex = 0;
            foreach (var kvp in itemsToTake)
            {
                ItemSlot playerSlot = kvp.Key;
                int amountToTake = kvp.Value;

                for (int i = 0; i < amountToTake; i++)
                {
                    if (altarSlotIndex < inventory.Count)
                    {
                        inventory[altarSlotIndex].Itemstack = playerSlot.TakeOut(1);
                        inventory[altarSlotIndex].MarkDirty();
                        altarSlotIndex++;
                    }
                }
                playerSlot.MarkDirty();
            }

            return true;
        }

        private void GiveOrDropItem(IPlayer player, ItemStack stack)
        {
            if (!player.InventoryManager.TryGiveItemstack(stack, true))
            {
                Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
            }
        }

        private void UpdateState(IPlayer player)
        {
            MarkDirty(true);
            if (player != null) Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/throw"), Pos.X, Pos.Y, Pos.Z, player);
            if (Api.Side == EnumAppSide.Client) renderer?.UpdateMeshes();
        }

        // ========================================================
        // ЛОГИКА КРАФТА И МАНЫ
        // ========================================================
        private void CheckRecipe()
        {
            TargetMana = 0;
            currentRecipeResult = null;

            List<string> currentItems = new List<string>();
            foreach (var slot in inventory)
            {
                if (!slot.Empty && slot.Itemstack != null)
                    currentItems.Add(slot.Itemstack.Collectible.Code.Path);
            }

            if (currentItems.Count == 0) return;

            foreach (var recipe in runeRecipes)
            {
                if (currentItems.Count == recipe.Value.items.Count)
                {
                    List<string> checklist = new List<string>(recipe.Value.items);
                    bool isMatch = true;

                    foreach (string item in currentItems)
                    {
                        // ИСПРАВЛЕННАЯ СТРОКА: Умная проверка с учетом окончания на "*"
                        string found = checklist.Find(req => req.EndsWith("*") ? item.StartsWith(req.TrimEnd('*')) : item.Contains(req));

                        if (found != null) checklist.Remove(found);
                        else { isMatch = false; break; }
                    }

                    if (isMatch && checklist.Count == 0)
                    {
                        TargetMana = recipe.Value.mana;
                        currentRecipeResult = recipe.Key;

                        if (Api.Side == EnumAppSide.Server)
                        {
                            soundFullPlayed = false; // Обязательно сбрасываем флаг для нового рецепта

                            // Если маны УЖЕ достаточно для этого рецепта в момент его выкладывания
                            if (CurrentMana >= TargetMana)
                            {
                                PlayAltarSound("runic_altar_full");
                                soundFullPlayed = true;
                            }
                        }
                        break;
                    }
                }
            }
        }

        public bool TryCompleteCrafting(IPlayer player)
        {
            if (TargetMana == 0 || CurrentMana < TargetMana || !HasLivingrock || currentRecipeResult == null) return false;

            if (Api.Side == EnumAppSide.Server)
            {
                Item runeItem = Api.World.GetItem(new AssetLocation("botaniastory", currentRecipeResult));
                if (runeItem != null) Api.World.SpawnItemEntity(new ItemStack(runeItem), Pos.ToVec3d().Add(0.5, 1.2, 0.5));

                for (int i = 0; i < inventory.Count; i++)
                {
                    if (!inventory[i].Empty)
                    {
                        if (inventory[i].Itemstack.Collectible.Code.Path.StartsWith("rune-"))
                        {
                            Api.World.SpawnItemEntity(inventory[i].Itemstack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
                        }
                        inventory[i].Itemstack = null;
                        inventory[i].MarkDirty();
                    }
                }
            }

            // === ВОТ ЗДЕСЬ АЛТАРЬ ЗАПОМИНАЕТ УСПЕШНЫЙ КРАФТ ===
            LastCraftedRecipe = currentRecipeResult;
            LastCraftTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // А теперь сбрасываем состояние алтаря
            CurrentMana = Math.Max(0, CurrentMana - TargetMana);
            TargetMana = 0;
            HasLivingrock = false;
            currentRecipeResult = null;

            MarkDirty(true);
            if (Api.Side == EnumAppSide.Client)
            {
                renderer?.UpdateMeshes();
            }

            if (Api.Side == EnumAppSide.Server)
            {
                PlayAltarSound("runic_altar_craft");
            }

            return true;
        }

        public void ReceiveMana(int amount)
        {
            // Алтарь принимает ману всегда, пока не заполнит свой глобальный буфер
            if (CurrentMana < MaxBufferMana)
            {
                if (Api.Side == EnumAppSide.Server)
                {
                    // Проверяем, пересекаем ли мы порог нужной маны ИМЕННО СЕЙЧАС
                    if (TargetMana > 0 && CurrentMana < TargetMana && (CurrentMana + amount) >= TargetMana && !soundFullPlayed)
                    {
                        PlayAltarSound("runic_altar_full");
                        soundFullPlayed = true;
                    }
                }

                // Физическое начисление маны
                CurrentMana = Math.Min(MaxBufferMana, CurrentMana + amount);
                MarkDirty(true);
            }
        }

        // ========================================================
        // ВИЗУАЛЫ И ОЧИСТКА
        // ========================================================
        private void SpawnIdleParticles(float dt)
        {
            if (Api.Side == EnumAppSide.Server || TargetMana <= 0) return;

            // Считаем прогресс. Если CurrentMana больше TargetMana, он будет > 1.0f
            float progress = (float)CurrentMana / TargetMana;

            if (Api.World.Rand.NextDouble() > 0.3)
            {
                Vec3d offset = new Vec3d(
                    (Api.World.Rand.NextDouble() - 0.5) * 1.2,
                    0.1,
                    (Api.World.Rand.NextDouble() - 0.5) * 1.2
                );

                // Базовая позиция для частицы
                Vec3d startPos = Pos.ToVec3d().Add(0.5, 1.0, 0.5).Add(offset);

                SimpleParticleProperties idleSpark = new SimpleParticleProperties(
                    1, 2, // Количество
                    ColorUtil.ToRgba(200, 255, 255, 100), // Бирюзово-голубой
                    startPos, // MinPos (Где спавним)
                    new Vec3d(0, 0, 0), // AddPos - КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: НУЛЕВОЙ разброс!
                    new Vec3f(-0.1f, 0.2f, -0.1f), // MinVelocity
                    new Vec3f(0.2f, 0.3f, 0.2f), // AddVelocity
                    1.0f + (float)Api.World.Rand.NextDouble(), // Жизнь
                    -0.02f, // Гравитация (взлетают вверх)
                    0.05f, 0.1f, // Мелкий размер
                    EnumParticleModel.Quad
                );
                idleSpark.VertexFlags = 128; // Свечение
                Api.World.SpawnParticles(idleSpark);
            }

            // Молнии при полной мане
            if (progress >= 1.0f)
            {
                lightningTimer += dt;
                if (lightningTimer > 0.4f)
                {
                    if (Api.World.Rand.NextDouble() > 0.4) SpawnCraftingLightning();
                    lightningTimer = 0;
                }
            }
        }

        private void SpawnCraftingLightning()
        {
            Vec3d startPos = Pos.ToVec3d().AddCopy(0.5, 1.0, 0.5);
            int count = 2 + Api.World.Rand.Next(3);
            for (int i = 0; i < count; i++) renderer?.AddLightning(startPos);
        }

        

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            renderer?.Dispose();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            for (int i = 0; i < inventory.Count; i++)
            {
                if (!inventory[i].Empty) Api.World.SpawnItemEntity(inventory[i].Itemstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
            if (HasLivingrock)
            {
                Block rockBlock = Api.World.GetBlock(new AssetLocation("botaniastory:livingrock"));
                if (rockBlock != null) Api.World.SpawnItemEntity(new ItemStack(rockBlock), Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
            base.OnBlockBroken(byPlayer);
        }

    }
}