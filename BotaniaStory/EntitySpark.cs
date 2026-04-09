using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class EntitySpark : Entity
    {
        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            // Никаких костылей с отключением физики! 
            // Искра и так не умеет падать, потому что мы не дали ей физику в JSON.
        }

        // Этот метод сработает, когда игрок кликнет ПКМ именно по самой ИСКРЕ
        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (mode == EnumInteractMode.Interact && byEntity.Controls.Sneak) // Проверка на Shift (Sneak) + ПКМ
            {
                // Проверяем, что в руках Посох Леса
                if (!slot.Empty && slot.Itemstack.Item is ItemWandOfTheForest)
                {
                    if (Api.Side == EnumAppSide.Server)
                    {
                        // Спавним предмет искры обратно в мир
                        ItemStack sparkStack = new ItemStack(Api.World.GetItem(new AssetLocation("botaniastory", "spark")));
                        Api.World.SpawnItemEntity(sparkStack, Pos.XYZ);

                        // Убиваем сущность искры
                        this.Die();
                    }
                    return; // Взаимодействие успешно
                }
            }

            base.OnInteract(byEntity, slot, hitPosition, mode);
        }

        // Обновление каждый тик
        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            Pos.Motion.Set(0, 0, 0);
            Pos.Motion.Set(0, 0, 0);

            // Вращаем искру только на стороне клиента (визуально)
            if (Api is ICoreClientAPI capi)
            {
                var camPos = capi.World.Player.Entity.CameraPos;

                // Математика для вычисления угла поворота (Yaw)
                double dx = camPos.X - Pos.X;
                double dz = camPos.Z - Pos.Z;

                // Устанавливаем поворот. Если текстура будет смотреть задом наперед, 
                // просто убери "+ GameMath.PI" из формулы.
                Pos.Yaw = (float)Math.Atan2(dx, dz) + GameMath.PIHALF;
            }
        }
    }
}