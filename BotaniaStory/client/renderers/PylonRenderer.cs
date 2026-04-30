using BotaniaStory.util;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.renderers
{
    // 1. Добавляем перечисление типов пилонов
    public enum EnumPylonType
    {
        Mana,   // Обычный (Синий)
        Natura, // Зеленый (Без колец, больше)
        Gaia    // Розовый
    }

    public class PylonRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private BlockPos pos;
        private Matrixf modelMat = new Matrixf();
        private LoadedTexture loadedTexture;
        private Dictionary<string, MeshRef> pylonParts = new Dictionary<string, MeshRef>();

        private bool isDisposed = false;
        private float animationOffset;

        // Переменная для хранения типа текущего пилона
        private EnumPylonType pylonType;

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        // В конструктор добавили параметр type (по умолчанию Mana)
        public PylonRenderer(ICoreClientAPI capi, BlockPos pos, EnumPylonType type = EnumPylonType.Mana)
        {
            this.capi = capi;
            this.pos = pos;
            this.pylonType = type; // Сохраняем тип

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "botaniapylon");

            capi.Event.RegisterRenderer(this, EnumRenderStage.ShadowFar, "botaniapylon");
            capi.Event.RegisterRenderer(this, EnumRenderStage.ShadowNear, "botaniapylon");

            this.animationOffset = (float)(new Random(pos.X ^ pos.Y ^ pos.Z).NextDouble() * 10000);

            // 2. Загрузка модели
            AssetLocation objPath = new AssetLocation("botaniastory", "shapes/block/pylon.obj");
            IAsset objAsset = capi.Assets.TryGet(objPath);

            if (objAsset != null)
            {
                Dictionary<string, MeshData> meshes = ObjParser.Parse(objAsset.ToText(), capi);
                foreach (var kvp in meshes)
                {
                    pylonParts[kvp.Key] = capi.Render.UploadMesh(kvp.Value);
                }
            }

            // 3. Выбор текстуры в зависимости от типа пилона
            string texName = "pylon_blue.png";
            if (pylonType == EnumPylonType.Natura) texName = "pylon_green.png";
            if (pylonType == EnumPylonType.Gaia) texName = "pylon_pink.png";

            AssetLocation texPath = new AssetLocation("botaniastory", "textures/block/" + texName);
            loadedTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(texPath, ref loadedTexture);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (isDisposed || pylonParts.Count == 0 || loadedTexture.TextureId == 0) return;

            IRenderAPI render = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            // Определяем, рисуем ли мы сейчас тень
            bool isShadowPass = stage == EnumRenderStage.ShadowFar || stage == EnumRenderStage.ShadowNear;

            IStandardShaderProgram stdProg = null;
            IShaderProgram shadowProg = null;

            if (isShadowPass)
            {
                // Движок сам подготовил шейдер для теней. Берем его.
                shadowProg = render.CurrentActiveShader;
                // Передаем текстуру (необходимо, если в текстуре есть альфа-канал, влияющий на форму тени)
                shadowProg.BindTexture2D("tex2d", loadedTexture.TextureId, 0);
            }
            else
            {
                // Обычная отрисовка пилона
                stdProg = render.PreparedStandardShader(pos.X, pos.Y, pos.Z);
                stdProg.Tex2D = loadedTexture.TextureId;
                stdProg.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
                stdProg.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            }

            float t = (capi.World.ElapsedMilliseconds + animationOffset) / 50f;
            float scale = (pylonType == EnumPylonType.Natura) ? 0.8f : 0.6f;

            foreach (var kvp in pylonParts)
            {
                string partName = kvp.Key;
                MeshRef meshRef = kvp.Value;

                if (pylonType == EnumPylonType.Natura && partName != "Crystal") continue;

                float bobbing = 0f;
                float angle = 0f;

                if (partName == "Crystal")
                {
                    bobbing = (float)(GameMath.Sin(t / 20f) / 17.5f);
                    angle = -t * GameMath.DEG2RAD;
                }
                else if (partName.Contains("Gem"))
                {
                    bobbing = (float)(GameMath.Sin(t / 20f) / 20f) - 0.025f;
                    angle = (t * 1.5f) * GameMath.DEG2RAD;
                }
                else
                {
                    bobbing = 0f;
                    angle = (t * 1.5f) * GameMath.DEG2RAD;
                }

                float tx = 0.2f + (pylonType == EnumPylonType.Natura ? -0.1f : 0f);
                float ty = 0.1f;
                float tz = 0.8f + (pylonType == EnumPylonType.Natura ? 0.1f : 0f);

                modelMat.Identity();
                modelMat.Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);
                modelMat.Translate(tx, ty, tz);
                modelMat.Scale(scale, 0.6f, scale);
                modelMat.Translate(0f, bobbing, 0f);
                modelMat.Translate(0.5f, 0f, -0.5f);
                modelMat.RotateY(angle);
                modelMat.Translate(-0.5f, 0f, 0.5f);

                if (isShadowPass)
                {
                    // === РЕНДЕР ТЕНИ ===
                    Matrixf mvpMat = new Matrixf();

                    // 1. Берем матрицу проекции от движка
                    mvpMat.Set(capi.Render.CurrentProjectionMatrix);

                    // 2. Умножаем на матрицу вида (позиция солнца/камеры теней)
                    mvpMat.Mul(capi.Render.CurrentModelviewMatrix);

                    // 3. Умножаем на матрицу нашего пилона (анимация и позиция в мире)
                    mvpMat.Mul(modelMat.Values);

                    // 4. Передаем готовую матрицу в теневой шейдер под правильным ключом
                    shadowProg.UniformMatrix("mvpMatrix", mvpMat.Values);

                    render.RenderMesh(meshRef);
                }
                else
                {
                    // === ОБЫЧНЫЙ РЕНДЕР === 
                    if (partName == "Crystal")
                    {
                        stdProg.ModelMatrix = modelMat.Values;
                        stdProg.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
                        stdProg.ExtraGlow = 150;
                        render.RenderMesh(meshRef);

                        render.GlToggleBlend(true, EnumBlendMode.Standard);
                        stdProg.AlphaTest = 0f;

                        float alpha = 0.6f + (float)Math.Cos(t * GameMath.DEG2RAD) * 0.4f;

                        modelMat.Scale(1.1f, 1.1f, 1.1f);
                        modelMat.Translate(-0.05f, -0.1f, 0.05f);

                        stdProg.ModelMatrix = modelMat.Values;
                        stdProg.RgbaTint = new Vec4f(1f, 1f, 1f, alpha);
                        stdProg.ExtraGlow = 100;

                        render.RenderMesh(meshRef);

                        render.GlToggleBlend(false);
                        stdProg.AlphaTest = 0.5f;
                    }
                    else
                    {
                        stdProg.ModelMatrix = modelMat.Values;
                        stdProg.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
                        stdProg.ExtraGlow = 60;
                        render.RenderMesh(meshRef);
                    }
                }
            }

            if (!isShadowPass)
            {
                stdProg.ExtraGlow = 0;
                stdProg.Stop();
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            capi.Event.UnregisterRenderer(this, EnumRenderStage.ShadowFar);
            capi.Event.UnregisterRenderer(this, EnumRenderStage.ShadowNear);

            foreach (var meshRef in pylonParts.Values)
            {
                meshRef?.Dispose();
            }
            pylonParts.Clear();
        }
    }
}