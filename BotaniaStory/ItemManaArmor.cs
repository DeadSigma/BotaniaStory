using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace BotaniaStory
{
    // Обязательно добавляем IManaRepairable, чтобы общая система тоже чинила броню
    public class ItemManaArmor : Item, IManaRepairable
    {
        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            // Используем наш общий помощник
            amount = ManaHelper.ProcessDamage(byEntity, amount);

            if (amount > 0)
            {
                base.DamageItem(world, byEntity, itemslot, amount, destroyOnZeroDurability);
            }
        }
    }
}