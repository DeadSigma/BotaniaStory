using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using OpenTK.Graphics.OpenGL;

namespace BotaniaStory
{
    public class SparkRenderer : IRenderer, ITexPositionSource
    {
        private ICoreClientAPI capi;
        private EntitySpark spark;
        private MeshRef meshRef;
        private Matrixf modelMat = new Matrixf();
        private Shape shape;

        private MeshRef runeMeshRef;

        private LoadedTexture recessiveTex;
        private LoadedTexture dominantTex;
        private LoadedTexture isolatedTex;
        private LoadedTexture dispersiveTex;

        private float orbitAngle = 0f;

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        public Size2i AtlasSize => capi.EntityTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (shape != null && shape.Textures.TryGetValue(textureCode, out AssetLocation texPath))
                {
                    capi.EntityTextureAtlas.GetOrInsertTexture(texPath, out _, out TextureAtlasPosition pos);
                    return pos ?? capi.EntityTextureAtlas.UnknownTexturePosition;
                }
                return capi.EntityTextureAtlas.UnknownTexturePosition;
            }
        }

        public SparkRenderer(ICoreClientAPI capi, EntitySpark spark)
        {
            this.capi = capi;
            this.spark = spark;

            // 1. Грузим модель искры
            shape = capi.Assets.TryGet("botaniastory:shapes/entity/spark_model.json")?.ToObject<Shape>();
            if (shape != null)
            {
                capi.Tesselator.TesselateShape("spark", shape, out MeshData mesh, this);
                if (mesh != null)
                {
                    meshRef = capi.Render.UploadMesh(mesh);

                    // ==========================================
                    // ХИТРЫЙ ТРЮК: Клонируем сетку искры для руны
                    // ==========================================
                    // Клон сохраняет абсолютно все нужные шейдеру внутренние настройки
                    MeshData runeMesh = mesh.Clone();

                    // Обрезаем её до 1 квадрата (4 вершины, 6 индексов)
                    runeMesh.VerticesCount = 4;
                    runeMesh.IndicesCount = 6;

                    // Превращаем форму звезды в ровный плоский квадрат
                    runeMesh.xyz = new float[] {
                        -0.5f, -0.5f, 0,
                         0.5f, -0.5f, 0,
                         0.5f,  0.5f, 0,
                        -0.5f,  0.5f, 0
                    };

                    // Натягиваем текстуру от края до края (0.0 - 1.0)
                    runeMesh.Uv = new float[] {
                        0, 1,
                        1, 1,
                        1, 0,
                        0, 0
                    };

                    // Указываем порядок отрисовки треугольников
                    runeMesh.Indices = new int[] { 0, 1, 2, 0, 2, 3 };

                    // Красим вершины в белый цвет, чтобы текстура руны не искажалась
                    if (runeMesh.Rgba != null)
                    {
                        for (int i = 0; i < 16 && i < runeMesh.Rgba.Length; i++)
                        {
                            runeMesh.Rgba[i] = 255;
                        }
                    }

                    // Теперь загружаем её в видеокарту. Она загрузится без ошибок!
                    runeMeshRef = capi.Render.UploadMesh(runeMesh);
                }
            }

            // 3. Текстуры
            recessiveTex = new LoadedTexture(capi);
            dominantTex = new LoadedTexture(capi);
            isolatedTex = new LoadedTexture(capi);
            dispersiveTex = new LoadedTexture(capi);

            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/entity/spark_augment_recessive.png"), ref recessiveTex);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/entity/spark_augment_dominant.png"), ref dominantTex);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/entity/spark_augment_isolated.png"), ref isolatedTex);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/entity/spark_augment_dispersive.png"), ref dispersiveTex);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (spark.State == EnumEntityState.Despawned || meshRef == null) return;

            orbitAngle += deltaTime * 1.5f;

            IStandardShaderProgram prog = capi.Render.PreparedStandardShader((int)spark.Pos.X, (int)spark.Pos.Y, (int)spark.Pos.Z);

            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            prog.ExtraGlow = 0;

            capi.Render.GlToggleBlend(true, EnumBlendMode.Standard);
            GL.DepthMask(false);

            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            float dx = (float)(spark.Pos.X - camPos.X);
            float dy = (float)(spark.Pos.Y - camPos.Y);
            float dz = (float)(spark.Pos.Z - camPos.Z);

            float yaw = (float)Math.Atan2(dx, dz) + (float)Math.PI;
            float horizontalDist = (float)Math.Sqrt(dx * dx + dz * dz);
            float pitch = (float)Math.Atan2(dy, horizontalDist);

            // Рендер самой искры
            capi.Render.BindTexture2d(capi.EntityTextureAtlas.AtlasTextures[0].TextureId);

            modelMat.Identity()
                    .Translate(dx, dy, dz)
                    .RotateY(yaw)
                    .RotateX(-pitch)
                    .Scale(3f, 3f, 3f)
                    .Translate(-0.5f, -0.09375f, -0.5f);

            prog.ModelMatrix = modelMat.Values;
            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;
            capi.Render.RenderMesh(meshRef);

            // Рендер руны-дополнителя
            string augmentType = spark.WatchedAttributes.GetString("augment", "none");

            if (augmentType != "none" && runeMeshRef != null)
            {
                int texToBind = 0;
                if (augmentType == "recessive" && recessiveTex != null) texToBind = recessiveTex.TextureId;
                else if (augmentType == "dominant" && dominantTex != null) texToBind = dominantTex.TextureId;
                else if (augmentType == "isolated" && isolatedTex != null) texToBind = isolatedTex.TextureId;
                else if (augmentType == "dispersive" && dispersiveTex != null) texToBind = dispersiveTex.TextureId;

                if (texToBind != 0)
                {
                    capi.Render.BindTexture2d(texToBind);
                    prog.ExtraGlow = 255;

                    float orbitRadius = 0.2f;
                    float orbitX = (float)Math.Cos(orbitAngle) * orbitRadius;
                    float orbitZ = (float)Math.Sin(orbitAngle) * orbitRadius;
                    float orbitY = (float)Math.Sin(orbitAngle * 2f) * 0.1f;

                    modelMat.Identity()
                            .Translate(dx + orbitX, dy + orbitY, dz + orbitZ)
                            .RotateY(yaw)
                            .RotateX(-pitch)
                            .Scale(0.4f, 0.4f, 0.4f);

                    prog.ModelMatrix = modelMat.Values;
                    capi.Render.RenderMesh(runeMeshRef);

                    prog.ExtraGlow = 0;
                }
            }

            prog.Stop();
            GL.DepthMask(true);
            capi.Render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            meshRef?.Dispose();
            meshRef = null;
            runeMeshRef?.Dispose();
            runeMeshRef = null;
        }
    }
}