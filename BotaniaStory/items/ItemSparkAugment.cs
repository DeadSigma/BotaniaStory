using BotaniaStory.entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory.items
{
    public class ItemSparkAugment : Item
    {
        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling
        )
        {
            if (!firstEvent) return;

            IWorldAccessor world = byEntity.World;

            Vec3d eyePos = byEntity.Pos.XYZ.Add(
                0,
                byEntity.LocalEyePos.Y,
                0
            );

            Vec3f viewVec = byEntity.Pos.GetViewVector();

            Vec3d lookDir = new Vec3d(
                viewVec.X,
                viewVec.Y,
                viewVec.Z
            );

            Entity[] nearbySparks = world.GetEntitiesAround(
                byEntity.Pos.XYZ,
                5,
                5,
                e => e is EntitySpark
            );

            EntitySpark targetSpark = null;
            double closestDistance = 5.0;

            foreach (Entity entity in nearbySparks)
            {
                if (!(entity is EntitySpark spark)) continue;

                // Центр искры используется для наведения
                Vec3d sparkCenter = new Vec3d(
                    spark.Pos.X,
                    spark.Pos.Y + 0.3,
                    spark.Pos.Z
                );

                Vec3d offset = new Vec3d(
                    sparkCenter.X - eyePos.X,
                    sparkCenter.Y - eyePos.Y,
                    sparkCenter.Z - eyePos.Z
                );

                double distanceAlongRay = offset.Dot(lookDir);

                if (
                    distanceAlongRay <= 0 ||
                    distanceAlongRay >= closestDistance
                )
                {
                    continue;
                }

                Vec3d projection = new Vec3d(
                    lookDir.X * distanceAlongRay,
                    lookDir.Y * distanceAlongRay,
                    lookDir.Z * distanceAlongRay
                );

                Vec3d perpendicular = new Vec3d(
                    offset.X - projection.X,
                    offset.Y - projection.Y,
                    offset.Z - projection.Z
                );

                if (perpendicular.Length() >= 0.4) continue;

                closestDistance = distanceAlongRay;
                targetSpark = spark;
            }

            if (targetSpark != null)
            {
                if (
                    targetSpark.WatchedAttributes.GetString(
                        "augment",
                        "none"
                    ) == "none"
                )
                {
                    string augmentType =
                        Code.Path.Replace("sparkaugment-", "");

                    targetSpark.WatchedAttributes.SetString(
                        "augment",
                        augmentType
                    );

                    targetSpark.WatchedAttributes.MarkAllDirty();

                    // Дополнитель применяется на сервере
                    if (world.Side == EnumAppSide.Server)
                    {
                        world.PlaySoundAt(
                            new AssetLocation(
                                "game",
                                "sounds/player/buildhigh"
                            ),
                            targetSpark.Pos.X,
                            targetSpark.Pos.Y,
                            targetSpark.Pos.Z,
                            null,
                            true,
                            16,
                            1f
                        );

                        if (
                            (byEntity as EntityPlayer)?.Player
                                .WorldData.CurrentGameMode
                            != EnumGameMode.Creative
                        )
                        {
                            slot.TakeOut(1);
                            slot.MarkDirty();
                        }
                    }

                    handling = EnumHandHandling.Handled;
                    return;
                }
            }

            base.OnHeldInteractStart(
                slot,
                byEntity,
                blockSel,
                entitySel,
                firstEvent,
                ref handling
            );
        }
    }
}