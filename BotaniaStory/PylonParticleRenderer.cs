using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using System;

namespace BotaniaStory
{
    public class PylonParticleRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private MeshRef quadMeshRef;
        private LoadedTexture[] textures = new LoadedTexture[5];
        private Matrixf ModelMat = new Matrixf();

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
            // Загружаем 4 текстуры искр
            for (int i = 0; i < 4; i++)
            {
                textures[i] = new LoadedTexture(capi);
                capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", $"textures/particle/pylon_particle_{i}.png"), ref textures[i]);
            }
            // Загружаем текстуру виспа (портала)
            textures[4] = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/particle/mana_particle.png"), ref textures[4]);

            MeshData quad = QuadMeshUtil.GetCustomQuadModelData(-0.5f, -0.5f, 0, 1f, 1f);
            quad.Rgba = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
            quadMeshRef = capi.Render.UploadMesh(quad);
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
            GL.DepthMask(false); // Отключаем глубину для светящегося эффекта

            // Группируем рендер по текстурам для оптимизации
            for (int texIndex = 0; texIndex < 5; texIndex++)
            {
                if (textures[texIndex] == null || textures[texIndex].TextureId == 0) continue;

                bool textureBound = false;

                // =========================================================
                // 1. ИЗОЛИРОВАННАЯ ФИЗИКА
                // =========================================================
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

                    // ВОТ ЗДЕСЬ РАЗДЕЛЯЕМ ФИЗИКУ:
                    if (p.TextureIndex != 4)
                    {
                        // Искры (бенгальский огонь) быстро тормозят
                        p.Velocity.X *= 1.0 - (1.5 * deltaTime);
                        p.Velocity.Y *= 1.0 - (1.5 * deltaTime);
                        p.Velocity.Z *= 1.0 - (1.5 * deltaTime);
                    }
                    else
                    {
                        // Магия (виспы) почти не испытывает сопротивления
                        p.Velocity.X *= 1.0 - (0.1 * deltaTime);
                        p.Velocity.Y *= 1.0 - (0.1 * deltaTime);
                        p.Velocity.Z *= 1.0 - (0.1 * deltaTime);
                    }

                if (p.Life <= 0)
                    {
                        if (texIndex == 0) Particles.RemoveAt(i);
                        continue;
                    }

                    // Отрисовываем только если индекс совпадает с текущим проходом
                    if (p.TextureIndex == texIndex)
                    {
                        if (!textureBound)
                        {
                            GL.ActiveTexture(TextureUnit.Texture0);
                            capi.Render.BindTexture2d(textures[texIndex].TextureId);
                            textureBound = true;
                        }

                        // Синусоида для прозрачности (плавно появляется и плавно исчезает)
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

                        // === НОВАЯ ЛОГИКА РАЗМЕРА ===
                        float currentSize;
                        if (p.ShrinkOnDeath)
                        {
                            // Спираль: вырастает и сжимается
                            currentSize = p.Size * curve;
                        }
                        else
                        {
                            // Ядро: быстро вырастает за первые 20% жизни и остается большим до исчезновения
                            float age = 1.0f - p.LifeRatio; // от 0.0 (рождение) до 1.0 (смерть)
                            float growFactor = Math.Min(1.0f, age * 5.0f);
                            currentSize = p.Size * growFactor;
                        }

                        ModelMat.Scale(currentSize, currentSize, currentSize);

                        prog.ModelMatrix = ModelMat.Values;
                        prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
                        prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;

                        capi.Render.RenderMesh(quadMeshRef);
                    }
                }
            }

            // Моем кисточки
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