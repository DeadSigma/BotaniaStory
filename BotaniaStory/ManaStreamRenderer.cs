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

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        public ManaStreamRenderer(ICoreClientAPI api)
        {
            this.capi = api;
            // Стабильный Opaque слой
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "manastream");
            LoadTextureAndMesh();
        }

        private void LoadTextureAndMesh()
        {
            // 1. СТАНДАРТНАЯ ЗАГРУЗКА
            // Путь "textures/particle/mana_particle.png" в домене "botaniastory" 
            // будет искать файл: assets/botaniastory/textures/particle/mana_particle.png
            AssetLocation texLocation = new AssetLocation("botaniastory", "textures/particle/mana_particle.png");
            particleTexture = new LoadedTexture(capi);

            // Родной метод движка (компилируется без ошибок)
            capi.Render.GetOrLoadTexture(texLocation, ref particleTexture);

            if (particleTexture.TextureId == 0)
            {
                capi.Logger.Error("[BotaniaStory] ОШИБКА: Текстура маны НЕ ЗАГРУЖЕНА! ID = 0. Проверьте путь: " + texLocation.Path);
            }
            else
            {
                capi.Logger.Notification("[BotaniaStory] Текстура маны успешно загружена! ID: " + particleTexture.TextureId);
            }

            // 2. ВОССТАНАВЛИВАЕМ МЕШ И ЦВЕТ
            MeshData quad = QuadMeshUtil.GetCustomQuadModelData(-0.5f, -0.5f, 0, 1f, 1f);

            // Обязательно заливаем белый цвет и 100% непрозрачность (255), иначе частицы будут невидимыми
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
            int count = capi.World.Rand.Next(2, 5);
            for (int i = 0; i < count; i++)
            {
                float randomSize = (float)(0.05f + capi.World.Rand.NextDouble() * 0.2f);
                Vec4f particleColor;
                double rand = capi.World.Rand.NextDouble();

                if (rand < 0.85) particleColor = new Vec4f(1f, 1f, 1f, 1f);
                else if (rand < 0.90) particleColor = new Vec4f(1f, 0.4f, 0.8f, 1f);
                else if (rand < 0.95) particleColor = new Vec4f(0.2f, 0.8f, 1f, 1f);
                else particleColor = new Vec4f(0.4f, 1f, 0.4f, 1f);

                activeParticles.Add(new ManaParticle(start, end, 2.0f, randomSize, particleColor));
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (activeParticles.Count == 0 || quadMeshRef == null || particleTexture == null || particleTexture.TextureId == 0) return;

            // Движение
            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                var p = activeParticles[i];
                p.Progress += p.Speed * deltaTime;
                if (p.Progress >= 1.0f) activeParticles.RemoveAt(i);
            }

            if (activeParticles.Count == 0) return;

            // Отрисовка
            IRenderAPI render = capi.Render;
            IClientPlayer player = capi.World.Player;
            Vec3d camPos = player.Entity.CameraPos;

            // Безопасный шейдер, который не вызывает InvalidOperation
            IStandardShaderProgram prog = render.PreparedStandardShader((int)camPos.X, (int)camPos.Y, (int)camPos.Z);

            prog.Tex2D = particleTexture.TextureId;

            // Убираем чёрные квадраты: отрезаем пиксели с альфой меньше 10%
            prog.Uniform("alphaTest", 0.1f);

            // Свечение в темноте
            prog.Uniform("extraGlow", 255);

            foreach (var p in activeParticles)
            {
                Vec3d pos = p.GetCurrentPosition();

                // Красим белую текстуру в нужный цвет маны
                prog.Uniform("rgbaAmbientIn", p.Color);
                prog.Uniform("rgbaLightIn", p.Color);

                ModelMat.Identity();
                ModelMat.Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);

                // Поворот лицом к игроку
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