using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class SparkRenderer : IRenderer, IDisposable
    {
        private ICoreClientAPI capi;
        private EntitySpark entity;
        private MeshRef meshRef;
        private MeshData mesh;
        private LoadedTexture texture;

        private long spawnTime;
        private int lastFrame = -1;

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        public SparkRenderer(EntitySpark entity, ICoreClientAPI capi)
        {
            this.entity = entity;
            this.capi = capi;
            this.spawnTime = capi.World.ElapsedMilliseconds;

            texture = new LoadedTexture(capi);

            // ТЫ БЫЛ ПРАВ! Оставляем твой вариант пути:
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "entity/spark_anim"), ref texture);

            // ВНИМАНИЕ: Теперь мы выделяем память на 12 индексов (две стороны)
            mesh = new MeshData(4, 12);

            mesh.xyz = new float[] { -0.5f, 0f, 0, 0.5f, 0f, 0, 0.5f, 1f, 0, -0.5f, 1f, 0 };

            // ДЕЛАЕМ ИСКРУ ДВУСТОРОННЕЙ! 
            // Теперь движок не сможет сделать её невидимой из-за поворота "спиной" к камере.
            mesh.Indices = new int[] {
                0, 1, 2, 0, 2, 3, // Лицевая сторона
                2, 1, 0, 3, 2, 0  // Изнаночная сторона
            };

            mesh.Uv = new float[] { 0, 1, 1, 1, 1, 0, 0, 0 };

            mesh.Rgba = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };

            // Ставим нули, чтобы никакие системные битовые маски не спрятали искру
            mesh.Flags = new int[4];

            mesh.Normals = new int[4];
            mesh.TextureIndices = new byte[4];
            mesh.RenderPassesAndExtraBits = new short[4];
            mesh.ClimateColorMapIds = new byte[4];
            mesh.SeasonColorMapIds = new byte[4];

            mesh.VerticesCount = 4;
            mesh.IndicesCount = 12; // 12 индексов!

            meshRef = capi.Render.UploadMesh(mesh);

            capi.Event.RegisterRenderer(this, EnumRenderStage.OIT, "spark-" + entity.EntityId);
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            // Если текстура не загрузилась, искра не нарисуется!
            if (texture.TextureId == 0) return;

            long aliveTime = capi.World.ElapsedMilliseconds - spawnTime;
            int currentFrame = (int)((aliveTime / 100) % 7);

            if (currentFrame != lastFrame)
            {
                lastFrame = currentFrame;

                // Текстура вертикальная: кадры идут сверху вниз
                float frameHeight = 1f / 7f;
                float vOffset = currentFrame * frameHeight;

                // U (горизонталь) всегда от 0 до 1. 
                // V (вертикаль) смещается на vOffset.
                mesh.Uv[0] = 0; mesh.Uv[1] = vOffset + frameHeight; // Нижний левый угол
                mesh.Uv[2] = 1; mesh.Uv[3] = vOffset + frameHeight; // Нижний правый угол
                mesh.Uv[4] = 1; mesh.Uv[5] = vOffset;               // Верхний правый угол
                mesh.Uv[6] = 0; mesh.Uv[7] = vOffset;               // Верхний левый угол

                capi.Render.UpdateMesh(meshRef, mesh);
            }

            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();

            prog.Tex2D = texture.TextureId;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.NormalShaded = 0;

            // Заставляем искру всегда быть яркой, игнорируя темноту
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);

            Matrixf modelMat = new Matrixf()
                .Identity()
                .Translate(entity.Pos.X - camPos.X, entity.Pos.Y - camPos.Y, entity.Pos.Z - camPos.Z);

            float yaw = capi.World.Player.Entity.Pos.Yaw;
            float pitch = capi.World.Player.Entity.Pos.Pitch;

            modelMat.RotateY(yaw + GameMath.PI);
            modelMat.RotateX(pitch);

            prog.ModelMatrix = modelMat.Values;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(meshRef);
            prog.Stop();
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.OIT);
            meshRef?.Dispose();

        }
    }
}