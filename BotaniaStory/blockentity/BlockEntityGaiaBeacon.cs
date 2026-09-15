using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using BotaniaStory.systems;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory.blocks
{
    public class BlockEntityBeacon : BlockEntityContainer
    {
        private readonly InventoryGeneric inventory;
        private BeaconBeamRenderer renderer;

        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "gaiabeacon";

        public BlockEntityBeacon()
        {
            inventory = new InventoryGeneric(1, "gaiabeacon-0", null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.LateInitialize("gaiabeacon-1", api);
            inventory.SlotModified += OnSlotModified;

            if (api is ICoreClientAPI capi)
            {
                renderer?.Dispose();
                renderer = new BeaconBeamRenderer(Pos, capi, this);
                capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "gaiabeacon");
            }
        }

        private void OnSlotModified(int slotid)
        {
            MarkDirty(true);
        }

        public bool IsActive
        {
            get
            {
                ItemStack stack = inventory[0].Itemstack;
                return stack != null && stack.Collectible.Code.Path == "gear-temporal";
            }
        }

        private void DisposeRenderer()
        {
            if (renderer == null) return;

            renderer.Dispose();
            renderer = null;
        }

        public override void OnBlockRemoved()
        {
            DisposeRenderer();
            base.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            DisposeRenderer();
            base.OnBlockUnloaded();
        }
    }

    public class BeaconBeamRenderer : IRenderer
    {
        private const float BeamStartY = 0.48f;
        private const float BeamHeight = 256f;

        private const float CoreWidth = 0.105f;
        private const float AuraWidth = 0.21f;
        private const float SourceWidth = 0.62f;
        private const float SourceHeight = 1.24f;

        private const float CoreSegmentHeight = 1.6f;
        private const float AuraSegmentHeight = 2.6f;

        private const float CoreScrollSpeed = 0.72f;
        private const float AuraScrollSpeed = 0.34f;

        private readonly ICoreClientAPI capi;
        private readonly BlockPos pos;
        private readonly BlockEntityBeacon be;
        private readonly Matrixf modelMat = new Matrixf();

        private MeshRef coreMeshRef;
        private MeshRef auraMeshRef;
        private MeshRef sourceMeshRef;
        private bool disposed;

        public double RenderOrder => 0.5;
        public int RenderRange => 500;

        public BeaconBeamRenderer(BlockPos pos, ICoreClientAPI capi, BlockEntityBeacon be)
        {
            this.pos = pos;
            this.capi = capi;
            this.be = be;

            coreMeshRef = capi.Render.UploadMesh(
                CreateBeamMesh(CoreWidth, CoreSegmentHeight, BeamHeight)
            );

            auraMeshRef = capi.Render.UploadMesh(
                CreateBeamMesh(AuraWidth, AuraSegmentHeight, BeamHeight)
            );

            sourceMeshRef = capi.Render.UploadMesh(
                CreateSourceMesh(SourceWidth, SourceHeight)
            );
        }

        private static MeshData CreateBeamMesh(float width, float segmentHeight, float height)
        {
            List<float> xyz = new List<float>();
            List<float> uv = new List<float>();
            List<byte> rgba = new List<byte>();
            List<int> indices = new List<int>();

            float half = width * 0.5f;

            for (float y = -segmentHeight; y < height + segmentHeight; y += segmentHeight)
            {
                AddQuad(
                    xyz, uv, rgba, indices,
                    -half, y, 0,
                    half, y, 0,
                    half, y + segmentHeight, 0,
                    -half, y + segmentHeight, 0
                );

                AddQuad(
                    xyz, uv, rgba, indices,
                    0, y, -half,
                    0, y, half,
                    0, y + segmentHeight, half,
                    0, y + segmentHeight, -half
                );
            }

            MeshData mesh = new MeshData();
            mesh.SetXyz(xyz.ToArray());
            mesh.SetUv(uv.ToArray());
            mesh.SetRgba(rgba.ToArray());
            mesh.SetVerticesCount(xyz.Count / 3);
            mesh.SetIndices(indices.ToArray());
            mesh.SetIndicesCount(indices.Count);

            return mesh;
        }

        private static MeshData CreateSourceMesh(float width, float height)
        {
            List<float> xyz = new List<float>();
            List<float> uv = new List<float>();
            List<byte> rgba = new List<byte>();
            List<int> indices = new List<int>();

            float half = width * 0.5f;

            AddQuad(
                xyz, uv, rgba, indices,
                -half, 0, 0,
                half, 0, 0,
                half, height, 0,
                -half, height, 0
            );

            AddQuad(
                xyz, uv, rgba, indices,
                0, 0, -half,
                0, 0, half,
                0, height, half,
                0, height, -half
            );

            MeshData mesh = new MeshData();
            mesh.SetXyz(xyz.ToArray());
            mesh.SetUv(uv.ToArray());
            mesh.SetRgba(rgba.ToArray());
            mesh.SetVerticesCount(xyz.Count / 3);
            mesh.SetIndices(indices.ToArray());
            mesh.SetIndicesCount(indices.Count);

            return mesh;
        }

        private static void AddQuad(
            List<float> xyz,
            List<float> uv,
            List<byte> rgba,
            List<int> indices,
            float x1, float y1, float z1,
            float x2, float y2, float z2,
            float x3, float y3, float z3,
            float x4, float y4, float z4
        )
        {
            int start = xyz.Count / 3;

            AddVertex(xyz, uv, rgba, x1, y1, z1, 0, 1);
            AddVertex(xyz, uv, rgba, x2, y2, z2, 1, 1);
            AddVertex(xyz, uv, rgba, x3, y3, z3, 1, 0);
            AddVertex(xyz, uv, rgba, x4, y4, z4, 0, 0);

            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start);
            indices.Add(start + 2);
            indices.Add(start + 3);
        }

        private static void AddVertex(
            List<float> xyz,
            List<float> uv,
            List<byte> rgba,
            float x, float y, float z,
            float u, float v
        )
        {
            xyz.Add(x);
            xyz.Add(y);
            xyz.Add(z);

            uv.Add(u);
            uv.Add(v);

            rgba.Add(255);
            rgba.Add(255);
            rgba.Add(255);
            rgba.Add(255);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (disposed || !be.IsActive) return;
            if (coreMeshRef == null || auraMeshRef == null || sourceMeshRef == null) return;

            IRenderAPI render = capi.Render;
            IClientWorldAccessor world = capi.World;

            if (world?.Player?.Entity == null) return;

            Vec3d camPos = world.Player.Entity.CameraPos;
            float time = world.ElapsedMilliseconds / 1000f;

            float coreScroll = time * CoreScrollSpeed % CoreSegmentHeight;
            float auraScroll = time * AuraScrollSpeed % AuraSegmentHeight;

            float pulse = 1f + (float)Math.Sin(time * 2.15f) * 0.035f;
            float sourcePulse = 1f + (float)Math.Sin(time * 2.4f) * 0.04f;

            render.GlDisableCullFace();
            render.GlToggleBlend(true);
            render.GLDepthMask(false);

            IStandardShaderProgram prog = null;

            try
            {
                prog = render.PreparedStandardShader(pos.X, pos.Y, pos.Z);
                ShaderSanitizer.Sanitize(prog);

                prog.Tex2D = render.GetOrLoadTexture(
                    new AssetLocation("botaniastory:textures/block/beacon_beam.png")
                );

                prog.ViewMatrix = render.CameraMatrixOriginf;
                prog.ProjectionMatrix = render.CurrentProjectionMatrix;
                prog.AlphaTest = 0.01f;

                RenderLayer(
                    render,
                    prog,
                    auraMeshRef,
                    camPos,
                    auraScroll,
                    -time * 0.11f,
                    pulse,
                    new Vec4f(0.60f, 1.0f, 0.82f, 0.20f),
                    170
                );

                prog.Tex2D = render.GetOrLoadTexture(
                    new AssetLocation("botaniastory:textures/block/beam_source.png")
                );

                RenderLayer(
                    render,
                    prog,
                    sourceMeshRef,
                    camPos,
                    0f,
                    time * 0.06f,
                    sourcePulse,
                    new Vec4f(1.0f, 1.0f, 1.0f, 0.48f),
                    240
                );

                prog.Tex2D = render.GetOrLoadTexture(
                    new AssetLocation("botaniastory:textures/block/beacon_beam.png")
                );

                RenderLayer(
                    render,
                    prog,
                    coreMeshRef,
                    camPos,
                    coreScroll,
                    time * 0.035f,
                    pulse,
                    new Vec4f(0.92f, 1.0f, 0.96f, 0.82f),
                    255
                );
            }
            finally
            {
                prog?.Stop();
                render.GLDepthMask(true);
                render.GlToggleBlend(false);
                render.GlEnableCullFace();
            }
        }

        private void RenderLayer(
            IRenderAPI render,
            IStandardShaderProgram prog,
            MeshRef mesh,
            Vec3d camPos,
            float scroll,
            float rotation,
            float scale,
            Vec4f tint,
            int glow
        )
        {
            prog.RgbaTint = tint;
            prog.ExtraGlow = glow;

            prog.ModelMatrix = modelMat
                .Identity()
                .Translate(
                    pos.X - camPos.X + 0.5,
                    pos.Y - camPos.Y + BeamStartY + scroll,
                    pos.Z - camPos.Z + 0.5
                )
                .RotateY(rotation)
                .Scale(scale, 1f, scale)
                .Values;

            render.RenderMesh(mesh);
        }

        public void Dispose()
        {
            if (disposed) return;

            disposed = true;

            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            coreMeshRef?.Dispose();
            coreMeshRef = null;

            auraMeshRef?.Dispose();
            auraMeshRef = null;

            sourceMeshRef?.Dispose();
            sourceMeshRef = null;
        }
    }
}
