using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using System.Collections.Concurrent;
using BotaniaStory.client.gui;

namespace BotaniaStory.network
{
    // Пакет записи рецепта на гало
    // Segment = -1  - записать в буфер "последний рецепт" (lastRecipe)
    // Segment >= 0  - записать сразу в конкретный сегмент
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class HaloRecipePacket
    {
        public int Segment;
        public byte[] Data; // сериализованное дерево: in0..in8 + out
    }

    // Привязка переднего сегмента (верстака) к взгляду на момент открытия меню.
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class HaloAnchorPacket
    {
        public float Yaw;
    }
}

namespace BotaniaStory.items
{
    using BotaniaStory.network;

    /// <summary>
    /// 12 слотов по кругу. Слот 0 - сам halo (узел-"верстак"), слоты 1..11 - рецепты.
    /// Рецепт хранится конкретными стаками в атрибутах предмета: на 1.22 ингредиенты
    /// GridRecipe обнулены и матчить рецепты во время игры нельзя - поэтому крафт идёт
    /// через настоящую сетку игрока, а сюда сохраняется уже результат+ингредиенты.
    /// </summary>
    public class ItemCraftingHalo : Item
    {
        public const int SEGMENTS = 12;

        const float FRONT_OFFSET_DEG = 90f;
        const float HALF_SEG = 360f / SEGMENTS / 2f; // центр сегмента = 15°

        // yaw на момент открытия. Ключ - PlayerUID.
        // На клиенте лежит локальный игрок, на сервере - все (приходит пакетом).
        // ConcurrentDictionary: в singleplayer статик общий для потоков клиента и сервера.
        static readonly ConcurrentDictionary<string, float> anchorYawByPlayer = new ConcurrentDictionary<string, float>();

        // окно крафта (одно на клиента)
        static GuiDialogHaloCraft craftDialog;

        // Выбор сегмента по взгляду (чистый yaw)
        public static int GetLookedAtSegment(EntityAgent e)
        {
            float ringRotDeg = GetRingRotDeg(e);
            float deg = GameMath.Mod(-e.Pos.Yaw * GameMath.RAD2DEG + FRONT_OFFSET_DEG - ringRotDeg, 360f);
            int seg = (int)(deg / (360f / SEGMENTS));
            return GameMath.Mod(seg, SEGMENTS);
        }

        // Поворот кольца, ставящий сегмент 0 по центру взгляда открытия
        // Нет привязки (меню ещё не открывали) - 0, т.е. прежнее поведение
        public static float GetRingRotDeg(EntityAgent e)
        {
            string uid = (e as EntityPlayer)?.PlayerUID;
            if (uid == null || !anchorYawByPlayer.TryGetValue(uid, out float yaw)) return 0f;
            return GameMath.Mod(-yaw * GameMath.RAD2DEG + FRONT_OFFSET_DEG - HALF_SEG, 360f);
        }

        // (КЛИЕНТ) меню открылось - запомнить yaw и отправить на сервер
        public static void SetAnchorYaw(ICoreClientAPI capi, float yaw)
        {
            string uid = capi?.World?.Player?.PlayerUID;
            if (uid == null) return;
            anchorYawByPlayer[uid] = yaw;
            (capi.Network.GetChannel("botanianetwork") as IClientNetworkChannel)
                ?.SendPacket(new HaloAnchorPacket { Yaw = yaw });
        }

        // (СЕРВЕР) приём привязки
        public static void OnHaloAnchor(IServerPlayer fromPlayer, HaloAnchorPacket msg)
        {
            if (fromPlayer?.PlayerUID == null) return;
            anchorYawByPlayer[fromPlayer.PlayerUID] = msg.Yaw;
        }

        // Взаимодействие
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
            EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefault;
            if (!firstEvent) return;

            int seg = GetLookedAtSegment(byEntity);

            // Слот 0 = узел - "верстак": открыть окно крафта (клиент)
            if (seg == 0)
            {
                if (byEntity.World.Side == EnumAppSide.Client && byEntity is EntityPlayer)
                {
                    ICoreClientAPI capi = byEntity.World.Api as ICoreClientAPI;
                    if (capi != null && (craftDialog == null || !craftDialog.IsOpened()))
                    {
                        craftDialog = new GuiDialogHaloCraft(capi);
                        craftDialog.TryOpen();
                    }
                }
                return;
            }

