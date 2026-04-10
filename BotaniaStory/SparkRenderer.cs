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

            shape = capi.Assets.TryGet("botaniastory:shapes/entity/spark_model.json")?.ToObject<Shape>();
            if (shape != null)
            {
                capi.Tesselator.TesselateShape("spark", shape, out MeshData mesh, this);
                if (mesh != null)
                {
                    meshRef = capi.Render.UploadMesh(mesh);
                }
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshRef == null) return;

            if (spark.State == EnumEntityState.Despawned || meshRef == null) return;

            IStandardShaderProgram prog = capi.Render.PreparedStandardShader((int)spark.Pos.X, (int)spark.Pos.Y, (int)spark.Pos.Z);

            // 1. Идеальные цвета 1:1 (отключаем влияние солнца и теней)
            prog.Uniform("rgbaAmbientIn", new Vec4f(1f, 1f, 1f, 1f));
            prog.Uniform("rgbaLightIn", new Vec4f(0f, 0f, 0f, 0f));
            prog.Uniform("rgbaGlowIn", new Vec4f(0f, 0f, 0f, 0f));
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            prog.ExtraGlow = 0;

            // 2. ВАЖНО: Отключаем Alpha-Test, чтобы игра не обрезала полупрозрачную ауру!
            prog.Uniform("alphaTest", 0f);

            capi.Render.BindTexture2d(capi.EntityTextureAtlas.AtlasTextures[0].TextureId);

            // --- МАГИЯ ПРОЗРАЧНОСТИ ИЗ ТВОИХ ЧАСТИЦ МАНЫ ---
            // Включаем стандартное смешивание (для точной передачи PNG с альфа-каналом)
            capi.Render.GlToggleBlend(true, EnumBlendMode.Standard);
            // Отключаем запись в буфер глубины, чтобы аура не перекрывала блоки сзади
            GL.DepthMask(false);

            // Расчет биллбординга (поворот к камере)
            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            float dx = (float)(spark.Pos.X - camPos.X);
            float dy = (float)(spark.Pos.Y - camPos.Y);
            float dz = (float)(spark.Pos.Z - camPos.Z);

            float yaw = (float)Math.Atan2(dx, dz) + (float)Math.PI;
            float horizontalDist = (float)Math.Sqrt(dx * dx + dz * dz);
            float pitch = (float)Math.Atan2(dy, horizontalDist);

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

            // Возвращаем кисточки в белый цвет (защита от багов)
            prog.Uniform("rgbaAmbientIn", new Vec4f(1f, 1f, 1f, 1f));
            prog.Stop();

            // --- ВОЗВРАЩАЕМ РЕНДЕР МИРА В НОРМУ ---
            GL.DepthMask(true);
            capi.Render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            meshRef?.Dispose();
            meshRef = null;
        }
    }
}