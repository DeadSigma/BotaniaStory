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

                // Преобладают цветные частицы (голубой, зеленый, розовый)
                if (rand < 0.35) particleColor = new Vec4f(0.2f, 0.8f, 1f, 1f);
                else if (rand < 0.70) particleColor = new Vec4f(0.4f, 1f, 0.4f, 1f);
                else if (rand < 0.90) particleColor = new Vec4f(1f, 0.4f, 0.8f, 1f);
                else particleColor = new Vec4f(1f, 1f, 1f, 1f);

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
            // ДЕБАГГЕР: Логируем состояние раз в 1 секунду
            debugTimer += deltaTime;
            if (debugTimer > 1.0f)
            {
                debugTimer = 0f;
                if (activeParticles.Count > 0)
                {
                    capi.Logger.Debug($"[BotaniaStory Debug] Активных частиц: {activeParticles.Count}. ID Текстуры: {particleTexture?.TextureId}");
                }
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

            // В слое Opaque нам ОБЯЗАТЕЛЬНО нужен alphaTest, чтобы скрыть черные (прозрачные) края текстуры.
            // 0.05f отрезает все пиксели, где прозрачность выше 95%.
            prog.Uniform("alphaTest", 0.05f);
            prog.Uniform("extraGlow", 255);

            foreach (var p in activeParticles)
            {
                Vec3d pos = p.GetCurrentPosition();

                prog.Uniform("rgbaAmbientIn", p.Color);
                prog.Uniform("rgbaLightIn", p.Color);

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

            prog.Stop();
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            quadMeshRef?.Dispose();
        }
    }
}