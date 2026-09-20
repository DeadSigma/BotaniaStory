using BotaniaStory.blockentity;
using BotaniaStory.blocks;
using BotaniaStory.entities;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace BotaniaStory.items
{
    public class ItemWandOfTheForest : Item, IContainedMeshSource
    {

        private ICoreClientAPI capi;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI;
        }

        public MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
        {
            if (capi == null) return null;

            Shape cachedShape = null;
            if (Shape?.Base != null)
            {
                cachedShape = capi.TesselatorManager.GetCachedShape(Shape.Base);
            }

            MeshData mesh;
            capi.Tesselator.TesselateItem(this, out mesh, new WandTexSource(targetAtlas, this, cachedShape));

            // Альфа-тест используется без отсечения граней
            mesh.RenderPassesAndExtraBits?.Fill((short)EnumChunkRenderPass.OpaqueNoCull);

            return mesh;
        }

        public string GetMeshCacheKey(ItemSlot slot)
        {
            return slot.Itemstack.Collectible.Code.ToString();
        }

        // Текстура берётся у предмета, затем у шейпа
        private class WandTexSource : ITexPositionSource
        {
            private readonly ITextureAtlasAPI atlas;
            private readonly Item item;
            private readonly Shape shape;

            public WandTexSource(ITextureAtlasAPI atlas, Item item, Shape shape)
            {
                this.atlas = atlas;
                this.item = item;
                this.shape = shape;
            }

            public Size2i AtlasSize => atlas.Size;

            public TextureAtlasPosition this[string textureCode]
            {
                get
                {
                    AssetLocation path = null;
                    CompositeTexture tex;

                    if (item.Textures.TryGetValue(textureCode, out tex)) path = tex.Baked.BakedName;
                    if (path == null && item.Textures.TryGetValue("all", out tex)) path = tex.Baked.BakedName;
                    if (path == null) shape?.Textures.TryGetValue(textureCode, out path);
                    if (path == null) path = new AssetLocation(textureCode);

                    TextureAtlasPosition texPos = atlas[path];
                    if (texPos == null)
                    {
                        int subId;
                        if (!atlas.GetOrInsertTexture(path, out subId, out texPos, null, 0f))
                        {
                            return atlas.UnknownTexturePosition;
                        }
                    }
                    return texPos;
                }
            }
        }

        // ЛКМ - отмена привязки
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            // ЛКМ по Wardbloom - смена режима частиц
            if (blockSel != null)
            {
                BlockEntity targetBe = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                BEBehaviorWardbloom wardbloom = targetBe?.GetBehavior<BEBehaviorWardbloom>();

                if (wardbloom != null)
                {
                    if (byEntity.World.Side == EnumAppSide.Server)
                    {
                        wardbloom.CycleParticleMode();
                    }

                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            bool hadFlower = slot.Itemstack.Attributes.HasAttribute("hasFlower");
            bool hadSpreader = slot.Itemstack.Attributes.HasAttribute("hasSpreader");
            bool hadAmaranthus = slot.Itemstack.Attributes.HasAttribute("hasAmaranthus");
            bool hadFunctionalFlower = slot.Itemstack.Attributes.HasAttribute("hasFunctionalFlower");

            if (hadFlower || hadSpreader || hadAmaranthus || hadFunctionalFlower)
            {
                slot.Itemstack.Attributes.RemoveAttribute("hasFlower");
                slot.Itemstack.Attributes.RemoveAttribute("hasSpreader");
                slot.Itemstack.Attributes.RemoveAttribute("hasAmaranthus");
                slot.Itemstack.Attributes.RemoveAttribute("hasFunctionalFlower");
                slot.MarkDirty();
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }

        // ПКМ - привязка и работа с искрами
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {


            if (!firstEvent) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            IWorldAccessor world = byEntity.World;
            float wandVolume = 1f;

            if (world.Api is ICoreClientAPI)
            {
                wandVolume = (BotaniaStoryModSystem.ClientConfig?.WandVolume ?? 50) / 100f;
            }




            // Искра ищется лучом от камеры
            EntitySpark targetSpark = null;

            Vec3d eyePos = byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
            Vec3f viewVec = byEntity.Pos.GetViewVector();
            Vec3d lookDir = new Vec3d(viewVec.X, viewVec.Y, viewVec.Z);

            Entity[] nearbySparks = world.GetEntitiesAround(byEntity.Pos.XYZ, 5, 5, e => e is EntitySpark);
            double closestDistance = 5.0; // Дальность луча

            foreach (Entity entity in nearbySparks)
            {
                if (entity is EntitySpark spark)
                {
                    Vec3d sparkCenter = new Vec3d(spark.Pos.X, spark.Pos.Y + 0.3, spark.Pos.Z);
                    Vec3d V = new Vec3d(sparkCenter.X - eyePos.X, sparkCenter.Y - eyePos.Y, sparkCenter.Z - eyePos.Z);

                    double t = V.Dot(lookDir);

                    if (t > 0 && t < closestDistance)
                    {
                        Vec3d projection = new Vec3d(lookDir.X * t, lookDir.Y * t, lookDir.Z * t);
                        Vec3d perpendicular = new Vec3d(V.X - projection.X, V.Y - projection.Y, V.Z - projection.Z);

                        // Допуск луча - 0.4 блока
                        if (perpendicular.Length() < 0.4)
                        {
                            closestDistance = t;
                            targetSpark = spark;
                        }
                    }
                }
            }

            if (targetSpark != null)
            {
                // Shift снимает руну или саму искру
                if (byPlayer.Entity.Controls.Sneak)
                {
                    string currentAugment = targetSpark.WatchedAttributes.GetString("augment", "none");

                    if (currentAugment != "none")
                    {
                        targetSpark.WatchedAttributes.SetString("augment", "none");
                        targetSpark.WatchedAttributes.MarkAllDirty();

                        if (world.Side == EnumAppSide.Server)
                        {
                            string itemCode = "sparkaugment-" + currentAugment;
                            Item augmentItem = world.GetItem(new AssetLocation("botaniastory", itemCode));
                            if (augmentItem != null)
                            {
                                world.SpawnItemEntity(new ItemStack(augmentItem), targetSpark.Pos.XYZ);
                            }

                            world.PlaySoundAt(new AssetLocation("game", "sounds/player/throw"), targetSpark.Pos.X, targetSpark.Pos.Y, targetSpark.Pos.Z, null, true, 16, 1f);
                        }
                    }
                    else
                    {
                        if (world.Side == EnumAppSide.Server)
                        {
                            Item itemSpark = world.GetItem(new AssetLocation("botaniastory", "spark"));
                            if (itemSpark != null)
                            {
                                world.SpawnItemEntity(new ItemStack(itemSpark), targetSpark.Pos.XYZ);
                            }
                            targetSpark.Die(EnumDespawnReason.PickedUp);
                        }

                        world.PlaySoundAt(new AssetLocation("botaniastory", "sounds/wand_bind"), targetSpark.Pos.X, targetSpark.Pos.Y, targetSpark.Pos.Z, byPlayer, true, 16, wandVolume);
                    }
                }
                // Обычный клик показывает связи искры
                else
                {
                    Vec3d sparkPos = targetSpark.Pos.XYZ.AddCopy(0, 0.1, 0);
                    int foundSparks = 0;

                    Entity[] linkedSparks = world.GetEntitiesAround(targetSpark.Pos.XYZ, 8, 8, e => e is EntitySpark && e.EntityId != targetSpark.EntityId);

                    foreach (Entity entity in linkedSparks)
                    {
                        if (entity is EntitySpark otherSpark)
                        {
                            Vec3d otherSparkPos = otherSpark.Pos.XYZ.AddCopy(0, 0.1, 0);
                            SpawnBindingParticles(world, sparkPos, otherSparkPos);
                            foundSparks++;
                        }
                    }

                    if (foundSparks > 0)
                    {
                        world.PlaySoundAt(new AssetLocation("botaniastory", "sounds/effect/translocate"), targetSpark.Pos.X, targetSpark.Pos.Y, targetSpark.Pos.Z, byPlayer, true, 16, wandVolume);
                    }
                    else
                    {
                        world.PlaySoundAt(new AssetLocation("botaniastory", "sounds/wand_bind"), targetSpark.Pos.X, targetSpark.Pos.Y, targetSpark.Pos.Z, byPlayer, true, 16, wandVolume);
                    }
                }

                handling = EnumHandHandling.Handled;
                return;
            }


            if (blockSel == null) return;

            BlockPos pos = blockSel.Position;
            Block block = world.BlockAccessor.GetBlock(pos);
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);

            AssetLocation wandSound = new AssetLocation("botaniastory", "sounds/wand_bind");

            // Shift+ПКМ меняет режим бассейна
            if (byPlayer.Entity.Controls.Sneak && block is BlockManaPool)
            {
                if (be is BlockEntityManaPool poolBE)
                {
                    // Режим меняется только на сервере - Sneak на клиенте приходит
                    // с задержкой и может рассинхронизироваться
                    if (world.Side == EnumAppSide.Server)
                    {
                        poolBE.IsAcceptingFromItems = !poolBE.IsAcceptingFromItems;
                        poolBE.MarkDirty(true);
                    }
                    world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            bool hasFlowerInMemory = slot.Itemstack.Attributes.GetBool("hasFlower");
            bool hasSpreaderInMemory = slot.Itemstack.Attributes.GetBool("hasSpreader");
            bool hasFunctionalFlowerInMemory = slot.Itemstack.Attributes.GetBool("hasFunctionalFlower");

            // Завершение привязки цветка к бассейну
            if (hasFunctionalFlowerInMemory)
            {
                if (block is BlockManaPool || be is BlockEntityManaPool)
                {
                    int ax = slot.Itemstack.Attributes.GetInt("functionalFlowerX");
                    int ay = slot.Itemstack.Attributes.GetInt("functionalFlowerY");
                    int az = slot.Itemstack.Attributes.GetInt("functionalFlowerZ");
                    BlockPos flowerPos = new BlockPos(ax, ay, az);

                    BlockEntity flowerBe = world.BlockAccessor.GetBlockEntity(flowerPos);

                    ILinkableToPool targetFlower = GetInterface<ILinkableToPool>(flowerBe);
                    if (targetFlower != null)
                    {
                        targetFlower.LinkedPool = pos.Copy();
                        flowerBe.MarkDirty(true);
                        world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                    }
                }

                slot.Itemstack.Attributes.RemoveAttribute("hasFunctionalFlower");
                slot.MarkDirty();
                handling = EnumHandHandling.Handled;
                return;
            }

            // Завершение привязки цветка к распространителю
            if (hasFlowerInMemory)
            {
                if (block is ManaSpreader)
                {
                    int fx = slot.Itemstack.Attributes.GetInt("flowerX");
                    int fy = slot.Itemstack.Attributes.GetInt("flowerY");
                    int fz = slot.Itemstack.Attributes.GetInt("flowerZ");
                    BlockPos flowerPos = new BlockPos(fx, fy, fz);

                    BlockEntity flowerEntity = world.BlockAccessor.GetBlockEntity(flowerPos);

                    ILinkableToSpreader targetGeneratingFlower = GetInterface<ILinkableToSpreader>(flowerEntity);
                    if (targetGeneratingFlower != null)
                    {
                        targetGeneratingFlower.LinkedSpreader = pos.Copy();
                        flowerEntity.MarkDirty(true);
                    }

                    slot.Itemstack.Attributes.RemoveAttribute("hasFlower");
                    slot.MarkDirty();
                    world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                }
                else
                {
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            // Завершение привязки распространителя к цели
            if (hasSpreaderInMemory)
            {
                int sx = slot.Itemstack.Attributes.GetInt("spreaderX");
                int sy = slot.Itemstack.Attributes.GetInt("spreaderY");
                int sz = slot.Itemstack.Attributes.GetInt("spreaderZ");
                BlockPos spreaderPos = new BlockPos(sx, sy, sz);

                BlockEntityManaSpreader spreaderBE = world.BlockAccessor.GetBlockEntity(spreaderPos) as BlockEntityManaSpreader;
                if (spreaderBE != null)
                {
                    double dx = pos.X - spreaderPos.X;
                    double dy = pos.Y - spreaderPos.Y;
                    double dz = pos.Z - spreaderPos.Z;

                    spreaderBE.Yaw = (float)Math.Atan2(dx, dz) + (float)Math.PI;
                    double distanceXZ = Math.Sqrt(dx * dx + dz * dz);
                    spreaderBE.Pitch = (float)Math.Atan2(dy, distanceXZ);

                    spreaderBE.TargetPos = pos.Copy();
                    spreaderBE.MarkDirty(true);
                    world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                }

                slot.Itemstack.Attributes.RemoveAttribute("hasSpreader");
                slot.MarkDirty();

                handling = EnumHandHandling.Handled;
                return;
            }

            // Wardbloom - настройка барьера
            BEBehaviorWardbloom wardbloom = be?.GetBehavior<BEBehaviorWardbloom>();
            if (wardbloom != null)
            {
                bool forceRelink = byPlayer.Entity.Controls.CtrlKey;

                // Первый клик или Ctrl+ПКМ запускает перепривязку
                if (wardbloom.LinkedPool == null || forceRelink)
                {
                    slot.Itemstack.Attributes.SetInt("functionalFlowerX", pos.X);
                    slot.Itemstack.Attributes.SetInt("functionalFlowerY", pos.Y);
                    slot.Itemstack.Attributes.SetInt("functionalFlowerZ", pos.Z);
                    slot.Itemstack.Attributes.SetBool("hasFunctionalFlower", true);
                    slot.MarkDirty();
                }
                else
                {
                    // ПКМ увеличивает радиус, Shift+ПКМ уменьшает
                    if (world.Side == EnumAppSide.Server)
                    {
                        int direction = byPlayer.Entity.Controls.Sneak ? -1 : 1;

                        if (wardbloom.StepRadius(direction) && byPlayer is IServerPlayer serverPlayer)
                        {
                            string message = Lang.Get(
                                 "botaniastory:wardbloom-radius",
                                 (int)wardbloom.BarrierRadius
                             );

                            serverPlayer.SendIngameError(
                                "wardbloom-radius",
                                message
                            );
                        }
                    }
                }

                world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                handling = EnumHandHandling.Handled;
                return;
            }

            if (GetInterface<ILinkableToPool>(be) != null)
            {
                slot.Itemstack.Attributes.SetInt("functionalFlowerX", pos.X);
                slot.Itemstack.Attributes.SetInt("functionalFlowerY", pos.Y);
                slot.Itemstack.Attributes.SetInt("functionalFlowerZ", pos.Z);
                slot.Itemstack.Attributes.SetBool("hasFunctionalFlower", true);
                slot.MarkDirty();

                world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                handling = EnumHandHandling.Handled;
                return;
            }


            if (GetInterface<ILinkableToSpreader>(be) != null)
            {
                slot.Itemstack.Attributes.SetInt("flowerX", pos.X);
                slot.Itemstack.Attributes.SetInt("flowerY", pos.Y);
                slot.Itemstack.Attributes.SetInt("flowerZ", pos.Z);
                slot.Itemstack.Attributes.SetBool("hasFlower", true);
                slot.MarkDirty();
                world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                handling = EnumHandHandling.Handled;
                return;
            }

            if (block is ManaSpreader)
            {
                slot.Itemstack.Attributes.SetInt("spreaderX", pos.X);
                slot.Itemstack.Attributes.SetInt("spreaderY", pos.Y);
                slot.Itemstack.Attributes.SetInt("spreaderZ", pos.Z);
                slot.Itemstack.Attributes.SetBool("hasSpreader", true);
                slot.MarkDirty();
                world.PlaySoundAt(wandSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                handling = EnumHandHandling.Handled;
                return;
            }

            if (be is BlockEntityRunicAltar altar)
            {
                if (altar.TryCompleteCrafting(byPlayer))
                {
                    handling = EnumHandHandling.Handled;
                }
                else
                {
                    handling = EnumHandHandling.PreventDefault;
                }
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

        }

        // Луч между связанными искрами
        private void SpawnBindingParticles(IWorldAccessor world, Vec3d start, Vec3d end)
        {
            double distance = start.DistanceTo(end);
            Vec3d direction = (end - start).Normalize();

            SimpleParticleProperties beamParticles = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(255, 100, 255, 200),
                new Vec3d(), new Vec3d(),
                new Vec3f(-0.05f, -0.05f, -0.05f),
                new Vec3f(0.05f, 0.05f, 0.05f),
                1.0f,
                0f,
                0.2f,
                0.4f,
                EnumParticleModel.Quad
            );

            beamParticles.VertexFlags = 128;
            beamParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -0.5f);

            for (float i = 0; i < distance; i += 0.3f)
            {
                beamParticles.MinPos.Set(start.X + direction.X * i, start.Y + direction.Y * i, start.Z + direction.Z * i);
                world.SpawnParticles(beamParticles);
            }
        }
        // Интерфейс ищется у сущности и её поведений
        private T GetInterface<T>(BlockEntity be) where T : class
        {
            if (be == null) return null;

            if (be is T entityInterface) return entityInterface;

            foreach (var behavior in be.Behaviors)
            {
                if (behavior is T behaviorInterface) return behaviorInterface;
            }

            return null;
        }
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            renderinfo.CullFaces = false;
            renderinfo.AlphaTest = 0.4f;

            // На земле нормали затемняют плоскую геометрию
            if (target == EnumItemRenderTarget.Ground)
            {
                renderinfo.NormalShaded = false;
            }
        }
    }
}