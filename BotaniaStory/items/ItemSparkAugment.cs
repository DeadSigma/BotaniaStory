using BotaniaStory.entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory.items
{
    public class ItemSparkAugment : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!firstEvent) return;

            IWorldAccessor world = byEntity.World;

            Vec3d eyePos = byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
            Vec3f viewVec = byEntity.Pos.GetViewVector();
            Vec3d lookDir = new Vec3d(viewVec.X, viewVec.Y, viewVec.Z);

            Entity[] nearbySparks = world.GetEntitiesAround(byEntity.Pos.XYZ, 5, 5, e => e is EntitySpark);

            EntitySpark targetSpark = null;
            double closestDistance = 5.0; // Максимальная дальность луча в 5 блоков

            foreach (Entity entity in nearbySparks)
            {
                if (entity is EntitySpark spark)
                {
                    // Целимся в визуальный центр искры
                    Vec3d sparkCenter = new Vec3d(spark.Pos.X, spark.Pos.Y + 0.3, spark.Pos.Z);

                    // Вектор от глаз до центра искры
                    Vec3d V = new Vec3d(sparkCenter.X - eyePos.X, sparkCenter.Y - eyePos.Y, sparkCenter.Z - eyePos.Z);

                    // 1. Проекция вектора на направление взгляда (насколько искра "впереди" нас по лучу)
                    double t = V.Dot(lookDir);

                    // Искра должна быть спереди (t > 0) и ближе, чем предыдущая найденная цель
                    if (t > 0 && t < closestDistance)
                    {
                        // 2. Вычисляем точку на луче, которая находится напротив искры
                        Vec3d projection = new Vec3d(lookDir.X * t, lookDir.Y * t, lookDir.Z * t);

                        // 3. Вычисляем кратчайшее расстояние от луча до центра искры
                        Vec3d perpendicular = new Vec3d(V.X - projection.X, V.Y - projection.Y, V.Z - projection.Z);
                        double distFromCenter = perpendicular.Length();

                        // Если луч прошел не дальше, чем в 0.4 блоках от центра искры (создаем виртуальный хитбокс)
                        if (distFromCenter < 0.4)
                        {
                            closestDistance = t; // Обновляем рекорд близости
                            targetSpark = spark;
                        }
                    }
                }
            }

            if (targetSpark != null)
            {
                if (targetSpark.WatchedAttributes.GetString("augment", "none") == "none")
                {
                    string augmentType = this.Code.Path.Replace("sparkaugment-", "");

                    targetSpark.WatchedAttributes.SetString("augment", augmentType);
                    targetSpark.WatchedAttributes.MarkAllDirty();

                    // --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
                    // Воспроизводим звук и забираем предмет ТОЛЬКО на сервере
                    if (world.Side == EnumAppSide.Server)
                    {
                        world.PlaySoundAt(new AssetLocation("game", "sounds/player/buildhigh"), targetSpark.Pos.X, targetSpark.Pos.Y, targetSpark.Pos.Z, null, true, 16, 1f);

                        if ((byEntity as EntityPlayer)?.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                        {
                            slot.TakeOut(1);
                            slot.MarkDirty();
                        }
                    }


                    handling = EnumHandHandling.Handled;
                    return;
                }
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}