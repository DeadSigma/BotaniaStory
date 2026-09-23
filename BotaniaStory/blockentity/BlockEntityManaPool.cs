using BotaniaStory.blocks;
using BotaniaStory.entities;
using BotaniaStory.items;
using BotaniaStory.util;
using System;
using System.Collections.Generic;
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
        private int _currentMana = 0;
        public bool IsAcceptingFromItems = true;

        // Творческий бассейн всегда хранит MaxMana
        public int CurrentMana
        {
            get { return isCreativePool ? MaxMana : _currentMana; }
            set { if (!isCreativePool) _currentMana = value; }
        }

        public int MaxMana = 1000000;

        private bool isDilutedPool = false;
        private bool isCreativePool = false;

        // Последний отрисованный уровень заполнения
        private int lastRenderedStep = -1;

        // Форма жидкости кэшируется для бассейна
        private Shape cachedLiquidShape;

        public bool IsFull() => CurrentMana >= MaxMana;

        public int GetAvailableSpace() => MaxMana - CurrentMana;

        public bool ConsumeMana(int amount)
        {
            if (isCreativePool) return true;

            if (CurrentMana >= amount)
            {
                CurrentMana -= amount;
                MarkDirty(false);
                return true;
            }

            return false;
        }
        public void ReceiveMana(int amount)
        {
            CurrentMana = Math.Clamp(CurrentMana + amount, 0, MaxMana);
            MarkDirty(false);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Block != null && Block.Attributes != null)
            {
                isDilutedPool = Block.Attributes["isDilutedPool"].AsBool(false);
                isCreativePool = Block.Attributes["isCreativePool"].AsBool(false);
            }

            MaxMana = isDilutedPool ? 10000 : 1000000;

            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(SpawnManaParticles, 100);
            }
            else if (api.Side == EnumAppSide.Server)
            {
                // Брошенные предметы проверяются каждые 500 мс
                RegisterGameTickListener(CheckForDroppedItems, 500);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("mana", _currentMana);
            tree.SetBool("isAcceptingFromItems", IsAcceptingFromItems);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _currentMana = tree.GetInt("mana", 0);
            IsAcceptingFromItems = tree.GetBool("isAcceptingFromItems", true);

            if (Api?.Side == EnumAppSide.Client)
            {
                // Ретесселяция выполняется только при смене видимого уровня
                int step = GetFillStep();
                if (step != lastRenderedStep)
                {
                    lastRenderedStep = step;
                    MarkDirty(true);
                }
            }
        }

        // Шаг заполнения задаётся в сотых долях
        private int GetFillStep()
        {
            if (MaxMana <= 0) return 0;
            return (int)((CurrentMana / (float)MaxMana) * 100f);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                Entity[] sparks = Api.World.GetEntitiesAround(
                    Pos.ToVec3d().Add(0.5, 1.7, 0.5),
                    1f,
                    12f,
                    e => e is EntitySpark
                );

                foreach (Entity entity in sparks)
                {
                    if (entity is EntitySpark spark && spark.IsAttachedTo(Pos))
                    {
                        spark.DropItemsAndDespawn();
                    }
                }
            }
        }

        // Отрисовка жидкости
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            MeshData baseMesh;
            tesselator.TesselateBlock(Block, out baseMesh);
            mesher.AddMeshData(baseMesh);

            if (CurrentMana > 0)
            {
                float fillRatio = (float)CurrentMana / MaxMana;

                float baseY = isDilutedPool ? 1.01f : 2.01f;
                float maxRise = isDilutedPool ? 4.0f : 5.0f;

                float heightPixels = baseY + (fillRatio * maxRise);
                float height = heightPixels / 16f;

                if (cachedLiquidShape == null)
                {
                    string shapeName = isDilutedPool ? "manapool_diluted_liquid.json" : "manapool_liquid.json";
                    AssetLocation shapeLoc = new AssetLocation("botaniastory", $"shapes/block/{shapeName}");
                    cachedLiquidShape = Api.Assets.TryGet(shapeLoc)?.ToObject<Shape>();
                }

                Shape shape = cachedLiquidShape;

                if (shape != null)
                {
                    MeshData liquidMesh;
                    tesselator.TesselateShape(Block, shape, out liquidMesh);
                    liquidMesh.Translate(0, height, 0);

                    if (liquidMesh.CustomInts == null)
                    {
                        liquidMesh.CustomInts = new CustomMeshDataPartInt(liquidMesh.VerticesCount);
                        liquidMesh.CustomInts.Count = liquidMesh.VerticesCount;
                    }

                    // Жидкость переводится в проход Liquid
                    int[] customInts = liquidMesh.CustomInts.Values;
                    for (int i = 0; i < liquidMesh.VerticesCount; i++)
                    {
                        customInts[i] |= 805306368;
                    }

                    mesher.AddMeshData(liquidMesh);
                }
            }

            return true;
        }

        private void SpawnManaParticles(float dt)
        {
            if (CurrentMana <= 0) return;
            if (Api.World.Rand.NextDouble() > 0.3) return;

            float fillRatio = (float)CurrentMana / MaxMana;

            float baseY = isDilutedPool ? 1.01f : 2.01f;
            float maxRise = isDilutedPool ? 4.89f : 5.89f;
            float heightPixels = baseY + (fillRatio * maxRise);
            float height = heightPixels / 16f;

            float posVariance = isDilutedPool ? 0.45f : 0.35f;

            Block blockAbove = Api.World.BlockAccessor.GetBlock(Pos.UpCopy());
            bool isPylonAbove = blockAbove is BlockPylon || (blockAbove.Code != null && blockAbove.Code.Path.Contains("pylon"));
            float particleLife = isPylonAbove ? 0.35f : 1.5f;

            AdvancedParticleProperties particles = new AdvancedParticleProperties()
            {
                basePos = new Vec3d(Pos.X + 0.5, Pos.Y + height, Pos.Z + 0.5),

                PosOffset = new NatFloat[] {
            NatFloat.createUniform(0, posVariance),
            NatFloat.createUniform(0, 0.05f),
            NatFloat.createUniform(0, posVariance)
        },

                Velocity = new NatFloat[] {
            NatFloat.createUniform(0.025f, 0.075f),
            NatFloat.createUniform(0.15f, 0.05f),
            NatFloat.createUniform(0.025f, 0.075f)
        },

                HsvaColor = new NatFloat[] {
                NatFloat.createUniform(128, 10),
                NatFloat.createUniform(155, 20),
                NatFloat.createUniform(255, 0),
                NatFloat.createUniform(255, 0)
                },

                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -255f),

                SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.15f),

                Quantity = NatFloat.createUniform(1.5f, 0.5f),
                LifeLength = NatFloat.createUniform(particleLife, 0.1f),
                GravityEffect = NatFloat.createUniform(-0.02f, 0f),

                Size = NatFloat.createUniform(0.225f, 0.125f),

                ParticleModel = EnumParticleModel.Quad
            };

            Api.World.SpawnParticles(particles);
        }
        private void CheckForDroppedItems(float dt)
        {
            Block blockBelow = Api.World.BlockAccessor.GetBlock(Pos.DownCopy());
            bool hasAlchemyCatalyst = blockBelow?.Code?.Path.Contains("catalyst_alchemy") == true;
            bool hasConjurationCatalyst = blockBelow?.Code?.Path.Contains("catalyst_conjuration") == true;

            // Узкий радиус не даёт соседнему бассейну захватить тот же предмет
            Entity[] entities = Api.World.GetEntitiesAround(Pos.ToVec3d().Add(0.5, 1.0, 0.5), 0.75f, 1.0f, e => e is EntityItem);

            foreach (Entity entity in entities)
            {
                // Уже удалённые сущности пропускаются
                if (!entity.Alive) continue;

                if (entity.Attributes.GetBool("bs_transmuted", false)) continue;

                if (entity is EntityItem entityItem && entityItem.Itemstack != null)
                {
                    ItemStack stack = entityItem.Itemstack;
                    string code = stack.Collectible.Code.Path;
                    string domain = stack.Collectible.Code.Domain;
                    string fullItemCode = $"{domain}:{code}";

                    // Предметы в полёте пропускаются
                    if (!entityItem.Collided && !entityItem.Swimming) continue;

                    // Обмен маной с планшетом
                    if (domain == "botaniastory" && code == "manatablet")
                    {
                        int maxTabletMana = ItemManaTablet.MaxMana;
                        int currentTabletMana = stack.Attributes.GetInt("mana", 0);

                        if (IsAcceptingFromItems)
                        {
                            if (currentTabletMana > 0 && CurrentMana < MaxMana)
                            {
                                int transferAmount = Math.Min(currentTabletMana, MaxMana - CurrentMana);
                                transferAmount = Math.Min(transferAmount, 10000);

                                CurrentMana += transferAmount;
                                stack.Attributes.SetInt("mana", currentTabletMana - transferAmount);
                                MarkDirty(true);

                                entityItem.Itemstack = stack;

                                entityItem.WatchedAttributes.SetItemstack("itemstack", stack);
                                entityItem.WatchedAttributes.MarkAllDirty();

                                SpawnCraftingParticles(entityItem.Pos.XYZ);
                                continue;
                            }
                        }
                        else
                        {
                            if (currentTabletMana < maxTabletMana && CurrentMana > 0)
                            {
                                int transferAmount = Math.Min(CurrentMana, maxTabletMana - currentTabletMana);
                                transferAmount = Math.Min(transferAmount, 10000);

                                CurrentMana -= transferAmount;
                                stack.Attributes.SetInt("mana", currentTabletMana + transferAmount);
                                MarkDirty(true);

                                entityItem.Itemstack = stack;

                                entityItem.WatchedAttributes.SetItemstack("itemstack", stack);
                                entityItem.WatchedAttributes.MarkAllDirty();

                                SpawnCraftingParticles(entityItem.Pos.XYZ);
                                continue;
                            }
                        }
                    }

                    // Зарядка Землекрушителя
                    if (stack.Item is ItemTerraShatterer shatterer)
                    {
                        if (!IsAcceptingFromItems && CurrentMana > 0)
                        {
                            int currentShattererMana = shatterer.GetCurrentMana(stack);
                            int maxShattererMana = shatterer.GetMaxMana(stack);

                            if (currentShattererMana < maxShattererMana)
                            {
                                int transferAmount = Math.Min(CurrentMana, maxShattererMana - currentShattererMana);
                                transferAmount = Math.Min(transferAmount, 10000);

                                CurrentMana -= transferAmount;
                                MarkDirty(true);

                                Item oldItem = entityItem.Slot.Itemstack.Item;

                                shatterer.ReceiveMana(entityItem.Slot, transferAmount, Api.World);

                                if (entityItem.Slot.Itemstack.Item != oldItem)
                                {
                                    Api.World.SpawnItemEntity(entityItem.Slot.Itemstack, entityItem.Pos.XYZ);
                                    entityItem.Die(EnumDespawnReason.Death);

                                    if (Api.Side == EnumAppSide.Server)
                                    {
                                        var sapi = Api as ICoreServerAPI;
                                        sapi.Network.GetChannel("botanianetwork").BroadcastPacket(new PlayManaSoundPacket()
                                        {
                                            Position = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5),
                                            SoundName = "terrashatterer_evolve"
                                        });
                                    }
                                }
                                else
                                {
                                    entityItem.WatchedAttributes.SetItemstack("itemstack", entityItem.Itemstack);
                                    entityItem.WatchedAttributes.MarkAllDirty();

                                    if (Api.Side == EnumAppSide.Server)
                                    {
                                        var sapi = Api as ICoreServerAPI;
                                        sapi.Network.GetChannel("botanianetwork").BroadcastPacket(new PlayManaSoundPacket()
                                        {
                                            Position = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5),
                                            SoundName = "terrashatterer_fill"
                                        });
                                    }
                                }

                                SpawnCraftingParticles(entityItem.Pos.XYZ);
                                continue;
                            }
                        }
                    }

                    // Рецепты катализаторов хранятся в CatalystRegistry
                    if (hasAlchemyCatalyst)
                    {
                        AlchemyRecipe recipe = null;

                        // Сначала ищется точное совпадение
                        if (CatalystRegistry.AlchemyRecipes.ContainsKey(fullItemCode))
                        {
                            recipe = CatalystRegistry.AlchemyRecipes[fullItemCode];
                        }
                        else
                        {
                            // Затем проверяются wildcard-рецепты
                            foreach (var kvp in CatalystRegistry.AlchemyRecipes)
                            {
                                if (kvp.Key.EndsWith("-"))
                                {
                                    string prefix = kvp.Key.TrimEnd('-');
                                    if (fullItemCode.StartsWith(prefix))
                                    {
                                        recipe = kvp.Value;
                                        break;
                                    }
                                }
                            }
                        }

                        if (recipe != null)
                        {
                            int totalAvailable = 0;
                            List<EntityItem> matchingItems = new List<EntityItem>();

                            foreach (Entity e in entities)
                            {
                                if (e.Alive && e is EntityItem ei &&
                                    ei.Itemstack?.Collectible.Code.Path == code &&
                                    ei.Itemstack?.Collectible.Code.Domain == domain &&
                                    (ei.Collided || ei.Swimming))
                                {
                                    matchingItems.Add(ei);
                                    totalAvailable += ei.Itemstack.StackSize;
                                }
                            }

                            if (totalAvailable >= recipe.InputAmount)
                            {
                                if (TryTransmuteMultiple(matchingItems, recipe.OutputCode, recipe.OutputAmount, recipe.InputAmount, recipe.ManaCost))
                                    continue;
                            }
                        }
                    }

                    if (hasConjurationCatalyst && CatalystRegistry.ConjurationRecipes.ContainsKey(fullItemCode))
                    {
                        int cost = CatalystRegistry.ConjurationRecipes[fullItemCode];
                        if (TryConjureItem(entityItem, fullItemCode, cost)) continue;
                    }

                    // Базовые преобразования
                    // Любой слиток -> манасталь
                    if (domain == "game" && code.StartsWith("ingot-"))
                    {
                        if (TryTransmuteItem(entityItem, "game:ingot-manasteel", 1, 1, 25000)) continue;
                    }

                    // Ржавая шестерёнка -> манашестерня
                    if (domain == "game" && code == "gear-rusty")
                    {
                        if (TryTransmuteItem(entityItem, "botaniastory:manaitem-managear", 1, 1, 30000)) continue;
                    }

                    // Волокно -> мана-нить
                    if (domain == "game" && code == "flaxfibers")
                    {
                        if (TryTransmuteItem(entityItem, "botaniastory:manaitem-manaflax", 1, 1, 10000)) continue;
                    }

                    // Чистый кварц -> манакварц
                    if (domain == "game" && code == "clearquartz")
                    {
                        if (TryTransmuteItem(entityItem, "botaniastory:manaitem-manaquartz", 1, 1, 25000)) continue;
                    }

                    // Стекло -> манастекло
                    if (domain == "game" && code.StartsWith("glass-"))
                    {
                        if (TryTransmuteItem(entityItem, "botaniastory:managlass", 1, 1, 5000)) continue;
                    }

                    // Порошок -> манапорошок
                    if (domain == "game" && code.StartsWith("powder-"))
                    {
                        if (TryTransmuteItem(entityItem, "botaniastory:manaitem-manapowder", 1, 1, 10000)) continue;
                    }

                    // Сухая трава -> луговое семя
                    if (domain == "game" && code.StartsWith("drygrass"))
                    {
                        if (TryTransmuteItem(entityItem, "botaniastory:meadowseed-normal", 1, 1, 10000)) continue;
                    }

                    // Луговое семя -> торфяное семя
                    if (domain == "botaniastory" && code.StartsWith("meadowseed-normal"))
                    {
                        if (TryTransmuteItem(entityItem, "botaniastory:meadowseed-peat", 1, 1, 15000)) continue;
                    }

                    // Торфяное семя -> плодородное семя
                    if (domain == "botaniastory" && code.StartsWith("meadowseed-peat"))
                    {
                        if (TryTransmuteItem(entityItem, "botaniastory:meadowseed-medium", 1, 1, 20000)) continue;
                    }
                }
            }
        }
        private bool TryTransmuteMultiple(List<EntityItem> inputs, string outputItemCode, int outputAmount, int inputAmount, int manaCost)
        {
            if (CurrentMana < manaCost) return false;

            AssetLocation loc = new AssetLocation(outputItemCode);
            ItemStack outputStack = null;

            Item outputItem = Api.World.GetItem(loc);
            if (outputItem != null) outputStack = new ItemStack(outputItem, outputAmount);
            else
            {
                Block outputBlock = Api.World.GetBlock(loc);
                if (outputBlock != null) outputStack = new ItemStack(outputBlock, outputAmount);
            }

            if (outputStack == null) return false;

            CurrentMana -= manaCost;
            MarkDirty(true);

            int remainingToConsume = inputAmount;
            Vec3d lastPos = inputs[0].Pos.XYZ;

            foreach (EntityItem entityItem in inputs)
            {
                if (remainingToConsume <= 0) break;

                int take = Math.Min(entityItem.Itemstack.StackSize, remainingToConsume);
                entityItem.Itemstack.StackSize -= take;
                remainingToConsume -= take;

                lastPos = entityItem.Pos.XYZ;

                if (entityItem.Itemstack.StackSize <= 0)
                {
                    entityItem.Die(EnumDespawnReason.Death);
                }
                else
                {
                    entityItem.WatchedAttributes.SetItemstack("itemstack", entityItem.Itemstack);
                    entityItem.WatchedAttributes.MarkAllDirty();
                }
            }

            // Результат помечается от повторной трансмутации
            Entity spawnedEntity = Api.World.SpawnItemEntity(outputStack, lastPos);
            if (spawnedEntity != null)
            {
                spawnedEntity.Attributes.SetBool("bs_transmuted", true);
            }


            SpawnCraftingParticles(lastPos);

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

        private bool TryTransmuteItem(EntityItem inputEntity, string outputItemCode, int outputAmount, int inputAmount, int manaCost)
        {
            if (CurrentMana < manaCost) return false;

            AssetLocation loc = new AssetLocation(outputItemCode);
            ItemStack outputStack = null;

            Item outputItem = Api.World.GetItem(loc);
            if (outputItem != null)
            {
                outputStack = new ItemStack(outputItem, outputAmount);
            }
            else
            {
                Block outputBlock = Api.World.GetBlock(loc);
                if (outputBlock != null)
                {
                    outputStack = new ItemStack(outputBlock, outputAmount);
                }
            }

            if (outputStack == null) return false;

            CurrentMana -= manaCost;
            MarkDirty(true);

            inputEntity.Itemstack.StackSize -= inputAmount;

            if (inputEntity.Itemstack.StackSize <= 0)
            {
                inputEntity.Die(EnumDespawnReason.Death);
            }
            else
            {
                // Остаток стака синхронизируется с клиентом
                inputEntity.WatchedAttributes.SetItemstack("itemstack", inputEntity.Itemstack);
                inputEntity.WatchedAttributes.MarkAllDirty();
            }



            // Результат помечается от повторной трансмутации
            Entity spawnedEntity = Api.World.SpawnItemEntity(outputStack, inputEntity.Pos.XYZ);
            if (spawnedEntity != null)
            {
                spawnedEntity.Attributes.SetBool("bs_transmuted", true);
            }

            SpawnCraftingParticles(inputEntity.Pos.XYZ);

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

            CurrentMana -= manaCost;
            MarkDirty(true);

            inputEntity.Itemstack.StackSize--;
            if (inputEntity.Itemstack.StackSize <= 0)
            {
                inputEntity.Die(EnumDespawnReason.Death);
            }

            // Результат помечается от повторного колдовства
            Entity spawnedEntity = Api.World.SpawnItemEntity(outputStack, inputEntity.Pos.XYZ);
            if (spawnedEntity != null)
            {
                spawnedEntity.Attributes.SetBool("bs_transmuted", true);
            }

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