using System;
using BotaniaStory.systems;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using BotaniaStory.entities;

namespace BotaniaStory.client.renderers
{
    public class SparkRenderer : IRenderer, ITexPositionSource
    {
        private ICoreClientAPI capi;
        private EntitySpark spark;

        private MeshRef[] frameMeshes = new MeshRef[7];
        private int currentFrame = 0;
        private float frameTimer = 0f;

        private MeshRef runeMeshRef;

        private LoadedTexture animTex;
        private LoadedTexture recessiveTex;
        private LoadedTexture dominantTex;
        private LoadedTexture isolatedTex;
        private LoadedTexture dispersiveTex;

        private float orbitAngle = 0f;
        private bool isInitialized = false;

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        // размер ни на что не влияет - текстуры грузятся мимо атласа
        public Size2i AtlasSize => new Size2i(128, 128);

        // тесселятору отдаётся вся текстура целиком, от 0 до 1
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
            // спрайт-лист грузится отдельной текстурой, минуя атлас
            animTex = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/entity/spark_anim.png"), ref animTex);

            Shape shape = capi.Assets.TryGet("botaniastory:shapes/entity/spark_model.json")?.ToObject<Shape>();
            if (shape != null)
            {
                capi.Tesselator.TesselateShape("spark", shape, out MeshData baseMesh, this);
                if (baseMesh != null)
                {
                    // базовая модель режется на 7 кадров сдвигом uv по оси v
                    for (int i = 0; i < 7; i++)
                    {
                        MeshData frameMesh = baseMesh.Clone();

                        for (int j = 1; j < frameMesh.Uv.Length; j += 2)
                        {
                            float v = frameMesh.Uv[j];
                            frameMesh.Uv[j] = (v / 7f) + (i / 7f);
                        }
                        frameMeshes[i] = capi.Render.UploadMesh(frameMesh);
                    }
                }
            }

            // руна собирается с нуля - у клона остаются побочные массивы на все вершины искры
            MeshData runeMesh = new MeshData(4, 6, false, true, true, true);

            runeMesh.xyz = new float[] { -0.5f, -0.5f, 0, 0.5f, -0.5f, 0, 0.5f, 0.5f, 0, -0.5f, 0.5f, 0 };
            runeMesh.Uv = new float[] { 0, 1, 1, 1, 1, 0, 0, 0 };
            runeMesh.Rgba = new byte[16];
            for (int i = 0; i < 16; i++) runeMesh.Rgba[i] = 255;
            runeMesh.Flags = new int[4];
            runeMesh.Indices = new int[] { 0, 1, 2, 0, 2, 3 };
            runeMesh.VerticesCount = 4;
            runeMesh.IndicesCount = 6;

            runeMeshRef = capi.Render.UploadMesh(runeMesh);

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

            if (frameMeshes[0] == null || animTex == null || animTex.TextureId == 0) return;

            frameTimer += deltaTime;
            if (frameTimer >= 0.1f)
            {
                frameTimer = 0f;
                currentFrame = (currentFrame + 1) % 7;
            }

            orbitAngle += deltaTime * 1.5f;

            IStandardShaderProgram prog = capi.Render.PreparedStandardShader((int)spark.Pos.X, (int)spark.Pos.Y, (int)spark.Pos.Z);
            ShaderSanitizer.Sanitize(prog);

            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            prog.ExtraGlow = 255;

            // порог прозрачности снижается, иначе края искры срезаются
            prog.AlphaTest = 0.01f;

            capi.Render.GlToggleBlend(true, EnumBlendMode.Standard);
            capi.Render.GLDepthMask(false);
            capi.Render.GlDisableCullFace();

            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            float dx = (float)(spark.Pos.X - camPos.X);
            float dy = (float)(spark.Pos.Y - camPos.Y);
            float dz = (float)(spark.Pos.Z - camPos.Z);

            float[] view = capi.Render.CameraMatrixOriginf;
            float[] billboardMatrix = new float[]
            {
                view[0], view[4], view[8],  0,
                view[1], view[5], view[9],  0,
                view[2], view[6], view[10], 0,
                0,       0,       0,        1
            };

            capi.Render.BindTexture2d(animTex.TextureId);

            Matrixf modelMat = new Matrixf();
            modelMat.Identity()
                    .Translate(dx, dy, dz)
                    .Mul(billboardMatrix)
                    .Scale(3f, 3f, 3f)
                    .Translate(-0.5f, -0.09375f, -0.5f);

            prog.ModelMatrix = modelMat.Values;
            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;

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

                    modelMat.Identity()
                            .Translate(dx + orbitX, dy + orbitY, dz + orbitZ)
                            .Mul(billboardMatrix)
                            .Scale(0.4f, 0.4f, 0.4f);

                    prog.ModelMatrix = modelMat.Values;
                    capi.Render.RenderMesh(runeMeshRef);

                    // бинд возвращается на искру, чтобы не оставлять чужую текстуру
                    capi.Render.BindTexture2d(animTex.TextureId);
                }
            }

            // юниформы сбрасываются - standard-программа общая на весь движок
            prog.ExtraGlow = 0;
            prog.AlphaTest = 0.001f;
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.Stop();

            capi.Render.GlEnableCullFace();
            capi.Render.GLDepthMask(true);
            capi.Render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            for (int i = 0; i < 7; i++)
            {
                frameMeshes[i]?.Dispose();
                frameMeshes[i] = null;
            }
            runeMeshRef?.Dispose();
            runeMeshRef = null;

            // текстуры не диспоузятся, они кэшируются движком
        }
    }
}