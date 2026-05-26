using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using HarmonyLib;
using botaniastory;

namespace BotaniaStory.items
{
    // 1. СИСТЕМА АВТОСБОРА 
    public class BotaniaStoryFlowerBagSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            api.Event.RegisterGameTickListener(OnServerTick, 500);
        }

        private void OnServerTick(float dt)
        {
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player is IServerPlayer serverPlayer)
                {
                    ItemFlowerBag.AbsorbFlowersFromPlayer(serverPlayer, sapi);
                }
            }
        }
    }

    // 2. ПАТЧ ДЛЯ КЛИКА В ИНВЕНТАРЕ
    [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.ActivateSlot))]
    public static class FlowerBagSlotClickPatch
    {
        public static bool Prefix(ItemSlot __instance, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            if (__instance.Empty && sourceSlot.Empty) return true;

            ItemSlot bagSlot = null;
            ItemSlot otherSlot = null;

            bool isBagOnCursor = sourceSlot.Itemstack?.Item is ItemFlowerBag;
            bool isBagInSlot = __instance.Itemstack?.Item is ItemFlowerBag;

            if (isBagOnCursor && isBagInSlot) return true;

            if (isBagOnCursor) { bagSlot = sourceSlot; otherSlot = __instance; }
            else if (isBagInSlot) { bagSlot = __instance; otherSlot = sourceSlot; }
            else { return true; }

            if (op.MouseButton == EnumMouseButton.Left && !op.ShiftDown && !op.CtrlDown)
            {
                if (otherSlot.Empty || !ItemFlowerBag.IsMysticalFlower(otherSlot.Itemstack)) return true;

                IWorldAccessor world = __instance.Inventory?.Api?.World ?? sourceSlot.Inventory?.Api?.World;
                if (world == null) return true;

                // Запрещаем класть цветы мышкой, если интерфейс мешочка открыт
                if (ItemFlowerBag.IsBagOpen(op.ActingPlayer)) return true;

                string playerUid = op.ActingPlayer != null ? op.ActingPlayer.PlayerUID : "1";
                var bagInv = new InventoryFlowerBag("tempBag-" + playerUid, world.Api, bagSlot);

                int amountToMove = otherSlot.Itemstack.StackSize;
                bool absorbedSomething = false;

                for (int i = 0; i < bagInv.Count && amountToMove > 0; i++)
                {
                    var slot = (ItemSlotFlowerBag)bagInv[i];
                    if (slot.AllowedFlowerCode == otherSlot.Itemstack.Collectible.Code.Path)
                    {
                        // Принудительная попытка восстановить болванку
                        if (slot.Itemstack == null) slot.RestoreDummy();

                        // Защита от краша
                        if (slot.Itemstack == null) break;

                        int space = otherSlot.Itemstack.Collectible.MaxStackSize - slot.Itemstack.StackSize;
                        if (space > 0)
                        {
                            int move = Math.Min(amountToMove, space);
                            slot.Itemstack.StackSize += move;
                            otherSlot.TakeOut(move);
                            amountToMove -= move;
                            absorbedSomething = true;
                            slot.MarkDirty();
                        }
                        break;
                    }
                }

                if (absorbedSomething)
                {
                    bagInv.Save();

                    bagInv.Save();
                    bagSlot.MarkDirty();

                    otherSlot.MarkDirty();
                    op.MovableQuantity = 0;

                    if (world.Api.Side == EnumAppSide.Server && op.ActingPlayer != null)
                    {
                        var sapi = world.Api as ICoreServerAPI;
                        var channel = sapi.Network.GetChannel("botanianetwork");
                        if (channel != null)
                        {
                            channel.BroadcastPacket(new PlayManaSoundPacket()
                            {
                                Position = op.ActingPlayer.Entity.Pos.XYZ.Clone().Add(0, 1, 0),
                                SoundName = "talisman_insert"
                            });
                        }
                    }
                    return false;
                }
            }
            return true;
        }
    }

    // 3. САМ ПРЕДМЕТ МЕШОЧКА
    public class ItemFlowerBag : Item
    {
        public static bool IsMysticalFlower(ItemStack stack)
        {
            if (stack == null || stack.Collectible == null) return false;
            string path = stack.Collectible.Code.Path;
            return path.StartsWith("mysticalflower-") && path.EndsWith("-free");
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity is not EntityPlayer player)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (player.Controls.Sneak && player.Controls.CtrlKey)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (player.Controls.Sneak)
            {
                if (blockSel != null)
                {
                    BlockEntity be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                    if (be is BlockEntityGenericTypedContainer container)
                    {
                        if (api.Side == EnumAppSide.Server)
                        {
                            DumpIntoContainer(slot, container, player);
                        }
                        handling = EnumHandHandling.Handled;
                        return;
                    }
                }
                else
                {
                    bool isActive = slot.Itemstack.Attributes.GetBool("isActive", true);
                    isActive = !isActive;
                    slot.Itemstack.Attributes.SetBool("isActive", isActive);
                    slot.MarkDirty();

                    if (api.Side == EnumAppSide.Client)
                    {
                        string msg = isActive ? Lang.Get("botaniastory:msg-flowerbag-on") : Lang.Get("botaniastory:msg-flowerbag-off");
                        ((ICoreClientAPI)api).TriggerIngameError(this, "flowerbagmode", msg);
                    }

                    handling = EnumHandHandling.Handled;
                    return;
                }
            }

            if (!player.Controls.Sneak && !player.Controls.CtrlKey)
            {
                string invId = "flowerbag-" + player.PlayerUID;

                if (api.Side == EnumAppSide.Server)
                {
                    var serverPlayer = (IServerPlayer)player.Player;

                    var existingInv = serverPlayer.InventoryManager.GetOwnInventory(invId);
                    if (existingInv != null) { existingInv.Close(serverPlayer); }

                    var bagInv = new InventoryFlowerBag(invId, api, slot);
                    serverPlayer.InventoryManager.OpenInventory(bagInv);
                }
                else
                {
                    var clientPlayer = (IClientPlayer)player.Player;

                    var existingInv = clientPlayer.InventoryManager.GetOwnInventory(invId);
                    if (existingInv != null) { existingInv.Close(clientPlayer); }

                    var bagInv = new InventoryFlowerBag(invId, api, slot);
                    clientPlayer.InventoryManager.OpenInventory(bagInv);

                    var dialog = new GuiDialogFlowerBag(bagInv, (ICoreClientAPI)api, slot);
                    dialog.TryOpen();
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        private void DumpIntoContainer(ItemSlot bagSlot, BlockEntityGenericTypedContainer container, EntityPlayer player)
        {
            if (IsBagOpen(player.Player)) return;

            var bagInv = new InventoryFlowerBag("tempBag-" + player.PlayerUID, api, bagSlot);

            bool dumpedAnything = false;

            foreach (var flowerSlot in bagInv)
            {
                if (flowerSlot.Itemstack == null || flowerSlot.Itemstack.StackSize <= 0) continue;

                int toTransfer = flowerSlot.Itemstack.StackSize;

                foreach (var chestSlot in container.Inventory)
                {
                    if (toTransfer <= 0) break;

                    if (chestSlot.Empty)
                    {
                        int transferAmount = Math.Min(toTransfer, flowerSlot.Itemstack.Collectible.MaxStackSize);
                        chestSlot.Itemstack = flowerSlot.TakeOut(transferAmount);
                        toTransfer -= transferAmount;
                        chestSlot.MarkDirty();
                        dumpedAnything = true;
                    }
                    else if (chestSlot.Itemstack.Equals(api.World, flowerSlot.Itemstack, "code"))
                    {
                        int space = chestSlot.Itemstack.Collectible.MaxStackSize - chestSlot.Itemstack.StackSize;
                        if (space > 0)
                        {
                            int transferAmount = Math.Min(toTransfer, space);
                            chestSlot.Itemstack.StackSize += transferAmount;
                            flowerSlot.TakeOut(transferAmount);
                            toTransfer -= transferAmount;
                            chestSlot.MarkDirty();
                            dumpedAnything = true;
                        }
                    }
                }
            }

            if (dumpedAnything)
            {
                bagInv.Save();
                bagSlot.MarkDirty();
                container.MarkDirty(true);
                api.World.PlaySoundAt(new AssetLocation("game:sounds/player/throw"), player.Player, null, true, 16, 0.5f);
            }
        }

        public static void AbsorbFlowersFromPlayer(IServerPlayer player, ICoreServerAPI sapi)
        {
            foreach (var inv in player.InventoryManager.Inventories.Values)
            {
                if (inv.ClassName != "hotbar" && inv.ClassName != "backpack") continue;

                foreach (var slot in inv)
                {
                    if (slot.Empty || slot.Itemstack.Item is not ItemFlowerBag) continue;
                    if (!slot.Itemstack.Attributes.GetBool("isActive", true)) continue;

                    if (IsBagOpen(player)) continue;

                    var bagInv = new InventoryFlowerBag("tempFlowerBag-" + player.PlayerUID, sapi, slot);

                    bool absorbedSomething = false;

                    foreach (var searchInv in player.InventoryManager.Inventories.Values)
                    {
                        if (searchInv.ClassName != "hotbar" && searchInv.ClassName != "backpack") continue;

                        foreach (var targetSlot in searchInv)
                        {
                            if (targetSlot.Empty || targetSlot == slot) continue;

                            if (IsMysticalFlower(targetSlot.Itemstack))
                            {
                                int amountToMove = targetSlot.Itemstack.StackSize;

                                for (int i = 0; i < bagInv.Count && amountToMove > 0; i++)
                                {
                                    var bagSlot = (ItemSlotFlowerBag)bagInv[i];
                                    if (bagSlot.AllowedFlowerCode == targetSlot.Itemstack.Collectible.Code.Path)
                                    {
                                        if (bagSlot.Itemstack == null) bagSlot.RestoreDummy();

                                        if (bagSlot.Itemstack == null) break;

                                        int space = targetSlot.Itemstack.Collectible.MaxStackSize - bagSlot.Itemstack.StackSize;
                                        if (space > 0)
                                        {
                                            int move = Math.Min(amountToMove, space);
                                            bagSlot.Itemstack.StackSize += move;
                                            targetSlot.TakeOut(move);
                                            amountToMove -= move;
                                            absorbedSomething = true;
                                            bagSlot.MarkDirty();
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (absorbedSomething)
                    {
                        bagInv.Save();
                        slot.MarkDirty();

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

            bool isActive = inSlot.Itemstack.Attributes.GetBool("isActive", true);
            dsc.AppendLine(isActive ? Lang.Get("botaniastory:flowerbag-mode-on") : Lang.Get("botaniastory:flowerbag-mode-off"));
            dsc.AppendLine();
            dsc.AppendLine(Lang.Get("botaniastory:flowerbag-hint-dump"));
            dsc.AppendLine(Lang.Get("botaniastory:flowerbag-hint-toggle"));
        }
        public static bool IsBagOpen(IPlayer player)
        {
            if (player?.InventoryManager?.Inventories == null) return false;

            foreach (var inv in player.InventoryManager.Inventories.Values)
            {
                if (inv is InventoryFlowerBag) return true;
            }
            return false;
        }
    }

    // 4. ИНВЕНТАРЬ МЕШОЧКА
    public class InventoryFlowerBag : InventoryBase
    {
        public static readonly string[] FlowerColors = new string[] {
            "white", "orange", "magenta", "lightblue", "yellow", "lime", "pink", "gray", "lightgray",
            "cyan", "purple", "blue", "brown", "green", "red", "black"
        };

        private ItemSlot[] slots;
        public ItemSlot BagSlot { get; }

        public override int Count => 16;

        public InventoryFlowerBag(string inventoryID, ICoreAPI api, ItemSlot bagSlot) : base(inventoryID, api)
        {
            this.BagSlot = bagSlot;

            slots = new ItemSlot[Count];
            for (int i = 0; i < Count; i++)
            {
                // Формируем код цветка для каждого из 16 слотов
                string code = "mysticalflower-" + FlowerColors[i] + "-free";
                slots[i] = new ItemSlotFlowerBag(this, code, api);
            }

            Load();

            // После загрузки обязательно восстанавливаем визуальные болванки для пустых слотов
            for (int i = 0; i < Count; i++)
            {
                ((ItemSlotFlowerBag)slots[i]).RestoreDummy();
            }
        }

        public override ItemSlot this[int slotId]
        {
            get => slots[slotId];
            set => slots[slotId] = value;
        }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            slots = SlotsFromTreeAttributes(tree, slots);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
        }

        public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot)
        {
            return sinkSlot.CanHold(sourceSlot);
        }

        public void Load()
        {
            if (BagSlot.Itemstack == null) return;
            ITreeAttribute tree = BagSlot.Itemstack.Attributes.GetTreeAttribute("flowerBagInv");
            if (tree != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    slots[i].Itemstack = tree.GetItemstack("slot" + i);
                    slots[i].Itemstack?.ResolveBlockOrItem(Api.World);
                }
            }
        }

        public void Save()
        {
            if (BagSlot.Itemstack == null) return;
            ITreeAttribute tree = new TreeAttribute();
            for (int i = 0; i < slots.Length; i++)
            {
                // Не сохраняем в NBT данные о пустых болванках (размер стака 0)
                if (slots[i].Itemstack != null && slots[i].Itemstack.StackSize > 0)
                {
                    tree.SetItemstack("slot" + i, slots[i].Itemstack);
                }
            }
            BagSlot.Itemstack.Attributes["flowerBagInv"] = tree;
        }

        public override void MarkSlotDirty(int slotId)
        {
            base.MarkSlotDirty(slotId);
            Save();
        }

        public override object Close(IPlayer player)
        {
            Save();
            if (Api.Side == EnumAppSide.Server)
            {
                BagSlot.MarkDirty();
            }
            return base.Close(player);
        }

        public override float GetTransitionSpeedMul(EnumTransitionType transType, ItemStack stack)
        {
            return 0f;
        }
    }

    // 5. GUI МЕШОЧКА (КЛИЕНТ)
    public class GuiDialogFlowerBag : GuiDialog
    {
        private InventoryFlowerBag inventory;
        private ItemSlot bagSlot;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogFlowerBag(InventoryFlowerBag inventory, ICoreClientAPI capi, ItemSlot bagSlot) : base(capi)
        {
            this.inventory = inventory;
            this.bagSlot = bagSlot;
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds mainBounds = ElementBounds.Fixed(0, 0, 400, 250);
            ElementBounds slotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.CenterMiddle, 0, 20, 8, 2);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            SingleComposer = capi.Gui.CreateCompo("flowerbag", ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("botaniastory:item-flower_bag"), OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddItemSlotGrid(inventory, SendInvPacket, 8, slotBounds, "flowerSlots")
                .EndChildElements()
                .Compose();
        }

        private void SendInvPacket(object p)
        {
            capi.Network.SendPacketClient(p);
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            capi.Network.SendPacketClient(capi.World.Player.InventoryManager.CloseInventory(inventory));
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }
    }
    // КАСТОМНЫЙ СЛОТ ДЛЯ КОНКРЕТНОГО ЦВЕТКА
    public class ItemSlotFlowerBag : ItemSlot
    {
        public string AllowedFlowerCode;
        public ICoreAPI Api;

        public ItemSlotFlowerBag(InventoryBase inventory, string allowedCode, ICoreAPI api) : base(inventory)
        {
            AllowedFlowerCode = allowedCode;
            Api = api;
        }

        // Восстанавливает "болванку" (стак с размером 0), если слот пуст
        public void RestoreDummy()
        {
            if (Api?.World == null) return;

            if (this.itemstack == null || this.itemstack.StackSize <= 0)
            {
                AssetLocation loc = new AssetLocation("botaniastory", AllowedFlowerCode);

                CollectibleObject collectible = Api.World.GetBlock(loc);

                if (collectible == null)
                {
                    collectible = Api.World.GetItem(loc);
                }

                if (collectible != null)
                {
                    this.itemstack = new ItemStack(collectible, 0);
                }
            }
        }

        public override bool CanHold(ItemSlot sourceSlot)
        {
            if (sourceSlot.Itemstack == null) return false;
            return sourceSlot.Itemstack.Collectible.Code.Path == AllowedFlowerCode;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            if (sourceSlot.Itemstack == null) return false;
            return sourceSlot.Itemstack.Collectible.Code.Path == AllowedFlowerCode;
        }

        // Каждый раз, когда содержимое слота меняется, проверяем, не нужно ли вернуть болванку
        public override void OnItemSlotModified(ItemStack sinkStack)
        {
            base.OnItemSlotModified(sinkStack);
            RestoreDummy();
        }
    }
}