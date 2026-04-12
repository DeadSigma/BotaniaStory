using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace BotaniaStory
{
    public class ItemManaArmor : Item
    {
        private ICoreServerAPI sapi;
        private static bool tickRegistered = false;
        private static long tickListenerId;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is ICoreServerAPI serverApi)
            {
                sapi = serverApi;
                if (!tickRegistered)
                {
                    // Ускорил таймер: теперь чинит раз в 1 секунду (1000 мс)
                    tickListenerId = sapi.Event.RegisterGameTickListener(OnArmorRepairTick, 1000);
                    tickRegistered = true;
                }
            }
        }

        // ВАЖНО: Этот метод сбрасывает таймер при выходе из мира, чтобы он не сломался при перезаходе
        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);
            if (api is ICoreServerAPI serverApi && tickRegistered)
            {
                serverApi.Event.UnregisterGameTickListener(tickListenerId);
                tickRegistered = false;
            }
        }

        // ==========================================
        // 1. ПОГЛОЩЕНИЕ УРОНА ПРИ УДАРАХ
        // ==========================================
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            if (amount <= 0) return;

            if (byEntity is EntityPlayer entityPlayer && entityPlayer.Player != null)
            {
                IPlayer player = entityPlayer.Player;

                //Съедает 120 маны за 1 единицу прочности
                int manaPerDamage = 120;
                int remainingDamage = amount;

                IInventory[] playerInventories = new IInventory[]
                {
                    player.InventoryManager.GetOwnInventory("character"), // Добавил character (для левой руки)
                    player.InventoryManager.GetOwnInventory("hotbar"),
                    player.InventoryManager.GetOwnInventory("backpack")
                };

                foreach (IInventory inv in playerInventories)
                {
                    if (inv == null) continue;
                    foreach (ItemSlot slot in inv)
                    {
                        if (slot.Empty) continue;
                        if (slot.Itemstack.Item is ItemManaTablet tabletItem)
                        {
                            int currentMana = tabletItem.GetMana(slot.Itemstack);
                            if (currentMana >= manaPerDamage)
                            {
                                int manaNeeded = remainingDamage * manaPerDamage;
                                if (currentMana >= manaNeeded)
                                {
                                    tabletItem.SetMana(slot.Itemstack, currentMana - manaNeeded);
                                    slot.MarkDirty();
                                    remainingDamage = 0;
                                    break;
                                }
                                else
                                {
                                    int absorbedDamage = currentMana / manaPerDamage;
                                    int leftoverMana = currentMana % manaPerDamage;
                                    tabletItem.SetMana(slot.Itemstack, leftoverMana);
                                    slot.MarkDirty();
                                    remainingDamage -= absorbedDamage;
                                }
                            }
                        }
                    }
                    if (remainingDamage <= 0) break;
                }
                amount = remainingDamage;
            }

            if (amount > 0)
            {
                base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
            }
        }

        // ==========================================
        // 2. АВТОМАТИЧЕСКАЯ ПОЧИНКА БРОНИ (ФОНОВАЯ)
        // ==========================================
        private void OnArmorRepairTick(float dt)
        {
            if (sapi == null) return;

            foreach (IServerPlayer player in sapi.Server.Players)
            {
                if (player.ConnectionState != EnumClientState.Playing || player.Entity == null) continue;
                RepairArmorForPlayer(player);
            }
        }

        private void RepairArmorForPlayer(IPlayer player)
        {
            //Автоматическая починка, съедает 120 маны за 1 единицу прочности
            int manaPerRepair = 120;

            IInventory gearInv = player.InventoryManager.GetOwnInventory("character");
            IInventory hotbarInv = player.InventoryManager.GetOwnInventory("hotbar");
            IInventory backpackInv = player.InventoryManager.GetOwnInventory("backpack");

            // Ищем планшет теперь и в инвентаре персонажа (например, в слоте левой руки)
            ItemSlot tabletSlot = FindTablet(new[] { gearInv, hotbarInv, backpackInv });
            if (tabletSlot == null) return;

            ItemManaTablet tablet = tabletSlot.Itemstack.Item as ItemManaTablet;
            int currentMana = tablet.GetMana(tabletSlot.Itemstack);

            if (currentMana < manaPerRepair) return;

            // Пробуем починить предметы по очереди
            bool repairedAnything = false;
            repairedAnything |= TryRepairInInventory(gearInv, ref currentMana, manaPerRepair);
            if (!repairedAnything) repairedAnything |= TryRepairInInventory(hotbarInv, ref currentMana, manaPerRepair);
            if (!repairedAnything) repairedAnything |= TryRepairInInventory(backpackInv, ref currentMana, manaPerRepair);

            if (repairedAnything)
            {
                tablet.SetMana(tabletSlot.Itemstack, currentMana);
                tabletSlot.MarkDirty(); // Обновляем планшет
            }
        }

        private ItemSlot FindTablet(IInventory[] inventories)
        {
            foreach (var inv in inventories)
            {
                if (inv == null) continue;
                foreach (var slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack.Item is ItemManaTablet) return slot;
                }
            }
            return null;
        }

        private bool TryRepairInInventory(IInventory inv, ref int currentMana, int manaPerRepair)
        {
            if (inv == null) return false;

            foreach (var slot in inv)
            {
                if (slot.Empty) continue;

                if (slot.Itemstack.Item is ItemManaArmor)
                {
                    // 1. Узнаем максимальную прочность (ту самую, что указана в твоем json: 1500)
                    int maxDurability = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);

                    // 2. Читаем ОСТАВШУЮСЯ прочность. Если атрибута нет, предполагаем, что предмет полностью цел.
                    int currentDurability = slot.Itemstack.Attributes.GetInt("durability", maxDurability);

                    // 3. Если текущая прочность меньше максимальной, броня повреждена и её нужно чинить
                    if (currentDurability < maxDurability && currentMana >= manaPerRepair)
                    {
                        // ПРИБАВЛЯЕМ 1 к оставшейся прочности
                        slot.Itemstack.Attributes.SetInt("durability", currentDurability + 1);
                        currentMana -= manaPerRepair;

                        // Заставляем игру перерисовать полоску прочности у брони
                        slot.MarkDirty();
                        return true;
                    }
                }
            }
            return false;
        }
    }
}