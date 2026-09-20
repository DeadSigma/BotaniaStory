using System;
using System.Collections.Generic;
using BotaniaStory.systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.renderers
{
    public class ManaStreamRenderer : IRenderer
    {
        private const double TickLength = 1.0 / 20.0;
        private const double Spread = 0.45;
        private const double MotionScale = 0.04;
        private const double MotionDrag = 0.98;
        private const float ParticleAlpha = 0.375f;

        private readonly ICoreClientAPI capi;
        private readonly List<WispParticle> activeParticles = new List<WispParticle>();

        private MeshRef quadMeshRef;
        private LoadedTexture particleTexture;
        private double tickAccumulator;

        public Matrixf ModelMat = new Matrixf();

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        public ManaStreamRenderer(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "manastream");
            LoadTextureAndMesh();
        }

        private void LoadTextureAndMesh()
        {
            AssetLocation texLocation = new AssetLocation(
                "botaniastory",
                "textures/particle/mana_particle.png"
            );

            particleTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(texLocation, ref particleTexture);

            MeshData quad = QuadMeshUtil.GetCustomQuadModelData(
                -0.5f,
                -0.5f,
                0f,
                1f,
                1f
            );

            quad.Rgba = new byte[]
            {
                255, 255, 255, 255,
                255, 255, 255, 255,
                255, 255, 255, 255,
                255, 255, 255, 255
            };

            quad.Flags = new int[] { 0, 0, 0, 0 };
            quadMeshRef = capi.Render.UploadMesh(quad);
        }

        public void AddParticle(Vec3d start, Vec3d end)
        {
            AddParticle(start, end, 0xFFFFFF);
        }

        public void AddParticle(Vec3d start, Vec3d end, int networkColor)
        {
            Random rand = capi.World.Rand;

            Vec3d particleStart = new Vec3d(
                start.X + (rand.NextDouble() - 0.5) * Spread,
                start.Y + (rand.NextDouble() - 0.5) * Spread,
                start.Z + (rand.NextDouble() - 0.5) * Spread
            );

            Vec3d particleEnd = new Vec3d(
                end.X + (rand.NextDouble() - 0.5) * Spread,
                end.Y + (rand.NextDouble() - 0.5) * Spread,
                end.Z + (rand.NextDouble() - 0.5) * Spread
            );

            Vec3d motion = new Vec3d(
                (particleEnd.X - particleStart.X) * MotionScale,
                (particleEnd.Y - particleStart.Y) * MotionScale,
                (particleEnd.Z - particleStart.Z) * MotionScale
            );

            float r = ((networkColor >> 16) & 255) / 255f;
            float g = ((networkColor >> 8) & 255) / 255f;
            float b = (networkColor & 255) / 255f;

            if (rand.NextDouble() < 0.25)
            {
                r += 0.2f * (float)rand.NextDouble();
                g += 0.2f * (float)rand.NextDouble();
                b += 0.2f * (float)rand.NextDouble();
            }

            float size = 0.125f + 0.125f * (float)rand.NextDouble();

            float moteParticleScale =
                ((float)rand.NextDouble() * 0.5f + 0.5f)
                * 2f
                * size;

            int maxAge = (int)(
                28.0
                / (rand.NextDouble() * 0.3 + 0.7)
            );

            activeParticles.Add(
                new WispParticle(
                    particleStart,
                    motion,
                    new Vec4f(r, g, b, ParticleAlpha),
                    moteParticleScale,
                    maxAge
                )
            );
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (particleTexture == null ||
                particleTexture.Disposed ||
                particleTexture.TextureId == 0 ||
                quadMeshRef == null)
            {
                return;
            }

            UpdateParticles(deltaTime);

            if (activeParticles.Count == 0)
            {
                return;
            }

            IRenderAPI render = capi.Render;
            IClientPlayer player = capi.World.Player;
            Vec3d camPos = player.Entity.CameraPos;

            IStandardShaderProgram prog = render.PreparedStandardShader(
                (int)camPos.X,
                (int)camPos.Y,
                (int)camPos.Z
            );

            ShaderSanitizer.Sanitize(prog);

            capi.Render.BindTexture2d(particleTexture.TextureId);

            prog.Uniform("alphaTest", 0f);
            prog.Uniform("extraGlow", 0);
            prog.NormalShaded = 0;

            render.GlToggleBlend(true, EnumBlendMode.Glow);
            render.GLDepthMask(false);

            float partialTick = (float)(tickAccumulator / TickLength);

            foreach (WispParticle particle in activeParticles)
            {
                Vec3d pos = particle.GetRenderPosition(partialTick);
                float size = particle.GetRenderScale() * 2f;

                if (size == 0f)
                {
                    continue;
                }

                Vec4f color = particle.Color;

                prog.RgbaAmbientIn = new Vec3f(
                    color.X,
                    color.Y,
                    color.Z
                );

                prog.RgbaLightIn = color;
                prog.RgbaGlowIn = color;
                prog.RgbaTint = color;

                ModelMat.Identity();
                ModelMat.Translate(
                    pos.X - camPos.X,
                    pos.Y - camPos.Y,
                    pos.Z - camPos.Z
                );
                ModelMat.RotateY(player.CameraYaw);
                ModelMat.RotateX(player.CameraPitch);
                ModelMat.Scale(size, size, size);

                prog.ModelMatrix = ModelMat.Values;
                prog.ViewMatrix = render.CameraMatrixOriginf;
                prog.ProjectionMatrix = render.CurrentProjectionMatrix;

                render.RenderMesh(quadMeshRef);
            }

            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);

            // отсечение прозрачности и нормали возвращаются - standard-программа общая на весь движок
            prog.Uniform("alphaTest", 0.001f);
            prog.NormalShaded = 1;

            prog.Stop();

            render.GLDepthMask(true);

            // режим смешивания применяется только при включении, поэтому сбрасывается отдельно
            render.GlToggleBlend(true, EnumBlendMode.Standard);
            render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        private void UpdateParticles(float deltaTime)
        {
            tickAccumulator += deltaTime;

            while (tickAccumulator >= TickLength)
            {
                tickAccumulator -= TickLength;

                for (int i = activeParticles.Count - 1; i >= 0; i--)
                {
                    WispParticle particle = activeParticles[i];
                    particle.Tick();

                    if (particle.Dead)
                    {
                        activeParticles.RemoveAt(i);
                    }
                }
            }
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            quadMeshRef?.Dispose();
            quadMeshRef = null;

            particleTexture = null;
            activeParticles.Clear();
        }

        private sealed class WispParticle
        {
            public readonly Vec4f Color;

            private readonly Vec3d previousPosition;
            private readonly Vec3d position;
            private readonly Vec3d motion;

            private readonly float moteParticleScale;
            private readonly int maxAge;
            private readonly int halfLife;

            private int age;

            public bool Dead { get; private set; }

            public WispParticle(
                Vec3d start,
                Vec3d initialMotion,
                Vec4f color,
                float moteParticleScale,
                int maxAge
            )
            {
                previousPosition = new Vec3d(
                    start.X,
                    start.Y,
                    start.Z
                );

                position = new Vec3d(
                    start.X,
                    start.Y,
                    start.Z
                );

                motion = new Vec3d(
                    initialMotion.X,
                    initialMotion.Y,
                    initialMotion.Z
                );

                Color = color;
                this.moteParticleScale = moteParticleScale;
                this.maxAge = maxAge;
                halfLife = maxAge / 2;
            }

            public void Tick()
            {
                previousPosition.X = position.X;
                previousPosition.Y = position.Y;
                previousPosition.Z = position.Z;

                if (age++ >= maxAge)
                {
                    Dead = true;
                    return;
                }

                position.X += motion.X;
                position.Y += motion.Y;
                position.Z += motion.Z;

                motion.X *= MotionDrag;
                motion.Y *= MotionDrag;
                motion.Z *= MotionDrag;
            }

            public Vec3d GetRenderPosition(float partialTick)
            {
                return new Vec3d(
                    previousPosition.X +
                    (position.X - previousPosition.X) * partialTick,

                    previousPosition.Y +
                    (position.Y - previousPosition.Y) * partialTick,

                    previousPosition.Z +
                    (position.Z - previousPosition.Z) * partialTick
                );
            }

            public float GetRenderScale()
            {
                if (halfLife <= 0)
                {
                    return 0f;
                }

                float life = age / (float)halfLife;

                if (life > 1f)
                {
                    life = 2f - life;
                }

                return moteParticleScale * life * 0.5f;
            }
        }
    }
}