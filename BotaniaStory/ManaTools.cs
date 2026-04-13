using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory
{
    // ==========================================
    // ИНТЕРФЕЙС И ПОМОЩНИК
    // ==========================================

    // Интерфейс-маркер. Если предмет его имеет, система автопочинки будет его чинить.
    public interface IManaRepairable { }

    // Выносим поглощение урона сюда, чтобы не дублировать код 20 раз
    public static class ManaHelper
    {
        public static int ProcessDamage(Entity byEntity, int amount, int manaPerDamage = 120)
        {
            if (amount <= 0 || !(byEntity is EntityPlayer entityPlayer) || entityPlayer.Player == null)
                return amount;

            IPlayer player = entityPlayer.Player;
            int remainingDamage = amount;

            IInventory[] invs = {
                player.InventoryManager.GetOwnInventory("character"),
                player.InventoryManager.GetOwnInventory("hotbar"),
                player.InventoryManager.GetOwnInventory("backpack")
            };

            foreach (var inv in invs)
            {
                if (inv == null) continue;
                foreach (var slot in inv)
                {
                    if (slot.Empty) continue;
                    if (slot.Itemstack.Item is ItemManaTablet tablet)
                    {
                        int currentMana = tablet.GetMana(slot.Itemstack);
                        if (currentMana >= manaPerDamage)
                        {
                            int manaNeeded = remainingDamage * manaPerDamage;
                            if (currentMana >= manaNeeded)
                            {
                                tablet.SetMana(slot.Itemstack, currentMana - manaNeeded);
                                slot.MarkDirty();
                                return 0; // Весь урон поглощен
                            }
                            else
                            {
                                int absorbed = currentMana / manaPerDamage;
                                tablet.SetMana(slot.Itemstack, currentMana % manaPerDamage);
                                slot.MarkDirty();
                                remainingDamage -= absorbed;
                            }
                        }
                    }
                }
                if (remainingDamage <= 0) break;
            }
            return remainingDamage;
        }
    }

    // ==========================================
    // КЛАССЫ ИНСТРУМЕНТОВ
    // ==========================================

    // Базовый класс для лопаты, кирки, меча, копья (не требуют сложной ванильной логики)
    public class ItemManaTool : Item, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    // Класс для Лома
    public class ItemManaCrowbar : ItemCrowbar, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    // Класс для Гаечного ключа
    public class ItemManaWrench : ItemWrench, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    // Топор (обязательно наследуется от ItemAxe)
    public class ItemManaAxe : ItemAxe, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemManaScythe : ItemScythe, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemManaKnife : ItemKnife, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemManaHammer : ItemHammer, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemManaChisel : ItemChisel, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemManaCleaver : ItemCleaver, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemManaTongs : ItemTongs, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemManaShears : ItemShears, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemManaHoe : ItemHoe, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

   

    public class ItemManaProspectingPick : ItemProspectingPick, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }
   

    // ==========================================
    // СИСТЕМА АВТОПОЧИНКИ (РАБОТАЕТ ДЛЯ ВСЕХ)
    // ==========================================
    // Эта система живет отдельно и никак не конфликтует с твоей основной ModSystem!
    public class ManaAutoRepairSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            // Ускорили таймер! Теперь он срабатывает каждые 250 миллисекунд (4 раза в секунду)
            sapi.Event.RegisterGameTickListener(OnRepairTick, 250);
        }

        private void OnRepairTick(float dt)
        {
            foreach (IServerPlayer player in sapi.Server.Players)
            {
                if (player.ConnectionState != EnumClientState.Playing || player.Entity == null) continue;
                RepairItemsForPlayer(player);
            }
        }

        private void RepairItemsForPlayer(IPlayer player)
        {
            int manaPerRepair = 120; // Твоя новая цена за 1 ед. прочности

            IInventory gearInv = player.InventoryManager.GetOwnInventory("character");
            IInventory hotbarInv = player.InventoryManager.GetOwnInventory("hotbar");
            IInventory backpackInv = player.InventoryManager.GetOwnInventory("backpack");

            ItemSlot tabletSlot = null;
            foreach (var inv in new[] { gearInv, hotbarInv, backpackInv })
            {
                if (inv == null) continue;
                foreach (var slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack.Item is ItemManaTablet)
                    {
                        tabletSlot = slot;
                        break;
                    }
                }
                if (tabletSlot != null) break;
            }

            if (tabletSlot == null) return;

            ItemManaTablet tablet = tabletSlot.Itemstack.Item as ItemManaTablet;
            int currentMana = tablet.GetMana(tabletSlot.Itemstack);

            if (currentMana < manaPerRepair) return;

            bool repairedAnything = false;
            repairedAnything |= TryRepairInInventory(gearInv, ref currentMana, manaPerRepair);
            if (!repairedAnything) repairedAnything |= TryRepairInInventory(hotbarInv, ref currentMana, manaPerRepair);
            if (!repairedAnything) repairedAnything |= TryRepairInInventory(backpackInv, ref currentMana, manaPerRepair);

            if (repairedAnything)
            {
                tablet.SetMana(tabletSlot.Itemstack, currentMana);
                tabletSlot.MarkDirty();
            }
        }

        private bool TryRepairInInventory(IInventory inv, ref int currentMana, int manaPerRepair)
        {
            if (inv == null) return false;

            foreach (var slot in inv)
            {
                if (slot.Empty) continue;

                // Проверяем, есть ли у предмета наш интерфейс IManaRepairable
                if (slot.Itemstack.Item is IManaRepairable)
                {
                    // 1. Узнаем максимальную прочность инструмента/брони
                    int maxDurability = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);

                    // 2. Читаем ОСТАВШУЮСЯ прочность. Если атрибута нет, предполагаем, что предмет цел.
                    int currentDurability = slot.Itemstack.Attributes.GetInt("durability", maxDurability);

                    // 3. Если текущая прочность меньше максимальной, предмет поврежден
                    if (currentDurability < maxDurability && currentMana >= manaPerRepair)
                    {
                        // Сколько максимум прочности мы восстанавливаем за один тик (для скорости)
                        int maxRepairPerTick = 1;

                        // Сколько прочности не хватает до максимума (с учетом лимита за тик)
                        int neededRepair = Math.Min(maxDurability - currentDurability, maxRepairPerTick);

                        // На сколько ремонта нам хватит маны
                        int affordableRepair = currentMana / manaPerRepair;

                        // Итоговое количество ремонта (выбираем меньшее)
                        int actualRepair = Math.Min(neededRepair, affordableRepair);

                        if (actualRepair > 0)
                        {
                            // ПРИБАВЛЯЕМ восстановленные единицы к текущей прочности
                            slot.Itemstack.Attributes.SetInt("durability", currentDurability + actualRepair);

                            // Забираем ману
                            currentMana -= (actualRepair * manaPerRepair);

                            // Обновляем предмет для клиента
                            slot.MarkDirty();

                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
    // Класс для Копья
    public class ItemManaSpear : ItemSpear, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }
}