using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BlockManaPool : Block
    {
        // ИСПРАВЛЕНИЕ 1: Меняем тип на MultiTextureMeshRef
        private MultiTextureMeshRef creativePoolMeshRef;
        private MeshData creativePoolMesh;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Client && Variant != null && Variant["type"] == "creative")
            {
                ICoreClientAPI capi = api as ICoreClientAPI;

                capi.Tesselator.TesselateBlock(this, out creativePoolMesh);

                AssetLocation shapeLoc = new AssetLocation("botaniastory", "shapes/block/manapool_liquid.json");
                Shape shape = capi.Assets.TryGet(shapeLoc)?.ToObject<Shape>();

                if (shape != null)
                {
                    capi.Tesselator.TesselateShape(this, shape, out MeshData liquidMesh);

                    float baseY = 2.01f;
                    float maxRise = 5.0f;
                    float height = (baseY + maxRise) / 16f;

                    liquidMesh.Translate(0, height, 0);

                    if (liquidMesh.CustomInts == null)
                    {
                        liquidMesh.CustomInts = new CustomMeshDataPartInt(liquidMesh.VerticesCount);
                        liquidMesh.CustomInts.Count = liquidMesh.VerticesCount;
                    }

                    int[] customInts = liquidMesh.CustomInts.Values;
                    for (int i = 0; i < liquidMesh.VerticesCount; i++)
                    {
                        customInts[i] |= 805306368;
                    }

                    creativePoolMesh.AddMeshData(liquidMesh);
                }
            }
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (Variant != null && Variant["type"] == "creative" && creativePoolMesh != null)
            {
                if (creativePoolMeshRef == null)
                {
                    // ИСПРАВЛЕНИЕ 2: Используем UploadMultiTextureMesh вместо UploadMesh
                    creativePoolMeshRef = capi.Render.UploadMultiTextureMesh(creativePoolMesh);
                }

                renderinfo.ModelRef = creativePoolMeshRef;
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);
            creativePoolMeshRef?.Dispose();
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (!activeSlot.Empty && activeSlot.Itemstack.Item is ItemWandOfTheForest)
            {
                return false;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}