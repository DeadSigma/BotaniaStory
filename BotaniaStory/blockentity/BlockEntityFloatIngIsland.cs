using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BotaniaStoryMod : ModSystem
    {
        public FloatingIslandRenderer IslandRenderer { get; private set; }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockEntityClass("BotaniaFloatingIsland", typeof(BlockEntityFloatingIsland));
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);

            IslandRenderer = new FloatingIslandRenderer(capi);
            capi.Event.RegisterRenderer(IslandRenderer, EnumRenderStage.Opaque, "botaniafloatingislands");

            // отладка - можно удалить вместе с блоком после проверки
            capi.ChatCommands.Create("island")
                .WithDescription("Отладка рендера островков")
                .BeginSubCommand("light")
                    .WithDescription("Динамический свет вкл/выкл")
                    .HandleWith(args =>
                    {
                        FloatingIslandRenderer.UseDynamicLight = !FloatingIslandRenderer.UseDynamicLight;
                        return TextCommandResult.Success("Динамический свет: " + FloatingIslandRenderer.UseDynamicLight);
                    })
                .EndSubCommand()
                .BeginSubCommand("glow")
                    .WithDescription("Уровень свечения 0-255")
                    .WithArgs(capi.ChatCommands.Parsers.OptionalInt("level"))
                    .HandleWith(args =>
                    {
                        int lvl = (int?)args[0] ?? 0;
                        FloatingIslandRenderer.GlowLevel = GameMath.Clamp(lvl, 0, 255);
                        return TextCommandResult.Success("Свечение: " + FloatingIslandRenderer.GlowLevel);
                    })
                .EndSubCommand();
        }

        public override void Dispose()
        {
            IslandRenderer?.Dispose();
            IslandRenderer = null;
            base.Dispose();
        }
    }

    // Один рендерер на все островки вместо одного на каждый
    public class FloatingIslandRenderer : IRenderer
    {
        public static bool UseDynamicLight = true;
        public static int GlowLevel = 0;

        readonly ICoreClientAPI capi;
        readonly List<BlockEntityFloatingIsland> islands = new List<BlockEntityFloatingIsland>();
        readonly Dictionary<int, MeshRef> meshCache = new Dictionary<int, MeshRef>();
        readonly Dictionary<int, int> atlasCache = new Dictionary<int, int>();
        readonly object listLock = new object();

        readonly Matrixf modelMat = new Matrixf();
        readonly Vec4f fixedLight = new Vec4f(1, 1, 1, 1);

        public double RenderOrder => 0.5;
        public int RenderRange => 48;

        public FloatingIslandRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public void Add(BlockEntityFloatingIsland island)
        {
            lock (listLock) islands.Add(island);
        }

        public void Remove(BlockEntityFloatingIsland island)
        {
            lock (listLock) islands.Remove(island);
        }

        // меш кешируется по id блока, а не создаётся на каждый островок
        MeshRef GetMesh(Block block)
        {
            if (meshCache.TryGetValue(block.Id, out MeshRef cached)) return cached;

            MeshData mesh = capi.TesselatorManager.GetDefaultBlockMesh(block);
            MeshRef mref = mesh != null ? capi.Render.UploadMesh(mesh) : null;

            meshCache[block.Id] = mref;
            atlasCache[block.Id] = ResolveAtlas(block);
            return mref;
        }

        int ResolveAtlas(Block block)
        {
            int id = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

            if (block.Textures != null && block.Textures.Count > 0)
            {
                var first = block.Textures.Values.First();
                if (first?.Baked != null)
                {
                    id = capi.BlockTextureAtlas.Positions[first.Baked.TextureSubId].atlasTextureId;
                }
            }
            return id;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (capi.World?.Player?.Entity == null) return;

            lock (listLock)
            {
                if (islands.Count == 0) return;

                IRenderAPI rpi = capi.Render;
                Vec3d camPos = capi.World.Player.Entity.CameraPos;
                float time = capi.World.ElapsedMilliseconds / 1000f;

                // шейдер готовим один раз за кадр, а не на каждый островок
                IStandardShaderProgram prog = rpi.PreparedStandardShader((int)camPos.X, (int)camPos.Y, (int)camPos.Z);
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                prog.ExtraGlow = GlowLevel;

                float lerpK = GameMath.Clamp(deltaTime * 4f, 0f, 1f);
                int maxDistSq = RenderRange * RenderRange;
                int boundAtlas = -1;

                for (int i = 0; i < islands.Count; i++)
                {
                    BlockEntityFloatingIsland island = islands[i];
                    if (island?.Block == null) continue;

                    double dx = island.Pos.X - camPos.X;
                    double dy = island.Pos.Y - camPos.Y;
                    double dz = island.Pos.Z - camPos.Z;
                    if (dx * dx + dy * dy + dz * dz > maxDistSq) continue;

                    MeshRef mesh = GetMesh(island.Block);
                    if (mesh == null) continue;

                    int atlas = atlasCache[island.Block.Id];
                    if (atlas != boundAtlas)
                    {
                        prog.Tex2D = atlas;
                        boundAtlas = atlas;
                    }

                    if (UseDynamicLight)
                    {
                        island.LerpLight(lerpK);
                        prog.RgbaLightIn = island.LightRgba;
                    }
                    else
                    {
                        prog.RgbaLightIn = fixedLight;
                    }

                    // сдвиг фазы по позиции - иначе все островки качаются синхронно
                    float phase = island.AnimPhase;
                    float rotationY = time * 0.25f + phase;
                    float bobbingY = (float)Math.Sin(time * 1.5f + phase) * 0.08f;
                    float swayX = (float)Math.Sin(time * 0.8f + phase) * 0.06f;
                    float swayZ = (float)Math.Cos(time * 0.9f + phase) * 0.06f;

                    modelMat.Identity();
                    modelMat.Translate(dx, dy, dz);
                    modelMat.Translate(0.5f, 0.5f + bobbingY, 0.5f);
                    modelMat.RotateX(swayX);
                    modelMat.RotateZ(swayZ);
                    modelMat.RotateY(rotationY);
                    modelMat.Translate(-0.5f, -0.5f, -0.5f);

                    prog.ModelMatrix = modelMat.Values;
                    rpi.RenderMesh(mesh);
                }

                prog.ExtraGlow = 0;
                prog.Stop();
            }
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            foreach (var mref in meshCache.Values) mref?.Dispose();
            meshCache.Clear();
            atlasCache.Clear();

            lock (listLock) islands.Clear();
        }
    }

    public class BlockEntityFloatingIsland : BlockEntity
    {
        const float MaxLightLevel = 32f;
        const int LightHistorySize = 8;

        ICoreClientAPI capi;
        FloatingIslandRenderer renderer;
        long lightListenerId;

        public readonly Vec4f LightRgba = new Vec4f(1, 1, 1, 1);
        readonly Vec4f targetLight = new Vec4f(1, 1, 1, 1);
        readonly Vec4f selfLight = new Vec4f();
        readonly Vec4f[] lightHistory = new Vec4f[LightHistorySize];
        int historyPos;
        bool historyReady;

        public float AnimPhase { get; private set; }

        static readonly Vec3i[] lightOffsets =
        {
            new Vec3i(0, 1, 0),
            new Vec3i(0, 0, 0),
            new Vec3i(1, 0, 0),
            new Vec3i(-1, 0, 0),
            new Vec3i(0, 0, 1),
            new Vec3i(0, 0, -1)
        };

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (!(api is ICoreClientAPI clientApi)) return;
            capi = clientApi;

            AnimPhase = GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 1000) / 1000f * GameMath.TWOPI;

            SetupSelfLight();

            for (int i = 0; i < LightHistorySize; i++) lightHistory[i] = new Vec4f();
            SampleLight();
            LightRgba.Set(targetLight.X, targetLight.Y, targetLight.Z, targetLight.W);

            renderer = api.ModLoader.GetModSystem<BotaniaStoryMod>()?.IslandRenderer;
            renderer?.Add(this);

            // свет читаем в игровом потоке, не в рендере
            lightListenerId = capi.Event.RegisterGameTickListener(dt => SampleLight(), 200);
        }

        void SetupSelfLight()
        {
            byte[] hsv = Block.LightHsv;
            if (hsv == null || hsv[2] == 0)
            {
                selfLight.Set(0, 0, 0, 0);
                return;
            }

            float v = hsv[2] / MaxLightLevel;

            if (hsv[1] > 0)
            {
                int rgb = ColorUtil.HsvToRgb(hsv[0] * 4, hsv[1] * 8, 255);
                selfLight.Set(
                    (rgb & 0xff) / 255f * v,
                    ((rgb >> 8) & 0xff) / 255f * v,
                    ((rgb >> 16) & 0xff) / 255f * v,
                    0
                );
            }
            else
            {
                selfLight.Set(v, v, v, 0);
            }
        }

        void SampleLight()
        {
            if (capi == null) return;

            IBlockAccessor ba = capi.World.BlockAccessor;
            if (ba.GetChunkAtBlockPos(Pos) == null) return;

            float r = 0, g = 0, b = 0, s = 0;

            for (int i = 0; i < lightOffsets.Length; i++)
            {
                Vec3i off = lightOffsets[i];
                Vec4f l = ba.GetLightRGBs(Pos.X + off.X, Pos.Y + off.Y, Pos.Z + off.Z);
                if (l == null) continue;

                if (l.X > r) r = l.X;
                if (l.Y > g) g = l.Y;
                if (l.Z > b) b = l.Z;
                if (l.W > s) s = l.W;
            }

            if (!historyReady)
            {
                for (int i = 0; i < LightHistorySize; i++) lightHistory[i].Set(r, g, b, s);
                historyReady = true;
            }
            else
            {
                lightHistory[historyPos].Set(r, g, b, s);
                historyPos = (historyPos + 1) % LightHistorySize;
            }

            // максимум по окну - провалы во время relight не проходят
            float mr = 0, mg = 0, mb = 0, ms = 0;
            for (int i = 0; i < LightHistorySize; i++)
            {
                Vec4f h = lightHistory[i];
                if (h.X > mr) mr = h.X;
                if (h.Y > mg) mg = h.Y;
                if (h.Z > mb) mb = h.Z;
                if (h.W > ms) ms = h.W;
            }

            // пол по собственному свечению - темнее островок быть не может
            targetLight.Set(
                Math.Max(mr, selfLight.X),
                Math.Max(mg, selfLight.Y),
                Math.Max(mb, selfLight.Z),
                ms
            );
        }

        public void LerpLight(float k)
        {
            LightRgba.X += (targetLight.X - LightRgba.X) * k;
            LightRgba.Y += (targetLight.Y - LightRgba.Y) * k;
            LightRgba.Z += (targetLight.Z - LightRgba.Z) * k;
            LightRgba.W += (targetLight.W - LightRgba.W) * k;
        }

        // блок рисуем сами, из меша чанка его убираем
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            return true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            Unregister();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            Unregister();
        }

        void Unregister()
        {
            if (capi == null) return;

            if (lightListenerId != 0)
            {
                capi.Event.UnregisterGameTickListener(lightListenerId);
                lightListenerId = 0;
            }

            renderer?.Remove(this);
            renderer = null;
        }
    }
}