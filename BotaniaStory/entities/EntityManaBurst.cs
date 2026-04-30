using BotaniaStory.blockentity;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory.entities
{
    public class EntityManaBurst : Entity
    {
        public int ManaPayload = 0;
        public BlockPos SourcePos = null;

        private Vec3d StartPos = null;

        public static bool IsManaPermeable(Block block)
        {
            if (block == null || block.Code == null) return false;
            string path = block.Code.Path;

            // managlass проверяем точным совпадением (если у него нет вариантов)
            // а elvenglass проверяем по началу названия это охватит elvenglass_1, elvenglass_2 и любые другие
            return path == "managlass" || path.StartsWith("elvenglass");
        }
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

                if (block.Id != 0 && block.MatterState != EnumMatterState.Liquid)
                {

                    if (IsManaPermeable(block))
                    {
                        // Просто выходим из проверки коллизии в этом тике.
                        // Искра не умрет и продолжит лететь сквозь блок.
                        return;
                    }

                    // ===  УНИВЕРСАЛЬНЫЙ ПРИЕМ МАНЫ ===
                    BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(currentPos);

                    // Проверяем, является ли блок приемником маны (Бассейн, Алтарь, Плита и т.д.)
                    if (be is IManaReceiver receiver)
                    {
                        int finalMana = (int)(ManaPayload * lifeRatio);
                        if (finalMana < 1) finalMana = 1;

                        // Отдаем ману через универсальный метод
                        receiver.ReceiveMana(finalMana);

                        // Сохраняем изменения в целевом блоке
                        if (be is BlockEntity blockEnt)
                        {
                            blockEnt.MarkDirty(true);
                        }

                        // Отдали ману — исчезаем
                        Die();
                        return;
                    }

                    // Если это обычный твердый блок без интерфейса (стена, земля) — разбиваемся
                    if (block.CollisionBoxes != null && block.CollisionBoxes.Length > 0)
                    {
                        Die();
                    }

                    // Если это НЕ бассейн и НЕ алтарь, проверяем блок на коллизию (стена/земля)
                    if (block.CollisionBoxes != null && block.CollisionBoxes.Length > 0)
                    {
                        Die();
                    }
                }
            }
        }
    }
}