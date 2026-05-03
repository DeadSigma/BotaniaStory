using BotaniaStory.blocks;
using BotaniaStory.entities;
using BotaniaStory.items;
using BotaniaStory.recipes;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.blockentity
{
    public class BlockEntityManaPool : BlockEntity, IManaReceiver
    {
        // Приватное хранилище маны
        private int _currentMana = 0;
        public bool IsAcceptingFromItems = false;

        // Свойство: если бассейн творческий, всегда отдаем MaxMana и игнорируем изменения!
        public int CurrentMana
        {
            get { return isCreativePool ? MaxMana : _currentMana; }
            set { if (!isCreativePool) _currentMana = value; }
        }

        public int MaxMana = 1000000;

        private bool isDilutedPool = false;
        private bool isCreativePool = false; // Флаг для творческого бассейна

        public bool IsFull() => CurrentMana >= MaxMana;

        public int GetAvailableSpace() => MaxMana - CurrentMana;

        public bool ConsumeMana(int amount)
        {
            // Творческий бассейн всегда отдает ману и никогда не пустеет
            if (isCreativePool) return true;

            if (CurrentMana >= amount)
            {
                CurrentMana -= amount;
                MarkDirty(true);
                return true;
            }

            return false; // Маны не хватило
        }
        public void ReceiveMana(int amount)
        {
            CurrentMana = Math.Clamp(CurrentMana + amount, 0, MaxMana);
            MarkDirty(true);
        }

        // ==========================================
        // ИНИЦИАЛИЗАЦИЯ
        // ==========================================
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Block != null && Block.Attributes != null)
            {
                isDilutedPool = Block.Attributes["isDilutedPool"].AsBool(false);
                isCreativePool = Block.Attributes["isCreativePool"].AsBool(false);
            }

            // Устанавливаем лимиты
            MaxMana = isDilutedPool ? 10000 : 1000000;

            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(SpawnManaParticles, 100);
            }
            else if (api.Side == EnumAppSide.Server)
            {
                // Запускаем проверку брошенных предметов каждые 500 мс (полсекунды)
                RegisterGameTickListener(CheckForDroppedItems, 500);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("mana", _currentMana);
            tree.SetBool("isAcceptingFromItems", IsAcceptingFromItems); // Сохраняем состояние
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _currentMana = tree.GetInt("mana", 0);
            IsAcceptingFromItems = tree.GetBool("isAcceptingFromItems", false); // Загружаем состояние

            if (Api?.Side == EnumAppSide.Client)
            {
                MarkDirty(true);
            }
        }

        // ==========================================
        // УДАЛЕНИЕ БАССЕЙНА (ДРОП ИСКРЫ)
        // ==========================================
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                // Слегка увеличил радиус поиска (с 1.0 до 2.0), чтобы гарантированно зацепить хитбокс
                Entity[] sparks = Api.World.GetEntitiesAround(Pos.ToVec3d().Add(0.5, 1.2, 0.5), 0.2f, 0.5f, e => e is EntitySpark);

                foreach (Entity entity in sparks)
                {
                    if (entity is EntitySpark spark)
                    {
                        Item itemSpark = Api.World.GetItem(new AssetLocation("botaniastory", "spark"));
                        if (itemSpark != null)
                        {
                            ItemStack dropStack = new ItemStack(itemSpark);
                            Api.World.SpawnItemEntity(dropStack, spark.Pos.XYZ);
                        }

                        // ИСПРАВЛЕНИЕ: Используем 'PickedUp' вместо обычной смерти. 
                        // Это заставит сервер удалить искру мгновенно, как будто игрок положил ее в карман.
                        spark.Die(EnumDespawnReason.PickedUp);
                    }
                }
            }
        }

        //  ОТРИСОВКА ЖИДКОСТИ, УБРАТЬ!
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            // 1. СНАЧАЛА ГЕНЕРИРУЕМ И РИСУЕМ САМ БАССЕЙН
            MeshData baseMesh;
            tesselator.TesselateBlock(Block, out baseMesh);
            mesher.AddMeshData(baseMesh);

            // 2. ЕСЛИ ЕСТЬ МАНА, РИСУЕМ ЕЁ ПОВЕРХ
            if (CurrentMana > 0)
            {
                float fillRatio = (float)CurrentMana / MaxMana;

                float baseY = isDilutedPool ? 1.01f : 2.01f;
                float maxRise = isDilutedPool ? 4.0f : 5.0f;

                float heightPixels = baseY + (fillRatio * maxRise);
                float height = heightPixels / 16f;

                string shapeName = isDilutedPool ? "manapool_diluted_liquid.json" : "manapool_liquid.json";
                AssetLocation shapeLoc = new AssetLocation("botaniastory", $"shapes/block/{shapeName}");
                Shape shape = Api.Assets.TryGet(shapeLoc)?.ToObject<Shape>();

                if (shape != null)
                {
                    MeshData liquidMesh;
                    tesselator.TesselateShape(Block, shape, out liquidMesh);
                    liquidMesh.Translate(0, height, 0);

                    // Создаем массив, если его нет
                    if (liquidMesh.CustomInts == null)
                    {
                        liquidMesh.CustomInts = new CustomMeshDataPartInt(liquidMesh.VerticesCount);
                        liquidMesh.CustomInts.Count = liquidMesh.VerticesCount;
                    }

                    // Задаем правильный проход рендера (Liquid)
                    int[] customInts = liquidMesh.CustomInts.Values;
                    for (int i = 0; i < liquidMesh.VerticesCount; i++)
                    {
                        // Используем ТОЛЬКО это число
                        customInts[i] |= 805306368;
                    }

                    mesher.AddMeshData(liquidMesh);
                }
            }

            return true;
        }

        // ГЕНЕРАЦИЯ ИСКР
        private void SpawnManaParticles(float dt)
        {
            if (CurrentMana <= 0) return;
            if (Api.World.Rand.NextDouble() > 0.3) return;

            float fillRatio = (float)CurrentMana / MaxMana;

            float baseY = isDilutedPool ? 1.01f : 2.01f;
            float maxRise = isDilutedPool ? 4.89f : 5.89f;
            float heightPixels = baseY + (fillRatio * maxRise);
            float height = heightPixels / 16f;

            // Считаем разброс частиц от центра бассейна (0.5)
            float posVariance = isDilutedPool ? 0.45f : 0.35f;

            // ПРОВЕРЯЕМ БЛОК СВЕРХУ (Код из предыдущего шага)
            Block blockAbove = Api.World.BlockAccessor.GetBlock(Pos.UpCopy());
            bool isPylonAbove = blockAbove is BlockPylon || (blockAbove.Code != null && blockAbove.Code.Path.Contains("pylon"));
            float particleLife = isPylonAbove ? 0.35f : 1.5f;

            // ИСПОЛЬЗУЕМ ADVANCED ПАРТИКЛЫ ДЛЯ ЭФФЕКТОВ ЗАТУХАНИЯ
            AdvancedParticleProperties particles = new AdvancedParticleProperties()
            {
                // Позиция: Центр бассейна
                basePos = new Vec3d(Pos.X + 0.5, Pos.Y + height, Pos.Z + 0.5),

                // Разброс вокруг центра
                PosOffset = new NatFloat[] {
            NatFloat.createUniform(0, posVariance),
            NatFloat.createUniform(0, 0.05f),
            NatFloat.createUniform(0, posVariance)
        },

                // Скорость: среднее значение + разброс (аналог min/max Velocity)
                Velocity = new NatFloat[] {
            NatFloat.createUniform(0.025f, 0.075f), // X: слегка в стороны
            NatFloat.createUniform(0.15f, 0.05f),   // Y: летят вверх
            NatFloat.createUniform(0.025f, 0.075f)  // Z: слегка в стороны
        },

                // Цвет в формате HSVA (Шкала 0-255 для всех параметров)
                HsvaColor = new NatFloat[] {
                NatFloat.createUniform(128, 10), // Оттенок: 212 (Тот самый розово-пурпурный цвет маны)
                NatFloat.createUniform(155, 20), // Насыщенность
                NatFloat.createUniform(255, 0),  // Яркость
                NatFloat.createUniform(255, 0)   // Прозрачность
                },

                // 1. ПЛАВНОЕ ЗАТУХАНИЕ ПРОЗРАЧНОСТИ
                // Линейно отнимаем 255 от Альфа-канала к концу жизни
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -255f),

                // 2. ПЛАВНОЕ СЖАТИЕ
                // Частицы будут слегка "сдуваться" перед исчезновением
                SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.15f),

                // Количество, Жизнь и Гравитация
                Quantity = NatFloat.createUniform(1.5f, 0.5f), // От 1 до 2 штук
                LifeLength = NatFloat.createUniform(particleLife, 0.1f),
                GravityEffect = NatFloat.createUniform(-0.02f, 0f),

                // Размер
                Size = NatFloat.createUniform(0.225f, 0.125f),

                ParticleModel = EnumParticleModel.Quad
            };

            Api.World.SpawnParticles(particles);
        }
        private void CheckForDroppedItems(float dt)
        {
            // Если маны нет, даже не пытаемся искать
            if (CurrentMana <= 0) return;

            Block blockBelow = Api.World.BlockAccessor.GetBlock(Pos.DownCopy());
            bool hasAlchemyCatalyst = blockBelow?.Code?.Path.Contains("catalyst_alchemy") == true;
            bool hasConjurationCatalyst = blockBelow?.Code?.Path.Contains("catalyst_conjuration") == true;

            // Ищем все сущности над бассейном. Радиус 1 блок вверх и в стороны
            Entity[] entities = Api.World.GetEntitiesAround(Pos.ToVec3d().Add(0.5, 1.0, 0.5), 1.0f, 1.0f, e => e is EntityItem);

            foreach (Entity entity in entities)
            {
                if (entity is EntityItem entityItem && entityItem.Itemstack != null)
                {
                    ItemStack stack = entityItem.Itemstack;
                    string code = stack.Collectible.Code.Path;
                    string domain = stack.Collectible.Code.Domain;
                    string fullItemCode = $"{domain}:{code}";

                  

                    // Игнорируем предметы, которые еще летят по воздуху
                    if (!entityItem.Collided && !entityItem.Swimming) continue;

                    // --- ВЗАИМОДЕЙСТВИЕ С ПЛАНШЕТОМ МАНЫ ---
                    if (domain == "botaniastory" && code == "manatablet")
                    {
                        int maxTabletMana = ItemManaTablet.MaxMana;
                        int currentTabletMana = stack.Attributes.GetInt("mana", 0);

                        if (IsAcceptingFromItems)
                        {
                            // РЕЖИМ 1: БАССЕЙН ЗАБИРАЕТ МАНУ У ПЛАНШЕТА
                            if (currentTabletMana > 0 && CurrentMana < MaxMana)
                            {
                                int transferAmount = Math.Min(currentTabletMana, MaxMana - CurrentMana);
                                transferAmount = Math.Min(transferAmount, 10000);

                                CurrentMana += transferAmount;
                                stack.Attributes.SetInt("mana", currentTabletMana - transferAmount);
                                MarkDirty(true);

                                entityItem.Itemstack = stack;

                                //  СИНХРОНИЗАЦИЯ ПРЕДМЕТА С КЛИЕНТОМ
                                entityItem.WatchedAttributes.SetItemstack("itemstack", stack);
                                entityItem.WatchedAttributes.MarkAllDirty();

                                SpawnCraftingParticles(entityItem.Pos.XYZ);
                                continue;
                            }
                        }
                        else
                        {
                            // РЕЖИМ 2: БАССЕЙН ОТДАЕТ МАНУ ПЛАНШЕТУ
                            if (currentTabletMana < maxTabletMana && CurrentMana > 0)
                            {
                                int transferAmount = Math.Min(CurrentMana, maxTabletMana - currentTabletMana);
                                transferAmount = Math.Min(transferAmount, 10000);

                                CurrentMana -= transferAmount;
                                stack.Attributes.SetInt("mana", currentTabletMana + transferAmount);
                                MarkDirty(true);

                                entityItem.Itemstack = stack;

                                // СИНХРОНИЗАЦИЯ ПРЕДМЕТА С КЛИЕНТОМ
                                entityItem.WatchedAttributes.SetItemstack("itemstack", stack);
                                entityItem.WatchedAttributes.MarkAllDirty();

                                SpawnCraftingParticles(entityItem.Pos.XYZ);
                                continue;
                            }
                        }
                    }

                    // --- РЕЦЕПТЫ ---

                    // Алхимия
                    if (hasAlchemyCatalyst && CatalystRegistry.AlchemyRecipes.ContainsKey(fullItemCode))
                    {
                        string outputCode = CatalystRegistry.AlchemyRecipes[fullItemCode];
                        int cost = CatalystRegistry.AlchemyManaCosts[fullItemCode];
                        if (TryTransmuteItem(entityItem, outputCode, cost)) continue;
                    }

                    // Колдовство
                    if (hasConjurationCatalyst && CatalystRegistry.ConjurationRecipes.ContainsKey(fullItemCode))
                    {
                        int cost = CatalystRegistry.ConjurationRecipes[fullItemCode];
                        if (TryConjureItem(entityItem, fullItemCode, cost)) continue;
                    }

                    // 1. Любой слиток -> Манасталь 
                    if (domain == "game" && code.StartsWith("ingot-"))
                    {
                        if (TryTransmuteItem(entityItem, "game:ingot-manasteel", 25000)) continue;
                    }

                    // 2. Ржавая шестеренка -> Манашестерня 
                    if (domain == "game" && code == "gear-rusty")
                    {
                        
                        if (TryTransmuteItem(entityItem, "botaniastory:manaitem-managear", 30000)) continue;
                    }

                    // 3. Волокно -> Мана-нить
                    if (domain == "game" && code == "flaxfibers")
                    {
                        if (TryTransmuteItem(entityItem, "botaniastory:manaitem-manaflax", 10000)) continue;
                    }

                    // 4. Смола -> Манакварц 
                    if (domain == "game" && code == "clearquartz")
                    {
                       
                        if (TryTransmuteItem(entityItem, "botaniastory:manaitem-manaquartz", 25000)) continue;
                    }

                    // 5. Стекло -> Манастекло
                    if (domain == "game" && code.StartsWith("glass-"))
                    {
                       
                        if (TryTransmuteItem(entityItem, "botaniastory:managlass", 5000)) continue;
                    }

                    // 6. Измельчённое что-то  -> манапорошок
                    if (domain == "game" && code.StartsWith("powder-"))
                    {
                       
                        if (TryTransmuteItem(entityItem, "botaniastory:manaitem-manapowder", 10000)) continue;
                    }
                }
            }
        }
        private bool TryTransmuteItem(EntityItem inputEntity, string outputItemCode, int manaCost)
        {
            // Проверяем, хватает ли маны
            if (CurrentMana < manaCost) return false;

            AssetLocation loc = new AssetLocation(outputItemCode);
            ItemStack outputStack = null;

            // 1. Сначала пытаемся найти результат как Предмет (Item)
            Item outputItem = Api.World.GetItem(loc);
            if (outputItem != null)
            {
                outputStack = new ItemStack(outputItem, 1);
            }
            else
            {
                // 2. Если предмета нет, пытаемся найти как Блок (Block)
                Block outputBlock = Api.World.GetBlock(loc);
                if (outputBlock != null)
                {
                    outputStack = new ItemStack(outputBlock, 1);
                }
            }

            // Если игра не нашла ни предмета, ни блока с таким кодом — отмена
            if (outputStack == null) return false;

            // Списываем ману и сохраняем бассейн
            CurrentMana -= manaCost;
            MarkDirty(true);

            // Забираем один элемент из стака, который бросил игрок
            inputEntity.Itemstack.StackSize--;

            // Если в стаке больше ничего не осталось — удаляем брошенную сущность
            if (inputEntity.Itemstack.StackSize <= 0)
            {
                inputEntity.Die(EnumDespawnReason.Death);
            }

            // Спавним созданный ItemStack (неважно, блок это или предмет) в тех же координатах
            Api.World.SpawnItemEntity(outputStack, inputEntity.Pos.XYZ);

            // Вызываем всплеск частиц
            SpawnCraftingParticles(inputEntity.Pos.XYZ);

            // ОТПРАВКА СЕТЕВОГО ПАКЕТА (ЗВУК)
            if (Api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = Api as ICoreServerAPI;
                IServerNetworkChannel channel = sapi.Network.GetChannel("botanianetwork");

                PlayManaSoundPacket soundMessage = new PlayManaSoundPacket()
                {
                    Position = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5),
                    SoundName = "transmute" 
                };

                channel.BroadcastPacket(soundMessage);
            }

            return true;
        }

        private void SpawnCraftingParticles(Vec3d pos)
        {
            SimpleParticleProperties particles = new SimpleParticleProperties(
                10, 15,
                ColorUtil.ToRgba(255, 0, 255, 200),
                new Vec3d(pos.X - 0.2, pos.Y, pos.Z - 0.2),
                new Vec3d(pos.X + 0.2, pos.Y + 0.5, pos.Z + 0.2),
                new Vec3f(-1f, 1f, -1f),
                new Vec3f(1f, 2f, 1f),
                1.5f,
                -0.05f,
                0.2f, 0.5f,
                EnumParticleModel.Cube
            );



            Api.World.SpawnParticles(particles);
        }
        private bool TryConjureItem(EntityItem inputEntity, string itemCode, int manaCost)
        {
            if (CurrentMana < manaCost) return false;

            AssetLocation loc = new AssetLocation(itemCode);
            ItemStack outputStack = null;

            Item outputItem = Api.World.GetItem(loc);
            if (outputItem != null) outputStack = new ItemStack(outputItem, 2);
            else
            {
                Block outputBlock = Api.World.GetBlock(loc);
                if (outputBlock != null) outputStack = new ItemStack(outputBlock, 2);
            }

            if (outputStack == null) return false;

            // Списываем ману
            CurrentMana -= manaCost;
            MarkDirty(true);

            // Расходуем оригинал
            inputEntity.Itemstack.StackSize--;
            if (inputEntity.Itemstack.StackSize <= 0)
            {
                inputEntity.Die(EnumDespawnReason.Death);
            }

            // Выдаем удвоенный результат
            Api.World.SpawnItemEntity(outputStack, inputEntity.Pos.XYZ);
            SpawnCraftingParticles(inputEntity.Pos.XYZ);

            if (Api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = Api as ICoreServerAPI;
                sapi.Network.GetChannel("botanianetwork").BroadcastPacket(new PlayManaSoundPacket()
                {
                    Position = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5),
                    SoundName = "transmute"
                });
            }

            return true;
        }
    }
}