            // Слоты 1..11 - рецепты. Авторитетно на сервере
            if (byEntity.World.Side != EnumAppSide.Server) return;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            bool sneak = byEntity.Controls.Sneak;
            ItemStack halo = slot.Itemstack;

            if (sneak)
            {
                if (HasSegment(halo, seg))
                {
                    halo.Attributes.RemoveAttribute("seg" + seg);
                    slot.MarkDirty();
                }
                return;
            }

            if (HasSegment(halo, seg))
            {
                TryCraft(player, halo, seg, true);                          // крафт
            }
            else if (halo.Attributes["lastRecipe"] is ITreeAttribute buf)
            {
                halo.Attributes["seg" + seg] = (ITreeAttribute)buf.Clone(); // записать из буфера
                slot.MarkDirty();
            }
        }

        // Крафт
        public static bool TryCraft(IPlayer player, ItemStack halo, int seg, bool doConsume)
        {
            IWorldAccessor world = player.Entity.World;

            ItemStack output = GetSegmentOutput(halo, world, seg);
            if (output?.Collectible == null) return false;

            ItemStack[] inputs = GetSegmentInputs(halo, world, seg);

            List<ItemSlot> invSlots = new List<ItemSlot>();
            player.Entity.WalkInventory(s =>
            {
                string cn = s?.Inventory?.ClassName;
                if (s?.Itemstack != null &&
                    (cn == GlobalConstants.hotBarInvClassName || cn == GlobalConstants.backpackInvClassName))
                    invSlots.Add(s);
                return true;
            });

            Dictionary<ItemSlot, int> reserved = new Dictionary<ItemSlot, int>();
            foreach (ItemStack inp in inputs)
            {
                if (inp?.Collectible == null) continue;

                bool found = false;
                foreach (ItemSlot s in invSlots)
                {
                    int used = reserved.TryGetValue(s, out int u) ? u : 0;
                    if (s.StackSize - used <= 0) continue;
                    if (Matches(inp, s.Itemstack))
                    {
                        reserved[s] = used + 1;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            if (!doConsume) return true;

            foreach (KeyValuePair<ItemSlot, int> kv in reserved)
            {
                kv.Key.TakeOut(kv.Value);
                kv.Key.MarkDirty();
            }

            ItemStack give = output.Clone();
            if (!player.InventoryManager.TryGiveItemstack(give, true))
                world.SpawnItemEntity(give, player.Entity.Pos.XYZ.Add(0, 0.5, 0));

            return true;
        }

        static bool Matches(ItemStack ingredient, ItemStack inStack)
        {
            return ingredient?.Collectible != null && inStack?.Collectible != null
                && ingredient.Collectible == inStack.Collectible;
        }

        // Хранилище рецептов в атрибутах стака
        static ItemStack ReadStack(ITreeAttribute tree, string key, IWorldAccessor world)
        {
            if (tree?[key] is ItemstackAttribute isa && isa.value != null)
            {
                isa.value.ResolveBlockOrItem(world);
                return isa.value;
            }
            return null;
        }

        public static bool HasSegment(ItemStack halo, int seg)
            => halo?.Attributes?["seg" + seg] is ITreeAttribute;

        public static ItemStack GetSegmentOutput(ItemStack halo, IWorldAccessor world, int seg)
            => halo?.Attributes?["seg" + seg] is ITreeAttribute t ? ReadStack(t, "out", world) : null;

        public static ItemStack[] GetSegmentInputs(ItemStack halo, IWorldAccessor world, int seg)
        {
            ItemStack[] arr = new ItemStack[9];
            if (halo?.Attributes?["seg" + seg] is ITreeAttribute t)
                for (int i = 0; i < 9; i++) arr[i] = ReadStack(t, "in" + i, world);
            return arr;
        }

        // (КЛИЕНТ) запись рецепта из сетки крафта
        // notify=true - с сообщениями (хоткей G); notify=false - молча (авто из окна крафта).
        public static bool OnCaptureHotkey(ICoreClientAPI capi, KeyCombination comb)
        {
            CaptureFromGrid(capi, true);
            return true;
        }

        public static bool CaptureFromGrid(ICoreClientAPI capi, bool notify)
        {
            if (capi == null) return false;

            IPlayer player = capi.World.Player;
            ItemStack halo = player.InventoryManager.ActiveHotbarSlot?.Itemstack;
            if (!(halo?.Collectible is ItemCraftingHalo)) return false;

            IInventory grid = GetCraftingGridInv(player);
            if (grid == null || grid.Count < 1) return false;

            int outIdx = grid.Count - 1;                 // последний слот сетки = результат
            ItemStack output = grid[outIdx]?.Itemstack;
            if (output == null)
            {
                if (notify) capi.TriggerIngameError(capi, "halo", "В сетке крафта нет готового результата");
                return false;
            }

            TreeAttribute tree = new TreeAttribute();
            for (int i = 0; i < outIdx && i < 9; i++)
            {
                ItemStack st = grid[i]?.Itemstack;
                if (st != null)
                {
                    ItemStack one = st.Clone();
                    one.StackSize = 1;
                    tree["in" + i] = new ItemstackAttribute(one);
                }
            }
            tree["out"] = new ItemstackAttribute(output.Clone());

            (capi.Network.GetChannel("botanianetwork") as IClientNetworkChannel)
                ?.SendPacket(new HaloRecipePacket() { Segment = -1, Data = SerializeTree(tree) });

            if (notify) capi.TriggerIngameError(capi, "halo", "Рецепт записан в гало");
            return true;
        }

        // (СЕРВЕР) приём рецепта
        public static void OnHaloRecipe(ICoreServerAPI sapi, IServerPlayer fromPlayer, HaloRecipePacket msg)
        {
            ItemSlot slot = fromPlayer.InventoryManager.ActiveHotbarSlot;
            if (!(slot?.Itemstack?.Collectible is ItemCraftingHalo)) return;

            ITreeAttribute tree = DeserializeTree(msg.Data);
            if (msg.Segment < 0) slot.Itemstack.Attributes["lastRecipe"] = tree;
            else slot.Itemstack.Attributes["seg" + msg.Segment] = tree;
            slot.MarkDirty();
        }

        // (СЕРВЕР) пассивный автокрафт (слоты 1..11)
        public static void RunAutocraft(ICoreServerAPI sapi)
        {
            foreach (IServerPlayer p in sapi.World.AllOnlinePlayers)
            {
                if (p.ConnectionState != EnumClientState.Playing) continue;

                List<ItemStack> halos = new List<ItemStack>();
                p.Entity.WalkInventory(s =>
                {
                    if (s?.Itemstack?.Collectible is ItemAutocraftingHalo) halos.Add(s.Itemstack);
                    return true;
                });

                foreach (ItemStack halo in halos)
                    for (int seg = 1; seg < SEGMENTS; seg++)
                        if (HasSegment(halo, seg)) TryCraft(p, halo, seg, true);
            }
        }

        // утилиты
        public static IInventory GetCraftingGridInv(IPlayer player)
        {
            IInventory inv = player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName);
            if (inv != null) return inv;

            foreach (KeyValuePair<string, IInventory> kv in player.InventoryManager.Inventories)
                if (kv.Key.IndexOf("craftinggrid", StringComparison.OrdinalIgnoreCase) >= 0) return kv.Value;
            return null;
        }

        public static byte[] SerializeTree(ITreeAttribute tree)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                tree.ToBytes(w);
                return ms.ToArray();
            }
        }

        public static ITreeAttribute DeserializeTree(byte[] data)
        {
            TreeAttribute tree = new TreeAttribute();
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader r = new BinaryReader(ms))
            {
                tree.FromBytes(r);
            }
            return tree;
        }
    }

    /// <summary>
    /// Автокрафтящее гало: в руке как обычное гало, а лёжа в инвентаре непрерывно
    /// крафтит все записанные рецепты через ItemCraftingHalo.RunAutocraft.
    /// </summary>
    public class ItemAutocraftingHalo : ItemCraftingHalo
    {
    }
}