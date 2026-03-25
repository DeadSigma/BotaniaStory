using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class EntityManaBurst : Entity
    {
        public int ManaPayload = 0;
        public BlockPos SourcePos = null; // Запоминаем, откуда вылетели, чтобы не взорваться внутри дула

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            // ==========================================
            // КЛИЕНТ: Отрисовка яркого хвоста из искр
            // ==========================================
            if (Api.Side == EnumAppSide.Client)
            {
                SimpleParticleProperties particles = new SimpleParticleProperties(
                    3, 5, // Количество искр
                    ColorUtil.ToRgba(255, 40, 255, 150), // Яркий бирюзовый
                    new Vec3d(Pos.X, Pos.Y, Pos.Z),
                    new Vec3d(Pos.X, Pos.Y, Pos.Z),
                    new Vec3f(-0.1f, -0.1f, -0.1f),
                    new Vec3f(0.1f, 0.1f, 0.1f),
                    0.5f, 0f, 0.2f, 0.4f, EnumParticleModel.Quad
                );
                particles.AddPos.Set(0.1, 0.1, 0.1);
                Api.World.SpawnParticles(particles);
            }

            // ==========================================
            // СЕРВЕР: Движение и столкновения
            // ==========================================
            if (Api.Side == EnumAppSide.Server)
            {
                // Двигаем сгусток вперед (Используем современный Pos!)
                Pos.X += Pos.Motion.X;
                Pos.Y += Pos.Motion.Y;
                Pos.Z += Pos.Motion.Z;

                BlockPos currentPos = Pos.AsBlockPos;

                // Игнорируем блок Распространителя, из которого только что вылетели
                if (SourcePos != null && currentPos.Equals(SourcePos)) return;

                Block block = Api.World.BlockAccessor.GetBlock(currentPos);

                // Если врезались во что-то твердое (не воздух и не жидкость)
                if (block.Id != 0 && block.MatterState != EnumMatterState.Liquid)
                {
                    // Если это Бассейн - отдаем ману!
                    if (block is BlockManaPool)
                    {
                        BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(currentPos);
                        if (be is BlockEntityManaPool pool)
                        {
                            pool.CurrentMana += ManaPayload;
                            if (pool.CurrentMana > pool.MaxMana) pool.CurrentMana = pool.MaxMana;
                            pool.MarkDirty(true);
                        }
                    }

                    // Разбиваемся вдребезги о любой блок
                    Die();
                }
            }
        }
    }
}