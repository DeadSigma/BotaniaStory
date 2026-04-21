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
        private float animationOffset;

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public PylonRenderer(ICoreClientAPI capi, BlockPos pos)
        {
            this.capi = capi;
            this.pos = pos;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "botaniapylon");

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

            float t = (capi.World.ElapsedMilliseconds + animationOffset) / 50f;
            float scale = 0.6f;

            foreach (var kvp in pylonParts)
            {
                string partName = kvp.Key;
                MeshRef meshRef = kvp.Value;

                float bobbing = 0f;
                float angle = 0f;

                // 1. Считаем анимацию
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

                // 2. Рассчитываем матрицу трансформации ПЕРЕД отрисовкой
                modelMat.Identity();
                modelMat.Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);
                modelMat.Translate(0.2f, 0.1f, 0.8f);
                modelMat.Scale(scale, scale, scale);
                modelMat.Translate(0f, bobbing, 0f);
                modelMat.Translate(0.5f, 0f, -0.5f);
                modelMat.RotateY(angle);
                modelMat.Translate(-0.5f, 0f, 0.5f);

                // 3. Отрисовка
                if (partName == "Crystal")
                {
                    // --- Внутренний кристалл (Плотный) ---
                    prog.ModelMatrix = modelMat.Values;
                    prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
                    prog.ExtraGlow = 60;
                    render.RenderMesh(meshRef);

                    // ---  СМЕШИВАНИЕ ---
                    render.GlToggleBlend(true, EnumBlendMode.Standard);
                    prog.AlphaTest = 0f; // Не даем движку отбрасывать прозрачные пиксели

                    // --- Внешний кристалл (Полупрозрачный) ---
                    float alpha = (float)((GameMath.Sin(t / 20f) / 2f + 0.5f) / 2f) + 0.183f;
                    modelMat.Scale(1.1f, 1.1f, 1.1f);
                    modelMat.Translate(-0.05f, -0.1f, 0.05f);

                    prog.ModelMatrix = modelMat.Values;
                    prog.RgbaTint = new Vec4f(1f, 1f, 1f, alpha);
                    prog.ExtraGlow = 100;

                    render.RenderMesh(meshRef);

                    render.GlToggleBlend(false);
                    prog.AlphaTest = 0.5f; // Возвращаем стандартный порог
                }
                else
                {
                    // --- Все остальные элементы (Кольца, Камни) ---
                    prog.ModelMatrix = modelMat.Values;
                    prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
                    prog.ExtraGlow = 60;
                    render.RenderMesh(meshRef);
                }
            }

            prog.ExtraGlow = 0;
            prog.Stop();
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            // Отписываемся тоже только от одной стадии!
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            foreach (var meshRef in pylonParts.Values)
            {
                meshRef?.Dispose();
            }
            pylonParts.Clear();
            loadedTexture?.Dispose();
        }
    }
}