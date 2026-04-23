using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using static OpenTK.Graphics.OpenGL.GL;

namespace BotaniaStory
{
    // 1. НАСЛЕДУЕМСЯ ОТ BlockEntityContainer
    public class BlockEntityApothecary : BlockEntityContainer
    {
        public bool HasWater = false;
        private MeshData waterMesh;
        public InventoryGeneric inventory;

        protected ApothecaryRenderer renderer;
        public string LastCraftedFlower = null;
        public long LastCraftTime = 0;

        // 2. ОБЯЗАТЕЛЬНЫЕ СВОЙСТВА ДЛЯ КОНТЕЙНЕРА
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
        }

        public void UpdateRenderer()
        {
            renderer?.SetContents(inventory);
            MarkDirty(true);
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

            Dictionary<string, int> totals = new Dictionary<string, int>();

            foreach (var slot in inventory)
            {
                if (slot.Empty) continue;
                string name = slot.Itemstack.GetName();
                if (totals.ContainsKey(name)) totals[name] += slot.StackSize;
                else totals[name] = slot.StackSize;
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
            MeshData baseMesh;
            tesselator.TesselateBlock(Block, out baseMesh);
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