using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace BotaniaStory
{
    public class BlockApothecary : Block
    {
        // Этот метод срабатывает, когда игрок кликает ПКМ по нашему блоку
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            string fillState = Variant["fill"];

            // Проверяем, держит ли игрок в руках контейнер (ведро, миску и т.д.)
            if (slot.Itemstack.Collectible is BlockLiquidContainerBase liquidContainer)
            {
                // Смотрим, что внутри ведра (возвращает ItemStack жидкости или null)
                ItemStack liquidInside = liquidContainer.GetContent(slot.Itemstack);

                // СЦЕНАРИЙ 1: Аптекарь ПУСТОЙ, а в ведре ВОДА
                if (fillState == "empty" && liquidInside != null && liquidInside.Collectible.Code.Path == "waterportion")
                {
                    if (liquidInside.StackSize >= 1000) // Нам нужно 10 порций (полное ведро = 10 литров)
                    {
                        // 1. Меняем блок аптекаря на заполненный
                        Block newBlock = world.GetBlock(CodeWithVariant("fill", "water"));
                        world.BlockAccessor.SetBlock(newBlock.Id, blockSel.Position);

                        // 2. Звук бульканья
                        world.PlaySoundAt(new AssetLocation("game:sounds/environment/water-splash"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);

                        // 3. Вычитаем 10 литров из ведра
                        liquidInside.StackSize -= 1000;
                        if (liquidInside.StackSize <= 0)
                        {
                            liquidContainer.SetContent(slot.Itemstack, null); // Ведро стало полностью пустым
                        }
                        else
                        {
                            liquidContainer.SetContent(slot.Itemstack, liquidInside); // Если это была бочка, сохраняем остатки
                        }

                        slot.MarkDirty(); // Обновляем инвентарь игрока (чтобы ведро визуально опустело)
                        return true;
                    }
                }

                // СЦЕНАРИЙ 2: Аптекарь ПОЛНЫЙ, а ведро ПУСТОЕ (или неполное)
                if (fillState == "water")
                {
                    bool canFill = false;

                    if (liquidInside == null)
                    {
                        // Если ведро абсолютно пустое, мы магически создаем 10 литров воды из алтаря
                        Item waterItem = world.GetItem(new AssetLocation("game:waterportion"));
                        liquidInside = new ItemStack(waterItem, 10);
                        canFill = true;
                    }
                    else if (liquidInside.Collectible.Code.Path == "waterportion" && liquidInside.StackSize + 1000 <= liquidContainer.CapacityLitres)
                    {
                        // Если в контейнере уже есть вода, и туда точно влезет еще 10 литров
                        liquidInside.StackSize += 1000;
                        canFill = true;
                    }

                    if (canFill)
                    {
                        // 1. Заливаем воду в ведро игрока
                        liquidContainer.SetContent(slot.Itemstack, liquidInside);

                        // 2. Опустошаем аптекарь
                        Block newBlock = world.GetBlock(CodeWithVariant("fill", "empty"));
                        world.BlockAccessor.SetBlock(newBlock.Id, blockSel.Position);

                        // 3. Снова булькаем!
                        world.PlaySoundAt(new AssetLocation("game:sounds/environment/water-splash"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);

                        slot.MarkDirty();
                        return true;
                    }
                }
            }

            // Если игрок кликнул чем-то другим (например, лепестком), передаем действие дальше
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}