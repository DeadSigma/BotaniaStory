using BotaniaStory.blocks; 
using BotaniaStory.client.renderers;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using static OpenTK.Graphics.OpenGL.GL;

namespace BotaniaStory.blockentity
{
    // НАСЛЕДУЕМСЯ ОТ BlockEntityContainer
    public class BlockEntityApothecary : BlockEntityContainer
    {
        public bool HasWater = false;
        private MeshData waterMesh;
        public InventoryGeneric inventory;

        protected ApothecaryRenderer renderer;
        public string LastCraftedFlower = null;
        public long LastCraftTime = 0;

        // ОБЯЗАТЕЛЬНЫЕ СВОЙСТВА ДЛЯ КОНТЕЙНЕРА
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "apothecary-inv";

        public BlockEntityApothecary()
        {
            inventory = new InventoryGeneric(16, "apothecary-inv", null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            // base.Initialize автоматически вызывает inventory.LateInitialize!

            if (api is ICoreClientAPI capi)
            {
                renderer = new ApothecaryRenderer(Pos, capi);
                capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "apothecary-items");
                renderer.SetContents(inventory);
            }

            // Сервер раз в 500мс собирает брошенные рядом ингредиенты 
            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(ScanForDroppedItems, 500);
            }
        }

        // БАЗА ДАННЫХ РЕЦЕПТОВ ЦВЕТОВ
        public static readonly Dictionary<string, Dictionary<string, int>> flowerRecipes = new()
        {
            { "puredaisy-free", new() { { "mysticalpetal-white", 4 } } },
            { "daybloom-free", new() { { "mysticalpetal-yellow", 2 }, { "mysticalpetal-orange", 1 }, { "mysticalpetal-lightblue", 1 } } },
            { "endoflame-free", new() { { "mysticalpetal-brown", 2 }, { "mysticalpetal-lightgray", 1 }, { "mysticalpetal-red", 1 } } },
            { "jadedamaranthus-free", new() { { "mysticalpetal-lime", 1 }, { "mysticalpetal-green", 1 }, { "mysticalpetal-magenta", 1 }, { "root-rusted", 1 }, { "rune-spring", 1 } } },
            { "rosaarcana-free", new() { { "mysticalpetal-brown", 2 }, { "mysticalpetal-pink", 2 }, { "gear-rusty", 1 } } },
            { "hopperhock-free", new() { { "mysticalpetal-lightgray", 2 }, { "mysticalpetal-gray", 2 }, { "rune-air", 1 }, { "root-rusted", 1 } } },
            { "witheredamaranthus-free", new() { { "mysticalpetal-red", 1 }, { "mysticalpetal-blue", 1 }, { "mysticalpetal-magenta", 1 }, { "rune-spring", 1 }, { "root-rusted", 1 } } },
            { "agricarnation-free", new() { { "mysticalpetal-lime", 2 }, { "mysticalpetal-green", 1 }, { "mysticalpetal-yellow", 1 }, { "root-rusted", 1 }, { "rune-spring", 1 } } },
            { "hydroangeas-free", new() { { "mysticalpetal-blue", 2 }, { "mysticalpetal-cyan", 2 }, { "manaitem-manapowder", 1 } } },
            { "nightshade-free", new() { { "mysticalpetal-black", 2 }, { "mysticalpetal-gray", 1 }, { "mysticalpetal-magenta", 1 } } }
        };

        public void CheckCrafting()
        {
            Dictionary<string, int> currentItems = new();
            int seedCount = 0;

            // Считаем все предметы внутри алтаря
            foreach (var slot in inventory)
            {
                if (slot.Empty) continue;
                string code = slot.Itemstack.Collectible.Code.Path;

                // Отдельно считаем семена
                if (code.StartsWith("treeseed") || code.StartsWith("seeds-"))
                {
                    seedCount += slot.StackSize;
                }
                else
                {
                    // Быстрое получение или добавление нового
                    currentItems.TryGetValue(code, out int count);
                    currentItems[code] = count + slot.StackSize;
                }
            }

            // Если внутри есть хотя бы 1 семечко и какие-то ингредиенты
            if (seedCount > 0 && currentItems.Count > 0)
            {
                string craftedFlower = null;

                // Сравниваем ингредиенты с базой рецептов
                foreach (var recipe in flowerRecipes)
                {
                    bool match = true;
                    if (recipe.Value.Count != currentItems.Count) continue;

                    foreach (var req in recipe.Value)
                    {
                        if (!currentItems.ContainsKey(req.Key) || currentItems[req.Key] != req.Value)
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        craftedFlower = recipe.Key;
                        break;
                    }
                }

                // Если рецепт совпал - крафтим!
                if (craftedFlower != null)
                {
                    inventory.Clear();
                    HasWater = false;
                    UpdateRenderer();

                    LastCraftedFlower = craftedFlower;
                    LastCraftTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (Api.Side == EnumAppSide.Server)
                    {
                        Block flowerBlock = Api.World.GetBlock(new AssetLocation("botaniastory", craftedFlower));
                        if (flowerBlock != null)
                        {
                            Api.World.SpawnItemEntity(new ItemStack(flowerBlock), Pos.ToVec3d().Add(0.5, 1.2, 0.5));
                        }
                        PlayApothecarySound("apothecary_craft");
                    }
                    MarkDirty(true);
                }
            }
        }

