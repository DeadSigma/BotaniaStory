using BotaniaStory.items;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory.util
{

    // Интерфейс-маркер. Если предмет его имеет, система автопочинки будет его чинить.
    public interface IManaRepairable { }

    public static class ManaHelper
    {
        // манаброня: процент скидки за полный сет 
        public static readonly Dictionary<string, int> ArmorSetDiscounts = new Dictionary<string, int>
        {
            { "manasteel", 10 },
            { "elementium", 15 },
            { "terrasteel", 20 }
        };

        private static readonly int[] ArmorSlotIds =
        {
            (int)EnumCharacterDressType.ArmorHead,
            (int)EnumCharacterDressType.ArmorBody,
            (int)EnumCharacterDressType.ArmorLegs
        };

        // манаброня: возвращается материал надетого сета, null - сет не собран
        // chain/scale/plate смешиваются, разные материалы - нет
        public static string GetArmorSet(Entity entity)
        {
            if (!(entity is EntityPlayer entityPlayer) || entityPlayer.Player?.InventoryManager == null) return null;

            IInventory gearInv = entityPlayer.Player.InventoryManager.GetOwnInventory("character");
            if (gearInv == null) return null;

            string setName = null;
            foreach (int id in ArmorSlotIds)
            {
                if (id >= gearInv.Count) return null;

                ItemSlot slot = gearInv[id];
                if (slot == null || slot.Empty || !(slot.Itemstack.Item is ItemManaArmor armor)) return null;

                string material = armor.FirstCodePart();
                if (setName == null) setName = material;
                else if (setName != material) return null;
            }
            return setName;
        }

        // процент скидки, 0 - сета нет
        public static int GetManaDiscount(Entity entity)
        {
            string setName = GetArmorSet(entity);
            if (setName != null && ArmorSetDiscounts.TryGetValue(setName, out int percent)) return percent;
            return 0;
        }

        // цена со скидкой, округляется - для HUD и проверок
        public static int GetDiscountedCost(Entity entity, int baseCost)
        {
            if (baseCost <= 0) return 0;

            int percent = GetManaDiscount(entity);
            if (percent <= 0) return baseCost;

            long scaled = (long)baseCost * (100 - percent);
            return Math.Max(1, (int)((scaled + 50) / 100));
        }

        private static int RollDiscountedCost(Entity entity, int baseCost)
        {
            if (baseCost <= 0) return 0;

            int percent = GetManaDiscount(entity);
            if (percent <= 0) return baseCost;

            long scaled = (long)baseCost * (100 - percent);
            int cost = (int)(scaled / 100);
            long rest = scaled % 100;
            if (rest > 0 && entity.World.Rand.Next(100) < rest) cost++;

            return Math.Max(1, cost);
        }

        // сумма маны во всех планшетах игрока
        public static int GetTotalMana(Entity entity)
        {
            int total = 0;
            foreach (ItemSlot slot in GetTabletSlots(entity))
            {
                total += ((ItemManaTablet)slot.Itemstack.Item).GetMana(slot.Itemstack);
            }
            return total;
        }

        // манаброня: скидка учитывается и при проверке
        public static bool HasMana(Entity entity, int baseCost, bool applyDiscount = true)
        {
            int cost = applyDiscount ? GetDiscountedCost(entity, baseCost) : baseCost;
            return GetTotalMana(entity) >= cost;
        }

        // Списание маны для всех инструментов
        // applyDiscount = false - для перекачки маны, иначе скидка создает ману из ничего
        public static bool TryConsumeMana(Entity entity, int baseCost, bool applyDiscount = true)
        {
            int cost = applyDiscount ? RollDiscountedCost(entity, baseCost) : baseCost;
            if (cost <= 0) return true;

            List<ItemSlot> tablets = GetTabletSlots(entity);

            int total = 0;
            foreach (ItemSlot slot in tablets)
            {
                total += ((ItemManaTablet)slot.Itemstack.Item).GetMana(slot.Itemstack);
            }
            if (total < cost) return false;

            // мана добирается из нескольких планшетов по очереди
            foreach (ItemSlot slot in tablets)
            {
                ItemManaTablet tablet = (ItemManaTablet)slot.Itemstack.Item;
                int mana = tablet.GetMana(slot.Itemstack);
                if (mana <= 0) continue;

                int take = Math.Min(mana, cost);
                tablet.SetMana(slot.Itemstack, mana - take);
                slot.MarkDirty();

                cost -= take;
                if (cost <= 0) break;
            }
            return true;
        }

        // планшеты ищутся в экипировке, хотбаре и рюкзаке
        private static List<ItemSlot> GetTabletSlots(Entity entity)
        {
            var result = new List<ItemSlot>();
            if (!(entity is EntityPlayer entityPlayer) || entityPlayer.Player?.InventoryManager == null) return result;

            var invMan = entityPlayer.Player.InventoryManager;
            IInventory[] invs = {
                invMan.GetOwnInventory("character"),
                invMan.GetOwnInventory("hotbar"),
                invMan.GetOwnInventory("backpack")
            };

            foreach (var inv in invs)
            {
                if (inv == null) continue;
                foreach (var slot in inv)
                {
                    if (slot != null && !slot.Empty && slot.Itemstack.Item is ItemManaTablet) result.Add(slot);
                }
            }
            return result;
        }

        public static int ProcessDamage(Entity byEntity, int amount, int manaPerDamage = 120)
        {
            if (amount <= 0 || !(byEntity is EntityPlayer entityPlayer) || entityPlayer.Player == null)
                return amount;

            // манаброня: скидка на ману вместо прочности
            manaPerDamage = RollDiscountedCost(byEntity, manaPerDamage);

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
                    if (slot == null || slot.Empty) continue;
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

    // КЛАССЫ ИНСТРУМЕНТОВ
    // манаброня: у всех классов ниже скидка учитывается внутри ProcessDamage

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

    public class ItemTerraTool : Item, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    // Класс для Лома
    public class ItemTerraCrowbar : ItemCrowbar, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    // Класс для Гаечного ключа
    public class ItemTerraWrench : ItemWrench, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    // Топор (обязательно наследуется от ItemAxe)
    public class ItemTerraAxe : ItemAxe, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemTerraScythe : ItemScythe, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemTerraKnife : ItemKnife, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemTerraHammer : ItemHammer, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemTerraChisel : ItemChisel, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemTerraCleaver : ItemCleaver, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemTerraTongs : ItemTongs, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemTerraShears : ItemShears, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemTerraHoe : ItemHoe, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    // Класс для Копья
    public class ItemTerraSpear : ItemSpear, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }
    public class ItemManaSpear : ItemSpear, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    public class ItemTerraProspectingPick : ItemProspectingPick, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);
            if (amount > 0) base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
        }
    }

    // 
    // СИСТЕМА АВТОПОЧИНКИ (РАБОТАЕТ НА ОБЕИХ СТОРОНАХ)
    // 
    public class ManaAutoRepairSystem : ModSystem
    {
        private ICoreAPI api;

        // Start, а не StartServerSide - клиент тоже предсказывает починку
        public override void Start(ICoreAPI api)
        {
            this.api = api;
            api.Event.RegisterGameTickListener(OnRepairTick, 250);
        }

        private void OnRepairTick(float dt)
        {
            if (api.Side == EnumAppSide.Server)
            {
                // сервер - все игроки
                ICoreServerAPI sapi = api as ICoreServerAPI;
                foreach (IServerPlayer player in sapi.Server.Players)
                {
                    if (player.ConnectionState != EnumClientState.Playing || player.Entity == null) continue;
                    RepairItemsForPlayer(player);
                }
            }
            else
            {
                // клиент - только свой персонаж
                ICoreClientAPI capi = api as ICoreClientAPI;
                if (capi.World.Player?.Entity != null)
                {
                    RepairItemsForPlayer(capi.World.Player);
                }
            }
        }

        private void RepairItemsForPlayer(IPlayer player)
        {
            if (player?.InventoryManager == null) return;

            // манаброня: скидка на починку
            int manaPerRepair = ManaHelper.GetDiscountedCost(player.Entity, 120); // мана за 1 ед. прочности

            IInventory gearInv = player.InventoryManager.GetOwnInventory("character");
            IInventory hotbarInv = player.InventoryManager.GetOwnInventory("hotbar");
            IInventory backpackInv = player.InventoryManager.GetOwnInventory("backpack");

            ItemSlot tabletSlot = null;
            foreach (var inv in new[] { gearInv, hotbarInv, backpackInv })
            {
                if (inv == null) continue;
                foreach (var slot in inv)
                {
                    // в рюкзаке бывают null-слоты
                    if (slot != null && !slot.Empty && slot.Itemstack.Item is ItemManaTablet)
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
                if (slot == null || slot.Empty) continue;

                if (slot.Itemstack.Item is IManaRepairable)
                {
                    int maxDurability = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);
                    int currentDurability = slot.Itemstack.Attributes.GetInt("durability", maxDurability);

                    if (currentDurability < maxDurability && currentMana >= manaPerRepair)
                    {
                        int maxRepairPerTick = 1;
                        int neededRepair = Math.Min(maxDurability - currentDurability, maxRepairPerTick);
                        int affordableRepair = currentMana / manaPerRepair;
                        int actualRepair = Math.Min(neededRepair, affordableRepair);

                        if (actualRepair > 0)
                        {
                            slot.Itemstack.Attributes.SetInt("durability", currentDurability + actualRepair);
                            currentMana -= (actualRepair * manaPerRepair);
                            slot.MarkDirty();
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    public class TerrasteelSetBonusSystem : ModSystem
    {
        private ICoreAPI api;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            // Запускаем таймер каждые 250мс (4 раза в секунду)
            api.Event.RegisterGameTickListener(OnManaGenTick, 250);
        }

        private void OnManaGenTick(float dt)
        {
            if (api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = api as ICoreServerAPI;
                foreach (IServerPlayer player in sapi.Server.Players)
                {
                    if (player.ConnectionState != EnumClientState.Playing || player.Entity == null) continue;
                    CheckAndGenerateMana(player);
                }
            }
            else
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                if (capi.World.Player?.Entity != null)
                {
                    CheckAndGenerateMana(capi.World.Player);
                }
            }
        }

        private void CheckAndGenerateMana(IPlayer player)
        {
            if (player?.InventoryManager == null) return;

            IInventory gearInv = player.InventoryManager.GetOwnInventory("character");
            if (gearInv == null) return;

            int terrasteelPieces = 0;

            foreach (var slot in gearInv)
            {
                if (slot == null || slot.Empty) continue;

                if (slot.Itemstack.Item?.Code?.Path?.StartsWith("terrasteel-armor") == true)
                {
                    terrasteelPieces++;
                }
            }

            // У нас сет из 3 частей (без ботинок)
            if (terrasteelPieces >= 3)
            {
                AddManaToTablet(player, 5);
            }
        }

        private void AddManaToTablet(IPlayer player, int amount)
        {
            IInventory gearInv = player.InventoryManager.GetOwnInventory("character");
            IInventory hotbarInv = player.InventoryManager.GetOwnInventory("hotbar");
            IInventory backpackInv = player.InventoryManager.GetOwnInventory("backpack");

            foreach (var inv in new[] { gearInv, hotbarInv, backpackInv })
            {
                if (inv == null) continue;
                foreach (var slot in inv)
                {
                    if (slot != null && !slot.Empty && slot.Itemstack.Item is ItemManaTablet tablet)
                    {
                        int currentMana = tablet.GetMana(slot.Itemstack);

                        if (currentMana < ItemManaTablet.MaxMana)
                        {
                            int newMana = Math.Min(currentMana + amount, ItemManaTablet.MaxMana);
                            tablet.SetMana(slot.Itemstack, newMana);
                            slot.MarkDirty();
                            return;
                        }
                    }
                }
            }
        }
    }



}