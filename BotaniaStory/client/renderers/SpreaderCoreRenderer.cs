using BotaniaStory.blockentity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using BotaniaStory.systems;

namespace BotaniaStory.client.renderers
{
    public class SpreaderCoreRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private BlockPos pos;
        private MeshRef coreMeshRef;
        private BlockEntityManaSpreader spreader;
        public Matrixf ModelMat = new Matrixf();

        private int coreTextureId;
        private float spinAngle = 0f;
        private bool cleanupQueued;
        private bool disposed;

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public SpreaderCoreRenderer(ICoreClientAPI capi, BlockPos pos, BlockEntityManaSpreader spreader)
        {
            this.capi = capi;
            this.pos = pos.Copy();
            this.spreader = spreader;

            TextureAtlasPosition texPos = capi.BlockTextureAtlas.GetPosition(spreader.Block, "livingwood");
            coreTextureId = texPos != null ? texPos.atlasTextureId : capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

            AssetLocation shapeLoc = new AssetLocation("botaniastory", "shapes/block/manaspreader_core.json");
            Shape shape = capi.Assets.TryGet(shapeLoc)?.ToObject<Shape>();

            if (shape != null)
            {
                MeshData mesh;
                capi.Tesselator.TesselateShape(spreader.Block, shape, out mesh);
                coreMeshRef = capi.Render.UploadMesh(mesh);
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (disposed || coreMeshRef == null || coreMeshRef.Disposed) return;

            // Рендер удаляется, если распространителя на позиции больше нет
            if (spreader == null || !object.ReferenceEquals(capi.World.BlockAccessor.GetBlockEntity(pos), spreader))
            {
                QueueCleanup();
                return;
            }

            IClientPlayer player = capi.World.Player;
            if (player?.Entity == null) return;

            spinAngle += deltaTime;

            IRenderAPI render = capi.Render;
            IStandardShaderProgram prog = render.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            ShaderSanitizer.Sanitize(prog);

            ModelMat.Identity();

            Vec3d camPos = player.Entity.CameraPos;
            ModelMat.Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);

            ModelMat.Translate(0.5f, 0.5f, 0.5f);
            ModelMat.RotateY(spreader.Yaw);
            ModelMat.RotateX(spreader.Pitch);
            ModelMat.RotateY(spinAngle);
            ModelMat.Translate(-0.5f, -0.5f, -0.5f);

            prog.ModelMatrix = ModelMat.Values;
            prog.ViewMatrix = render.CameraMatrixOriginf;
            prog.ProjectionMatrix = render.CurrentProjectionMatrix;

            prog.Tex2D = coreTextureId;

            render.RenderMesh(coreMeshRef);
            prog.Stop();
        }

        private void QueueCleanup()
        {
            if (cleanupQueued || disposed) return;

            cleanupQueued = true;

            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (disposed) return;

                capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
                Dispose();
            }, "botaniastory-spreader-core-cleanup");
        }

        public void Dispose()
        {
            if (disposed) return;

            disposed = true;
            coreMeshRef?.Dispose();
            coreMeshRef = null;
            spreader = null;
        }
    }
}