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
               
                // Ищем искру строго по координатам спавна с крошечным радиусом в 0.1 блока (10 см)
                var existingSparks = byEntity.World.GetEntitiesAround(
                    blockSel.Position.ToVec3d().Add(0.5, 1.5, 0.5), // Обязательно укажи свою высоту (1.5)
                    0.1f, // Горизонтальный радиус
                    0.1f, // Вертикальный радиус
                    e => e is EntitySpark
                );

                if (existingSparks.Length > 0)
                {
                    handling = EnumHandHandling.PreventDefaultAction; // Обязательно отменяем анимацию взмаха рукой
                    return;
                }

                if (byEntity.World.Side == EnumAppSide.Server)
                {
                    EntityProperties entityType = byEntity.World.GetEntityType(new AssetLocation("botaniastory", "spark"));
                    if (entityType != null)
                    {
                        Entity sparkEntity = byEntity.World.ClassRegistry.CreateEntity(entityType);

                        // Центрируем искру над бассейном
                        sparkEntity.Pos.X = blockSel.Position.X + 0.5;
                        sparkEntity.Pos.Y = blockSel.Position.Y + 1.7;
                        sparkEntity.Pos.Z = blockSel.Position.Z + 0.5;
                        sparkEntity.Pos.SetFrom(sparkEntity.Pos);

                        sparkEntity.WatchedAttributes.SetDouble("baseX", sparkEntity.Pos.X);
                        sparkEntity.WatchedAttributes.SetDouble("baseY", blockSel.Position.Y + 1.5); // Высота самого бассейна
                        sparkEntity.WatchedAttributes.SetDouble("baseZ", sparkEntity.Pos.Z);

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