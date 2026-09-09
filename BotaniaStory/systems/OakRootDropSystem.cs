using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.systems
{
    public class OakRootDropSystem : ModSystem
    {
        private const double RootDropChance = 0.0050;
        private const int OakRadius = 2;
        private const int TrunkSearchHeight = 4;
        private const int LeafRadius = 3;
        private const int LeafSearchHeight = 16;

        private ICoreServerAPI sapi;
        private CollectibleObject rootCollectible;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            AssetLocation rootCode = new AssetLocation("botaniastory", "root-normal");
            rootCollectible = api.World.GetItem(rootCode);

            if (rootCollectible == null)
            {
                rootCollectible = api.World.GetBlock(rootCode);
            }

            if (rootCollectible == null)
            {
                api.Logger.Warning("[BotaniaStory] Не найден botaniastory:root-normal");
                return;
            }

            api.Event.DidBreakBlock += OnDidBreakBlock;
        }

        private void OnDidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (blockSel == null || blockSel.Position == null || rootCollectible == null) return;

            Block brokenBlock = sapi.World.GetBlock(oldblockId);
            if (brokenBlock == null || brokenBlock.BlockMaterial != EnumBlockMaterial.Soil) return;

            // Шанс проверяем первым - 80% ломаний не запускают поиск дуба
            if (sapi.World.Rand.NextDouble() >= RootDropChance) return;

            if (!HasLeafyOakNearby(blockSel.Position)) return;

            BlockPos pos = blockSel.Position;
            Vec3d dropPos = new Vec3d(pos.X + 0.5, pos.InternalY + 0.5, pos.Z + 0.5);
            sapi.World.SpawnItemEntity(new ItemStack(rootCollectible), dropPos);
        }

        private bool HasLeafyOakNearby(BlockPos dugPos)
        {
            IBlockAccessor accessor = sapi.World.BlockAccessor;
            BlockPos scanPos = new BlockPos(dugPos.dimension);

            for (int dx = -OakRadius; dx <= OakRadius; dx++)
            {
                for (int dz = -OakRadius; dz <= OakRadius; dz++)
                {
                    int trunkY = -1;

                    for (int dy = 1; dy <= TrunkSearchHeight; dy++)
                    {
                        scanPos.Set(dugPos.X + dx, dugPos.Y + dy, dugPos.Z + dz);
                        Block block = accessor.GetBlock(scanPos, BlockLayersAccess.Solid);

                        if (!IsGeneratedOakLog(block)) continue;

                        trunkY = scanPos.Y;
                        break;
                    }

                    if (trunkY >= 0 && HasOakLeaves(accessor, dugPos.X + dx, trunkY, dugPos.Z + dz, dugPos.dimension))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasOakLeaves(IBlockAccessor accessor, int trunkX, int trunkY, int trunkZ, int dimension)
        {
            BlockPos scanPos = new BlockPos(dimension);

            for (int dy = 2; dy <= LeafSearchHeight; dy++)
            {
                int y = trunkY + dy;

                for (int dx = -LeafRadius; dx <= LeafRadius; dx++)
                {
                    for (int dz = -LeafRadius; dz <= LeafRadius; dz++)
                    {
                        scanPos.Set(trunkX + dx, y, trunkZ + dz);
                        Block block = accessor.GetBlock(scanPos, BlockLayersAccess.Solid);

                        if (IsGeneratedOakLeaf(block))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsGeneratedOakLog(Block block)
        {
            if (block == null || block.Code == null || block.Code.Domain != "game") return false;

            return block.Code.Path.StartsWith("log-grown-oak-", StringComparison.Ordinal);
        }

        private static bool IsGeneratedOakLeaf(Block block)
        {
            if (block == null || block.Code == null || block.Code.Domain != "game") return false;

            string path = block.Code.Path;
            if (!path.StartsWith("leaves", StringComparison.Ordinal)) return false;
            if (path.IndexOf("oak", StringComparison.Ordinal) < 0) return false;
            if (path.IndexOf("placed", StringComparison.Ordinal) >= 0) return false;

            return true;
        }

        public override void Dispose()
        {
            if (sapi != null)
            {
                sapi.Event.DidBreakBlock -= OnDidBreakBlock;
            }

            rootCollectible = null;
            sapi = null;

            base.Dispose();
        }
    }
}
