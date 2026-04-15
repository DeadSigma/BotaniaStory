using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BlockEntityRunicAltar : BlockEntity
    {
        public InventoryGeneric inventory;

        // --- НОВЫЕ ПЕРЕМЕННЫЕ ДЛЯ БУФЕРА ---
        public int MaxBufferMana = 5000; // Максимальная вместимость алтаря
        public int CurrentMana = 0;        // Текущая мана в буфере
        private float lightningTimer = 0;
        public int TargetMana = 0;         // Сколько нужно для текущего рецепта (0 = нет рецепта)
        public bool HasLivingrock = false; // Положили ли мы жизнекамень?

        private Dictionary<string, (int mana, List<string> items)> runeRecipes = new Dictionary<string, (int, List<string>)>
        {
            // Тестовый рецепт: 4 Жизнедерева = Руна Воды
            { "rune-water", (5000, new List<string> { "livingwood", "livingwood", "livingwood", "livingwood" }) }
        };

        private string currentRecipeResult = null;
        private RunicAltarRenderer renderer;

        public BlockEntityRunicAltar()
        {
            inventory = new InventoryGeneric(16, "runicaltar-inv", null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inventory.LateInitialize("runicaltar-inv-" + Pos.ToString(), api);

            if (api is ICoreClientAPI capi)
            {
                renderer = new RunicAltarRenderer(Pos, capi, this);

                // Оставляем ТОЛЬКО Opaque:
                capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "runicaltar-render");

                RegisterGameTickListener(SpawnIdleParticles, 50);
            }
        }

        public bool TryAddItem(ItemSlot slot, IPlayer player)
        {
            // ЛОГИКА ЖИЗНЕКАМНЯ (КАТАЛИЗАТОР) - кладем в любой момент
            if (slot.Itemstack.Collectible.Code.Path.Contains("livingrock"))
            {
                if (HasLivingrock) return false; // Больше одного нельзя

                HasLivingrock = true;
                slot.TakeOut(1);
                slot.MarkDirty();
                MarkDirty(true);

                if (Api.Side == EnumAppSide.Client) renderer?.UpdateMeshes();
                Api.World.PlaySoundAt(new AssetLocation("game:sounds/block/stone"), Pos.X, Pos.Y, Pos.Z, player);
                return true;
            }

            // ОСТАЛЬНЫЕ ПРЕДМЕТЫ (ИНГРЕДИЕНТЫ)
            string itemPath = slot.Itemstack.Collectible.Code.Path;
            string[] allowedKeywords = new string[] { "livingwood", "botania-manasteel", "mysticalpetal" };

            bool isAllowed = itemPath.StartsWith("rune-");
            if (!isAllowed)
            {
                foreach (string keyword in allowedKeywords)
                {
                    if (itemPath.Contains(keyword)) { isAllowed = true; break; }
                }
            }

            if (!isAllowed) return false;

            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].Empty)
                {
                    inventory[i].Itemstack = slot.TakeOut(1);
                    slot.MarkDirty();
                    inventory[i].MarkDirty();
                    CheckRecipe(); // Пересчитываем TargetMana
                    MarkDirty(true);
                    Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/throw"), Pos.X, Pos.Y, Pos.Z, player);
                    if (Api.Side == EnumAppSide.Client) renderer?.UpdateMeshes();
                    return true;
                }
            }
            return false;
        }

        public bool TryTakeItem(IPlayer player)
        {
            // Сначала пытаемся забрать Жизнекамень
            if (HasLivingrock)
            {
                ItemStack rock = new ItemStack(Api.World.GetBlock(new AssetLocation("botaniastory:livingrock")));
                if (!player.InventoryManager.TryGiveItemstack(rock, true))
                {
                    Api.World.SpawnItemEntity(rock, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
                }
                HasLivingrock = false;
                MarkDirty(true);
                if (Api.Side == EnumAppSide.Client) renderer?.UpdateMeshes();
                return true;
            }

            // Если камня нет, забираем ингредиенты (начиная с последнего)
            for (int i = inventory.Count - 1; i >= 0; i--)
            {
                if (!inventory[i].Empty)
                {
                    ItemStack stackToTake = inventory[i].TakeOut(1);
                    if (!player.InventoryManager.TryGiveItemstack(stackToTake, true))
                    {
                        Api.World.SpawnItemEntity(stackToTake, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
                    }
                    inventory[i].MarkDirty();
                    CheckRecipe(); // Пересчитываем TargetMana (вдруг мы сломали рецепт)
                    MarkDirty(true);
                    if (Api.Side == EnumAppSide.Client) renderer?.UpdateMeshes();
                    return true;
                }
            }
            return false;
        }

        private void CheckRecipe()
        {
            TargetMana = 0;
            currentRecipeResult = null;

            List<string> currentItems = new List<string>();
            foreach (var slot in inventory)
            {
                if (!slot.Empty) currentItems.Add(slot.Itemstack.Collectible.Code.Path);
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
                        string foundMatch = checklist.Find(req => item.Contains(req));
                        if (foundMatch != null)
                        {
                            checklist.Remove(foundMatch);
                        }
                        else
                        {
                            isMatch = false;
                            break;
                        }
                    }

                    if (isMatch && checklist.Count == 0)
                    {
                        TargetMana = recipe.Value.mana;
                        currentRecipeResult = recipe.Key;
                        break;
                    }
                }
            }
        }

        public bool TryCompleteCrafting(IPlayer player)
        {
            // Отменяем крафт, если условия не соблюдены (тихо, без спама в чат)
            if (TargetMana == 0 || CurrentMana < TargetMana || !HasLivingrock || currentRecipeResult == null)
                return false;

            if (Api.Side == EnumAppSide.Server)
            {
                // Создаем руну
                Item runeItem = Api.World.GetItem(new AssetLocation("botaniastory", currentRecipeResult));
                if (runeItem != null)
                {
                    Api.World.SpawnItemEntity(new ItemStack(runeItem), Pos.ToVec3d().Add(0.5, 1.2, 0.5));
                }

                // Возвращаем руны-ингредиенты, если они были в рецепте
                for (int i = 0; i < inventory.Count; i++)
                {
                    if (!inventory[i].Empty)
                    {
                        string path = inventory[i].Itemstack.Collectible.Code.Path;
                        if (path.StartsWith("rune-"))
                        {
                            Api.World.SpawnItemEntity(inventory[i].Itemstack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
                        }
                        inventory[i].Itemstack = null;
                        inventory[i].MarkDirty();
                    }
                }
            }

            //  ТРАТИМ МАНУ ИЗ БУФЕРА ===
            CurrentMana -= TargetMana;
            if (CurrentMana < 0) CurrentMana = 0;

            TargetMana = 0;
            HasLivingrock = false;
            currentRecipeResult = null;
            MarkDirty(true);

            if (Api.Side == EnumAppSide.Client) renderer?.UpdateMeshes();
            Api.World.PlaySoundAt(new AssetLocation("botaniastory:sounds/runic_altar_craft"), Pos.X, Pos.Y, Pos.Z);

            return true;
        }

        // Вызывается из Распространителя маны
        public void ReceiveMana(int amount)
        {
            // Теперь принимаем ману всегда, пока не заполним буфер!
            if (CurrentMana < MaxBufferMana)
            {
                CurrentMana += amount;
                if (CurrentMana > MaxBufferMana) CurrentMana = MaxBufferMana;
                MarkDirty(true);
            }
        }


        // Красивые частицы вокруг алтаря
        private void SpawnIdleParticles(float dt)
        {
            if (Api.Side == EnumAppSide.Server) return; // Строго только для клиента!

            if (TargetMana > 0)
            {
                float progress = (float)CurrentMana / TargetMana;
                if (progress > 1.0f) progress = 1.0f;

                //  Стандартное свечение (glow)
                SimpleParticleProperties glow = new SimpleParticleProperties(
                    1, 1, ColorUtil.ToRgba(200, 64, 255, 200),
                    Pos.ToVec3d().Add(0.5, 1.1, 0.5), Pos.ToVec3d().Add(0.5, 1.1, 0.5),
                    new Vec3f(-0.01f, -0.01f, -0.01f), new Vec3f(0.01f, 0.01f, 0.01f),
                    0.5f, 0, 0.2f + progress * 0.5f, 0.5f, EnumParticleModel.Quad
                );
                glow.VertexFlags = 128;
                Api.World.SpawnParticles(glow);

                //  ЭФФЕКТ ПЕРЕГРУЗКИ (Молнии)
                if (progress >= 1.0f)
                {
                    lightningTimer += dt;

                    // Если алтарь полон, каждые 0.3 - 0.7 секунды бьем большой молнией
                    if (lightningTimer > 0.4f)
                    {
                        // Шанс 60% (повысил, чтобы ты точно увидел) на каждый тик таймера
                        if (Api.World.Rand.NextDouble() > 0.4)
                        {
                            SpawnCraftingLightning();
                        }
                        lightningTimer = 0;
                    }
                }
            }
        }

        // ВЫЗОВ МОЛНИИ
        private void SpawnCraftingLightning()
        {
            if (Api.Side == EnumAppSide.Server) return;

            // Начинаем чуть ниже, прямо из лежащей руны/катализатора
            Vec3d startPos = Pos.ToVec3d().AddCopy(0.5, 1.1, 0.5);

            // Спавним от 2 до 4 молний одновременно для эффекта "пучка" энергии!
            int count = 2 + Api.World.Rand.Next(3);
            for (int i = 0; i < count; i++)
            {
                renderer?.AddLightning(startPos);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("currentMana", CurrentMana);
            tree.SetInt("targetMana", TargetMana);
            tree.SetBool("hasLivingrock", HasLivingrock);
            if (currentRecipeResult != null) tree.SetString("recipe", currentRecipeResult);
            inventory.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            CurrentMana = tree.GetInt("currentMana");
            TargetMana = tree.GetInt("targetMana");
            HasLivingrock = tree.GetBool("hasLivingrock");
            currentRecipeResult = tree.HasAttribute("recipe") ? tree.GetString("recipe") : null;
            inventory.FromTreeAttributes(tree);

            if (worldForResolving != null)
            {
                inventory.AfterBlocksLoaded(worldForResolving);
            }

            if (Api?.Side == EnumAppSide.Client)
            {
                renderer?.UpdateMeshes();
            }
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
                if (!inventory[i].Empty)
                {
                    Api.World.SpawnItemEntity(inventory[i].Itemstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }

            if (HasLivingrock)
            {
                Block rockBlock = Api.World.GetBlock(new AssetLocation("botaniastory:livingrock"));
                if (rockBlock != null)
                {
                    Api.World.SpawnItemEntity(new ItemStack(rockBlock), Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }

            base.OnBlockBroken(byPlayer);
        }
    }
}