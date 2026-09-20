using botaniastory;
using BotaniaStory.blocks;
using BotaniaStory.client.renderers;
using BotaniaStory.entities;
using BotaniaStory.items;
using BotaniaStory.items.lenses;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory.blockentity
{
    public interface IManaReceiver
    {
        bool IsFull();
        void ReceiveMana(int amount);
        int GetAvailableSpace();
    }

    public interface IManaLensHost
    {
        ItemStack LensStack { get; }
        IWorldAccessor LensWorld { get; }
        BlockPos LensHostPos { get; }
        void InteractLens(IPlayer byPlayer);
        ItemStack TakeLens();
        BlockPos FindNearestManaReceiver(double range);
        void SetLensTarget(BlockPos targetPos, bool rotate);
    }

    public class BlockEntityManaSpreader : BlockEntity, IManaReceiver, IManaLensHost
    {
        public float Yaw = 0f;
        public float Pitch = 0f;
        public int CurrentMana = 0;
        public int MaxMana = 1000;

        public BlockPos TargetPos = null;

        private bool isDischarging = false;
        private long lastFireMs = 0;
        private int fireCooldownMs = 500;
        private int burstManaAmount = 190;
        private long lastLensTickMs = 0;
        private static readonly AssetLocation LensAttachSound = new AssetLocation("botaniastory", "sounds/lensattach");
        private static readonly AssetLocation LensRemoveSound = new AssetLocation("botaniastory", "sounds/lensremove");

        public ItemStack LensStack { get; private set; }
        public IWorldAccessor LensWorld => Api?.World;
        public BlockPos LensHostPos => Pos;

        public BlockEntityAnimationUtil animUtil;

        private SpreaderCoreRenderer coreRenderer;

        private MeshData baseMesh;
        private readonly Dictionary<string, MeshData> lensMeshCache = new Dictionary<string, MeshData>();

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 100);
            }

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                coreRenderer = new SpreaderCoreRenderer(capi, Pos, this);
                capi.Event.RegisterRenderer(coreRenderer, EnumRenderStage.Opaque, "botaniastory");
            }
        }

        public bool IsFull()
        {
            return CurrentMana >= MaxMana;
        }

        public void ReceiveMana(int amount)
        {
            CurrentMana += amount;
            if (CurrentMana > MaxMana) CurrentMana = MaxMana;

            MarkDirty(false);
        }

        public int GetAvailableSpace()
        {
            return MaxMana - CurrentMana;
        }

        public BlockPos FindNearestManaReceiver(double range)
        {
            if (Api?.World == null) return null;

            int radius = (int)Math.Ceiling(range);
            double maxDistanceSq = range * range;

            double bestReceiverDistanceSq = double.MaxValue;
            BlockPos bestReceiverPos = null;

            double bestSpreaderDistanceSq = double.MaxValue;
            BlockPos bestSpreaderPos = null;

            int chunkSize = GlobalConstants.ChunkSize;

            int minChunkX = FloorDiv(Pos.X - radius, chunkSize);
            int maxChunkX = FloorDiv(Pos.X + radius, chunkSize);
            int minChunkY = FloorDiv(Pos.InternalY - radius, chunkSize);
            int maxChunkY = FloorDiv(Pos.InternalY + radius, chunkSize);
            int minChunkZ = FloorDiv(Pos.Z - radius, chunkSize);
            int maxChunkZ = FloorDiv(Pos.Z + radius, chunkSize);

            for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            {
                for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
                {
                    for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
                    {
                        IWorldChunk chunk = Api.World.BlockAccessor.GetChunk(chunkX, chunkY, chunkZ);
                        if (chunk?.BlockEntities == null) continue;

                        foreach (BlockEntity blockEntity in chunk.BlockEntities.Values)
                        {
                            if (blockEntity?.Pos == null || blockEntity.Pos.Equals(Pos)) continue;
                            if (!(blockEntity is IManaReceiver receiver)) continue;
                            if (receiver.GetAvailableSpace() <= 0) continue;

                            double dx = blockEntity.Pos.X - Pos.X;
                            double dy = blockEntity.Pos.InternalY - Pos.InternalY;
                            double dz = blockEntity.Pos.Z - Pos.Z;
                            double distanceSq = dx * dx + dy * dy + dz * dz;

                            if (distanceSq > maxDistanceSq) continue;

                            bool isSpreader = blockEntity is BlockEntityManaSpreader;

                            if (isSpreader)
                            {
                                if (distanceSq >= bestSpreaderDistanceSq) continue;
                            }
                            else
                            {
                                if (distanceSq >= bestReceiverDistanceSq) continue;
                            }

                            if (!HasClearPath(blockEntity.Pos)) continue;

                            if (isSpreader)
                            {
                                bestSpreaderDistanceSq = distanceSq;
                                bestSpreaderPos = blockEntity.Pos;
                            }
                            else
                            {
                                bestReceiverDistanceSq = distanceSq;
                                bestReceiverPos = blockEntity.Pos;
                            }
                        }
                    }
                }
            }

            return (bestReceiverPos ?? bestSpreaderPos)?.Copy();
        }

        private static int FloorDiv(int value, int divisor)
        {
            int result = value / divisor;
            int remainder = value % divisor;

            if (remainder < 0)
            {
                result--;
            }

            return result;
        }

        public void SetLensTarget(BlockPos targetPos, bool rotate)
        {
            bool targetChanged = TargetPos == null
                ? targetPos != null
                : targetPos == null || !TargetPos.Equals(targetPos);

            if (targetChanged)
            {
                TargetPos = targetPos?.Copy();
            }

            bool rotationChanged = false;

            if (rotate && targetPos != null)
            {
                double dx = targetPos.X - Pos.X;
                double dy = targetPos.Y - Pos.Y;
                double dz = targetPos.Z - Pos.Z;
                double horizontal = Math.Sqrt(dx * dx + dz * dz);

                float newYaw = (float)Math.Atan2(-dx, -dz);
                float newPitch = (float)Math.Atan2(dy, horizontal);

                rotationChanged = Math.Abs(Yaw - newYaw) > 0.0001f || Math.Abs(Pitch - newPitch) > 0.0001f;

                Yaw = newYaw;
                Pitch = newPitch;
            }

            if (targetChanged || rotationChanged)
            {
                MarkDirty(true);
            }
        }

        private bool HasClearPath(BlockPos targetPos)
        {
            Vec3d startPos = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
            Vec3d targetCenter = new Vec3d(targetPos.X + 0.5, targetPos.Y + 0.5, targetPos.Z + 0.5);
            double distance = startPos.DistanceTo(targetCenter);
            if (distance <= 0.001) return false;

            Vec3d direction = (targetCenter - startPos).Normalize();

            for (double step = 0.5; step < distance - 0.2; step += 0.5)
            {
                BlockPos checkPos = new BlockPos(
                    (int)Math.Floor(startPos.X + direction.X * step),
                    (int)Math.Floor(startPos.Y + direction.Y * step),
                    (int)Math.Floor(startPos.Z + direction.Z * step)
                );

                if (checkPos.Equals(Pos) || checkPos.Equals(targetPos)) continue;

                Block block = Api.World.BlockAccessor.GetBlock(checkPos);

                if (block.Id != 0 && block.CollisionBoxes != null && block.CollisionBoxes.Length > 0)
                {
                    if (EntityManaBurst.IsManaPermeable(block)) continue;
                    return false;
                }
            }

            return true;
        }

        public void InteractLens(IPlayer byPlayer)
        {
            if (Api?.Side != EnumAppSide.Server || byPlayer == null) return;

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemManaLens heldLens = slot?.Itemstack?.Item as ItemManaLens;

            if (heldLens != null)
            {
                if (!heldLens.CanAttach(this, slot.Itemstack)) return;

                if (LensStack != null && LensStack.Collectible.Code.Equals(slot.Itemstack.Collectible.Code))
                {
                    ItemStack removedLens = TakeLens();
                    GiveLensToPlayer(byPlayer, removedLens);

                    if (removedLens != null)
                    {
                        PlayLensSound(LensRemoveSound);
                    }

                    return;
                }

                ItemStack newLens;

                if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    newLens = slot.Itemstack.Clone();
                    newLens.StackSize = 1;
                }
                else
                {
                    newLens = slot.TakeOut(1);
                    slot.MarkDirty();
                }

                ItemStack oldLens = LensStack;
                LensStack = newLens;

                if (oldLens != null)
                {
                    GiveLensToPlayer(byPlayer, oldLens);
                }

                MarkLensDirty();
                PlayLensSound(LensAttachSound);
                return;
            }

            if ((slot == null || slot.Empty) && LensStack != null)
            {
                ItemStack removedLens = TakeLens();
                GiveLensToPlayer(byPlayer, removedLens);

                if (removedLens != null)
                {
                    PlayLensSound(LensRemoveSound);
                }
            }
        }

        private void PlayLensSound(AssetLocation sound)
        {
            Api?.World?.PlaySoundAt(sound, Pos, 0.5, null, true, 16f, 0.8f);
        }

        public ItemStack TakeLens()
        {
            if (LensStack == null) return null;

            ItemStack stack = LensStack;
            LensStack = null;
            MarkLensDirty();
            return stack;
        }

        private void GiveLensToPlayer(IPlayer byPlayer, ItemStack stack)
        {
            if (stack == null) return;

            if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
            {
                Api.World.SpawnItemEntity(stack, new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5));
            }
        }

        private void MarkLensDirty()
        {
            lastLensTickMs = 0;
            MarkDirty(true);
            Api?.World?.BlockAccessor?.MarkBlockDirty(Pos);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            DisposeRenderer();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            DisposeRenderer();
        }

        private void DisposeRenderer()
        {
            if (Api is ICoreClientAPI capi && coreRenderer != null)
            {
                capi.Event.UnregisterRenderer(coreRenderer, EnumRenderStage.Opaque);
                coreRenderer.Dispose();
                coreRenderer = null;
            }

            baseMesh = null;
            lensMeshCache.Clear();
        }

        private void OnServerTick(float dt)
        {
            long currentMs = Api.World.ElapsedMilliseconds;

            if (LensStack?.Item is ItemManaLens tickLens && tickLens.SpreaderTickIntervalMs > 0)
            {
                if (currentMs - lastLensTickMs >= tickLens.SpreaderTickIntervalMs)
                {
                    lastLensTickMs = currentMs;
                    tickLens.OnSpreaderTick(this, LensStack);
                }
            }

            // Мана забирается из соседних бассейнов
            if (CurrentMana < MaxMana)
            {
                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos adjPos = Pos.AddCopy(facing);

                    if (TargetPos != null && adjPos.Equals(TargetPos)) continue;

                    BlockEntity adjBlockEntity = Api.World.BlockAccessor.GetBlockEntity(adjPos);

                    if (adjBlockEntity is BlockEntityManaPool adjacentPool)
                    {
                        if (adjacentPool.CurrentMana > 0)
                        {
                            int neededMana = MaxMana - CurrentMana;

                            int manaToTake = Math.Min(neededMana, adjacentPool.CurrentMana);

                            this.CurrentMana += manaToTake;
                            adjacentPool.CurrentMana -= manaToTake;

                            this.MarkDirty(true);
                            adjacentPool.MarkDirty(true);

                            if (this.CurrentMana >= MaxMana)
                            {
                                break;
                            }
                        }
                    }
                }
            }


            if (TargetPos != null)
            {
                BlockEntity targetBlock = Api.World.BlockAccessor.GetBlockEntity(TargetPos);

                if (!(targetBlock is IManaReceiver))
                {
                    TargetPos = null;
                    MarkDirty(true);
                }
            }

            // Цель ищется по направлению распространителя
            if (TargetPos == null)
            {
                double dy = Math.Sin(Pitch);
                double distanceXZ = Math.Cos(Pitch);
                double dx = -Math.Sin(Yaw) * distanceXZ;
                double dz = -Math.Cos(Yaw) * distanceXZ;

                for (float i = 1f; i <= 12f; i += 0.5f)
                {
                    int cx = (int)Math.Floor(Pos.X + 0.5 + dx * i);
                    int cy = (int)Math.Floor(Pos.Y + 0.5 + dy * i);
                    int cz = (int)Math.Floor(Pos.Z + 0.5 + dz * i);
                    BlockPos checkPos = new BlockPos(cx, cy, cz);

                    Block hitBlock = Api.World.BlockAccessor.GetBlock(checkPos);

                    if (hitBlock is BlockManaPool || hitBlock is BlockRunicAltar || hitBlock is BlockTerrestrialPlate || hitBlock is ManaSpreader)
                    {
                        if (!checkPos.Equals(Pos))
                        {
                            TargetPos = checkPos.Copy();
                            MarkDirty(true);
                            break;
                        }
                    }
                    else if (hitBlock.Id != 0 && hitBlock.CollisionBoxes != null && hitBlock.CollisionBoxes.Length > 0)
                    {
                        if (EntityManaBurst.IsManaPermeable(hitBlock))
                        {
                            continue;
                        }
                        break;
                    }
                }
            }

            int threshold = (int)(MaxMana * 0.20f);

            if (CurrentMana >= threshold)
            {
                isDischarging = true;
            }

            if (CurrentMana < burstManaAmount)
            {
                isDischarging = false;
            }

            if (!isDischarging || TargetPos == null) return;

            if (currentMs - lastFireMs < fireCooldownMs) return;

            BlockEntity receiverBlock = Api.World.BlockAccessor.GetBlockEntity(TargetPos);

            if (receiverBlock is IManaReceiver receiver)
            {
                int availableSpace = receiver.GetAvailableSpace();

                if (availableSpace <= 0)
                {
                    return;
                }

            }
            else
            {
                return;
            }

            Vec3d startPos = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
            Vec3d targetCenter = new Vec3d(TargetPos.X + 0.5, TargetPos.Y + 0.5, TargetPos.Z + 0.5);

            double distance = startPos.DistanceTo(targetCenter);
            Vec3d direction = (targetCenter - startPos).Normalize();

            bool isBlocked = false;

            for (float step = 0.5f; step < distance - 0.2f; step += 0.5f)
            {
                BlockPos checkPos = new BlockPos(
                    (int)Math.Floor(startPos.X + direction.X * step),
                    (int)Math.Floor(startPos.Y + direction.Y * step),
                    (int)Math.Floor(startPos.Z + direction.Z * step)
                );

                if (checkPos.Equals(Pos)) continue;

                Block hitBlock = Api.World.BlockAccessor.GetBlock(checkPos);

                if (hitBlock.Id != 0 && hitBlock.CollisionBoxes != null && hitBlock.CollisionBoxes.Length > 0)
                {
                    if (checkPos.Equals(TargetPos) || hitBlock is BlockManaPool)
                    {
                        break;
                    }

                    if (EntityManaBurst.IsManaPermeable(hitBlock))
                    {
                        continue;
                    }

                    isBlocked = true;
                    break;
                }
            }

            if (isBlocked) return;

            // Параметры сгустка изменяются установленной линзой
            EntityProperties type = Api.World.GetEntityType(new AssetLocation("botaniastory", "manaburst"));
            if (type == null) return;

            EntityManaBurst burstEntity = (EntityManaBurst)Api.World.ClassRegistry.CreateEntity(type);

            var burstContext = new ManaBurstContext
            {
                ManaPayload = burstManaAmount,
                Speed = 4.5,
                MaxDistance = 8.0,
                Color = 0x5CC94A,
                Direction = direction,
                TargetPos = TargetPos.Copy()
            };

            if (LensStack?.Item is ItemManaLens lens)
            {
                lens.ModifyBurst(this, LensStack, burstContext);
            }

            if (burstContext.ManaPayload <= 0 || CurrentMana < burstContext.ManaPayload) return;

            direction = burstContext.Direction?.Normalize() ?? direction;

            burstEntity.ManaPayload = burstContext.ManaPayload;
            burstEntity.SourcePos = Pos.Copy();
            burstEntity.WatchedAttributes.SetDouble("maxDist", burstContext.MaxDistance);
            burstEntity.WatchedAttributes.SetInt("burstColor", burstContext.Color);

            burstEntity.Pos.SetPos(startPos);
            burstEntity.Pos.SetFrom(burstEntity.Pos);

            burstEntity.WatchedAttributes.SetDouble("startX", startPos.X);
            burstEntity.WatchedAttributes.SetDouble("startY", startPos.Y);
            burstEntity.WatchedAttributes.SetDouble("startZ", startPos.Z);

            double motionX = direction.X * burstContext.Speed;
            double motionY = direction.Y * burstContext.Speed;
            double motionZ = direction.Z * burstContext.Speed;

            burstEntity.Pos.Motion.Set(motionX, motionY, motionZ);
            burstEntity.Pos.Motion.Set(motionX, motionY, motionZ);

            burstEntity.WatchedAttributes.SetDouble("motionX", motionX);
            burstEntity.WatchedAttributes.SetDouble("motionY", motionY);
            burstEntity.WatchedAttributes.SetDouble("motionZ", motionZ);

            if (LensStack?.Item is ItemManaLens activeLens)
            {
                activeLens.OnBurstCreated(this, LensStack, burstEntity);
            }

            Api.World.SpawnEntity(burstEntity);

            ICoreServerAPI sapi = Api as ICoreServerAPI;
            IServerNetworkChannel channel = sapi.Network.GetChannel("botanianetwork");

            PlayManaSoundPacket soundMessage = new PlayManaSoundPacket()
            {
                Position = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5),
                SoundName = "manaspreaderfire"
            };

            channel.BroadcastPacket(soundMessage);

            lastFireMs = currentMs;
            this.CurrentMana -= burstContext.ManaPayload;
            this.MarkDirty(true);
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("yaw", Yaw);
            tree.SetFloat("pitch", Pitch);
            tree.SetInt("mana", CurrentMana);
            tree.SetItemstack("lens", LensStack);

            if (TargetPos != null)
            {
                tree.SetInt("tgtX", TargetPos.X);
                tree.SetInt("tgtY", TargetPos.Y);
                tree.SetInt("tgtZ", TargetPos.Z);
                tree.SetBool("hasTarget", true);
            }
            else
            {
                tree.SetBool("hasTarget", false);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            float oldYaw = Yaw;
            float oldPitch = Pitch;
            string oldLensCode = LensStack?.Collectible?.Code?.ToString();

            Yaw = tree.GetFloat("yaw", 0f);
            Pitch = tree.GetFloat("pitch", 0f);
            CurrentMana = tree.GetInt("mana", 0);
            LensStack = tree.GetItemstack("lens");

            if (worldForResolving != null)
            {
                LensStack?.ResolveBlockOrItem(worldForResolving);
            }

            if (tree.GetBool("hasTarget"))
            {
                TargetPos = new BlockPos(tree.GetInt("tgtX"), tree.GetInt("tgtY"), tree.GetInt("tgtZ"));
            }
            else
            {
                TargetPos = null;
            }

            if (Api?.Side == EnumAppSide.Client)
            {
                string newLensCode = LensStack?.Collectible?.Code?.ToString();

                if (Yaw != oldYaw || Pitch != oldPitch || oldLensCode != newLensCode)
                {
                    MarkDirty(true);
                    Api.World.BlockAccessor.MarkBlockDirty(Pos);
                }
            }
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);

            if (LensStack != null)
            {
                LensStack.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(LensStack), blockIdMapping, itemIdMapping);
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolving, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForResolving, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            LensStack?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolving);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            MeshData source = baseMesh;

            if (source == null)
            {
                AssetLocation shapeLoc = new AssetLocation("botaniastory", "shapes/block/manaspreader.json");
                Shape shape = Api.Assets.TryGet(shapeLoc)?.ToObject<Shape>();

                if (shape == null) return false;

                tesselator.TesselateShape(Block, shape, out source);
                if (source == null) return false;

                baseMesh = source;
            }

            MeshData mesh = source.Clone();

            Matrixf matrix = new Matrixf();
            matrix.Translate(0.5f, 0.5f, 0.5f)
                  .RotateY(Yaw)
                  .RotateX(Pitch)
                  .Translate(-0.5f, -0.5f, -0.5f);

            mesh.MatrixTransform(matrix.Values);
            mesher.AddMeshData(mesh);

            AddLensMesh(mesher, tesselator);

            return true;
        }

        private void AddLensMesh(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (!(LensStack?.Item is ItemManaLens lens)) return;
            if (!(Api is ICoreClientAPI capi)) return;

            string cacheKey = LensStack.Collectible.Code.ToString();

            if (!lensMeshCache.TryGetValue(cacheKey, out MeshData source))
            {
                AssetLocation baseLoc = LensStack.Item.Shape?.Base;
                if (baseLoc == null) return;

                AssetLocation shapeLoc = new AssetLocation(baseLoc.Domain, "shapes/" + baseLoc.Path + ".json");
                Shape shape = capi.Assets.TryGet(shapeLoc)?.ToObject<Shape>();
                if (shape == null) return;

                var texSource = new LensTexSource(capi.BlockTextureAtlas, LensStack.Item);
                tesselator.TesselateShape("manaspreader-lens-" + cacheKey, shape, out source, texSource);
                if (source == null) return;

                lensMeshCache[cacheKey] = source;
            }

            MeshData lensMesh = source.Clone();
            Vec3f offset = lens.SpreaderOffset;
            Vec3f rotation = lens.SpreaderRotation;
            float rad = (float)Math.PI / 180f;

            Matrixf matrix = new Matrixf();
            matrix.Translate(0.5f, 0.5f, 0.5f)
                  .RotateY(Yaw)
                  .RotateX(Pitch)
                  .Translate(offset.X, offset.Y, offset.Z)
                  .RotateX(rotation.X * rad)
                  .RotateY(rotation.Y * rad)
                  .RotateZ(rotation.Z * rad)
                  .Scale(lens.SpreaderScale, lens.SpreaderScale, lens.SpreaderScale)
                  .Translate(-0.5f, -0.5f, -0.5f);

            lensMesh.MatrixTransform(matrix.Values);
            mesher.AddMeshData(lensMesh);
        }

        private class LensTexSource : ITexPositionSource
        {
            private readonly ITextureAtlasAPI targetAtlas;
            private readonly Item item;

            public LensTexSource(ITextureAtlasAPI targetAtlas, Item item)
            {
                this.targetAtlas = targetAtlas;
                this.item = item;
            }

            public Size2i AtlasSize => targetAtlas.Size;

            public TextureAtlasPosition this[string textureCode]
            {
                get
                {
                    AssetLocation texPath;

                    if (item.Textures != null &&
                        item.Textures.TryGetValue(textureCode, out CompositeTexture texture) &&
                        texture?.Baked?.BakedName != null)
                    {
                        texPath = texture.Baked.BakedName;
                    }
                    else
                    {
                        texPath = new AssetLocation("unknown");
                    }

                    targetAtlas.GetOrInsertTexture(texPath, out _, out TextureAtlasPosition pos);
                    return pos;
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            Item activeItem = forPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Item;

            bool holdsWand = activeItem is ItemWandOfTheForest;
            bool isSneaking = forPlayer.Entity.Controls.Sneak;
        }
    }
}
