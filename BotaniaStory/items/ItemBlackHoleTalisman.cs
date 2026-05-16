using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Collections.Generic;
using HarmonyLib;
using botaniastory;

namespace BotaniaStory.items
{
    // ИНИЦИАЛИЗАЦИЯ HARMONY ДЛЯ ИНВЕНТАРЯ

    public class BotaniaStoryTalismanSystem : ModSystem
    {
        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            harmony = new Harmony("botaniastory.talisman");
            harmony.PatchAll(); // Применяем патч при запуске
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll("botaniastory.talisman");
            base.Dispose();
        }
    }

    // ПАТЧ КЛИКА В ИНВЕНТАРЕ (DRAG & DROP)

    [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.ActivateSlot))]
    public static class TalismanSlotClickPatch
    {
        // Метод-помощник для отправки звука всем игрокам вокруг
        public static void SendTalismanSound(IWorldAccessor world, IPlayer player, string soundName)
        {
            if (world.Api.Side == EnumAppSide.Server && player != null)
            {
                var sapi = world.Api as ICoreServerAPI;
                var channel = sapi.Network.GetChannel("botanianetwork");
                if (channel != null)
                {
                    channel.BroadcastPacket(new PlayManaSoundPacket() // Проверь правильность пути (namespace) до пакета
                    {
                        Position = player.Entity.Pos.XYZ.Clone().Add(0, 1, 0), // Звук примерно на уровне груди игрока
                        SoundName = soundName
                    });
                }
            }
        }

        public static bool Prefix(ItemSlot __instance, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            if (__instance.Empty && sourceSlot.Empty) return true;

            ItemSlot talismanSlot = null;
            ItemSlot otherSlot = null;

            bool isTalismanOnCursor = sourceSlot.Itemstack?.Item is ItemBlackHoleTalisman;
            bool isTalismanInSlot = __instance.Itemstack?.Item is ItemBlackHoleTalisman;

            if (isTalismanOnCursor && isTalismanInSlot) return true;

            if (isTalismanOnCursor) { talismanSlot = sourceSlot; otherSlot = __instance; }
            else if (isTalismanInSlot) { talismanSlot = __instance; otherSlot = sourceSlot; }
            else { return true; }

            // Получаем доступ к миру
            IWorldAccessor world = __instance.Inventory?.Api?.World ?? sourceSlot.Inventory?.Api?.World;
            if (world == null) return true;

            var attrs = talismanSlot.Itemstack.Attributes;
            string storedCode = attrs.GetString("blockCode", "");
            int storedCount = attrs.GetInt("count", 0);
            int storedClassInt = attrs.GetInt("storedClass", (int)EnumItemClass.Block);

            // 1. ЛОГИКА ПОГЛОЩЕНИЯ (ЛКМ)
            if (op.MouseButton == EnumMouseButton.Left && !op.ShiftDown && !op.CtrlDown)
            {
                if (otherSlot.Empty) return true;

                var itemStack = otherSlot.Itemstack;

                if (storedCount == 0 || string.IsNullOrEmpty(storedCode))
                {
                    attrs.SetString("blockCode", itemStack.Collectible.Code.ToShortString());
                    attrs.SetInt("storedClass", (int)itemStack.Class);
                    attrs.SetInt("count", 0);
                    talismanSlot.MarkDirty();

                    storedCode = attrs.GetString("blockCode", "");
                    storedClassInt = attrs.GetInt("storedClass", (int)EnumItemClass.Block);
                }

                if (itemStack.Collectible.Code.ToShortString() == storedCode && (int)itemStack.Class == storedClassInt)
                {
                    int space = int.MaxValue - storedCount;
                    if (space > 0)
                    {
                        int toTake = Math.Min(space, itemStack.StackSize);
                        attrs.SetInt("count", storedCount + toTake);
                        otherSlot.TakeOut(toTake);
                        otherSlot.MarkDirty();
                        talismanSlot.MarkDirty();

                        // ОТПРАВЛЯЕМ ЗВУК ВСТАВКИ ПРЕДМЕТА
                        SendTalismanSound(world, op.ActingPlayer, "talisman_insert");
                    }
                    return false;
                }
                return true;
            }

            // 2. ЛОГИКА ВЫКЛАДЫВАНИЯ (ПКМ или CTRL+ПКМ)
            if (op.MouseButton == EnumMouseButton.Right && storedCount > 0 && !string.IsNullOrEmpty(storedCode))
            {
                CollectibleObject objToPlace = storedClassInt == (int)EnumItemClass.Block
                    ? (CollectibleObject)world.GetBlock(new AssetLocation(storedCode))
                    : (CollectibleObject)world.GetItem(new AssetLocation(storedCode));

                if (objToPlace != null)
                {
                    bool isSameItem = !otherSlot.Empty &&
                                      otherSlot.Itemstack.Class == (EnumItemClass)storedClassInt &&
                                      otherSlot.Itemstack.Collectible.Code.Path == objToPlace.Code.Path;

                    if (otherSlot.Empty || isSameItem)
                    {
                        int maxStack = objToPlace.MaxStackSize;
                        int currentStack = otherSlot.Empty ? 0 : otherSlot.Itemstack.StackSize;
                        int space = maxStack - currentStack;

                        if (space > 0)
                        {
                            int amountToPlace = op.CtrlDown ? maxStack : 1;

                            amountToPlace = Math.Min(amountToPlace, space);
                            amountToPlace = Math.Min(amountToPlace, storedCount);

                            if (amountToPlace > 0)
                            {
                                if (otherSlot.Empty) { otherSlot.Itemstack = new ItemStack(objToPlace, amountToPlace); }
                                else { otherSlot.Itemstack.StackSize += amountToPlace; }

                                attrs.SetInt("count", storedCount - amountToPlace);
                                otherSlot.MarkDirty();
                                talismanSlot.MarkDirty();

                                // ОТПРАВЛЯЕМ ЗВУК ИЗВЛЕЧЕНИЯ ПРЕДМЕТА
                                SendTalismanSound(world, op.ActingPlayer, "talisman_extract");

                                op.MovableQuantity = 0;
                                return false;
                            }
                        }
                        return false;
                    }
                }
            }
            return true;
        }
    }


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
            int storedClassInt = attrs.GetInt("storedClass", (int)EnumItemClass.Block);

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

                // Выгрузка в контейнер
                if (be is BlockEntityGenericTypedContainer container)
                {
                    if (api.Side == EnumAppSide.Server && storedCount > 0 && !string.IsNullOrEmpty(storedCode))
                    {
                        CollectibleObject objToPlace = storedClassInt == (int)EnumItemClass.Block
                            ? (CollectibleObject)api.World.GetBlock(new AssetLocation(storedCode))
                            : (CollectibleObject)api.World.GetItem(new AssetLocation(storedCode));

                        if (objToPlace != null)
                        {
                            int toTransfer = storedCount;

                            foreach (var invSlot in container.Inventory)
                            {
                                if (toTransfer <= 0) break;

                                if (invSlot.Empty)
                                {
                                    int transferAmount = Math.Min(toTransfer, objToPlace.MaxStackSize);
                                    invSlot.Itemstack = new ItemStack(objToPlace, transferAmount);
                                    toTransfer -= transferAmount;
                                    invSlot.MarkDirty();
                                }
                                else if (invSlot.Itemstack.Class == (EnumItemClass)storedClassInt && invSlot.Itemstack.Collectible.Code.Path == objToPlace.Code.Path)
                                {
                                    int space = objToPlace.MaxStackSize - invSlot.Itemstack.StackSize;
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

                // Бинд блока по клику в мире
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
                    attrs.SetInt("storedClass", (int)EnumItemClass.Block); // Указываем, что это блок
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

            //  ЗАПРЕТ УСТАНОВКИ, ЕСЛИ ЭТО ПРЕДМЕТ (НЕ БЛОК)
            if (storedCount > 0 && !string.IsNullOrEmpty(storedCode) && isActive)
            {
                if (storedClassInt == (int)EnumItemClass.Block) // Проверка на блок
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
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        // ЛОГИКА СКРЫТИЯ ШЕЙПОВ ПРИ РЕНДЕРЕ

        private MultiTextureMeshRef activeMesh;
        private MultiTextureMeshRef inactiveMesh;

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (activeMesh == null)
            {
                activeMesh = renderinfo.ModelRef;
            }

            bool isActive = itemstack.Attributes.GetBool("isActive", true);

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
                renderinfo.ModelRef = activeMesh;
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        private MultiTextureMeshRef GenerateInactiveMesh(ICoreClientAPI capi)
        {
            var shapeLoc = this.Shape.Base.Clone();
            shapeLoc.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            var shape = capi.Assets.TryGet(shapeLoc)?.ToObject<Shape>();

            if (shape == null) return null;

            shape.Elements = RemoveAnimatedParts(shape.Elements);
            var texSource = new TalismanTexSource(capi, this, shape);

            capi.Tesselator.TesselateShape(
                "blackholetalisman_inactive",
                shape,
                out MeshData meshData,
                texSource
            );

            return capi.Render.UploadMultiTextureMesh(meshData);
        }

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
                    if (baseSource != null)
                    {
                        var pos = baseSource[textureCode];
                        if (pos != null) return pos;
                    }

                    if (shape.Textures != null && shape.Textures.TryGetValue(textureCode, out var texPath))
                    {
                        capi.ItemTextureAtlas.GetOrInsertTexture(texPath, out _, out var shapePos);
                        return shapePos;
                    }

                    return capi.ItemTextureAtlas.UnknownTexturePosition;
                }
            }
        }

        //  ДЕБАГГЕР И УЛУЧШЕННОЕ ПОГЛОЩЕНИЕ 
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
                    int storedClassInt = attrs.GetInt("storedClass", (int)EnumItemClass.Block);
                    bool absorbedSomething = false;

                    foreach (var searchInv in player.InventoryManager.Inventories.Values)
                    {
                        if (searchInv.ClassName != "hotbar" && searchInv.ClassName != "backpack") continue;

                        foreach (var targetSlot in searchInv)
                        {
                            if (targetSlot.Empty || targetSlot == talismanSlot) continue;

                            // проверяем и тип (блок/предмет), и код
                            if ((int)targetSlot.Itemstack.Class == storedClassInt && targetSlot.Itemstack.Collectible.Code.Path == targetLoc.Path)
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

                        // ЗВУК АВТОМАТИЧЕСКОГО ПОГЛОЩЕНИЯ 
                        var channel = sapi.Network.GetChannel("botanianetwork");
                        if (channel != null)
                        {
                            channel.BroadcastPacket(new PlayManaSoundPacket()
                            {
                                Position = player.Entity.Pos.XYZ.Clone().Add(0, 1, 0),
                                SoundName = "talisman_absorb"
                            });
                        }
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
            int storedClassInt = attrs.GetInt("storedClass", (int)EnumItemClass.Block);

            dsc.AppendLine();
            dsc.AppendLine(isActive ? Lang.Get("botaniastory:talisman-mode-on") : Lang.Get("botaniastory:talisman-mode-off"));

            if (string.IsNullOrEmpty(storedCode) || storedCount == 0)
            {
                dsc.AppendLine(Lang.Get("botaniastory:talisman-empty"));
                dsc.AppendLine(Lang.Get("botaniastory:talisman-hint-bind"));
            }
            else
            {
                string displayName = storedCode;

                if (storedClassInt == (int)EnumItemClass.Block)
                {
                    Block storedBlock = world.GetBlock(new AssetLocation(storedCode));
                    if (storedBlock != null) displayName = storedBlock.GetPlacedBlockName(world, null);
                }
                else
                {
                    Item storedItem = world.GetItem(new AssetLocation(storedCode));
                    if (storedItem != null) displayName = storedItem.GetHeldItemName(new ItemStack(storedItem));
                }

                dsc.AppendLine(Lang.Get("botaniastory:talisman-contains", displayName));
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