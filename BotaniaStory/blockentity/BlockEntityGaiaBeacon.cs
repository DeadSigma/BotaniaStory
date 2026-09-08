using Vintagestory.API.Client;
using BotaniaStory.systems;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory.blocks
{
    public class BlockEntityGaiaBeacon : BlockEntityContainer
    {
        private readonly InventoryGeneric inventory;
        private BeaconBeamRenderer renderer;

        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "gaiabeacon";

        public BlockEntityGaiaBeacon()
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
        private readonly ICoreClientAPI capi;
        private readonly BlockPos pos;
        private readonly BlockEntityGaiaBeacon be;
        private readonly Matrixf modelMat = new Matrixf();

        private MeshRef beamMeshRef;
        private bool disposed;

        public double RenderOrder => 0.5;
        public int RenderRange => 500;

        public BeaconBeamRenderer(BlockPos pos, ICoreClientAPI capi, BlockEntityGaiaBeacon be)
        {
            this.pos = pos;
            this.capi = capi;
            this.be = be;

            capi.Tesselator.TesselateBlock(be.Block, out MeshData mesh);
            beamMeshRef = capi.Render.UploadMesh(mesh);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (disposed || beamMeshRef == null || !be.IsActive) return;

            IRenderAPI render = capi.Render;
            IClientWorldAccessor world = capi.World;

            if (world?.Player?.Entity == null) return;

            Vec3d camPos = world.Player.Entity.CameraPos;

            render.GlDisableCullFace();
            render.GlToggleBlend(true);

            IStandardShaderProgram prog = null;

            try
            {
                prog = render.PreparedStandardShader(pos.X, pos.Y, pos.Z);
                ShaderSanitizer.Sanitize(prog);
                prog.Tex2D = render.GetOrLoadTexture(
                    new AssetLocation("botaniastory:textures/block/mana.png")
                );

                prog.ModelMatrix = modelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y + 1, pos.Z - camPos.Z)
                    .Translate(0.5f, 0, 0.5f)
                    .RotateY(world.ElapsedMilliseconds / 500f)
                    .Scale(0.3f, 256f, 0.3f)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values;

                prog.ViewMatrix = render.CameraMatrixOriginf;
                prog.ProjectionMatrix = render.CurrentProjectionMatrix;

                if (!disposed && beamMeshRef != null)
                {
                    render.RenderMesh(beamMeshRef);
                }
            }
            finally
            {
                prog?.Stop();
                render.GlToggleBlend(false);
                render.GlEnableCullFace();
            }
        }

        public void Dispose()
        {
            if (disposed) return;

            disposed = true;

            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            beamMeshRef?.Dispose();
            beamMeshRef = null;
        }
    }
}
