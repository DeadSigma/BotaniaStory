using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using BotaniaStory.items;

namespace BotaniaStory.client.gui
{
    public class GuiDialogHaloCraft : GuiDialog
    {
        readonly IInventory craftInv;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogHaloCraft(ICoreClientAPI capi) : base(capi)
        {
            craftInv = capi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName);
            ComposeDialog();
        }

        void ComposeDialog()
        {
            if (craftInv == null) return;

            // 3x3 сетка ввода + 1 слот результата справа
            ElementBounds inputBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 35, 3, 3);
            ElementBounds outputBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 35, 1, 1)
                .FixedRightOf(inputBounds, 60);

            ElementBounds hintBounds = ElementBounds.Fixed(0, 0, 260, 25);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(hintBounds, inputBounds, outputBounds);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle);

            SingleComposer = capi.Gui.CreateCompo("halocraft", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Ускоряшечка", OnTitleClose)
                .BeginChildElements(bgBounds)
                    .AddStaticText("", CairoFont.WhiteSmallText(), hintBounds)
                    .AddItemSlotGrid(craftInv, SendInvPacket, 3, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, inputBounds, "input")
                    .AddItemSlotGrid(craftInv, SendInvPacket, 1, new int[] { 9 }, outputBounds, "output")
                .EndChildElements()
                .Compose();
        }

        // Слот-взаимодействия настоящего инвентаря игрока уходят на сервер
        void SendInvPacket(object packet)
        {
            capi.Network.SendPacketClient(packet);
        }

        void OnTitleClose() => TryClose();

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            if (craftInv == null) return;

            // открыть/синхронизировать сетку крафта на время окна
            object pkt = capi.World.Player.InventoryManager.OpenInventory(craftInv);
            if (pkt != null) capi.Network.SendPacketClient(pkt);

            craftInv.SlotModified += OnGridChanged;
        }

        public override void OnGuiClosed()
        {
            if (craftInv != null)
            {
                craftInv.SlotModified -= OnGridChanged;

                // вернуть  "висящий на курсоре" предмет и закрыть инвентарь
                SingleComposer.GetSlotGrid("input")?.OnGuiClosed(capi);
                SingleComposer.GetSlotGrid("output")?.OnGuiClosed(capi);

                object pkt = capi.World.Player.InventoryManager.CloseInventory(craftInv);
                if (pkt != null) capi.Network.SendPacketClient(pkt);
            }
            base.OnGuiClosed();
        }

        // На любое изменение сетки: если есть результат — гало молча запоминает рецепт
        void OnGridChanged(int slotId)
        {
            ItemCraftingHalo.CaptureFromGrid(capi, false);
        }
    }
}