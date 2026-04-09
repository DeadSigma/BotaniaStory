using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class EntitySpark : Entity
    {
        // ГЛАВНЫЙ ФИКС: Делает искру призраком. Она больше не перекрывает установку блоков!
        public override bool IsInteractable => false;

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
        }


        // полностью отключает стандартные клики по сущности.

        public override void OnGameTick(float dt)
        {
            // ==========================================
            // 1. ВИЗУАЛЬНАЯ ЧАСТЬ (Отрабатывает ДО базового тика)
            // ==========================================
            if (Api is ICoreClientAPI capi)
            {
                float camYaw = capi.World.Player.CameraYaw;

                // Вращаем только влево-вправо
                Pos.Yaw = camYaw + GameMath.PIHALF;

                // Строго нули, чтобы модель не плющило!
                Pos.Pitch = 0;
                Pos.Roll = 0;
            }

            // ==========================================
            // 2. БАЗОВЫЙ ТИК (Движок переносит наш Pos.Yaw в рендер)
            // ==========================================
            base.OnGameTick(dt);

            // ==========================================
            // 3. СЕРВЕРНАЯ ЧАСТЬ (Всплытие над блоками)
            // ==========================================
            if (Api.Side == EnumAppSide.Server && Api.World.ElapsedMilliseconds % 200 == 0)
            {
                double baseX = WatchedAttributes.GetDouble("baseX", Pos.X);
                double baseY = WatchedAttributes.GetDouble("baseY", Pos.Y);
                double baseZ = WatchedAttributes.GetDouble("baseZ", Pos.Z);

                int currentY = (int)baseY;
                bool foundTop = false;

                for (int i = 0; i < 10; i++)
                {
                    Block block = Api.World.BlockAccessor.GetBlock(new BlockPos((int)baseX, currentY, (int)baseZ));

                    if (block.Id == 0 || block.Replaceable > 5000)
                    {
                        foundTop = true;
                        break;
                    }
                    currentY++;
                }

                double targetY = foundTop ? currentY + 0.2 : baseY;

                if (Math.Abs(Pos.Y - targetY) > 0.05)
                {
                    Pos.Y = targetY;
                    Pos.Y = targetY; // <--- ИСПРАВЛЕНА ОПЕЧАТКА!
                }
            }
        }
    }
}