using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config; // Важно для Lang.Get
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Collections.Generic;

namespace BotaniaStory.items
{
    public class ItemBlackHoleTalisman : Item
    {
        private const int MaxCapacity = int.MaxValue;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity is not EntityPlayer player)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            var attrs = slot.Itemstack.Attributes;
            string storedCode = attrs.GetString("blockCode", "");
            int storedCount = attrs.GetInt("count", 0);
            bool isActive = attrs.GetBool("isActive", true);

            if (player.Controls.Sneak && player.Controls.CtrlKey)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (blockSel == null)
            {
                if (player.Controls.Sneak)
                {
                    isActive = !isActive;
                    attrs.SetBool("isActive", isActive);
                    slot.MarkDirty();

                    if (api.Side == EnumAppSide.Client)
                    {
                        string msg = isActive ? Lang.Get("botaniastory:msg-mode-on") : Lang.Get("botaniastory:msg-mode-off");
                        ((ICoreClientAPI)api).TriggerIngameError(this, "talismanmode", msg);
                    }
                    handling = EnumHandHandling.Handled;
                }
                return;
            }

            Block clickedBlock = api.World.BlockAccessor.GetBlock(blockSel.Position);
            if (clickedBlock.Id == 0) return;

            if (player.Controls.Sneak)
            {
                BlockEntity be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position);

                if (be is BlockEntityGenericTypedContainer container)
                {
                    if (api.Side == EnumAppSide.Server && storedCount > 0 && !string.IsNullOrEmpty(storedCode))
                    {
                        Block blockToPlace = api.World.GetBlock(new AssetLocation(storedCode));
                        if (blockToPlace != null)
                        {
                            int toTransfer = storedCount;

                            foreach (var invSlot in container.Inventory)
                            {
                                if (toTransfer <= 0) break;

                                if (invSlot.Empty)
                                {
                                    int transferAmount = Math.Min(toTransfer, blockToPlace.MaxStackSize);
                                    invSlot.Itemstack = new ItemStack(blockToPlace, transferAmount);
                                    toTransfer -= transferAmount;
                                    invSlot.MarkDirty();
                                }
                                else if (invSlot.Itemstack.Class == EnumItemClass.Block && invSlot.Itemstack.Block.Code.Path == blockToPlace.Code.Path)
                                {
                                    int space = blockToPlace.MaxStackSize - invSlot.Itemstack.StackSize;
                                    if (space > 0)
                                    {
                                        int transferAmount = Math.Min(toTransfer, space);
                                        invSlot.Itemstack.StackSize += transferAmount;
                                        toTransfer -= transferAmount;
                                        invSlot.MarkDirty();
                                    }
                                }
                            }

                            if (toTransfer != storedCount)
                            {
                                attrs.SetInt("count", toTransfer);
                                slot.MarkDirty();
                                api.World.PlaySoundAt(new AssetLocation("game:sounds/player/throw"), player.Player, null, true, 16, 0.5f);
                            }
                        }
                    }
                    handling = EnumHandHandling.Handled;
                    return;
                }

