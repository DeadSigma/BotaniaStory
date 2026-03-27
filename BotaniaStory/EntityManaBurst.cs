using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class EntityManaBurst : Entity
    {
        public int ManaPayload = 0;
        public BlockPos SourcePos = null;

        private Vec3d StartPos = null;

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            double maxDistance = WatchedAttributes.GetDouble("maxDist", 8.0);

            if (StartPos == null)
            {
                // Берем точные координаты старта от сервера, чтобы дистанция считалась идеально
                if (WatchedAttributes.HasAttribute("startX"))
                {
                    StartPos = new Vec3d(
                        WatchedAttributes.GetDouble("startX"),
                        WatchedAttributes.GetDouble("startY"),
                        WatchedAttributes.GetDouble("startZ")
                    );
                }
                else
                {
                    StartPos = Pos.XYZ.Clone();
                }
            }

            // Вычисляем пройденное расстояние
            double distanceTraveled = StartPos.DistanceTo(Pos.XYZ);
            float lifeRatio = Math.Max(0f, 1f - (float)(distanceTraveled / maxDistance));

            // ==========================================
            // КЛИЕНТ: Плавное движение и частицы
            // ==========================================
            if (Api.Side == EnumAppSide.Client)
            {
                Pos.Motion.X = WatchedAttributes.GetDouble("motionX", 0);
                Pos.Motion.Y = WatchedAttributes.GetDouble("motionY", 0);
                Pos.Motion.Z = WatchedAttributes.GetDouble("motionZ", 0);

                // Клиент сам двигает позицию
                Pos.X += Pos.Motion.X;
                Pos.Y += Pos.Motion.Y;
                Pos.Z += Pos.Motion.Z;

                float currentSize = Math.Max(0.1f, 0.5f * lifeRatio);
                int currentAlpha = (int)(255 * lifeRatio);

                // Оставляем в конструкторе только самое базовое: количество, цвет, позицию и скорость
                SimpleParticleProperties particles = new SimpleParticleProperties(
                    1, 3,
                    ColorUtil.ToRgba(currentAlpha, 40, 255, 150),
                    new Vec3d(Pos.X, Pos.Y, Pos.Z),
                    new Vec3d(Pos.X, Pos.Y, Pos.Z),
                    new Vec3f(-0.1f, -0.1f, -0.1f),
                    new Vec3f(0.1f, 0.1f, 0.1f)
                );

                // А теперь задаем всё остальное явно:
                particles.ParticleModel = EnumParticleModel.Quad;
                particles.GravityEffect = 0f;

                // РАЗМЕР ЧАСТИЦ
                particles.MinSize = 0.2f; // Твои изначальные значения
                particles.MaxSize = 0.4f;

                // ВРЕМЯ ЖИЗНИ (СКОРОСТЬ ИСЧЕЗНОВЕНИЯ ХВОСТА)
                // Если хочешь длинный хвост, ставь больше (например, 0.5f - полсекунды)
                // Если короткий - меньше (например, 0.15f)
                particles.LifeLength = 2.0f;

                // сдвиг частиц относительно центра
                particles.AddPos.Set(0.1, 0.1, 0.1);

                // Наше плавное растворение (убирает 255 альфы за время LifeLength)
                particles.OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -255f);

                Api.World.SpawnParticles(particles);
            }

            // ==========================================
            // СЕРВЕР: Честная сетевая позиция и логика
            // ==========================================
            if (Api.Side == EnumAppSide.Server)
            {
                // Двигаем позицию
                Pos.X += Pos.Motion.X;
                Pos.Y += Pos.Motion.Y;
                Pos.Z += Pos.Motion.Z;

                // ВАЖНО: Обновляем именно Pos! Иначе чанки потеряют сущность и удалят её.
                Pos.SetFrom(Pos);

                if (distanceTraveled >= maxDistance)
                {
                    Die();
                    return;
                }

                BlockPos currentPos = Pos.AsBlockPos;
                if (SourcePos != null && currentPos.Equals(SourcePos)) return;

                Block block = Api.World.BlockAccessor.GetBlock(currentPos);

                // Если блок не воздух и не жидкость
                if (block.Id != 0 && block.MatterState != EnumMatterState.Liquid)
                {
                    // 1. Сначала проверяем, не бассейн ли это
                    if (block is BlockManaPool)
                    {
                        BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(currentPos);
                        if (be is BlockEntityManaPool pool)
                        {
                            int finalMana = (int)(ManaPayload * lifeRatio);
                            if (finalMana < 1) finalMana = 1;

                            pool.CurrentMana += finalMana;
                            if (pool.CurrentMana > pool.MaxMana) pool.CurrentMana = pool.MaxMana;
                            pool.MarkDirty(true);
                        }

                        // Отдали ману — исчезаем
                        Die();
                        return;
                    }

                    // 2. Если это НЕ бассейн, проверяем блок на "твердость" (коллизию)
                    // У цветов, высокой травы, лиан и факелов нет CollisionBoxes, поэтому искра полетит дальше!
                    if (block.CollisionBoxes != null && block.CollisionBoxes.Length > 0)
                    {
                        // Врезались в твердую стену или землю — исчезаем
                        Die();
                    }
                }
            }
        }
    }
}