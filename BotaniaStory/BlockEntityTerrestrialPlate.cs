using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory
{
    public class BlockEntityTerrestrialPlate : BlockEntity, IManaReceiver
    {
        public InventoryGeneric inventory;
        public int CurrentMana = 0;
        public const int MaxManaRequired = 250000; // Четверть базового бассейна

        public bool IsStructureValid = false;
        public bool IsCrafting = false;
        private int lastClientMana = 0;
        private PlateCraftingRenderer particleRenderer;

        public BlockEntityTerrestrialPlate()
        {
            // 3 слота для: Манакварц, Манашестерня, Манасталь
            inventory = new InventoryGeneric(3, "terrestrialplate-0", null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inventory.LateInitialize("terrestrialplate-1", api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 250);
            }
            else if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                particleRenderer = new PlateCraftingRenderer(capi, Pos, this);
                capi.Event.RegisterRenderer(particleRenderer, EnumRenderStage.Opaque, "terrestrialplate");
                RegisterGameTickListener(UpdateClientParticles, 50); // Тик для частиц
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            inventory.DropAll(Pos.ToVec3d().AddCopy(0.5, 0.5, 0.5));
            if (Api is ICoreClientAPI capi && particleRenderer != null)
            {
                capi.Event.UnregisterRenderer(particleRenderer, EnumRenderStage.Opaque);
                particleRenderer.Dispose();
            }
        }

        // --- ЛОГИКА ВЗАИМОДЕЙСТВИЯ (ИНВЕНТАРЬ) ---
        public bool OnInteract(IPlayer byPlayer)
        {
            CheckStructure();
            if (!IsStructureValid)
            {
                if (Api.Side == EnumAppSide.Client)
                    ((ICoreClientAPI)Api).ShowChatMessage("Конструкция плиты не завершена или неверна!");
                return false;
            }

            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            // 1. Пытаемся забрать предмет, если рука пуста
            if (hotbarSlot.Empty)
            {
                for (int i = 2; i >= 0; i--) // Забираем с конца
                {
                    if (!inventory[i].Empty)
                    {
                        byPlayer.InventoryManager.TryGiveItemstack(inventory[i].TakeOutWhole());
                        inventory[i].MarkDirty();
                        CheckRecipeState();
                        return true;
                    }
                }
                return false;
            }

            // 2. Пытаемся положить предмет
            string code = hotbarSlot.Itemstack.Collectible.Code.ToString();
            bool isValidItem = code == "botaniastory:manaitem-manaquartz" ||
                               code == "botaniastory:manaitem-managear" ||
                               code == "game:ingot-manasteel";

            if (isValidItem && !IsCrafting) // Нельзя докладывать, если крафт уже идет
            {
                for (int i = 0; i < 3; i++)
                {
                    if (inventory[i].Empty)
                    {
                        inventory[i].Itemstack = hotbarSlot.TakeOut(1);
                        inventory[i].MarkDirty();
                        hotbarSlot.MarkDirty();
                        CheckRecipeState();
                        return true;
                    }
                }
            }

            return false;
        }

        private void CheckRecipeState()
        {
            // Проверяем, есть ли все 3 уникальных нужных предмета
            bool hasQuartz = false, hasGear = false, hasSteel = false;

            for (int i = 0; i < 3; i++)
            {
                if (inventory[i].Empty) continue;
                string code = inventory[i].Itemstack.Collectible.Code.ToString();
                if (code == "botaniastory:manaitem-manaquartz") hasQuartz = true;
                if (code == "botaniastory:manaitem-managear") hasGear = true;
                if (code == "game:ingot-manasteel") hasSteel = true;
            }

            bool wasCrafting = IsCrafting;
            IsCrafting = hasQuartz && hasGear && hasSteel;

            // Если игрок забрал предмет во время готовки — сбрасываем прогресс
            if (wasCrafting && !IsCrafting)
            {
                CurrentMana = 0;
            }

            MarkDirty(true);
        }

        private void OnServerTick(float dt)
        {
            CheckStructure();

            // Завершение крафта
            if (IsCrafting && CurrentMana >= MaxManaRequired)
            {
                // Очищаем инвентарь
                for (int i = 0; i < 3; i++) inventory[i].TakeOutWhole();

                CurrentMana = 0;
                IsCrafting = false;
                MarkDirty(true);

                // Спавним Террасталь
                Item terrasteel = Api.World.GetItem(new AssetLocation("botaniastory", "ingot-terrasteel"));
                if (terrasteel != null)
                {
                    Api.World.SpawnItemEntity(new ItemStack(terrasteel), Pos.ToVec3d().AddCopy(0.5, 1.0, 0.5));
                }

                // Звук
                var modsys = Api.ModLoader.GetModSystem<BotaniaStoryModSystem>();
                modsys.serverChannel.BroadcastPacket(new PlayManaSoundPacket()
                {
                    Position = Pos.ToVec3d(),
                    SoundName = "terrasteel_craft"
                });

            }

        }

        private void CheckStructure()
        {
            bool oldValid = IsStructureValid;
            IsStructureValid = true;

            // Проходим по сетке 3х3 на один уровень ниже плиты (Y - 1)
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Block block = Api.World.BlockAccessor.GetBlock(Pos.AddCopy(x, -1, z));
                    string blockCode = block.Code.ToString();

                    // Логика шахматного порядка: 
                    // Углы (±1, ±1) и Центр (0, 0) удовлетворяют условию Math.Abs(x) == Math.Abs(z)
                    if (Math.Abs(x) == Math.Abs(z))
                    {
                        // Требуем Живой камень
                        if (!blockCode.Contains("botaniastory:livingrock"))
                        {
                            IsStructureValid = false;
                        }
                    }
                    // Блоки "креста": (±1, 0) и (0, ±1)
                    else
                    {
                        // Требуем Блок Манастали
                        if (!blockCode.Contains("game:metalblock-new-riveted-manasteel"))
                        {
                            IsStructureValid = false;
                        }
                    }
                }
            }

            if (oldValid != IsStructureValid) MarkDirty(true);
        }

        // --- ИНТЕРФЕЙС ПРИЕМА МАНЫ ---
        public bool IsFull() => CurrentMana >= MaxManaRequired;

        public int GetAvailableSpace()
        {
            // Если крафт не запущен — плите мана не нужна вообще!
            if (!IsCrafting) return 0;

            // Если крафт идет, просим ровно столько, сколько не хватает до завершения
            return MaxManaRequired - CurrentMana;
        }

        public void ReceiveMana(int amount)
        {
            if (IsCrafting && !IsFull())
            {
                CurrentMana += amount;
                MarkDirty(true);
            }
        }

        // --- КЛИЕНТ: ЧАСТИЦЫ ---
        private void UpdateClientParticles(float dt)
        {
            if (particleRenderer == null) return;

            if (IsCrafting)
            {
                float progress = (float)CurrentMana / MaxManaRequired;
                particleRenderer.UpdateProgress(progress);
                lastClientMana = CurrentMana; // Сохраняем текущий прогресс
            }
            else
            {
                // Если крафт прервался, но маны было накоплено больше 95% — значит это успешное завершение!
                if (lastClientMana >= MaxManaRequired * 0.95f)
                {
                    particleRenderer.TriggerExplosion(); // Вызываем кастомный взрыв
                }

                particleRenderer.UpdateProgress(0);
                lastClientMana = 0;
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            inventory.ToTreeAttributes(tree);
            tree.SetInt("mana", CurrentMana);
            tree.SetBool("isCrafting", IsCrafting);
            tree.SetBool("isValid", IsStructureValid);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            inventory.FromTreeAttributes(tree);
            CurrentMana = tree.GetInt("mana", 0);
            IsCrafting = tree.GetBool("isCrafting");
            IsStructureValid = tree.GetBool("isValid");
        }
      

    }
}