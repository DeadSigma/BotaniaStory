using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class TiaraParticleRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private MeshRef quadMeshRef;
        private LoadedTexture[] textures = new LoadedTexture[4];
        private Matrixf ModelMat = new Matrixf();
        private Random rand = new Random();

        public List<PylonParticle> Particles = new List<PylonParticle>();

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        public TiaraParticleRenderer(ICoreClientAPI api)
        {
            this.capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "tiaraparticles");
            LoadGraphics();
        }

        private void LoadGraphics()
        {
            for (int i = 0; i < 4; i++)
            {
                textures[i] = new LoadedTexture(capi);
                capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", $"textures/particle/pylon_particle_{i}.png"), ref textures[i]);
            }

            MeshData quad = QuadMeshUtil.GetCustomQuadModelData(-0.5f, -0.5f, 0, 1f, 1f);
            quad.Rgba = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
            quadMeshRef = capi.Render.UploadMesh(quad);
        }

        // МЕТОД ДЛЯ ГЕНЕРАЦИИ ШЛЕЙФА
        public void SpawnFlightParticles(IClientPlayer player, bool isDashing)
        {
            int count = isDashing ? 3 : 1;
            Vec3d pos = player.Entity.Pos.XYZ.Add(0, 0.9, 0); // Уровень пояса/спины

            for (int i = 0; i < count; i++)
            {
                var p = new PylonParticle();

                // Небольшой разброс вокруг центра
                p.Position = pos.Clone().Add((rand.NextDouble() - 0.5) * 0.5, (rand.NextDouble() - 0.5) * 0.5, (rand.NextDouble() - 0.5) * 0.5);

                // Откидываем частицы слегка вниз и в случайные стороны
                p.Velocity = new Vec3d(
                    (rand.NextDouble() - 0.5) * 0.5,
                    -0.3 - rand.NextDouble() * 0.5,
                    (rand.NextDouble() - 0.5) * 0.5
                );

                p.TextureIndex = rand.Next(0, 4);
                p.Life = 4.3f + (float)rand.NextDouble() * 4.3f;
                p.MaxLife = p.Life; 

                p.Size = 0.13f + (float)rand.NextDouble() * 0.2f;
                p.Color = new Vec4f(1f, 0.9f + (float)rand.NextDouble() * 0.1f, 0.6f + (float)rand.NextDouble() * 0.4f, 0.8f);
                p.ShrinkOnDeath = true;

                Particles.Add(p);
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (Particles.Count == 0) return;

            IClientPlayer player = capi.World.Player;
            Vec3d camPos = player.Entity.CameraPos;
            IStandardShaderProgram prog = capi.Render.PreparedStandardShader((int)camPos.X, (int)camPos.Y, (int)camPos.Z);

            prog.Uniform("alphaTest", 0.05f);
            prog.NormalShaded = 0;
            capi.Render.GlToggleBlend(true, EnumBlendMode.Glow);
            GL.DepthMask(false);

            for (int texIndex = 0; texIndex < 4; texIndex++)
            {
                if (textures[texIndex] == null || textures[texIndex].TextureId == 0) continue;

                bool textureBound = false;

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

                    // Торможение частиц (сопротивление воздуха)
                    p.Velocity.X *= 1.0 - (2.0 * deltaTime);
                    p.Velocity.Y *= 1.0 - (2.0 * deltaTime);
                    p.Velocity.Z *= 1.0 - (2.0 * deltaTime);

                    if (p.TextureIndex == texIndex)
                    {
                        if (!textureBound)
                        {
                            GL.ActiveTexture(TextureUnit.Texture0);
                            capi.Render.BindTexture2d(textures[texIndex].TextureId);
                            textureBound = true;
                        }

                        float curve = (float)Math.Sin(p.LifeRatio * Math.PI);
                        float alpha = p.Color.A * curve;
                        Vec4f renderColor = new Vec4f(p.Color.X, p.Color.Y, p.Color.Z, alpha);

                        prog.RgbaAmbientIn = new Vec3f(renderColor.X, renderColor.Y, renderColor.Z);
                        prog.RgbaLightIn = renderColor;
                        prog.RgbaGlowIn = renderColor;
                        prog.RgbaTint = renderColor;

                        ModelMat.Identity();
                        ModelMat.Translate(p.Position.X - camPos.X, p.Position.Y - camPos.Y, p.Position.Z - camPos.Z);
                        ModelMat.RotateY(player.CameraYaw);
                        ModelMat.RotateX(player.CameraPitch);

                        float currentSize = p.ShrinkOnDeath ? p.Size * curve : p.Size;
                        ModelMat.Scale(currentSize, currentSize, currentSize);

                        prog.ModelMatrix = ModelMat.Values;
                        prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
                        prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;

                        capi.Render.RenderMesh(quadMeshRef);
                    }
                }
            }

            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            prog.Stop();

            GL.DepthMask(true);
            capi.Render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            quadMeshRef?.Dispose();
            foreach (var t in textures) t?.Dispose();
        }
    }
}