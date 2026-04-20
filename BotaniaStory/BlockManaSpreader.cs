using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ManaSpreader : Block
    {
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool placed = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (placed && byPlayer != null)
            {
                BlockEntityManaSpreader be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityManaSpreader;
                if (be != null)
                {
                    // ==========================================
                    // 1. ВЫРОВНЕННЫЙ ПОВОРОТ К ИГРОКУ (6 направлений)
                    // ==========================================

                    double blockX = blockSel.Position.X + 0.5;
                    double blockY = blockSel.Position.Y + 0.5;
                    double blockZ = blockSel.Position.Z + 0.5;

                    double playerX = byPlayer.Entity.Pos.X;
                    double playerY = byPlayer.Entity.Pos.Y + byPlayer.Entity.LocalEyePos.Y;
                    double playerZ = byPlayer.Entity.Pos.Z;

                    // Реальная разница в координатах
                    double dx = playerX - blockX;
                    double dy = playerY - blockY;
                    double dz = playerZ - blockZ;

                    // Берем абсолютные значения (модуль), чтобы найти доминирующее направление
                    double absX = Math.Abs(dx);
                    double absY = Math.Abs(dy);
                    double absZ = Math.Abs(dz);

                    // Сюда запишем  "выровненные" координаты
                    double snappedDx = 0;
                    double snappedDy = 0;
                    double snappedDz = 0;

                    // Ищем самую большую разницу:
                    if (absY > absX && absY > absZ)
                    {
                        // Игрок в основном сверху или снизу. 
                        // Math.Sign вернет 1 (если игрок сверху) или -1 (если снизу).
                        snappedDy = Math.Sign(dy);
                    }
                    else if (absX > absZ)
                    {
                        // Игрок в основном на Западе или Востоке
                        snappedDx = Math.Sign(dx);
                    }
                    else
                    {
                        // Игрок в основном на Севере или Юге
                        snappedDz = Math.Sign(dz);
                    }

                    // Передаем в твою рабочую формулу  чистые, выровненные векторы
                    be.Yaw = (float)Math.Atan2(snappedDx, snappedDz) + (float)Math.PI;
                    double distanceXZ = Math.Sqrt(snappedDx * snappedDx + snappedDz * snappedDz);
                    be.Pitch = (float)Math.Atan2(snappedDy, distanceXZ);


                    // ==========================================
                    // 2. АВТО-ПРИВЯЗКА (Математический радар)
                    // ==========================================

                    Vec3f viewVec = byPlayer.Entity.Pos.GetViewVector();

                    for (float i = 1f; i <= 12f; i += 0.5f)
                    {
                        int cx = (int)Math.Floor(blockSel.Position.X + 0.5f + viewVec.X * i);
                        int cy = (int)Math.Floor(blockSel.Position.Y + 0.5f + viewVec.Y * i);
                        int cz = (int)Math.Floor(blockSel.Position.Z + 0.5f + viewVec.Z * i);
                        BlockPos checkPos = new BlockPos(cx, cy, cz);

                        Block hitBlock = world.BlockAccessor.GetBlock(checkPos);

                        // Ищем Бассейн или другой Распространитель
                        if (hitBlock is BlockManaPool || hitBlock is ManaSpreader)
                        {
                            // Проверяем, что это не тот блок, который мы только что поставили
                            if (!checkPos.Equals(blockSel.Position))
                            {
                                be.TargetPos = checkPos.Copy();
                                if (world.Side == EnumAppSide.Client)
                                {
                                    string targetName = hitBlock is BlockManaPool ? "Бассейну" : "Распространителю";
                                    (world.Api as Vintagestory.API.Client.ICoreClientAPI)?.ShowChatMessage($"Авто-привязка к {targetName} успешна!");
                                }
                                break;
                            }
                        }
                    }

                    be.MarkDirty(true);
                }
            }
            return placed;
        }
    }
}