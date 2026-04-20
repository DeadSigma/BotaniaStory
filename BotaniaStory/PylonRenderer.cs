using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class PylonRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private BlockPos pos;
        private Matrixf modelMat = new Matrixf();
        private LoadedTexture loadedTexture;
        private Dictionary<string, MeshRef> pylonParts = new Dictionary<string, MeshRef>();

        private bool isDisposed = false;
        private float animationOffset; // Рандомное смещение для каждого пилона

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public PylonRenderer(ICoreClientAPI capi, BlockPos pos)
        {
            this.capi = capi;
            this.pos = pos;

            // Чтобы пилоны не крутились синхронно, добавим рандомный сдвиг по времени
            this.animationOffset = (float)(new Random(pos.X ^ pos.Y ^ pos.Z).NextDouble() * 10000);

            AssetLocation objPath = new AssetLocation("botaniastory", "shapes/pylon.obj");
            IAsset objAsset = capi.Assets.TryGet(objPath);

            if (objAsset != null)
            {
                Dictionary<string, MeshData> meshes = ObjParser.Parse(objAsset.ToText(), capi);
                foreach (var kvp in meshes)
                {
                    pylonParts[kvp.Key] = capi.Render.UploadMesh(kvp.Value);
                }
            }

            AssetLocation texPath = new AssetLocation("botaniastory", "textures/block/pylon.png");
            loadedTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(texPath, ref loadedTexture);

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "botaniapylon");
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (isDisposed || pylonParts.Count == 0 || loadedTexture.TextureId == 0) return;

            IRenderAPI render = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            IStandardShaderProgram prog = render.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            prog.Tex2D = loadedTexture.TextureId;
            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);

            // Мягкое магическое свечение (60 из 255). 
            // Если захочешь ярче - ставь 80-100. Если темнее - 20-40.
            prog.ExtraGlow = 60;

            // Переводим миллисекунды в "тики" (1 тик = 50мс), чтобы скорости совпали с оригиналом
            float t = (capi.World.ElapsedMilliseconds + animationOffset) / 50f;

            // Тот самый масштаб 0.6 из оригинальной Botania
            float scale = 0.6f;

            foreach (var kvp in pylonParts)
            {
                string partName = kvp.Key;
                MeshRef meshRef = kvp.Value;

                float bobbing = 0f;
                float angle = 0f;

                // Точные тайминги и синусоиды прямо из RenderTilePylon.java!
                if (partName.Contains("Crystal"))
                {
                    bobbing = (float)(GameMath.Sin(t / 20f) / 17.5f);
                    angle = -t * GameMath.DEG2RAD; // Крутится в обратную сторону
                }
                else if (partName.Contains("Gem"))
                {
                    bobbing = (float)(GameMath.Sin(t / 20f) / 20f) - 0.025f;
                    angle = (t * 1.5f) * GameMath.DEG2RAD;
                }
                else // Золотое кольцо
                {
                    bobbing = 0f;
                    angle = (t * 1.5f) * GameMath.DEG2RAD;
                }

                modelMat.Identity();

                // 1. Двигаем рендер к нашему блоку в мире
                modelMat.Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);

                // 2. ТЕ САМЫЕ ИДЕАЛЬНЫЕ КООРДИНАТЫ ИЗ ОРИГИНАЛА: 0.2, 0.05, 0.8
                // С ними центр модели (с учетом сдвига и масштаба) окажется ровно на 0.5, 0.5!
                modelMat.Translate(0.2f, 0.1f, 0.8f); // Слегка приподнял Y до 0.1f, чтобы не торчало в полу

                // 3. Масштабируем
                modelMat.Scale(scale, scale, scale);

                // 4. Применяем эффект парения (вверх-вниз)
                modelMat.Translate(0f, bobbing, 0f);

                // 5. Ось вращения из Botania
                modelMat.Translate(0.5f, 0f, -0.5f);
                modelMat.RotateY(angle);
                modelMat.Translate(-0.5f, 0f, 0.5f);

                prog.ModelMatrix = modelMat.Values;
                prog.ViewMatrix = render.CameraMatrixOriginf;
                prog.ProjectionMatrix = render.CurrentProjectionMatrix;

                render.RenderMesh(meshRef);
            }

            // Обязательно выключаем свечение для остальных блоков
            prog.ExtraGlow = 0;
            prog.Stop();
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            // Сначала отписываемся от события отрисовки
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            // Затем удаляем меши
            foreach (var meshRef in pylonParts.Values)
            {
                meshRef?.Dispose();
            }
            pylonParts.Clear();

        }
    }
}