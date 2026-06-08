using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace BotaniaStory.items
{
    public class ItemFilterScroll : ItemRollable, IContainedMeshSource
    {
        /// <summary>
        /// Тип листка определяется автоматически по коду предмета в JSON.
        ///
        ///   "botaniastory:filter-paper-black"  →  IsBlacklist = true  (чёрный список)
        ///   "botaniastory:filter-paper-white"  →  IsBlacklist = false (белый список)
        ///
        /// Оба предмета в JSON используют "class": "ItemFilterScroll" — дополнительных классов не нужно.
        /// </summary>
        public bool IsBlacklist => Code?.Path.Contains("black") == true;

        public override void OnHeldInteractStart(
    ItemSlot slot,
    EntityAgent byEntity,
    BlockSelection blockSel,
    EntitySelection entitySel,
    bool firstEvent,
    ref EnumHandHandling handling)
        {
            bool isSneak = byEntity.Controls.Sneak;
            bool isCtrl = byEntity.Controls.CtrlKey;

            // Перехватываем кастомное поведение (только Shift)
            // Если зажат только Shift + ПКМ: Открываем GUI и блокируем стандартное действие.
            if (isSneak && !isCtrl)
            {
                handling = EnumHandHandling.PreventDefault;

                if (api.Side == EnumAppSide.Client && byEntity is EntityPlayer)
                {
                    new GuiDialogFilterScroll(api as ICoreClientAPI, slot, IsBlacklist).TryOpen();
                }

                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }


        /// <summary>
        /// Проверяет, разрешает ли данный листок принять предмет с кодом <paramref name="fullItemCode"/>.
        ///
        /// Белый список: разрешает ТОЛЬКО коды/маски из списка.  Пустые списки = ничего не пропускает.
        /// Чёрный список: блокирует ТОЛЬКО коды/маски из списка. Пустые списки = всё пропускает.
        /// </summary>
        public bool AllowsItem(ItemStack filterStack, string fullItemCode)
        {
            if (filterStack?.Attributes == null || string.IsNullOrEmpty(fullItemCode))
                return false;

            var attr = filterStack.Attributes;
            string fullCodeLower = fullItemCode.ToLowerInvariant(); // например, "game:soil-medium"

            // Отрезаем домен (всё, что до двоеточия), чтобы получить только путь для текстового поиска
            string pathOnly = fullCodeLower;
            int colonIndex = fullCodeLower.IndexOf(':');
            if (colonIndex >= 0)
            {
                pathOnly = fullCodeLower.Substring(colonIndex + 1); // получаем "soil-medium"
            }

            bool matchFound = false;

            // Точная проверка по кликнутым предметам в сетке (filterList)
            if (attr.HasAttribute("filterList"))
            {
                var exactCodes = (attr["filterList"] as StringArrayAttribute)?.value;
                if (exactCodes != null)
                {
                    foreach (var code in exactCodes)
                    {
                        if (fullCodeLower == code.ToLowerInvariant())
                        {
                            matchFound = true;
                            break;
                        }
                    }
                }
            }

            // Проверка по умным текстовым маскам (filterPatterns), если точное совпадение ещё не найдено
            if (!matchFound && attr.HasAttribute("filterPatterns"))
            {
                var patterns = (attr["filterPatterns"] as StringArrayAttribute)?.value;
                if (patterns != null)
                {
                    foreach (var rawPattern in patterns)
                    {
                        // Убираем звёздочки и пробелы (" *soil* " -> "soil")
                        string pattern = rawPattern.Replace("*", "").Trim().ToLowerInvariant();
                        if (string.IsNullOrEmpty(pattern)) continue;

                        // Ищем подстроку ТОЛЬКО в пути (без "game:")
                        if (pathOnly.Contains(pattern))
                        {
                            matchFound = true;
                            break;
                        }
                    }
                }
            }

            // Проверяем, заданы ли вообще какие-то фильтры
            bool hasList = attr.HasAttribute("filterList") && (attr["filterList"] as StringArrayAttribute)?.value?.Length > 0;
            bool hasPatterns = attr.HasAttribute("filterPatterns") && (attr["filterPatterns"] as StringArrayAttribute)?.value?.Length > 0;

            // Если фильтр абсолютно пустой, то:
            // - Чёрный список (пустой) пропускает всё.
            // - Белый список (пустой) не пропускает ничего.
            if (!hasList && !hasPatterns)
            {
                return IsBlacklist;
            }

            // Для белого списка возвращаем true.
            // Для чёрного списка возвращаем false 
            return IsBlacklist ? !matchFound : matchFound;
        }
        public new MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos = null)
        {
            if (!Attributes.KeyExists("rolledShape")) return null;

            if (atBlockPos != null)
            {
                var ba = api.World.BlockAccessor;

                bool unrolledHere =
                    ba.GetBlock(atBlockPos) is BlockGroundStorage              // пол / стол
                    || ba.GetBlockEntity(atBlockPos) is BlockEntityDisplayCase; // витрина

                if (unrolledHere) return null;
            }

            var capi = api as ICoreClientAPI;
            AssetLocation loc = AssetLocation.Create(Attributes["rolledShape"].AsString(null), Code.Domain)
                .WithPathPrefixOnce("shapes/")
                .WithPathAppendixOnce(".json");

            Shape shape = capi.Assets.TryGet(loc, true).ToObject<Shape>();

            var textures = new Dictionary<string, AssetLocation>();
            if (shape.Textures != null)
                foreach (var p in shape.Textures) textures[p.Key] = p.Value;
            if (this.Textures != null)
                foreach (var p in this.Textures) textures[p.Key] = p.Value.Base;

            var cnts = new ContainedTextureSource(capi, targetAtlas, textures, $"Displayed item {Code}");

            capi.Tesselator.TesselateShape(new TesselationMetaData { TexSource = cnts }, shape, out MeshData mesh);
            return mesh;
        }
    }

}