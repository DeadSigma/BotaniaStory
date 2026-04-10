using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using OpenTK.Graphics.OpenGL;

namespace BotaniaStory
{
    public class ManaStreamRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private List<ManaParticle> activeParticles = new List<ManaParticle>();

        private MeshRef quadMeshRef = null;
        private LoadedTexture particleTexture = null;
        public Matrixf ModelMat = new Matrixf();

        // Переменные для дебага
        private float debugTimer = 0f;

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        public ManaStreamRenderer(ICoreClientAPI api)
        {
            this.capi = api;
            // ВОЗВРАЩАЕМ OPAQUE: это самый стабильный слой для таких частиц
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "manastream");
            LoadTextureAndMesh();
        }

        private void LoadTextureAndMesh()
        {
            AssetLocation texLocation = new AssetLocation("botaniastory", "textures/particle/mana_particle.png");
            particleTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(texLocation, ref particleTexture);

            if (particleTexture.TextureId == 0)
                capi.Logger.Error("[BotaniaStory] ОШИБКА: Текстура маны НЕ ЗАГРУЖЕНА!");
            else
                capi.Logger.Notification("[BotaniaStory] Текстура маны загружена! ID: " + particleTexture.TextureId);

            MeshData quad = QuadMeshUtil.GetCustomQuadModelData(-0.5f, -0.5f, 0, 1f, 1f);
            quad.Rgba = new byte[] {
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
            int count = capi.World.Rand.Next(3, 7);
            for (int i = 0; i < count; i++)
            {
                float randomSize = (float)(0.05f + capi.World.Rand.NextDouble() * 0.3f);
                Vec4f particleColor;
                double rand = capi.World.Rand.NextDouble();

                // ИСПОЛЬЗУЕМ БОЛЕЕ ГЛУБОКИЕ ЦВЕТА И СНИЖАЕМ АЛЬФУ ДО 0.6f
                if (rand < 0.35)
                    particleColor = new Vec4f(0.0f, 0.4f, 1.0f, 0.6f); // Насыщенный сине-голубой
                else if (rand < 0.70)
                    particleColor = new Vec4f(0.0f, 1.0f, 0.1f, 0.6f); // Чистый зеленый
                else if (rand < 0.90)
                    particleColor = new Vec4f(1.0f, 0.0f, 0.8f, 0.6f); // Яркий розовый/магента
                else
                    particleColor = new Vec4f(0.8f, 0.8f, 1.0f, 0.4f); // Бело-голубой (сделали еще прозрачнее)

                // Смещение для красивой дуги распыления
                Vec3d offset = new Vec3d(
                    (capi.World.Rand.NextDouble() - 0.5) * 1.5,
                    (capi.World.Rand.NextDouble() - 0.5) * 1.5,
                    (capi.World.Rand.NextDouble() - 0.5) * 1.5
                );

                float speed = (float)(1.5f + capi.World.Rand.NextDouble());
                activeParticles.Add(new ManaParticle(start, end, speed, randomSize, particleColor, offset));
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (particleTexture == null || particleTexture.Disposed || particleTexture.TextureId == 0) return;
            // ДЕБАГГЕР 
            debugTimer += deltaTime;
            if (debugTimer > 1.0f)
            {
                debugTimer = 0f;
                if (activeParticles.Count > 0)
                    capi.Logger.Debug($"[BotaniaStory Debug] Активных частиц: {activeParticles.Count}. ID Текстуры: {particleTexture?.TextureId}");
            }

            if (activeParticles.Count == 0 || quadMeshRef == null || particleTexture == null || particleTexture.TextureId == 0) return;

            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                var p = activeParticles[i];
                p.Progress += p.Speed * deltaTime;
                if (p.Progress >= 1.0f) activeParticles.RemoveAt(i);
            }

            if (activeParticles.Count == 0) return;

            IRenderAPI render = capi.Render;
            IClientPlayer player = capi.World.Player;
            Vec3d camPos = player.Entity.CameraPos;

            IStandardShaderProgram prog = render.PreparedStandardShader((int)camPos.X, (int)camPos.Y, (int)camPos.Z);

            prog.Tex2D = particleTexture.TextureId;
            prog.Uniform("alphaTest", 0f);
            prog.Uniform("extraGlow", 0);

            // 1. Включаем аддитивное смешивание (свечение)
            render.GlToggleBlend(true, EnumBlendMode.Glow);

            // 2. ИСПРАВЛЕНИЕ: Отключаем запись в буфер глубины напрямую через OpenGL!
            GL.DepthMask(false);

            foreach (var p in activeParticles)
            {
                Vec3d pos = p.GetCurrentPosition();

                prog.Uniform("rgbaAmbientIn", p.Color);
                prog.Uniform("rgbaLightIn", p.Color);
                prog.Uniform("rgbaGlowIn", p.Color);

                ModelMat.Identity();
                ModelMat.Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);
                ModelMat.RotateY(player.CameraYaw);
                ModelMat.RotateX(player.CameraPitch);
                ModelMat.Scale(p.Size, p.Size, p.Size);

                prog.ModelMatrix = ModelMat.Values;
                prog.ViewMatrix = render.CameraMatrixOriginf;
                prog.ProjectionMatrix = render.CurrentProjectionMatrix;

                render.RenderMesh(quadMeshRef);
            }

            // --- ИСПРАВЛЕНИЕ УТЕЧКИ ЦВЕТА (МОЕМ КИСТОЧКИ) ---
            // Возвращаем глобальному шейдеру чистый белый свет, 
            // чтобы он не покрасил искры и другие блоки в мире!
            prog.Uniform("rgbaAmbientIn", new Vec4f(1f, 1f, 1f, 1f));
            prog.Uniform("rgbaLightIn", new Vec4f(1f, 1f, 1f, 1f));
            prog.Uniform("rgbaGlowIn", new Vec4f(0f, 0f, 0f, 0f)); // Glow лучше обнулить

            prog.Stop();

            // 3. ОБЯЗАТЕЛЬНО возвращаем всё как было, иначе сломаем рендер всего мира!
            GL.DepthMask(true); // Включаем буфер глубины обратно
            render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            
            quadMeshRef?.Dispose();
            quadMeshRef = null;

            particleTexture?.Dispose();
            particleTexture = null;
        }
    }
}