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

        // Переменные для дополнителей
        private MeshRef runeMeshRef;
        private LoadedTexture recessiveTex;
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

            // 1. Грузим модель искры (Твой стабильный код)
            shape = capi.Assets.TryGet("botaniastory:shapes/entity/spark_model.json")?.ToObject<Shape>();
            if (shape != null)
            {
                capi.Tesselator.TesselateShape("spark", shape, out MeshData mesh, this);
                if (mesh != null) meshRef = capi.Render.UploadMesh(mesh);
            }

            // 2. Грузим меш и текстуры для руны-дополнителя
            MeshData runeMesh = QuadMeshUtil.GetCustomQuadModelData(-0.5f, -0.5f, 0, 1f, 1f);
            runeMesh.Rgba = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
            runeMesh.Flags = new int[] { 0, 0, 0, 0 };
            runeMeshRef = capi.Render.UploadMesh(runeMesh);

            recessiveTex = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/entity/spark_augment_recessive.png"), ref recessiveTex);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (spark.State == EnumEntityState.Despawned || meshRef == null) return;

            orbitAngle += deltaTime * 1.5f; // Скорость вращения руны по орбите

            IStandardShaderProgram prog = capi.Render.PreparedStandardShader((int)spark.Pos.X, (int)spark.Pos.Y, (int)spark.Pos.Z);

            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            prog.ExtraGlow = 0;
            prog.Uniform("alphaTest", 0f);

            capi.Render.GlToggleBlend(true, EnumBlendMode.Standard);
            GL.DepthMask(false);

            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            float dx = (float)(spark.Pos.X - camPos.X);
            float dy = (float)(spark.Pos.Y - camPos.Y);
            float dz = (float)(spark.Pos.Z - camPos.Z);

            float yaw = (float)Math.Atan2(dx, dz) + (float)Math.PI;
            float horizontalDist = (float)Math.Sqrt(dx * dx + dz * dz);
            float pitch = (float)Math.Atan2(dy, horizontalDist);

            // ==========================================
            // РЕНДЕР САМОЙ ИСКРЫ
            // ==========================================
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

            // ==========================================
            // РЕНДЕР РУНЫ-ДОПОЛНИТЕЛЯ
            // ==========================================
            string augmentType = spark.WatchedAttributes.GetString("augment", "none");

            if (augmentType != "none" && runeMeshRef != null)
            {
                if (augmentType == "recessive" && recessiveTex != null && recessiveTex.TextureId != 0)
                {
                    capi.Render.BindTexture2d(recessiveTex.TextureId);
                }

                // ВОТ ТУТ ВСЕ НАШИ ВИЗУАЛЬНЫЕ ПРАВКИ:
                capi.Render.GlToggleBlend(true, EnumBlendMode.Standard); // Standard вместо Glow
                prog.Uniform("alphaTest", 0.1f); // Убираем прозрачный фон
                prog.ExtraGlow = 255; // Заставляем руну светиться своим цветом в темноте

                GL.Disable(EnableCap.CullFace);

                // Уменьшили орбиту до 0.2
                float orbitRadius = 0.2f;
                float orbitX = (float)Math.Cos(orbitAngle) * orbitRadius;
                float orbitZ = (float)Math.Sin(orbitAngle) * orbitRadius;
                float orbitY = (float)Math.Sin(orbitAngle * 2f) * 0.1f;

                // Уменьшили размер руны до 0.4
                modelMat.Identity()
                        .Translate(dx + orbitX, dy + orbitY, dz + orbitZ)
                        .RotateY(yaw)
                        .RotateX(-pitch)
                        .Scale(0.4f, 0.4f, 0.4f);

                prog.ModelMatrix = modelMat.Values;
                capi.Render.RenderMesh(runeMeshRef);

                GL.Enable(EnableCap.CullFace);
                prog.Uniform("alphaTest", 0f); // Возвращаем как было
                prog.ExtraGlow = 0; // Возвращаем как было
            }

            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.Stop();

            GL.DepthMask(true);
            capi.Render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            // Возвращаем твой стабильный Dispose
            meshRef?.Dispose();
            meshRef = null;
            runeMeshRef?.Dispose();
            runeMeshRef = null;
        }
    }
}