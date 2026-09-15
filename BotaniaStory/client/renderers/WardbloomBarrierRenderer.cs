using System;
using BotaniaStory.blockentity;
using BotaniaStory.systems;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.renderers
{
    public class WardbloomBarrierRenderer : IRenderer
    {
        private const int BaseDomePointCount = 360;
        private const int MaxDomePointCount = 12000;
        private const int BaseUndergroundPointCount = 140;
        private const int MaxUndergroundPointCount = 3200;
        private const float BaseAlpha = 0.42f;

        private readonly ICoreClientAPI capi;
        private readonly BEBehaviorWardbloom behavior;
        private readonly BarrierPoint[] domePoints;
        private readonly BarrierPoint[] undergroundPoints;
        private readonly float noisePhaseA;
        private readonly float noisePhaseB;

        private MeshRef quadMeshRef;
        private LoadedTexture particleTexture;
        private float time;

        private readonly Matrixf modelMat = new Matrixf();

        private struct BarrierPoint
        {
            public float Azimuth;
            public float Height;
            public float Phase;
            public float Drift;
            public float Size;
            public float RadialNoise;
        }

        public double RenderOrder => 0.58;
        public int RenderRange => 600;

        public WardbloomBarrierRenderer(ICoreClientAPI capi, BEBehaviorWardbloom behavior)
        {
            this.capi = capi;
            this.behavior = behavior;

            int seed = behavior.Pos.X * 73856093 ^ behavior.Pos.Y * 19349663 ^ behavior.Pos.Z * 83492791;
            var random = new Random(seed);

            noisePhaseA = (float)(random.NextDouble() * GameMath.TWOPI);
            noisePhaseB = (float)(random.NextDouble() * GameMath.TWOPI);
            domePoints = BuildDomePoints(random);
            undergroundPoints = BuildUndergroundPoints(random);

            LoadTextureAndMesh();
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "wardbloom-barrier-" + behavior.Pos);
        }

        private static BarrierPoint[] BuildDomePoints(Random random)
        {
            var result = new BarrierPoint[MaxDomePointCount];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new BarrierPoint
                {
                    Azimuth = (float)(random.NextDouble() * GameMath.TWOPI),
                    Height = 0.006f + (float)random.NextDouble() * 0.989f,
                    Phase = (float)(random.NextDouble() * GameMath.TWOPI),
                    Drift = ((float)random.NextDouble() * 0.22f + 0.035f) * (random.Next(2) == 0 ? -1f : 1f),
                    Size = 0.18f + (float)random.NextDouble() * 0.18f,
                    RadialNoise = (float)(random.NextDouble() * 2.0 - 1.0)
                };
            }

            return result;
        }

        private static BarrierPoint[] BuildUndergroundPoints(Random random)
        {
            var result = new BarrierPoint[MaxUndergroundPointCount];

            for (int i = 0; i < result.Length; i++)
            {
                float depth = (float)random.NextDouble();
                depth *= depth;

                result[i] = new BarrierPoint
                {
                    Azimuth = (float)(random.NextDouble() * GameMath.TWOPI),
                    Height = depth,
                    Phase = (float)(random.NextDouble() * GameMath.TWOPI),
                    Drift = ((float)random.NextDouble() * 0.13f + 0.025f) * (random.Next(2) == 0 ? -1f : 1f),
                    Size = 0.16f + (float)random.NextDouble() * 0.16f,
                    RadialNoise = (float)(random.NextDouble() * 2.0 - 1.0)
                };
            }

            return result;
        }

        private void LoadTextureAndMesh()
        {
            AssetLocation textureLocation = new AssetLocation("botaniastory", "textures/particle/mana_particle.png");
            particleTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(textureLocation, ref particleTexture);

            MeshData quad = QuadMeshUtil.GetCustomQuadModelData(-0.5f, -0.5f, 0, 1f, 1f);
            quad.Rgba = new byte[]
            {
                255, 255, 255, 255,
                255, 255, 255, 255,
                255, 255, 255, 255,
                255, 255, 255, 255
            };
            quad.Flags = new[] { 0, 0, 0, 0 };
            quadMeshRef = capi.Render.UploadMesh(quad);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!behavior.Active) return;

            IClientPlayer player = capi.World.Player;
            if (player?.Entity == null) return;

            // Физический барьер работает независимо от видимости частиц
            behavior.ApplyBarrierToEntity(player.Entity);

            Vec3d center = behavior.GetBarrierCenter();
            Vec3d camPos = player.Entity.CameraPos;

            float radius = behavior.BarrierRadius;

            double dx = camPos.X - center.X;
            double dz = camPos.Z - center.Z;

            double maxHorizontalDistance = radius + 160.0;

            if (dx * dx + dz * dz >
                maxHorizontalDistance * maxHorizontalDistance)
            {
                return;
            }

            // Под землёй частицы рисуются только возле верхней части стены
            if (camPos.Y <
                center.Y - BEBehaviorWardbloom.UndergroundRenderDepth - 24.0)
            {
                return;
            }

            if (camPos.Y >
                center.Y + radius + 160.0)
            {
                return;
            }

            float densityMultiplier = behavior.GetParticleDensityMultiplier();
            if (densityMultiplier <= 0f) return;

            if (particleTexture == null ||
                particleTexture.Disposed ||
                particleTexture.TextureId == 0)
            {
                return;
            }

            if (quadMeshRef == null) return;

            time += deltaTime;

            float radiusRatio =
                radius / BEBehaviorWardbloom.MinBarrierRadius;

            int fullDomeCount = GameMath.Clamp(
                (int)Math.Round(BaseDomePointCount * radiusRatio),
                BaseDomePointCount,
                MaxDomePointCount);

            int fullUndergroundCount = GameMath.Clamp(
                (int)Math.Round(BaseUndergroundPointCount * radiusRatio),
                BaseUndergroundPointCount,
                MaxUndergroundPointCount);

            int domePointCount = GameMath.Clamp(
                (int)Math.Round(fullDomeCount * densityMultiplier),
                1,
                MaxDomePointCount);

            int undergroundPointCount = GameMath.Clamp(
                (int)Math.Round(fullUndergroundCount * densityMultiplier),
                1,
                MaxUndergroundPointCount);

            float radiusScale = (float)Math.Sqrt(
                radius / BEBehaviorWardbloom.MinBarrierRadius
            );

            float particleScale =
                1f + (radiusScale - 1f) * 0.24f;

            float roughness =
                0.38f + Math.Min(1.25f, radius * 0.0035f);

            GetPalette(
                behavior.BarrierColor,
                out Vec3f hotColor,
                out Vec3f coolColor
            );

            IRenderAPI render = capi.Render;

            IStandardShaderProgram prog = render.PreparedStandardShader(
                (int)camPos.X,
                (int)camPos.Y,
                (int)camPos.Z);

            ShaderSanitizer.Sanitize(prog);
            capi.Render.BindTexture2d(particleTexture.TextureId);

            prog.Uniform("alphaTest", 0.03f);
            prog.Uniform("extraGlow", 0);
            prog.NormalShaded = 0;

            render.GlToggleBlend(true, EnumBlendMode.Glow);
            GL.DepthMask(false);

            for (int i = 0; i < domePointCount; i++)
            {
                DrawDomePoint(
                    render,
                    prog,
                    player,
                    camPos,
                    center,
                    domePoints[i],
                    radius,
                    particleScale,
                    roughness,
                    hotColor,
                    coolColor);
            }

            for (int i = 0; i < undergroundPointCount; i++)
            {
                DrawUndergroundPoint(
                    render,
                    prog,
                    player,
                    camPos,
                    center,
                    undergroundPoints[i],
                    radius,
                    particleScale,
                    roughness,
                    hotColor,
                    coolColor);
            }

            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            prog.Stop();

            GL.DepthMask(true);
            render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        private void DrawDomePoint(
            IRenderAPI render,
            IStandardShaderProgram prog,
            IClientPlayer player,
            Vec3d camPos,
            Vec3d center,
            BarrierPoint point,
            float radius,
            float particleScale,
            float roughness,
            Vec3f hotColor,
            Vec3f coolColor)
        {
            float height = point.Height + GameMath.Sin(time * 0.75f + point.Phase) * 0.012f;
            height = Math.Max(0.002f, Math.Min(0.997f, height));

            float azimuth = point.Azimuth + time * point.Drift;
            float contour = GetContourNoise(azimuth, height, point.RadialNoise, roughness);
            float localRadius = Math.Max(1f, radius + contour);
            float horizontal = (float)Math.Sqrt(Math.Max(0f, 1f - height * height));
            float pulse = 0.5f + 0.5f * GameMath.Sin(time * 2.4f + point.Phase);

            Vec3d pos = new Vec3d(
                center.X + GameMath.Cos(azimuth) * localRadius * horizontal,
                center.Y + localRadius * height,
                center.Z + GameMath.Sin(azimuth) * localRadius * horizontal);

            float mix = height * 0.75f + pulse * 0.25f;
            float alpha = BaseAlpha * (0.5f + pulse * 0.5f);
            float size = point.Size * particleScale * (0.82f + pulse * 0.34f);

            SetParticleColor(prog, mix, alpha, hotColor, coolColor);
            DrawQuad(render, prog, player, camPos, pos, size);
        }

        private void DrawUndergroundPoint(
            IRenderAPI render,
            IStandardShaderProgram prog,
            IClientPlayer player,
            Vec3d camPos,
            Vec3d center,
            BarrierPoint point,
            float radius,
            float particleScale,
            float roughness,
            Vec3f hotColor,
            Vec3f coolColor)
        {
            float azimuth = point.Azimuth + time * point.Drift;
            float depth01 = point.Height;
            float contour = GetContourNoise(azimuth, -depth01, point.RadialNoise, roughness * 1.2f);
            float localRadius = Math.Max(1f, radius + contour);

            float depth = depth01 * BEBehaviorWardbloom.UndergroundRenderDepth;
            float verticalJitter = GameMath.Sin(point.Phase + azimuth * 3.7f) * 0.28f;
            float pulse = 0.5f + 0.5f * GameMath.Sin(time * 1.9f + point.Phase);

            Vec3d pos = new Vec3d(
                center.X + GameMath.Cos(azimuth) * localRadius,
                center.Y - depth + verticalJitter,
                center.Z + GameMath.Sin(azimuth) * localRadius);

            float depthFade = 1f - depth01;
            depthFade *= depthFade;
            float alpha = BaseAlpha * (0.16f + depthFade * 0.58f) * (0.7f + pulse * 0.3f);
            float size = point.Size * particleScale * (0.75f + pulse * 0.3f);
            float mix = 0.3f + depth01 * 0.45f + pulse * 0.15f;

            SetParticleColor(prog, mix, alpha, hotColor, coolColor);
            DrawQuad(render, prog, player, camPos, pos, size);
        }

        private float GetContourNoise(float azimuth, float height, float pointNoise, float amplitude)
        {
            float broad = GameMath.Sin(azimuth * 4.7f + height * 2.1f + noisePhaseA);
            float sharp = GameMath.Sin(azimuth * 11.3f - height * 5.6f + noisePhaseB);
            float flicker = GameMath.Sin(time * 0.65f + azimuth * 2.2f + height * 4.1f) * 0.12f;

            return amplitude * (broad * 0.55f + sharp * 0.28f + pointNoise * 0.22f + flicker);
        }

        private static void GetPalette(string colorCode, out Vec3f hotColor, out Vec3f coolColor)
        {
            Vec3f baseColor;

            switch (colorCode)
            {
                case "white": baseColor = new Vec3f(1f, 1f, 1f); break;
                case "orange": baseColor = new Vec3f(1f, 0.43f, 0.08f); break;
                case "magenta": baseColor = new Vec3f(1f, 0.16f, 0.78f); break;
                case "lightblue": baseColor = new Vec3f(0.32f, 0.72f, 1f); break;
                case "yellow": baseColor = new Vec3f(1f, 0.9f, 0.12f); break;
                case "lime": baseColor = new Vec3f(0.52f, 1f, 0.16f); break;
                case "gray": baseColor = new Vec3f(0.42f, 0.44f, 0.48f); break;
                case "lightgray": baseColor = new Vec3f(0.72f, 0.76f, 0.8f); break;
                case "cyan": baseColor = new Vec3f(0.1f, 0.86f, 0.9f); break;
                case "purple": baseColor = new Vec3f(0.58f, 0.18f, 1f); break;
                case "blue": baseColor = new Vec3f(0.16f, 0.34f, 1f); break;
                case "brown": baseColor = new Vec3f(0.5f, 0.27f, 0.09f); break;
                case "green": baseColor = new Vec3f(0.12f, 0.7f, 0.24f); break;
                case "red": baseColor = new Vec3f(1f, 0.1f, 0.12f); break;
                case "black": baseColor = new Vec3f(0.2f, 0.08f, 0.24f); break;
                case "pink":
                default:
                    baseColor = new Vec3f(1f, 0.45f, 0.72f);
                    break;
            }

            hotColor = new Vec3f(
                GameMath.Clamp(baseColor.X * 1.08f + 0.06f, 0f, 1f),
                GameMath.Clamp(baseColor.Y * 1.08f + 0.06f, 0f, 1f),
                GameMath.Clamp(baseColor.Z * 1.08f + 0.06f, 0f, 1f));

            coolColor = new Vec3f(
                GameMath.Clamp(baseColor.X * 0.5f + 0.025f, 0f, 1f),
                GameMath.Clamp(baseColor.Y * 0.5f + 0.025f, 0f, 1f),
                GameMath.Clamp(baseColor.Z * 0.5f + 0.025f, 0f, 1f));
        }

        private static void SetParticleColor(
            IStandardShaderProgram prog,
            float mix,
            float alpha,
            Vec3f hotColor,
            Vec3f coolColor)
        {
            mix = GameMath.Clamp(mix, 0f, 1f);

            Vec3f rgb = new Vec3f(
                hotColor.X + (coolColor.X - hotColor.X) * mix,
                hotColor.Y + (coolColor.Y - hotColor.Y) * mix,
                hotColor.Z + (coolColor.Z - hotColor.Z) * mix);

            Vec4f color = new Vec4f(rgb.X, rgb.Y, rgb.Z, alpha);
            prog.RgbaAmbientIn = rgb;
            prog.RgbaLightIn = color;
            prog.RgbaGlowIn = color;
            prog.RgbaTint = color;
        }

        private void DrawQuad(
            IRenderAPI render,
            IStandardShaderProgram prog,
            IClientPlayer player,
            Vec3d camPos,
            Vec3d pos,
            float size)
        {
            modelMat.Identity();
            modelMat.Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);
            modelMat.RotateY(player.CameraYaw);
            modelMat.RotateX(player.CameraPitch);
            modelMat.Scale(size, size, size);

            prog.ModelMatrix = modelMat.Values;
            prog.ViewMatrix = render.CameraMatrixOriginf;
            prog.ProjectionMatrix = render.CurrentProjectionMatrix;

            render.RenderMesh(quadMeshRef);
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            quadMeshRef?.Dispose();
            quadMeshRef = null;
            particleTexture = null;
        }
    }
}
