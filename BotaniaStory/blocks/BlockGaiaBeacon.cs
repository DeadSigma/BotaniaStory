using BotaniaStory.ritual;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace BotaniaStory.blocks
{
    public class BlockBeacon : Block
    {
        private const string TerrasteelIngotCode =
            "game:ingot-terrasteel";

        private const string EmpoweredTerrasteelIngotCode =
            "game:ingot-terrasteel-empowered";

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer?.InventoryManager?.ActiveHotbarSlot;

            // получаем энтити блока маяка
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBeacon be)
            {
                // кладем шестерню внутрь - если маяк пуст
                if (slot?.Itemstack?.Collectible?.Code?.Path == "gear-temporal" && be.Inventory[0].Empty)
                {
                    if (world.Side == EnumAppSide.Server)
                    {
                        slot.TryPutInto(world, be.Inventory[0], 1);
                        be.MarkDirty(true);
                    }
                    return true;
                }

                // забираем предмет обратно - если кликаем пустой рукой
                if (slot.Empty && !be.Inventory[0].Empty)
                {
                    if (world.Side == EnumAppSide.Server)
                    {
                        byPlayer.InventoryManager.TryGiveItemstack(be.Inventory[0].TakeOutWhole());
                        be.MarkDirty(true);
                    }
                    return true;
                }
            }

            string itemCode =
                slot?.Itemstack?.Collectible?.Code?.ToString();

            world.Logger.Notification(
                "[BotaniaStory][Beacon] Interact. Side={0}, item={1}, pos={2}",
                world.Side,
                itemCode ?? "null",
                blockSel?.Position
            );

            int gaiaLevel = itemCode switch
            {
                TerrasteelIngotCode => 1,
                EmpoweredTerrasteelIngotCode => 2,
                _ => 0
            };

            if (gaiaLevel == 0)
            {
                return base.OnBlockInteractStart(
                    world,
                    byPlayer,
                    blockSel
                );
            }

            // Клиент подтверждает, что взаимодействие обработано.
            // Сам ритуал запускаем только сервером.
            if (world.Side == EnumAppSide.Client)
            {
                return true;
            }

            if (byPlayer is not IServerPlayer serverPlayer)
            {
                world.Logger.Error(
                    "[BotaniaStory][Beacon] Player is not IServerPlayer"
                );

                return true;
            }

            GaiaRitualSystem ritual =
                GaiaRitualSystem.ServerInstance;

            if (ritual == null)
            {
                world.Logger.Error(
                    "[BotaniaStory][Beacon] GaiaRitualSystem.ServerInstance is null"
                );

                return true;
            }

            ritual.TryStartRitual(
                serverPlayer,
                blockSel.Position,
                gaiaLevel
            );

            return true;
        }
    }
}