        private void PlayApothecarySound(string soundName)
        {
            if (Api is Vintagestory.API.Server.ICoreServerAPI sapi)
            {
                var channel = sapi.Network.GetChannel("botanianetwork");
                channel.BroadcastPacket(new PlayManaSoundPacket()
                {
                    Position = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5),
                    SoundName = soundName
                });
            }
        }

        public void UpdateRenderer()
        {
            renderer?.SetContents(inventory);
            MarkDirty(true);
        }

        // ЛОГИКА ПРЕДМЕТОВ (общая для ПКМ и для брошенных предметов)

        // Кладёт 1 предмет из слота в первую свободную ячейку.
        // Вызывается и из BlockApothecary (клик рукой), и из сканера брошенных предметов.
        public bool TryAddItem(ItemSlot slot, IPlayer player = null)
        {
            if (slot == null || slot.Empty) return false;

            // Аптекарь принимает ингредиенты только когда в нём есть вода
            if (!HasWater) return false;

            string path = slot.Itemstack.Collectible.Code.Path;
            if (!IsAllowedIngredient(path)) return false;

            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].Empty)
                {
                    inventory[i].Itemstack = slot.TakeOut(1);
                    slot.MarkDirty();
                    inventory[i].MarkDirty();
                    UpdateRenderer();
                    CheckCrafting();

                    return true;
                }
            }

            return false;
        }

        // Белый список ингредиентов аптекаря 
        private static bool IsAllowedIngredient(string path)
        {
            if (path == null) return false;

            string[] allowedKeywords = ["petal", "flower", "gear-rusty", "berry", "fruit", "manaitem", "vine", "fern", "seed", "root", "rune"];

            foreach (string keyword in allowedKeywords)
            {
                if (path.Contains(keyword)) return true;
            }

            return false;
        }

        // Сканер брошенных предметов: засасывает лежащие сверху ингредиенты внутрь аптекаря
        private void ScanForDroppedItems(float dt)
        {
            // Без воды складывать ингредиенты некуда
            if (!HasWater) return;

            // Есть ли вообще свободное место?
            bool hasEmptySlot = false;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].Empty) { hasEmptySlot = true; break; }
            }
            if (!hasEmptySlot) return;

            Entity[] entities = Api.World.GetEntitiesAround(Pos.ToVec3d().Add(0.5, 0.9, 0.5), 0.9f, 0.9f,
                e => e is EntityItem item &&
                     item.Alive &&
                     item.Itemstack != null &&
                     IsAllowedIngredient(item.Itemstack.Collectible.Code.Path));

            foreach (Entity entity in entities)
            {
                if (entity is not EntityItem entityItem) continue;

                ItemStack stack = entityItem.Itemstack;
                if (stack == null || stack.StackSize == 0) continue;

                DummySlot dummySlot = new DummySlot(stack);

                if (TryAddItem(dummySlot, null))
                {
                    if (dummySlot.Empty) entityItem.Die();
                    else entityItem.Itemstack = dummySlot.Itemstack;
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree); // Автоматически безопасно сохраняет инвентарь и маппинг
            tree.SetBool("hasWater", HasWater);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            bool prevWater = HasWater;
            base.FromTreeAttributes(tree, worldForResolving); // Автоматически загружает инвентарь с нужными проверками
            HasWater = tree.GetBool("hasWater");

            if (Api is ICoreClientAPI)
            {
                if (prevWater != HasWater) MarkDirty(true);
                renderer?.SetContents(inventory);
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            renderer?.Dispose();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if (Api?.World != null)
            {
                for (int i = 0; i < inventory.Count; i++)
                {
                    if (!inventory[i].Empty)
                    {
                        Api.World.SpawnItemEntity(inventory[i].Itemstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                }
            }
            base.OnBlockBroken(byPlayer);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (inventory == null || inventory.Empty) return;

            Dictionary<string, int> totals = new();

            foreach (var slot in inventory)
            {
                if (slot.Empty) continue;
                string name = slot.Itemstack.GetName();

                totals.TryGetValue(name, out int count);
                totals[name] = count + slot.StackSize;
            }

            if (totals.Count > 0)
            {
                dsc.AppendLine("\n" + Lang.Get("Содержимое:"));
                foreach (var item in totals)
                {
                    dsc.AppendLine($"{item.Value}x {item.Key}");
                }
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            tesselator.TesselateBlock(Block, out MeshData baseMesh);
            mesher.AddMeshData(baseMesh);

            if (HasWater)
            {
                if (waterMesh == null) GenerateWaterMesh(tesselator);
                if (waterMesh != null) mesher.AddMeshData(waterMesh);
            }

            return true;
        }

        private void GenerateWaterMesh(ITesselatorAPI tesselator)
        {
            AssetLocation shapeLoc = new AssetLocation("botaniastory", "shapes/block/waterplane.json");
            Shape shape = Api.Assets.TryGet(shapeLoc)?.ToObject<Shape>();
            if (shape == null) return;

            ITexPositionSource texSource = tesselator.GetTextureSource(Block);
            tesselator.TesselateShape("apothecarywater", shape, out waterMesh, texSource);

            if (waterMesh != null)
            {
                if (waterMesh.CustomInts == null)
                {
                    waterMesh.CustomInts = new CustomMeshDataPartInt(waterMesh.VerticesCount);
                    waterMesh.CustomInts.Count = waterMesh.VerticesCount;
                }

                int[] customInts = waterMesh.CustomInts.Values;
                for (int i = 0; i < waterMesh.VerticesCount; i++)
                {
                    customInts[i] |= 805306368;
                }
            }
        }
    }
}