using BotaniaStory.blockentity;
using BotaniaStory.entities;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory.items
{
    public class ItemSpark : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityManaPool || be is BlockEntityTerrestrialPlate)
            {
                Entity[] nearbySparks = byEntity.World.GetEntitiesAround(
                    blockSel.Position.ToVec3d().Add(0.5, 1.7, 0.5),
                    1f,
                    12f,
                    e => e is EntitySpark
                );

                foreach (Entity entity in nearbySparks)
                {
                    if (entity is EntitySpark spark && spark.IsAttachedTo(blockSel.Position))
                    {
                        handling = EnumHandHandling.PreventDefaultAction;
                        return;
                    }
                }

                if (byEntity.World.Side == EnumAppSide.Server)
                {
                    EntityProperties entityType = byEntity.World.GetEntityType(new AssetLocation("botaniastory", "spark"));
                    if (entityType != null)
                    {
                        Entity sparkEntity = byEntity.World.ClassRegistry.CreateEntity(entityType);

                        // Искра ставится над центром опоры
                        sparkEntity.Pos.X = blockSel.Position.X + 0.5;
                        sparkEntity.Pos.Y = blockSel.Position.Y + 1.7;
                        sparkEntity.Pos.Z = blockSel.Position.Z + 0.5;
                        sparkEntity.Pos.SetFrom(sparkEntity.Pos);

                        // Привязка сохраняется отдельно от текущей позиции
                        sparkEntity.WatchedAttributes.SetDouble("baseX", sparkEntity.Pos.X);
                        sparkEntity.WatchedAttributes.SetDouble("baseY", sparkEntity.Pos.Y);
                        sparkEntity.WatchedAttributes.SetDouble("baseZ", sparkEntity.Pos.Z);

                        byEntity.World.SpawnEntity(sparkEntity);


                        byEntity.World.PlaySoundAt(new AssetLocation("game", "sounds/player/buildhigh"), sparkEntity.Pos.X, sparkEntity.Pos.Y, sparkEntity.Pos.Z, null, true, 16, 1f);

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