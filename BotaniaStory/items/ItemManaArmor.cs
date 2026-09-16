using BotaniaStory.util;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace BotaniaStory.items
{
    public class ItemManaArmor : Item, IManaRepairable
    {
        public override void DamageItem(
            IWorldAccessor world,
            Entity byEntity,
            ItemSlot itemslot,
            int amount = 1,
            bool destroyOnZeroDurability = true)
        {
            amount = ManaHelper.ProcessDamage(byEntity, amount);

            if (amount > 0)
            {
                base.DamageItem(
                    world,
                    byEntity,
                    itemslot,
                    amount,
                    destroyOnZeroDurability
                );
            }
        }

        public override string GetItemDescText()
        {
            string text = base.GetItemDescText();

            string material = FirstCodePart();
            if (material != null &&
                ManaHelper.ArmorSetDiscounts.TryGetValue(material, out int percent))
            {
                text +=
                    "<font color=\"#86aad0\">" +
                    Lang.Get("botaniastory:armor-manadiscount", percent) +
                    "</font>\n";
            }

            return text;
        }

        public static bool HasFullElementiumSet(EntityPlayer player)
        {
            IInventory inventory =
                player?.Player?.InventoryManager?.GetOwnInventory("character");

            if (inventory == null) return false;

            int head = (int)EnumCharacterDressType.ArmorHead;
            int body = (int)EnumCharacterDressType.ArmorBody;
            int legs = (int)EnumCharacterDressType.ArmorLegs;

            if (inventory.Count <= legs) return false;

            return IsElementiumPiece(inventory[head], "head") &&
                   IsElementiumPiece(inventory[body], "body") &&
                   IsElementiumPiece(inventory[legs], "legs");
        }

        private static bool IsElementiumPiece(ItemSlot slot, string bodypart)
        {
            AssetLocation code = slot?.Itemstack?.Collectible?.Code;
            if (code == null || code.Domain != "botaniastory") return false;

            return code.Path.StartsWith(
                $"elementium-armor-{bodypart}-",
                System.StringComparison.Ordinal
            );
        }
    }
}
