using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ManaStreamRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private List<ManaParticle> activeParticles = new List<ManaParticle>();

        private MeshRef quadMeshRef = null;
        private LoadedTexture particleTexture = null;

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        public ManaStreamRenderer(ICoreClientAPI api)
        {
            this.capi = api;

            // ИСПРАВЛЕНИЕ 1: В актуальных версиях прозрачные кастомные вещи рисуются в слое Opaque, 
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "manastream");

            LoadTextureAndMesh(); // <-- Убедись, что эта строчка есть!
        }

        private void LoadTextureAndMesh()
        {
            AssetLocation texLocation = new AssetLocation("botaniastory", "particle/mana_particle");
            particleTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(texLocation, ref particleTexture);

            // ИСПРАВЛЕНИЕ: Включаем генерацию цвета (пятый true) и флагов (шестой true)
            MeshData quad = new MeshData(4, 6, true, false, true, true);

            quad.xyz = new float[] {
                -0.5f, -0.5f, 0,
                 0.5f, -0.5f, 0,
                 0.5f,  0.5f, 0,
                -0.5f,  0.5f, 0
            };

            quad.Uv = new float[] {
                0, 0,
                1, 0,
                1, 1,
                0, 1
            };

            // ЗАПОЛНЯЕМ ЦВЕТА (4 вершины * 4 канала RGBA = 16 байт)
            quad.Rgba = new byte[] {
                255, 255, 255, 255,
                255, 255, 255, 255,
                255, 255, 255, 255,
                255, 255, 255, 255
            };

            // ЗАПОЛНЯЕМ ФЛАГИ (4 вершины = 4 инта)
            quad.Flags = new int[] { 0, 0, 0, 0 };

            quad.Indices = new int[] { 0, 1, 2, 0, 2, 3 };
            quad.VerticesCount = 4;
            quad.IndicesCount = 6;

            quadMeshRef = capi.Render.UploadMesh(quad);
        }

        public void AddParticle(Vec3d start, Vec3d end)
        {
            activeParticles.Add(new ManaParticle(start, end, 2.0f, 0.2f));
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (activeParticles.Count == 0) return;

            // --- ДВИЖЕНИЕ ---
            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                var p = activeParticles[i];
                p.Progress += p.Speed * deltaTime;

                if (p.Progress >= 1.0f)
                {
                    activeParticles.RemoveAt(i);
                }
            }

            if (activeParticles.Count == 0) return;

            // --- ОТРИСОВКА ---
            IShaderProgram prog = capi.Render.GetEngineShader(EnumShaderProgram.Standard);
            prog.Use();

            prog.Uniform("rgbaAmbientIn", new Vec4f(1f, 1f, 1f, 1f));
            prog.Uniform("rgbaLightIn", new Vec4f(1f, 1f, 1f, 1f));
            prog.Uniform("rgbaFogIn", new Vec4f(1f, 1f, 1f, 1f));
            prog.Uniform("fogMinIn", capi.Render.FogMin);
            prog.Uniform("fogDensityIn", capi.Render.FogDensity);
            prog.Uniform("alphaTest", 0.05f);

            capi.Render.BindTexture2d(particleTexture.TextureId);

            IClientPlayer player = capi.World.Player;
            Vec3d camPos = player.Entity.CameraPos;

            float[] cameraOriginMat = new float[16];
            for (int i = 0; i < 16; i++)
            {
                cameraOriginMat[i] = (float)capi.Render.CameraMatrixOrigin[i];
            }

            // МЫ УБРАЛИ GlDisableCullFace() ОТСЮДА!

            foreach (var p in activeParticles)
            {
                Vec3d pos = p.GetCurrentPosition();

                float[] modelMatrix = Mat4f.Create();
                Mat4f.Identity(modelMatrix);

                Mat4f.Translate(modelMatrix, modelMatrix, (float)(pos.X - camPos.X), (float)(pos.Y - camPos.Y), (float)(pos.Z - camPos.Z));

                Mat4f.RotateY(modelMatrix, modelMatrix, player.CameraYaw);
                Mat4f.RotateX(modelMatrix, modelMatrix, player.CameraPitch);

                // Делаем частицу покрупнее
                float testSize = p.Size * 3.0f;
                Mat4f.Scale(modelMatrix, modelMatrix, testSize, testSize, testSize);

                prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
                prog.UniformMatrix("modelMatrix", modelMatrix);
                prog.UniformMatrix("viewMatrix", cameraOriginMat);

                capi.Render.RenderMesh(quadMeshRef);
            }

            // МЫ УБРАЛИ GlEnableCullFace() ОТСЮДА!

            prog.Stop();
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque); // Тоже поменяли на Opaque
            quadMeshRef?.Dispose();
            particleTexture?.Dispose();
        }
    }
}