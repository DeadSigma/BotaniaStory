using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory.items
{
    public class ItemManaTablet : Item, IContainedMeshSource
    {
        public const int MaxMana = 500000;

        // Кэш для инвентаря и рук
        private MultiTextureMeshRef[] meshRefs;

        // Кэш для мира (пол/витрины)
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
                // Читаем чистую модель напрямую из ассетов для каждого шага.
                // Это навсегда убьет баги с общими ссылками массивов From/To/Uv
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

            // Защита от нулевой высоты
            double ratio = Math.Max(0.001, fillRatio);

            // 1. Изменяем высоту геометрии
            liquidElem.To[1] = baseY + (maxRise * ratio);

            // 2. Обрезаем текстуру через массив FacesResolved
            // Используем готовые константы сторон света из движка
            BlockFacing[] sideFaces = { BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.EAST, BlockFacing.WEST };

            foreach (BlockFacing facing in sideFaces)
            {
                // Берем грань по индексу (0 - North, 1 - East, 2 - South, 3 - West)
                ShapeElementFace face = liquidElem.FacesResolved[facing.Index];

                // Проверяем, что грань существует и у нее есть UV
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

        // ==========================================
        // ИНТЕРФЕЙС IContainedMeshSource (ДЛЯ ВИТРИН И ПОЛА)
        // ==========================================

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

            // Если меш уже был сгенерирован для этого атласа и уровня маны — отдаем клон
            if (blockMeshCache.TryGetValue(key, out MeshData cached)) return cached.Clone();

            ICoreClientAPI capi = api as ICoreClientAPI;
            AssetLocation shapeLoc = new AssetLocation("botaniastory", "shapes/item/manatablet.json");

            // Парсим чистую форму из JSON на каждый вызов, поэтому Clone() больше не нужен
            Shape shape = capi.Assets.TryGet(shapeLoc)?.ToObject<Shape>();
            if (shape == null) return null;

            // Настраиваем уровень маны напрямую в shape
            int step = GetManaStep(inSlot.Itemstack);
            UpdateLiquidLevel(shape, step / 10f);

            // Используем умный адаптер текстур
            ITexPositionSource texSource = new ContainedItemTexSource(targetAtlas, this);

            // ВАЖНО: здесь мы передаем shape, а не удаленный groundShape
            capi.Tesselator.TesselateShape("manatablet-ground", shape, out MeshData mesh, texSource);

            blockMeshCache[key] = mesh;
            return mesh.Clone();
        }

        // ==========================================
        // ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ РАБОТЫ С МАНОЙ
        // ==========================================
        public int GetMana(ItemStack stack)
        {
            if (stack == null) return 0;
            return stack.Attributes.GetInt("mana", 0);
        }

        public void SetMana(ItemStack stack, int amount)
        {
            if (stack == null) return;
            // GameMath.Clamp не даст мане опуститься ниже 0 или превысить MaxMana
            stack.Attributes.SetInt("mana", GameMath.Clamp(amount, 0, MaxMana));
        }

        private int GetManaStep(ItemStack stack)
        {
            int currentMana = stack.Attributes.GetInt("mana", 0);
            int step = (int)Math.Round((currentMana / (float)MaxMana) * 10);
            return GameMath.Clamp(step, 0, 10);
        }

        // ==========================================
        // РЕНДЕР В РУКАХ И ИНВЕНТАРЕ
        // ==========================================

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

            // Получаем реальное "внутреннее" количество маны
            int currentMana = inSlot.Itemstack.Attributes.GetInt("mana", 0);

            // Делим значения на 1000 для интерфейса игрока (используем float, чтобы корректно обрабатывать дроби)
            float displayMana = currentMana / 1000f;
            float displayMax = MaxMana / 1000f;

           
            dsc.AppendLine($"\nМана: {displayMana:0.##} / {displayMax:0.##}");
        }

        // ==========================================
        // ВНУТРЕННИЙ КЛАСС: УМНЫЙ АДАПТЕР ТЕКСТУР
        // ==========================================
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
                    // Ищем путь к текстуре в json предмета
                    if (item.Textures.TryGetValue(textureCode, out CompositeTexture compTex))
                    {
                        texPath = compTex.Baked.BakedName;
                    }
                    else
                    {
                        texPath = new AssetLocation("unknown");
                    }

                    //  Метод GetOrInsertTexture проверяет, есть ли текстура в атласе блоков.
                    // Если её нет (из-за чего был баг X-Ray), он динамически вшивает её туда!
                    targetAtlas.GetOrInsertTexture(texPath, out _, out TextureAtlasPosition pos);
                    return pos;
                }
            }
        }
    }
}