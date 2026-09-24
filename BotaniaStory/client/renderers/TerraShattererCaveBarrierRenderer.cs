using BotaniaStory.systems;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.renderers
{
    public class TerraShattererCaveBarrierRenderer : IRenderer
    {
        private const int MaxParticles = 1800;
        private const float SpawnPerCellPerSec = 42f;
        private const float MaxSpawnPerSec = 900f;
        private const float BaseAlpha = 0.58f;
        private const float SizeMin = 0.22f;
        private const float SizeMax = 0.48f;
        private const float VisibleDistance = 32f;

        private static readonly Vec3f HotColor = new Vec3f(1.0f, 0.5f, 0.8f);
        private static readonly Vec3f CoolColor = new Vec3f(0.75f, 0.0f, 0.35f);

        private readonly ICoreClientAPI capi;
        private readonly List<BarrierParticle> particles = new List<BarrierParticle>();
        private readonly List<BlockPos> visibleCells = new List<BlockPos>();
        private readonly Matrixf modelMat = new Matrixf();

        private MeshRef quadMeshRef;
        private LoadedTexture particleTexture;
        private float spawnAccum;
        private float fxTime;
        private float scanAccum;

        private class BarrierParticle
        {
            public readonly Vec3d Pos = new Vec3d();
            public float Age;
            public float MaxAge;
            public float Size;
            public float Phase;
            public float DriftX;
            public float DriftZ;
        }

        public TerraShattererCaveBarrierRenderer(ICoreClientAPI api)
        {
            capi = api;
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "terrashatterercavebarrier");
            LoadTextureAndMesh();
        }

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        private void LoadTextureAndMesh()
        {
            AssetLocation texLocation = new AssetLocation("botaniastory", "textures/particle/mana_particle.png");
            particleTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(texLocation, ref particleTexture);

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
            if (particleTexture == null || particleTexture.Disposed || particleTexture.TextureId == 0) return;
            if (quadMeshRef == null) return;

            IClientPlayer player = capi.World.Player;
            EntityPlayer entity = player?.Entity;
            if (entity == null) return;

            fxTime += deltaTime;
            scanAccum += deltaTime;

            if (scanAccum >= 0.2f)
            {
                scanAccum = 0f;
                RefreshVisibleCells(entity);
            }

            SpawnParticles(deltaTime);
            UpdateParticles(deltaTime);

            if (particles.Count == 0) return;
            DrawParticles(player);
        }

        // Рисуются только установленные ячейки барьера
        private void RefreshVisibleCells(EntityPlayer player)
        {
            visibleCells.Clear();

            double maxDistSq = VisibleDistance * VisibleDistance;
            List<BlockPos> barriers = TerraShattererCaveBarrierSystem.GetClientBarrierSnapshot();

            foreach (BlockPos pos in barriers)
            {
                if (pos.dimension != player.Pos.Dimension) continue;

                double dx = pos.X + 0.5 - player.Pos.X;
                double dy = pos.Y + 0.5 - player.Pos.Y;
                double dz = pos.Z + 0.5 - player.Pos.Z;
                if (dx * dx + dy * dy + dz * dz > maxDistSq) continue;

                if (!TerraShattererCaveBarrierSystem.IsBarrierValid(capi.World, pos.X, pos.Y, pos.Z, pos.dimension)) continue;

                visibleCells.Add(pos);
            }
        }

        private void SpawnParticles(float deltaTime)
        {
            if (visibleCells.Count == 0)
            {
                spawnAccum = 0f;
                return;
            }

            float spawnRate = Math.Min(MaxSpawnPerSec, visibleCells.Count * SpawnPerCellPerSec);
            spawnAccum += deltaTime * spawnRate;
            Random rnd = capi.World.Rand;

            while (spawnAccum >= 1f && particles.Count < MaxParticles)
            {
                spawnAccum -= 1f;

                BlockPos cell = visibleCells[rnd.Next(visibleCells.Count)];
                BarrierParticle particle = new BarrierParticle
                {
                    Age = 0f,
                    MaxAge = 0.7f + (float)rnd.NextDouble() * 0.65f,
                    Size = SizeMin + (float)rnd.NextDouble() * (SizeMax - SizeMin),
                    Phase = (float)(rnd.NextDouble() * GameMath.TWOPI),
                    DriftX = (float)(rnd.NextDouble() - 0.5) * 0.08f,
                    DriftZ = (float)(rnd.NextDouble() - 0.5) * 0.08f
                };

                particle.Pos.Set(
                    cell.X + 0.08 + rnd.NextDouble() * 0.84,
                    cell.Y + 0.94 + rnd.NextDouble() * 0.04,
                    cell.Z + 0.08 + rnd.NextDouble() * 0.84
                );

                particles.Add(particle);
            }
        }

        private void UpdateParticles(float deltaTime)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                BarrierParticle particle = particles[i];
                particle.Age += deltaTime;

                if (particle.Age >= particle.MaxAge)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                particle.Pos.X += particle.DriftX * deltaTime;
                particle.Pos.Z += particle.DriftZ * deltaTime;
            }
        }

        private void DrawParticles(IClientPlayer player)
        {
            IRenderAPI render = capi.Render;
            Vec3d camPos = player.Entity.CameraPos;
            IStandardShaderProgram prog = render.PreparedStandardShader((int)camPos.X, (int)camPos.Y, (int)camPos.Z);
            ShaderSanitizer.Sanitize(prog);

            capi.Render.BindTexture2d(particleTexture.TextureId);

            prog.Uniform("alphaTest", 0.05f);
            prog.Uniform("extraGlow", 0);
            prog.NormalShaded = 0;

            render.GlToggleBlend(true, EnumBlendMode.Glow);
            GL.DepthMask(false);

            foreach (BarrierParticle particle in particles)
            {
                float f = particle.Age / particle.MaxAge;
                float pulse = 0.9f + GameMath.Sin(particle.Phase + fxTime * 4f) * 0.1f;
                float size = particle.Size * pulse * (1f - f * 0.35f);
                float alpha = FadeAlpha(f);

                Vec3f rgb = new Vec3f(
                    HotColor.X + (CoolColor.X - HotColor.X) * f,
                    HotColor.Y + (CoolColor.Y - HotColor.Y) * f,
                    HotColor.Z + (CoolColor.Z - HotColor.Z) * f
                );

                Vec4f col = new Vec4f(rgb.X, rgb.Y, rgb.Z, alpha);
                prog.RgbaAmbientIn = rgb;
                prog.RgbaLightIn = col;
                prog.RgbaGlowIn = col;
                prog.RgbaTint = col;

                DrawQuad(render, prog, player, camPos, particle.Pos, size);
            }

            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);

            prog.Stop();
            GL.DepthMask(true);
            render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        private void DrawQuad(IRenderAPI render, IStandardShaderProgram prog, IClientPlayer player, Vec3d camPos, Vec3d pos, float size)
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

        private static float FadeAlpha(float f)
        {
            const float fadeIn = 0.14f;
            const float fadeOut = 0.38f;

            float alpha;
            if (f < fadeIn) alpha = f / fadeIn;
            else if (f > 1f - fadeOut) alpha = (1f - f) / fadeOut;
            else alpha = 1f;

            return alpha * BaseAlpha;
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            quadMeshRef?.Dispose();
            quadMeshRef = null;
            particleTexture = null;
            particles.Clear();
            visibleCells.Clear();
        }
    }
}
