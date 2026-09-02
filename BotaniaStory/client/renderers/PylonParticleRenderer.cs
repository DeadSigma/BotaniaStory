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
        private readonly List<PylonParticle> pendingImpacts = new List<PylonParticle>();
        public double RenderOrder => 0.9;
        public int RenderRange => 64;
        private bool graphicsLoaded = false;

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

                if (p.Target != null && HandleArrival(p))
                {
                    Particles.RemoveAt(i);
                }
            }

            if (pendingImpacts.Count > 0)
            {
                Particles.AddRange(pendingImpacts);
                pendingImpacts.Clear();
            }
        }

        // true - частица дошла до цели и должна исчезнуть
        private bool HandleArrival(PylonParticle p)
        {
            if (p.TargetDir == null)
            {
                Vec3d d = new Vec3d(
                    p.Target.X - p.Position.X,
                    p.Target.Y - p.Position.Y,
                    p.Target.Z - p.Position.Z
                );

                double len = d.Length();
                if (len < 1e-6) return true;

                d.X /= len;
                d.Y /= len;
                d.Z /= len;
                p.TargetDir = d;
            }

            double ox = p.Position.X - p.Target.X;
            double oy = p.Position.Y - p.Target.Y;
            double oz = p.Position.Z - p.Target.Z;

            // проекция на ось полета: отрицательная пока летим, ноль в центре цели
            double along = ox * p.TargetDir.X + oy * p.TargetDir.Y + oz * p.TargetDir.Z;

            // до поверхности еще не долетели
            if (along < -p.TargetRadius) return false;

            // прижимаем ровно к поверхности, даже если за кадр перепрыгнули цель насквозь
            double back = along + p.TargetRadius;
            p.Position.X -= p.TargetDir.X * back;
            p.Position.Y -= p.TargetDir.Y * back;
            p.Position.Z -= p.TargetDir.Z * back;

            if (!p.ImpactOnArrive) return true;

            // боковое смещение: попали в ядро или прошли мимо него
            double lx = ox - along * p.TargetDir.X;
            double ly = oy - along * p.TargetDir.Y;
            double lz = oz - along * p.TargetDir.Z;

            double hitRadius = p.TargetRadius * 1.5;
            if (lx * lx + ly * ly + lz * lz <= hitRadius * hitRadius)
            {
                QueueImpact(p);
            }

            return true;
        }

        private void QueueImpact(PylonParticle src)
        {
            int count = capi.World.Rand.Next(2, 5);
            for (int i = 0; i < count; i++)
            {
                float life = 0.25f + (float)capi.World.Rand.NextDouble() * 0.3f;

                pendingImpacts.Add(new PylonParticle()
                {
                    Position = src.Position.Clone(),
                    Velocity = new Vec3d(
                        (capi.World.Rand.NextDouble() - 0.5) * 2.0,
                        (capi.World.Rand.NextDouble() - 0.5) * 2.0,
                        (capi.World.Rand.NextDouble() - 0.5) * 2.0
                    ),
                    Color = new Vec4f(src.Color.X, src.Color.Y, src.Color.Z, src.Color.A),
                    Size = src.Size * 0.7f,
                    Life = life,
                    MaxLife = life,
                    TextureIndex = src.TextureIndex,
                    ShrinkOnDeath = true,
                    Drag = 5f,
                    FadeIn = 0f,
                    FadeStart = 0.35f
                });
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Opaque) return;

            UpdateParticles(deltaTime);
            if (Particles.Count == 0) return;

            IClientPlayer player = capi.World.Player;
            if (player?.Entity == null) return;

            if (stage != EnumRenderStage.Opaque) return;

            if (!graphicsLoaded)
            {
                graphicsLoaded = true;
                LoadGraphics();
            }

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
            quadMeshRef = null;

            for (int i = 0; i < textures.Length; i++) textures[i] = null;

            Particles.Clear();
        }
    }
}