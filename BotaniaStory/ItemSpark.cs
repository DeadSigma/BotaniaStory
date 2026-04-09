using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ItemSpark : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            // Ищем BlockEntity по координатам клика. Это надежнее, чем искать сам блок.
            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityManaPool)
            {
                // Убеждаемся, что над бассейном ещё нет другой искры
                var existingSparks = byEntity.World.GetEntitiesAround(blockSel.Position.ToVec3d().Add(0.5, 1.2, 0.5), 0.5f, 0.5f, e => e is EntitySpark);
                if (existingSparks.Length > 0) return; // Искра уже есть!

                if (byEntity.World.Side == EnumAppSide.Server)
                {
                    EntityProperties entityType = byEntity.World.GetEntityType(new AssetLocation("botaniastory", "spark"));
                    if (entityType != null)
                    {
                        Entity sparkEntity = byEntity.World.ClassRegistry.CreateEntity(entityType);

                        // Центрируем искру над бассейном
                        sparkEntity.Pos.X = blockSel.Position.X + 0.5;
                        sparkEntity.Pos.Y = blockSel.Position.Y + 1.2;
                        sparkEntity.Pos.Z = blockSel.Position.Z + 0.5;
                        sparkEntity.Pos.SetFrom(sparkEntity.Pos);

                        byEntity.World.SpawnEntity(sparkEntity);

                        if (byEntity is EntityPlayer player && player.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                        {
                            slot.TakeOut(1);
                            slot.MarkDirty();
                        }
                    }
                }
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}