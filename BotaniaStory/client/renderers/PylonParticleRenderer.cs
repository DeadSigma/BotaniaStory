using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using BotaniaStory.client.particles;

namespace BotaniaStory.client.renderers
{
    public class PylonParticleRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private MeshRef quadMeshRef;
        private LoadedTexture[] textures = new LoadedTexture[5];
        private Matrixf ModelMat = new Matrixf();

        private static readonly float GlowContribution = 0f;
        public List<PylonParticle> Particles = new List<PylonParticle>();

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        public PylonParticleRenderer(ICoreClientAPI api)
        {
            this.capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "pylonparticles");
            LoadGraphics();
        }

        private void LoadGraphics()
        {
            for (int i = 0; i < 4; i++)
            {
                textures[i] = new LoadedTexture(capi);
                capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", $"textures/particle/pylon_particle_{i}.png"), ref textures[i]);
            }

            textures[4] = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/particle/mana_particle.png"), ref textures[4]);

            MeshData quad = QuadMeshUtil.GetCustomQuadModelData(-0.5f, -0.5f, 0, 1f, 1f);
            quad.Rgba = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
            quadMeshRef = capi.Render.UploadMesh(quad);
        }

        // Поворот вектора вокруг оси (Родригес). Мутирует v на месте - гнёт траекторию.
        private static void RotateAround(Vec3d v, Vec3d axis, double angle)
        {
            double c = Math.Cos(angle);
            double s = Math.Sin(angle);
            double dot = axis.X * v.X + axis.Y * v.Y + axis.Z * v.Z;

            double cx = axis.Y * v.Z - axis.Z * v.Y;
            double cy = axis.Z * v.X - axis.X * v.Z;
            double cz = axis.X * v.Y - axis.Y * v.X;

            v.X = v.X * c + cx * s + axis.X * dot * (1 - c);
            v.Y = v.Y * c + cy * s + axis.Y * dot * (1 - c);
            v.Z = v.Z * c + cz * s + axis.Z * dot * (1 - c);
        }

        private void UpdateParticles(float dt)
        {
            if (dt > 0.1f) dt = 0.1f;   // защита от лагового скачка

            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                var p = Particles[i];

                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    Particles.RemoveAt(i);
                    continue;
                }
                p.Age += dt;

                if (p.SwirlStrength != 0f)
                {
                    double k = 0.35 + 0.65 * Math.Sin(p.Age * p.WobbleFreq + p.WobblePhase);
                    RotateAround(p.Velocity, p.SwirlAxis, p.SwirlStrength * k * dt);
                }

                if (p.Gravity != 0f) p.Velocity.Y -= p.Gravity * dt;

                if (p.Drag > 0f)
                {
                    double f = Math.Exp(-p.Drag * dt);   // корректно при любом dt
                    p.Velocity.X *= f;
                    p.Velocity.Y *= f;
                    p.Velocity.Z *= f;
                }

                p.Position.X += p.Velocity.X * dt;
                p.Position.Y += p.Velocity.Y * dt;
                p.Position.Z += p.Velocity.Z * dt;
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Opaque) return;

            UpdateParticles(deltaTime);
            if (Particles.Count == 0) return;

            IClientPlayer player = capi.World.Player;
            if (player?.Entity == null) return;

            Vec3d camPos = player.Entity.CameraPos;
            IStandardShaderProgram prog = capi.Render.PreparedStandardShader((int)camPos.X, (int)camPos.Y, (int)camPos.Z);

            prog.AlphaTest = 0.05f;
            prog.NormalShaded = 0;
            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;

            capi.Render.GlToggleBlend(true, EnumBlendMode.Glow);
            GL.DepthMask(false);

            Vec4f glowOut = new Vec4f(0f, 0f, 0f, 0f);

            for (int texIndex = 0; texIndex < 5; texIndex++)
            {
                LoadedTexture tex = textures[texIndex];
                if (tex == null || tex.TextureId == 0) continue;

                bool textureBound = false;

                for (int i = 0; i < Particles.Count; i++)
                {
                    var p = Particles[i];
                    if (p.TextureIndex != texIndex) continue;

                    float fade = p.Fade;
                    if (fade <= 0.001f) continue;

                    if (!textureBound)
                    {
                        capi.Render.BindTexture2d(tex.TextureId);
                        textureBound = true;
                    }

                    float alpha = p.Color.A * fade;
                    Vec4f renderColor = new Vec4f(p.Color.X, p.Color.Y, p.Color.Z, alpha);

                    prog.RgbaAmbientIn = new Vec3f(renderColor.X, renderColor.Y, renderColor.Z);
                    prog.RgbaLightIn = renderColor;
                    prog.RgbaTint = renderColor;

                    if (GlowContribution > 0f)
                    {
                        glowOut.Set(renderColor.X, renderColor.Y, renderColor.Z, alpha * GlowContribution);
                    }
                    prog.RgbaGlowIn = glowOut;

                    ModelMat.Identity();
                    ModelMat.Translate(p.Position.X - camPos.X, p.Position.Y - camPos.Y, p.Position.Z - camPos.Z);
                    ModelMat.RotateY(player.CameraYaw);
                    ModelMat.RotateX(player.CameraPitch);

                    float currentSize;
                    if (p.ShrinkOnDeath)
                    {
                        currentSize = p.Size * (0.25f + 0.75f * fade);   // полный размер, ужимается к концу
                    }
                    else
                    {
                        float growFactor = Math.Min(1f, p.Progress * 5f); // мягкое проявление
                        currentSize = p.Size * growFactor;
                    }

                    ModelMat.Scale(currentSize, currentSize, currentSize);
                    prog.ModelMatrix = ModelMat.Values;

                    capi.Render.RenderMesh(quadMeshRef);
                }
            }

            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            prog.ExtraGlow = 0;
            prog.NormalShaded = 1;
            prog.AlphaTest = 0.05f;
            prog.Stop();

            GL.DepthMask(true);
            capi.Render.GlToggleBlend(true, EnumBlendMode.Standard);
            capi.Render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            quadMeshRef?.Dispose();
            foreach (var t in textures) t?.Dispose();
            Particles.Clear();
        }
    }
}