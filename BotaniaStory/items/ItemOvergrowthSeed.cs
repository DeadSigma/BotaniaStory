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

            // Если кликнули по грядке
            if (path.StartsWith("farmland"))
            {
                targetBlock = api.World.GetBlock(new AssetLocation("botaniastory", "enchantedfarmland"));
            }
            // Если кликнули по земле
            else if (path.StartsWith("soil"))
            {
                targetBlock = api.World.GetBlock(new AssetLocation("botaniastory", "enchantedsoil"));
            }

            if (targetBlock != null)
            {
                api.World.BlockAccessor.SetBlock(targetBlock.BlockId, blockSel.Position);

                api.World.PlaySoundAt(
                    new AssetLocation("game", "sounds/block/dirt"),
                    blockSel.Position,
                    0,
                    byEntity is EntityPlayer soundPlayer ? soundPlayer.Player : null,
                    true,
                    16f,
                    1f
                );

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