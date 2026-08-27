using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.Items
{
    public class ItemOvergrowthSeed : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            Block clickedBlock = api.World.BlockAccessor.GetBlock(blockSel.Position);
            string path = clickedBlock.Code.Path;

            Block targetBlock = null;

            // Если кликнули по грядке (включая компостную и терра прету)
            if (path.StartsWith("farmland"))
            {
                targetBlock = api.World.GetBlock(new AssetLocation("botaniastory", "enchantedfarmland"));
            }
            // Если кликнули по земле
            else if (path.StartsWith("soil"))
            {
                targetBlock = api.World.GetBlock(new AssetLocation("botaniastory", "enchantedsoil"));
            }

            // Если нашли во что превратить
            if (targetBlock != null)
            {
                // Заменяем блок. Движок игры сам создаст BlockEntityEnchantedFarmland, 
                api.World.BlockAccessor.SetBlock(targetBlock.BlockId, blockSel.Position);

                // Тратим семечко, если игрок в режиме выживания
                if (byEntity is EntityPlayer player && player.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }

                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}