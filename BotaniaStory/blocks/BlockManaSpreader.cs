using BotaniaStory.blockentity;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace BotaniaStory.blocks
{
    public class ManaSpreader : BlockManaSpreaderBase
    {
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool placed = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (placed && byPlayer != null)
            {
                BlockEntityManaSpreader be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityManaSpreader;
                if (be != null)
                {

                    // Направление фиксируется по ближайшей оси
                    double blockX = blockSel.Position.X + 0.5;
                    double blockY = blockSel.Position.Y + 0.5;
                    double blockZ = blockSel.Position.Z + 0.5;

                    double playerX = byPlayer.Entity.Pos.X;
                    double playerY = byPlayer.Entity.Pos.Y + byPlayer.Entity.LocalEyePos.Y;
                    double playerZ = byPlayer.Entity.Pos.Z;

                    double dx = playerX - blockX;
                    double dy = playerY - blockY;
                    double dz = playerZ - blockZ;

                    double absX = Math.Abs(dx);
                    double absY = Math.Abs(dy);
                    double absZ = Math.Abs(dz);

                    double snappedDx = 0;
                    double snappedDy = 0;
                    double snappedDz = 0;

                    if (absY > absX && absY > absZ)
                    {
                        snappedDy = Math.Sign(dy);
                    }
                    else if (absX > absZ)
                    {
                        snappedDx = Math.Sign(dx);
                    }
                    else
                    {
                        snappedDz = Math.Sign(dz);
                    }

                    be.Yaw = (float)Math.Atan2(snappedDx, snappedDz) + (float)Math.PI;
                    double distanceXZ = Math.Sqrt(snappedDx * snappedDx + snappedDz * snappedDz);
                    be.Pitch = (float)Math.Atan2(snappedDy, distanceXZ);



                    // Цель ищется по направлению взгляда
                    Vec3f viewVec = byPlayer.Entity.Pos.GetViewVector();

                    for (float i = 1f; i <= 12f; i += 0.5f)
                    {
                        int cx = (int)Math.Floor(blockSel.Position.X + 0.5f + viewVec.X * i);
                        int cy = (int)Math.Floor(blockSel.Position.Y + 0.5f + viewVec.Y * i);
                        int cz = (int)Math.Floor(blockSel.Position.Z + 0.5f + viewVec.Z * i);
                        BlockPos checkPos = new BlockPos(cx, cy, cz);

                        Block hitBlock = world.BlockAccessor.GetBlock(checkPos);

                        if (hitBlock is BlockManaPool || hitBlock is ManaSpreader)
                        {
                            if (!checkPos.Equals(blockSel.Position))
                            {
                                be.TargetPos = checkPos.Copy();
                                if (world.Side == EnumAppSide.Client)
                                {
                                    string messageKey = hitBlock is BlockManaPool
                                        ? "botaniastory:manaspreader-autolink-pool-success"
                                        : "botaniastory:manaspreader-autolink-spreader-success";

                                    (world.Api as Vintagestory.API.Client.ICoreClientAPI)?.ShowChatMessage(Lang.Get(messageKey));
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
