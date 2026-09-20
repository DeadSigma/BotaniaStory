using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory.items
{
    public class ItemManaTablet : Item, IContainedMeshSource
    {
        public const int MaxMana = 500000;

        // Меши кэшируются для рук и инвентаря
        private MultiTextureMeshRef[] meshRefs;

        // Меши кэшируются для мира и витрин
        private Dictionary<string, MeshData> blockMeshCache = new Dictionary<string, MeshData>();

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (api.Side == EnumAppSide.Client)
            {
                GenerateHandMeshes(api as ICoreClientAPI);
            }
        }

        private void GenerateHandMeshes(ICoreClientAPI capi)
        {
            meshRefs = new MultiTextureMeshRef[11];
            AssetLocation shapeLoc = new AssetLocation("botaniastory", "shapes/item/manatablet.json");

            for (int i = 0; i < 11; i++)
            {
                // Форма загружается заново для каждого уровня маны
                Shape tempShape = capi.Assets.TryGet(shapeLoc)?.ToObject<Shape>();
                if (tempShape == null) continue;

                UpdateLiquidLevel(tempShape, i / 10f);

                capi.Tesselator.TesselateShape(this, tempShape, out MeshData mesh);
                meshRefs[i] = capi.Render.UploadMultiTextureMesh(mesh);
            }
        }

        private void UpdateLiquidLevel(Shape shape, float fillRatio)
        {
            ShapeElement liquidElem = FindElement(shape.Elements, "manaliquid");
            if (liquidElem == null) return;

            double baseY = liquidElem.From[1];
            double originalMaxY = liquidElem.To[1];
            double maxRise = originalMaxY - baseY;

            double ratio = Math.Max(0.001, fillRatio);

            liquidElem.To[1] = baseY + (maxRise * ratio);

            // Боковые UV обрезаются вместе с уровнем жидкости
            BlockFacing[] sideFaces = { BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.EAST, BlockFacing.WEST };

            foreach (BlockFacing facing in sideFaces)
            {
                ShapeElementFace face = liquidElem.FacesResolved[facing.Index];

                if (face != null && face.Uv != null)
                {
                    float[] newUv = new float[4];
                    Array.Copy(face.Uv, newUv, 4);

                    float vTopOriginal = face.Uv[1];
                    float vBottom = face.Uv[3];
                    float uvHeight = vBottom - vTopOriginal;

                    newUv[1] = vBottom - (uvHeight * (float)ratio);

                    face.Uv = newUv;
                }
            }
        }

        private ShapeElement FindElement(ShapeElement[] elements, string name)
        {
            if (elements == null) return null;
            foreach (var el in elements)
            {
                if (el.Name == name) return el;
                var found = FindElement(el.Children, name);
                if (found != null) return found;
            }
            return null;
        }

        public string GetMeshCacheKey(ItemSlot inSlot)
        {
            if (inSlot.Empty) return Code.ToString();
            int step = GetManaStep(inSlot.Itemstack);
            return $"{Code}-step-{step}";
        }

        public MeshData GenMesh(ItemSlot inSlot, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
        {
            if (inSlot.Empty) return null;
            string key = GetMeshCacheKey(inSlot);

            if (blockMeshCache.TryGetValue(key, out MeshData cached)) return cached.Clone();

            ICoreClientAPI capi = api as ICoreClientAPI;
            AssetLocation shapeLoc = new AssetLocation("botaniastory", "shapes/item/manatablet.json");

            Shape shape = capi.Assets.TryGet(shapeLoc)?.ToObject<Shape>();
            if (shape == null) return null;

            int step = GetManaStep(inSlot.Itemstack);
            UpdateLiquidLevel(shape, step / 10f);

            ITexPositionSource texSource = new ContainedItemTexSource(targetAtlas, this);

            capi.Tesselator.TesselateShape("manatablet-ground", shape, out MeshData mesh, texSource);

            blockMeshCache[key] = mesh;
            return mesh.Clone();
        }

        public int GetMana(ItemStack stack)
        {
            if (stack == null) return 0;
            return stack.Attributes.GetInt("mana", 0);
        }

        public void SetMana(ItemStack stack, int amount)
        {
            if (stack == null) return;
            stack.Attributes.SetInt("mana", GameMath.Clamp(amount, 0, MaxMana));
        }

        private int GetManaStep(ItemStack stack)
        {
            int currentMana = stack.Attributes.GetInt("mana", 0);
            int step = (int)Math.Round((currentMana / (float)MaxMana) * 10);
            return GameMath.Clamp(step, 0, 10);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            int step = GetManaStep(itemstack);
            if (meshRefs != null && meshRefs[step] != null)
            {
                renderinfo.ModelRef = meshRefs[step];
            }
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);
            if (meshRefs != null)
            {
                foreach (var mr in meshRefs) mr?.Dispose();
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool boolVal)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, boolVal);

            int currentMana = inSlot.Itemstack.Attributes.GetInt("mana", 0);

            float displayMana = currentMana / 1000f;
            float displayMax = MaxMana / 1000f;


            dsc.AppendLine("\n" + Lang.Get(
                "botaniastory:item-manatablet-mana",
                displayMana.ToString("0.##"),
                displayMax.ToString("0.##")
            ));
        }

        private class ContainedItemTexSource : ITexPositionSource
        {
            private ITextureAtlasAPI targetAtlas;
            private Item item;

            public ContainedItemTexSource(ITextureAtlasAPI targetAtlas, Item item)
            {
                this.targetAtlas = targetAtlas;
                this.item = item;
            }

            public Size2i AtlasSize => targetAtlas.Size;

            public TextureAtlasPosition this[string textureCode]
            {
                get
                {
                    AssetLocation texPath = null;
                    if (item.Textures.TryGetValue(textureCode, out CompositeTexture compTex))
                    {
                        texPath = compTex.Baked.BakedName;
                    }
                    else
                    {
                        texPath = new AssetLocation("unknown");
                    }

                    // Текстура добавляется в целевой атлас при необходимости
                    targetAtlas.GetOrInsertTexture(texPath, out _, out TextureAtlasPosition pos);
                    return pos;
                }
            }
        }
    }
}