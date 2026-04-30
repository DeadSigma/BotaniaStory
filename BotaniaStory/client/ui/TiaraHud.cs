using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.ui
{
    public class TiaraHud : HudElement
    {
        public float StaminaValue = 1f;
        public bool IsTiaraEquipped = false;
        private float lastFillRatio = -1f; 

        private MeshRef quadMesh;
        private MeshRef fillMesh;
        private Matrixf modelMat = new Matrixf();
        private LoadedTexture bgTex;
        private LoadedTexture fgTex;

        public TiaraHud(ICoreClientAPI capi) : base(capi)
        {
            // Базовый квадрат для фона
            quadMesh = capi.Render.UploadMesh(QuadMeshUtil.GetQuad());
            // Квадрат, который мы будем динамически кадрировать (обрезать)
            fillMesh = capi.Render.UploadMesh(QuadMeshUtil.GetQuad());

            bgTex = new LoadedTexture(capi);
            fgTex = new LoadedTexture(capi);

            // Загружаем текстуры фона и заливки
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/tiara_bg.png"), ref bgTex);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/tiara_fg.png"), ref fgTex);
        }

        public override void OnOwnPlayerDataReceived() => SetupHud();

        private void SetupHud()
        {
            // Нам всё ещё нужен пустой композер, чтобы движок понимал, что HUD существует 
            // и корректно обрабатывал TryOpen / TryClose
            ElementBounds dialogBounds = ElementBounds.Fixed(EnumDialogArea.CenterBottom, 0, -130, 150, 25);

            SingleComposer = capi.Gui.CreateCompo("tiaraStaminaHud", dialogBounds).Compose();

            TryOpen();
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            // Если выносливость полная - не рисуем HUD
            if (!IsTiaraEquipped || StaminaValue >= 0.99f) return;

            IShaderProgram sh = capi.Render.CurrentActiveShader;
            if (sh == null) return;

            // --- НАСТРОЙКИ РАЗМЕРА ---
            float width = 242f;  
            float height = 24f;  

            // --- НАСТРОЙКИ ПОЗИЦИИ ---
            float x = capi.Render.FrameWidth / 2f + 93f;

            // Это поднимет её над полоской голода.
            float y = capi.Render.FrameHeight - 140f;

            float fillRatio = Math.Clamp(StaminaValue, 0f, 1f);

            // Обновляем текстурную сетку ТОЛЬКО когда изменилось количество сил
            if (Math.Abs(lastFillRatio - fillRatio) > 0.001f)
            {
                lastFillRatio = fillRatio;
                MeshData fillData = QuadMeshUtil.GetQuad();

                // Обрезаем координаты наложения текстуры (UV) с правой стороны
                fillData.Uv[2] = fillRatio; // Низ-право
                fillData.Uv[4] = fillRatio; // Верх-право

                // Передаем обрезанную сетку в видеокарту
                capi.Render.UpdateMesh(fillMesh, fillData);
            }

            sh.Uniform("noTexture", 0f);

            // 1. СЛОЙ: ФОНОВАЯ ТЕКСТУРА (tiara_bg.png)
            DrawTexture(sh, quadMesh, bgTex.TextureId, x, y, width, height);

            // 2. СЛОЙ: КАДРИРОВАННАЯ ТЕКСТУРА ЗАЛИВКИ (tiara_fg.png)
            float fillWidth = width * fillRatio;
            DrawTexture(sh, fillMesh, fgTex.TextureId, x, y, fillWidth, height);
        }

        private void DrawTexture(IShaderProgram sh, MeshRef mesh, int textureId, float x, float y, float w, float h)
        {
            modelMat.Set(capi.Render.CurrentModelviewMatrix)
                    .Translate(x, y, 50f)
                    .Scale(w, h, 0f)
                    .Translate(0.5f, 0.5f, 0f)
                    .Scale(0.5f, 0.5f, 0f);

            sh.UniformMatrix("modelViewMatrix", modelMat.Values);
            sh.Uniform("rgbaIn", new Vec4f(1f, 1f, 1f, 1f));
            sh.BindTexture2D("tex2d", textureId, 0);
            capi.Render.RenderMesh(mesh);
        }

        // Заглушка, чтобы игра не пыталась привязать HUD к горячим клавишам
        public override string ToggleKeyCombinationCode => null;

        public override void Dispose()
        {
            quadMesh?.Dispose();
            fillMesh?.Dispose();
            base.Dispose();
        }
    }
}