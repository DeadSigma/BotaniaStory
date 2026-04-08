using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace BotaniaStory
{
    public class BlockEntityApothecary : BlockEntity
    {
        public bool HasWater = false;
        private MeshData waterMesh;
        public InventoryGeneric inventory;
        
        protected ApothecaryRenderer renderer;
        public string LastCraftedFlower = null;
        public long LastCraftTime = 0;
        public BlockEntityApothecary()
        {
            inventory = new InventoryGeneric(16, "apothecary-inv", null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inventory.LateInitialize("apothecary-inv-" + Pos.ToString(), api);

            if (api is ICoreClientAPI capi)
            {
                renderer = new ApothecaryRenderer(Pos, capi);
                capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "apothecary-items");
                renderer.SetContents(inventory);
            }
        }

        // Вызывается из блока при взаимодействии
        public void UpdateRenderer()
        {
            renderer?.SetContents(inventory);
            MarkDirty(true);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("hasWater", HasWater);
            inventory.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            bool prevWater = HasWater;
            base.FromTreeAttributes(tree, worldForResolving);
            HasWater = tree.GetBool("hasWater");
            inventory.FromTreeAttributes(tree);

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
            for (int i = 0; i < inventory.Count; i++)
            {
                if (!inventory[i].Empty)
                {
                    Api.World.SpawnItemEntity(inventory[i].Itemstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }
            base.OnBlockBroken(byPlayer);
        }

        // ==========================================
        // ИНФОРМАЦИЯ ДЛЯ HUD (С ГРУППИРОВКОЙ)
        // ==========================================
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

        // ==========================================
        // ВОДА
        // ==========================================
        // ==========================================
        // ВОДА
        // ==========================================
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            // 1. СНАЧАЛА рисуем сам каменный блок аптекаря
            MeshData baseMesh;
            tesselator.TesselateBlock(Block, out baseMesh);
            mesher.AddMeshData(baseMesh);

            // 2. ЗАТЕМ рисуем воду поверх него
            if (HasWater)
            {
                if (waterMesh == null) GenerateWaterMesh(tesselator);
                if (waterMesh != null) mesher.AddMeshData(waterMesh);
            }

            // 3. Возвращаем TRUE, забирая рендер на себя
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
                // Убеждаемся, что массив для флагов существует
                if (waterMesh.CustomInts == null)
                {
                    waterMesh.CustomInts = new CustomMeshDataPartInt(waterMesh.VerticesCount);
                    waterMesh.CustomInts.Count = waterMesh.VerticesCount;
                }

                // Задаем правильный проход рендера (Liquid)
                int[] customInts = waterMesh.CustomInts.Values;
                for (int i = 0; i < waterMesh.VerticesCount; i++)
                {
                    customInts[i] |= 805306368;
                }

                // ВАЖНО: Я удалил отсюда весь старый код с ClimateColorMapIds и SeasonColorMapIds.
                // Теперь твоя текстура не будет менять цвет в зависимости от биома или времени года,
                // а будет отображаться в своих оригинальных цветах!
            }
        }
    }
}