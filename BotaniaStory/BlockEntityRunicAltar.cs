using System;
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

        public int MaxBufferMana = 5000;
        public int CurrentMana = 0;
        public int TargetMana = 0;
        public bool HasLivingrock = false;
        private bool soundFullPlayed = false;
        private float lightningTimer = 0;
        private string currentRecipeResult = null;
        private RunicAltarRenderer renderer;

        private Dictionary<string, (int mana, List<string> items)> runeRecipes = new Dictionary<string, (int, List<string>)>
        {
            { "rune-water", (5000, new List<string> { "livingwood", "livingwood", "livingwood", "livingwood" }) }
        };

        public BlockEntityRunicAltar()
        {
            // Стандартная инициализация
            inventory = new InventoryGeneric(16, "runicaltar-inv", null);
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
            if (!path.StartsWith("rune-") && !path.Contains("livingwood") && !path.Contains("botania-manasteel") && !path.Contains("mysticalpetal"))
                return false;

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
                string found = checklist.Find(req => item.Contains(req));
                if (found != null) checklist.Remove(found);
                else { isMatch = false; break; }
            }

            if (isMatch && checklist.Count == 0)
            {
                TargetMana = recipe.Value.mana;
                currentRecipeResult = recipe.Key;

                // --- ИСПРАВЛЕННАЯ ЛОГИКА ЗВУКА ЗДЕСЬ ---
                if (Api.Side == EnumAppSide.Server)
                {
                    soundFullPlayed = false;
                    // Если мана уже была накоплена ДО того, как положили все предметы:
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
            if (CurrentMana < MaxBufferMana)
            {
                // --- ИСПРАВЛЕННАЯ ЛОГИКА ЗВУКА ЗДЕСЬ ---
                if (Api.Side == EnumAppSide.Server)
                {
                    long currentTime = Api.World.ElapsedMilliseconds;

   

                    // Звук "Готово", если есть активный рецепт и мы только что добили нужное количество маны
                    if (TargetMana > 0 && CurrentMana + amount >= TargetMana && !soundFullPlayed)
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

            float progress = Math.Min(1.0f, (float)CurrentMana / TargetMana);

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
            Vec3d startPos = Pos.ToVec3d().AddCopy(0.5, 1.1, 0.5);
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