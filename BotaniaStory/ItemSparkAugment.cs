using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ItemSparkAugment : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!firstEvent) return;

            IWorldAccessor world = byEntity.World;

            // Получаем направление взгляда игрока
            Vec3d eyePos = byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
            Vec3f viewVec = byEntity.Pos.GetViewVector();
            Vec3d lookDir = new Vec3d(viewVec.X, viewVec.Y, viewVec.Z);

            Entity[] nearbySparks = world.GetEntitiesAround(byEntity.Pos.XYZ, 5, 5, e => e is EntitySpark);

            foreach (Entity entity in nearbySparks)
            {
                if (entity is EntitySpark spark)
                {
                    Vec3d dirToSpark = new Vec3d(spark.Pos.X - eyePos.X, spark.Pos.Y - eyePos.Y, spark.Pos.Z - eyePos.Z);

                    // Если смотрим на искру (смягчили до 0.90)
                    if (dirToSpark.Length() < 5.0 && dirToSpark.Normalize().Dot(lookDir) > 0.90)
                    {
                        // Проверяем, свободна ли она
                        if (spark.WatchedAttributes.GetString("augment", "none") == "none")
                        {
                            string augmentType = this.Code.Path.Replace("sparkaugment-", "");

                            spark.WatchedAttributes.SetString("augment", augmentType);
                            spark.WatchedAttributes.MarkAllDirty();

                            world.PlaySoundAt(new AssetLocation("game", "sounds/player/buildhigh"), spark.Pos.X, spark.Pos.Y, spark.Pos.Z, null, true, 16, 1f);

                            if (world.Side == EnumAppSide.Client)
                            {
                                (world.Api as ICoreClientAPI)?.ShowChatMessage($"Дополнитель '{augmentType}' успешно установлен!");
                            }

                            if ((byEntity as EntityPlayer)?.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                            {
                                slot.TakeOut(1);
                                slot.MarkDirty();
                            }

                            handling = EnumHandHandling.Handled;
                            return; // Выходим, дело сделано!
                        }
                    }
                }
            }

            // Если не попали по пустой искре, отдаем управление игре
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}