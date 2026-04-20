using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class PylonRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private BlockPos pos;
        private MeshRef pylonMeshRef;
        private Matrixf modelMat = new Matrixf();
        private LoadedTexture loadedTexture;

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public PylonRenderer(ICoreClientAPI capi, BlockPos pos)
        {
            this.capi = capi;
            this.pos = pos;

            // Ищем файл с расширением .obj, но в папке shapes
            AssetLocation objPath = new AssetLocation("botaniastory", "shapes/pylon.obj");
            IAsset objAsset = capi.Assets.TryGet(objPath);

            if (objAsset != null)
            {
                try
                {
                    string objText = objAsset.ToText();

                    // Передаем capi для отладки
                    MeshData mesh = ObjParser.Parse(objText, capi);

                    if (mesh.GetVerticesCount() > 0)
                    {
                        pylonMeshRef = capi.Render.UploadMesh(mesh);
                        capi.Logger.Notification($"[BotaniaStory] УСПЕХ! Модель пилона загружена! Вершин: {mesh.GetVerticesCount()}");
                    }
                    else
                    {
                        capi.Logger.Error("[BotaniaStory] ПРОВАЛ: Вершин по-прежнему 0.");
                    }
                }
                catch (System.Exception ex)
                {
                    capi.Logger.Error($"[BotaniaStory] Ошибка парсинга: {ex.Message}");
                }
            }
            else
            {
                capi.Logger.Error("[BotaniaStory] Файл shapes/pylon.obj НЕ НАЙДЕН в ассетах!");
            }

            AssetLocation texPath = new AssetLocation("botaniastory", "textures/block/pylon.png");
            loadedTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(texPath, ref loadedTexture);

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "botaniapylon");
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (pylonMeshRef == null || loadedTexture.TextureId == 0) return;

            IRenderAPI render = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            IStandardShaderProgram prog = render.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            prog.Tex2D = loadedTexture.TextureId;
            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);

            float rotation = (float)((capi.World.Calendar.TotalHours * 50.0) % 360.0);
            float scale = 1.0f;

            modelMat.Identity()
                .Translate(pos.X - camPos.X + 0.5, pos.Y - camPos.Y, pos.Z - camPos.Z + 0.5)
                .RotateY(rotation * GameMath.DEG2RAD)
                .Scale(scale, scale, scale)
                .Translate(-0.5, 0, -0.5);

            prog.ModelMatrix = modelMat.Values;
            prog.ViewMatrix = render.CameraMatrixOriginf;
            prog.ProjectionMatrix = render.CurrentProjectionMatrix;

            render.RenderMesh(pylonMeshRef);
            prog.Stop();
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            pylonMeshRef?.Dispose();
            loadedTexture?.Dispose();
        }
    }
}