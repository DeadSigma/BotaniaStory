using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using System;
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

        /// <summary>
        /// Физика - один раз за кадр. Раньше она была внутри цикла по 5 текстурам, то есть все частицы старели и разгонялись в 5 раз быстрее нужного.
        /// </summary>
        private void UpdateParticles(float deltaTime)
        {
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                var p = Particles[i];

                p.Life -= deltaTime;
                if (p.Life <= 0)
                {
                    Particles.RemoveAt(i);
                    continue;
                }

                p.Position.X += p.Velocity.X * deltaTime;
                p.Position.Y += p.Velocity.Y * deltaTime;
                p.Position.Z += p.Velocity.Z * deltaTime;

                // Искры (бенгальский огонь) быстро тормозят, магия (виспы) - почти нет
                double drag = p.TextureIndex != 4 ? 1.5 : 0.1;
                double factor = 1.0 - drag * deltaTime;
                if (factor < 0) factor = 0;

                p.Velocity.X *= factor;
                p.Velocity.Y *= factor;
                p.Velocity.Z *= factor;
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

                    if (!textureBound)
                    {
                        capi.Render.BindTexture2d(tex.TextureId);
                        textureBound = true;
                    }

                    float curve = (float)Math.Sin(p.LifeRatio * Math.PI);
                    float alpha = p.Color.A * curve;
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
                        currentSize = p.Size * curve;
                    }
                    else
                    {
                        float age = 1.0f - p.LifeRatio;
                        float growFactor = Math.Min(1.0f, age * 5.0f);
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