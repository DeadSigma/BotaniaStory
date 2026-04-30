using BotaniaStory.blockentity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.renderers
{
    public class SpreaderCoreRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private BlockPos pos;
        private MeshRef coreMeshRef;
        private BlockEntityManaSpreader spreader;
        public Matrixf ModelMat = new Matrixf();

        // Добавляем переменную для правильного ID текстуры
        private int coreTextureId;

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public SpreaderCoreRenderer(ICoreClientAPI capi, BlockPos pos, BlockEntityManaSpreader spreader)
        {
            this.capi = capi;
            this.pos = pos;
            this.spreader = spreader;

            // 1. Ищем, на какой странице атласа находится 'livingwood'
            TextureAtlasPosition texPos = capi.BlockTextureAtlas.GetPosition(spreader.Block, "livingwood");

            // Если текстура найдена - берем её atlasTextureId, иначе используем 0 как запасной вариант
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
            if (coreMeshRef == null) return;

            IRenderAPI render = capi.Render;
            IStandardShaderProgram prog = render.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            float spinAngle = (capi.World.ElapsedMilliseconds / 1000f);

            ModelMat.Identity();

            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            ModelMat.Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);

            ModelMat.Translate(0.5f, 0.5f, 0.5f);

            ModelMat.RotateY(spreader.Yaw);
            ModelMat.RotateX(spreader.Pitch);

            ModelMat.RotateY(spinAngle);

            ModelMat.Translate(-0.5f, -0.5f, -0.5f);

            prog.ModelMatrix = ModelMat.Values;
            prog.ViewMatrix = render.CameraMatrixOriginf;
            prog.ProjectionMatrix = render.CurrentProjectionMatrix;

            // 2. Биндим не нулевую страницу атласа, а ту, где реально лежит текстура
            prog.Tex2D = coreTextureId;

            render.RenderMesh(coreMeshRef);
            prog.Stop();
        }

        public void Dispose()
        {
            coreMeshRef?.Dispose();
        }
    }
}