                if (string.IsNullOrEmpty(storedCode) || storedCount == 0)
                {
                    if (clickedBlock.EntityClass != null)
                    {
                        if (api is ICoreClientAPI capi) capi.TriggerIngameError(this, "cannotbind", Lang.Get("botaniastory:error-cannotbind"));
                        handling = EnumHandHandling.Handled;
                        return;
                    }

                    ItemStack pickStack = clickedBlock.OnPickBlock(api.World, blockSel.Position);
                    AssetLocation codeToSave = (pickStack != null && pickStack.Class == EnumItemClass.Block)
                        ? pickStack.Collectible.Code
                        : clickedBlock.Code;

                    attrs.SetString("blockCode", codeToSave.ToShortString());
                    attrs.SetInt("count", 0);
                    slot.MarkDirty();

                    if (api.Side == EnumAppSide.Client)
                    {
                        ((ICoreClientAPI)api).TriggerIngameError(this, "talismanbind", Lang.Get("botaniastory:msg-bound"));
                    }
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            if (storedCount > 0 && !string.IsNullOrEmpty(storedCode) && isActive)
            {
                Block blockToPlace = api.World.GetBlock(new AssetLocation(storedCode));
                if (blockToPlace != null && clickedBlock.EntityClass == null)
                {
                    BlockPos placePos = blockSel.Position.AddCopy(blockSel.Face);
                    Block blockAtPos = api.World.BlockAccessor.GetBlock(placePos);

                    if (blockAtPos.IsReplacableBy(blockToPlace))
                    {
                        if (api.Side == EnumAppSide.Server)
                        {
                            api.World.BlockAccessor.SetBlock(blockToPlace.BlockId, placePos);
                            attrs.SetInt("count", storedCount - 1);
                            slot.MarkDirty();
                        }
                        handling = EnumHandHandling.Handled;
                        api.World.PlaySoundAt(blockToPlace.Sounds?.Place.Location ?? new AssetLocation("game:sounds/block/dirt"), placePos.X, placePos.Y, placePos.Z, player.Player);
                        return;
                    }
                }
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
        // ==========================================
        // === ЛОГИКА СКРЫТИЯ ШЕЙПОВ ПРИ РЕНДЕРЕ ====
        // ==========================================

        private MultiTextureMeshRef activeMesh;   // Оригинальная (включенная) модель
        private MultiTextureMeshRef inactiveMesh;

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            // 1. При самом первом рендере кэшируем оригинальную модель, которую собрала игра
            if (activeMesh == null)
            {
                activeMesh = renderinfo.ModelRef;
            }

            bool isActive = itemstack.Attributes.GetBool("isActive", true);

            // 2. Если талисман выключен — генерируем (один раз) и ставим модель без анимации
            if (!isActive)
            {
                if (inactiveMesh == null)
                {
                    inactiveMesh = GenerateInactiveMesh(capi);
                }

                renderinfo.ModelRef = inactiveMesh;
            }
            else
            {
                // 3. ОБЯЗАТЕЛЬНО возвращаем оригинальную модель!
                // Если этого не сделать, игра применит "кастрированную" модель к другим предметам на витрине.
                renderinfo.ModelRef = activeMesh;
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        private MultiTextureMeshRef GenerateInactiveMesh(ICoreClientAPI capi)
        {
            // ВАЖНО: Добавляем .Clone(), чтобы не сломать глобальный путь модели в памяти игры!
            var shapeLoc = this.Shape.Base.Clone();

            // Безопасно формируем путь к json-файлу
            shapeLoc.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            var shape = capi.Assets.TryGet(shapeLoc)?.ToObject<Shape>();

            if (shape == null) return null;

            // Вырезаем все детали с именем "animatedpart"
            shape.Elements = RemoveAnimatedParts(shape.Elements);

            // Используем НАШ источник текстур, чтобы избежать невидимых моделей и поломок полок
            var texSource = new TalismanTexSource(capi, this, shape);

            capi.Tesselator.TesselateShape(
                "blackholetalisman_inactive",
                shape,
                out MeshData meshData,
                texSource
            );

            return capi.Render.UploadMultiTextureMesh(meshData);
        }

        // Рекурсивный метод удаления элементов
        private Vintagestory.API.Common.ShapeElement[] RemoveAnimatedParts(Vintagestory.API.Common.ShapeElement[] elements)
        {
            if (elements == null) return null;

            var result = new System.Collections.Generic.List<Vintagestory.API.Common.ShapeElement>();

            foreach (var el in elements)
            {
                if (el.Name == "animatedpart") continue;

                if (el.Children != null)
                {
                    el.Children = RemoveAnimatedParts(el.Children);
                }

                result.Add(el);
            }

            return result.ToArray();
        }

        // Вспомогательный класс-источник, который умеет читать текстуры прямо из Shape
        private class TalismanTexSource : ITexPositionSource
        {
            private ICoreClientAPI capi;
            private ITexPositionSource baseSource;
            private Shape shape;

            public TalismanTexSource(ICoreClientAPI capi, Item item, Shape shape)
            {
                this.capi = capi;
                this.baseSource = capi.Tesselator.GetTextureSource(item);
                this.shape = shape;
            }

            public Size2i AtlasSize => capi.ItemTextureAtlas.Size;

            public TextureAtlasPosition this[string textureCode]
            {
                get
                {
                    // Шаг 1: Ищем текстуру в конфигурации предмета
                    if (baseSource != null)
                    {
                        var pos = baseSource[textureCode];
                        if (pos != null) return pos;
                    }

                    // Шаг 2: Если не нашли, ищем внутри самого JSON-файла модели (это чинит невидимость!)
                    if (shape.Textures != null && shape.Textures.TryGetValue(textureCode, out var texPath))
                    {
                        capi.ItemTextureAtlas.GetOrInsertTexture(texPath, out _, out var shapePos);
                        return shapePos;
                    }

                    // Если вообще ничего не найдено, отдаем текстуру "неизвестно" (фиолетово-черные квадраты), 
                    // чтобы не крашить OpenGL и не ломать полки.
                    return capi.ItemTextureAtlas.UnknownTexturePosition;
                }
            }
        }
        // --- ДЕБАГГЕР И УЛУЧШЕННОЕ ПОГЛОЩЕНИЕ ---
        public static void AbsorbBlocksFromPlayer(IServerPlayer player, ICoreServerAPI sapi)
        {
            
            foreach (var inv in player.InventoryManager.Inventories.Values)
            {
                if (inv.ClassName != "hotbar" && inv.ClassName != "backpack") continue;

                foreach (var talismanSlot in inv)
                {
                    if (talismanSlot.Empty || talismanSlot.Itemstack.Item is not ItemBlackHoleTalisman) continue;

                    var attrs = talismanSlot.Itemstack.Attributes;
                    if (!attrs.GetBool("isActive", true)) continue;

                    string storedCode = attrs.GetString("blockCode", "");
                    if (string.IsNullOrEmpty(storedCode)) continue;

                    AssetLocation targetLoc = new AssetLocation(storedCode);
                    int count = attrs.GetInt("count", 0);
                    bool absorbedSomething = false;


                    foreach (var searchInv in player.InventoryManager.Inventories.Values)
                    {
                        if (searchInv.ClassName != "hotbar" && searchInv.ClassName != "backpack") continue;

                        foreach (var targetSlot in searchInv)
                        {
                            if (targetSlot.Empty || targetSlot == talismanSlot) continue;


                            // Сравниваем только по Path (игнорируем домен game:)
                            if (targetSlot.Itemstack.Class == EnumItemClass.Block && targetSlot.Itemstack.Block.Code.Path == targetLoc.Path)
                            {
                                int amount = targetSlot.Itemstack.StackSize;
                                if (count + amount <= MaxCapacity && count + amount > 0)
                                {
                                    count += amount;
                                    targetSlot.TakeOutWhole();
                                    targetSlot.MarkDirty();
                                    absorbedSomething = true;

                                }
                            }
                        }
                    }

                    if (absorbedSomething)
                    {
                        attrs.SetInt("count", count);
                        talismanSlot.MarkDirty();
                    }
                }
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Inventory == null ||
                inSlot.Inventory.ClassName == "creative" ||
                inSlot.Inventory.ClassName == "recipe")
            {
                return;
            }

            var attrs = inSlot.Itemstack.Attributes;
            string storedCode = attrs.GetString("blockCode", "");
            int storedCount = attrs.GetInt("count", 0);
            bool isActive = attrs.GetBool("isActive", true);

            dsc.AppendLine();
            dsc.AppendLine(isActive ? Lang.Get("botaniastory:talisman-mode-on") : Lang.Get("botaniastory:talisman-mode-off"));

            if (string.IsNullOrEmpty(storedCode) || storedCount == 0)
            {
                dsc.AppendLine(Lang.Get("botaniastory:talisman-empty"));
                dsc.AppendLine(Lang.Get("botaniastory:talisman-hint-bind"));
            }
            else
            {
                Block storedBlock = world.GetBlock(new AssetLocation(storedCode));
                string blockName = storedBlock != null ? storedBlock.GetPlacedBlockName(world, null) : storedCode;

                dsc.AppendLine(Lang.Get("botaniastory:talisman-contains", blockName));
                dsc.AppendLine(Lang.Get("botaniastory:talisman-count", storedCount));
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("botaniastory:talisman-hint-place"));
                dsc.AppendLine(Lang.Get("botaniastory:talisman-hint-dump"));
            }

            dsc.AppendLine(Lang.Get("botaniastory:talisman-hint-toggle"));
            dsc.AppendLine(Lang.Get("botaniastory:talisman-hint-drop"));
        }
    }
}