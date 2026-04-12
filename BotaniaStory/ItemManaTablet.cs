using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ItemManaTablet : Item
    {
        public const int MaxMana = 500000;

        // Массив для хранения готовых 3D-моделей с разным уровнем жидкости
        private MultiTextureMeshRef[] meshRefs;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            // Модельки нужно генерировать только на стороне клиента (визуал)
            if (api.Side == EnumAppSide.Client)
            {
                GenerateMeshes(api as ICoreClientAPI);
            }
        }

        private void GenerateMeshes(ICoreClientAPI capi)
        {
            meshRefs = new MultiTextureMeshRef[11];

            AssetLocation shapeLoc = new AssetLocation("botaniastory", "shapes/item/manatablet.json");
            Shape shape = capi.Assets.TryGet(shapeLoc)?.ToObject<Shape>();

            if (shape == null) return;

            ShapeElement liquidElem = FindElement(shape.Elements, "manaliquid");
            if (liquidElem == null) return;

            double baseY = liquidElem.From[1];
            double originalMaxY = liquidElem.To[1];
            double maxRise = originalMaxY - baseY;

            for (int i = 0; i < 11; i++)
            {
                float fillRatio = i / 10f;
                // Временно меняем высоту для генерации конкретного меша
                liquidElem.To[1] = baseY + Math.Max(0.01, maxRise * fillRatio);
                if (i == 0) liquidElem.To[1] = baseY;

                capi.Tesselator.TesselateShape(this, shape, out MeshData mesh);
                meshRefs[i] = capi.Render.UploadMultiTextureMesh(mesh);
            }

            // ВАЖНО: Возвращаем высоту в исходное (пустое) состояние.
            // Теперь во всех витринах и на земле планшет будет выглядеть пустым.
            liquidElem.To[1] = baseY;
        }

        // Вспомогательная функция для рекурсивного поиска элемента в json модели
        private ShapeElement FindElement(ShapeElement[] elements, string name)
        {
            if (elements == null) return null;
            foreach (var el in elements)
            {
                if (el.Name == name) return el;
                if (el.Children != null)
                {
                    var found = FindElement(el.Children, name);
                    if (found != null) return found;
                }
            }
            return null;
        }

        // ==========================================
        // МАГИЯ РЕНДЕРА
        // ==========================================
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (meshRefs != null)
            {
                int currentMana = GetMana(itemstack);

                // Вычисляем, какую по счету модельку использовать (0 до 10)
                int step = (int)Math.Round((currentMana / (float)MaxMana) * 10);
                step = GameMath.Clamp(step, 0, 10);

                if (meshRefs[step] != null)
                {
                    // Подменяем стандартную модель на ту, что с нужным уровнем жидкости
                    renderinfo.ModelRef = meshRefs[step];
                }
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        // Очищаем память при выходе из игры
        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);
            if (meshRefs != null)
            {
                foreach (var meshRef in meshRefs)
                {
                    meshRef?.Dispose();
                }
            }
        }

        // ==========================================
        // ЛОГИКА МАНЫ И ИНФОРМАЦИЯ
        // ==========================================
        public int GetMana(ItemStack stack)
        {
            return stack.Attributes.GetInt("mana", 0);
        }

        public void SetMana(ItemStack stack, int amount)
        {
            stack.Attributes.SetInt("mana", GameMath.Clamp(amount, 0, MaxMana));
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            int currentMana = GetMana(inSlot.Itemstack);
            dsc.AppendLine($"\nМана: {currentMana:N0} / {MaxMana:N0}");
        }
    }
}