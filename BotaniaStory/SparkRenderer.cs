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

        // Массив из 7 моделей для каждого кадра анимации
        private MeshRef[] frameMeshes = new MeshRef[7];
        private int currentFrame = 0;
        private float frameTimer = 0f;

        private MeshRef runeMeshRef;

        // Отдельные независимые текстуры
        private LoadedTexture animTex;
        private LoadedTexture recessiveTex;
        private LoadedTexture dominantTex;
        private LoadedTexture isolatedTex;
        private LoadedTexture dispersiveTex;

        private float orbitAngle = 0f;
        private bool isInitialized = false;

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        public Size2i AtlasSize => new Size2i(128, 128); // Фейковый размер, больше не влияет

        // Обманываем движок: говорим, что текстура всегда занимает всё пространство (от 0.0 до 1.0)
        public TextureAtlasPosition this[string textureCode]
        {
            get { return new TextureAtlasPosition { x1 = 0, y1 = 0, x2 = 1, y2 = 1, atlasTextureId = 0 }; }
        }

        public SparkRenderer(ICoreClientAPI capi, EntitySpark spark)
        {
            this.capi = capi;
            this.spark = spark;
        }

        private void InitializeGraphics()
        {
            // 1. Грузим спрайт-лист как НЕЗАВИСИМУЮ текстуру, минуя атлас
            animTex = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/entity/spark_anim.png"), ref animTex);

            Shape shape = capi.Assets.TryGet("botaniastory:shapes/entity/spark_model.json")?.ToObject<Shape>();
            if (shape != null)
            {
                capi.Tesselator.TesselateShape("spark", shape, out MeshData baseMesh, this);
                if (baseMesh != null)
                {
                    // 2. Нарезаем базовую модель на 7 кадров!
                    for (int i = 0; i < 7; i++)
                    {
                        MeshData frameMesh = baseMesh.Clone();

                        // Сдвигаем UV-координаты по вертикали (ось V) для текущего кадра
                        for (int j = 1; j < frameMesh.Uv.Length; j += 2)
                        {
                            float v = frameMesh.Uv[j]; // Изначально от 0.0 до 1.0
                            frameMesh.Uv[j] = (v / 7f) + (i / 7f); // Сжимаем до 1/7 и сдвигаем вниз
                        }
                        frameMeshes[i] = capi.Render.UploadMesh(frameMesh);
                    }

                    // 3. Создаем плоскую модель для рун
                    MeshData runeMesh = baseMesh.Clone();
                    runeMesh.VerticesCount = 4;
                    runeMesh.IndicesCount = 6;
                    runeMesh.xyz = new float[] { -0.5f, -0.5f, 0, 0.5f, -0.5f, 0, 0.5f, 0.5f, 0, -0.5f, 0.5f, 0 };
                    runeMesh.Uv = new float[] { 0, 1, 1, 1, 1, 0, 0, 0 };
                    runeMesh.Indices = new int[] { 0, 1, 2, 0, 2, 3 };

                    if (runeMesh.Rgba != null)
                    {
                        for (int i = 0; i < 16 && i < runeMesh.Rgba.Length; i++) runeMesh.Rgba[i] = 255;
                    }
                    runeMeshRef = capi.Render.UploadMesh(runeMesh);
                }
            }

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
            if (spark.State == EnumEntityState.Despawned) return;

            if (!isInitialized)
            {
                InitializeGraphics();
                isInitialized = true;
            }

            // Проверяем, что первый кадр и текстура загрузились
            if (frameMeshes[0] == null || animTex == null || animTex.TextureId == 0) return;

            // Таймер анимации (10 кадров в секунду)
            frameTimer += deltaTime;
            if (frameTimer >= 0.1f)
            {
                frameTimer = 0f;
                currentFrame = (currentFrame + 1) % 7;
            }

            orbitAngle += deltaTime * 1.5f;

            IStandardShaderProgram prog = capi.Render.PreparedStandardShader((int)spark.Pos.X, (int)spark.Pos.Y, (int)spark.Pos.Z);

            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            prog.ExtraGlow = 255;

            capi.Render.GlToggleBlend(true, EnumBlendMode.Standard);
            GL.DepthMask(false);

            // === ИДЕАЛЬНЫЙ БИЛЛБОРДИНГ ===
            // 1. Оставляем позицию относительно сущности (как и было)
            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            float dx = (float)(spark.Pos.X - camPos.X);
            float dy = (float)(spark.Pos.Y - camPos.Y);
            float dz = (float)(spark.Pos.Z - camPos.Z);

            // 2. Достаем матрицу камеры (View Matrix)
            float[] view = capi.Render.CameraMatrixOriginf;

            // 3. Создаем матрицу биллборда (Транспонируем 3x3 часть матрицы камеры).
            // Это скопирует поворот камеры 1 в 1, заставляя плоскость всегда смотреть в экран.
            float[] billboardMatrix = new float[]
            {
                view[0], view[4], view[8],  0,
                view[1], view[5], view[9],  0,
                view[2], view[6], view[10], 0,
                0,       0,       0,        1
            };

            // БИНДИМ  НЕЗАВИСИМУЮ ТЕКСТУРУ
            capi.Render.BindTexture2d(animTex.TextureId);

            Matrixf modelMat = new Matrixf();
            modelMat.Identity()
                    .Translate(dx, dy, dz)
                    .Mul(billboardMatrix) // <-- ПРИМЕНЯЕМ ПОВОРОТ КАМЕРЫ
                    .Scale(3f, 3f, 3f)
                    .Translate(-0.5f, -0.09375f, -0.5f);

            prog.ModelMatrix = modelMat.Values;
            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;

            // Рендерим ТОЛЬКО ТЕКУЩИЙ КАДР!
            capi.Render.RenderMesh(frameMeshes[currentFrame]);

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

                    float orbitRadius = 0.2f;
                    float orbitX = (float)Math.Cos(orbitAngle) * orbitRadius;
                    float orbitZ = (float)Math.Sin(orbitAngle) * orbitRadius;
                    float orbitY = (float)Math.Sin(orbitAngle * 2f) * 0.1f;

                    // Применяем ту же матрицу биллборда к Руне (дополнитель)
                    modelMat.Identity()
                            .Translate(dx + orbitX, dy + orbitY, dz + orbitZ)
                            .Mul(billboardMatrix) // <-- ПРИМЕНЯЕМ ПОВОРОТ КАМЕРЫ ДЛЯ РУНЫ
                            .Scale(0.4f, 0.4f, 0.4f);

                    prog.ModelMatrix = modelMat.Values;
                    capi.Render.RenderMesh(runeMeshRef);
                }
            }

            prog.ExtraGlow = 0;
            prog.Stop();
            GL.DepthMask(true);
            capi.Render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            // Очищаем все 7 моделей
            for (int i = 0; i < 7; i++)
            {
                frameMeshes[i]?.Dispose();
                frameMeshes[i] = null;
            }
            runeMeshRef?.Dispose();
            runeMeshRef = null;

            // Текстуры мы не диспоузим, так как они кэшируются движком.
        }
    }
}