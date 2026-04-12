using BotaniaStory.Flora.GeneratingFlora;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BotaniaWandHud : HudElement
    {
        private MeshRef quadMesh;
        private MeshRef fillMesh; 
        private Matrixf modelMat = new Matrixf();

        // Переменные для текстур
        private LoadedTexture bgTex;
        private LoadedTexture frameTex;
        private LoadedTexture fillTex; 
        private LoadedTexture linkedTex;
        private LoadedTexture unlinkedTex;

        private LoadedTexture poolAcceptingTex; 
        private LoadedTexture poolGivingTex;    

        private int displayMana = 0;
        private int displayMaxMana = 1;
        private float lastFillRatio = -1f; // Следим за изменением маны, чтобы не обновлять сетку каждый кадр

        private bool isLookingAtPool = false;
        private bool poolIsAccepting = false;

        private BlockPos highlightPos = null;
        private string displayName = "";
        private bool showHud = false;
        private bool showConnectionStatus = true;

        public BotaniaWandHud(ICoreClientAPI capi) : base(capi)
        {
            // Базовый квадрат для статичных элементов
            quadMesh = capi.Render.UploadMesh(QuadMeshUtil.GetQuad());

            // Квадрат, который мы будем динамически кадрировать
            fillMesh = capi.Render.UploadMesh(QuadMeshUtil.GetQuad());

            bgTex = new LoadedTexture(capi);
            frameTex = new LoadedTexture(capi);
            fillTex = new LoadedTexture(capi);
            linkedTex = new LoadedTexture(capi);
            unlinkedTex = new LoadedTexture(capi);
            poolAcceptingTex = new LoadedTexture(capi);
            poolGivingTex = new LoadedTexture(capi);


            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/pool_accepting.png"), ref poolAcceptingTex);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/pool_giving.png"), ref poolGivingTex);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/manabar_bg.png"), ref bgTex);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/manabar_frame.png"), ref frameTex);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/manabar_fill.png"), ref fillTex);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/status_linked.png"), ref linkedTex);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/status_unlinked.png"), ref unlinkedTex);

            ElementBounds dialogBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, 60, 300, 80);
            ElementBounds textBounds = ElementBounds.Fill.WithFixedPadding(5);

            Composers["botaniaHud"] = capi.Gui
                .CreateCompo("botaniaHud", dialogBounds)
                .AddDynamicText("", CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center).WithWeight(Cairo.FontWeight.Bold), textBounds, "manaText")
                .Compose();

            capi.Event.RegisterGameTickListener(OnCheckTarget, 100);
        }

        private void OnCheckTarget(float dt)
        {
            var player = capi.World.Player;
            if (player?.Entity == null) return;

            var slot = player.InventoryManager.ActiveHotbarSlot;
            bool hasWand = !slot.Empty && (slot.Itemstack.Item is ItemWandOfTheForest);
            bool isSneaking = player.Entity.Controls.Sneak;
            var sel = player.CurrentBlockSelection;

            showHud = false;
            isLookingAtPool = false;

            if (hasWand && sel != null)
            {
                BlockEntity be = capi.World.BlockAccessor.GetBlockEntity(sel.Position);
                showConnectionStatus = true;

                if (be is BlockEntityGeneratingFlower flower)
                {
                    displayMana = flower.CurrentMana;
                    displayMaxMana = flower.MaxMana;
                    highlightPos = flower.LinkedSpreader;
                    displayName = flower.Block.GetPlacedBlockName(capi.World, sel.Position);
                    showHud = true;
                }
                else if (be is BlockEntityManaSpreader spreader)
                {
                    displayMana = spreader.CurrentMana;
                    displayMaxMana = spreader.MaxMana;
                    highlightPos = spreader.TargetPos;
                    displayName = spreader.Block.GetPlacedBlockName(capi.World, sel.Position);
                    showHud = true;
                }
                else if (be is BlockEntityManaPool pool)
                {
                    displayMana = pool.CurrentMana;
                    displayMaxMana = pool.MaxMana;
                    highlightPos = null;
                    showConnectionStatus = false;

                    isLookingAtPool = true;
                    poolIsAccepting = pool.IsAcceptingFromItems;

                    displayName = pool.Block.GetPlacedBlockName(capi.World, sel.Position);
                    showHud = true;
                }
            }

            if (showHud)
            {
                if (highlightPos != null)
                    capi.World.HighlightBlocks(player, 2, new List<BlockPos>() { highlightPos });
                else
                    capi.World.HighlightBlocks(player, 2, new List<BlockPos>());

                string textToDisplay;

                if (isSneaking)
                {
                    string visualMana = (displayMana / 1000f).ToString("0.##");
                    string visualMax = (displayMaxMana / 1000f).ToString("0.##");
                    textToDisplay = $"{displayName}\n\n{visualMana} / {visualMax}";
                }
                else
                {
                    textToDisplay = $"{displayName}\n\n ";
                }

                Composers["botaniaHud"].GetDynamicText("manaText").SetNewText(textToDisplay);

                if (!IsOpened())
                {
                    TryOpen();
                }
            }
            else
            {
                capi.World.HighlightBlocks(player, 2, new List<BlockPos>());
                if (IsOpened())
                {
                    TryClose();
                }
            }
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            if (!showHud) return;

            IShaderProgram sh = capi.Render.CurrentActiveShader;
            if (sh == null) return;

            // Настройки размеров основной полоски
            float width = 290f;
            float height = 15f;
            float x = capi.Render.FrameWidth / 2f - width / 2f;
            float y = capi.Render.FrameHeight / 2f + 55f;

            float fillRatio = (float)displayMana / displayMaxMana;
            fillRatio = Math.Clamp(fillRatio, 0f, 1f);

            // ==========================================
            // МАГИЯ КАДРИРОВАНИЯ ТЕКСТУРЫ ПОЛОСКИ
            // ==========================================
            // Мы обновляем текстурную сетку ТОЛЬКО когда изменилось количество маны (ради оптимизации)
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

            // 1. СЛОЙ: ФОНОВАЯ ТЕКСТУРА (bgTex)
            DrawTexture(sh, quadMesh, bgTex.TextureId, x, y, width, height);

            // 2. СЛОЙ: КАДРИРОВАННАЯ ТЕКСТУРА ЗАЛИВКИ (fillTex)
            // Важно: ширина фигуры уменьшается (width * fillRatio), а вместе с ней мы используем нашу обрезанную сетку (fillMesh)
            float fillWidth = width * fillRatio;
            DrawTexture(sh, fillMesh, fillTex.TextureId, x, y, fillWidth, height);

            // 3. СЛОЙ: РАМКА ПОВЕРХ ВСЕГО (frameTex)
            DrawTexture(sh, quadMesh, frameTex.TextureId, x, y, width, height);

            // 4. СЛОЙ: ИКОНКА СТАТУСА ПРИВЯЗКИ
            if (showConnectionStatus)
            {
                int iconTexId = (highlightPos != null) ? linkedTex.TextureId : unlinkedTex.TextureId;

                float iconSize = 50f;
                // Сдвигаем вправо: берем начало полоски (x), прибавляем всю её длину (width) и 5 пикселей для зазора
                float iconX = x + width + 10f;
                // Центрируем иконку по вертикали ровно напротив полоски маны
                float iconY = y + (height / 2f) - (iconSize / 2f);

                DrawTexture(sh, quadMesh, iconTexId, iconX, iconY, iconSize, iconSize);

                DrawTexture(sh, quadMesh, iconTexId, iconX, iconY, iconSize, iconSize);
            }

            // 4. СЛОЙ: ИКОНКА СТАТУСА ПРИВЯЗКИ
            if (showConnectionStatus)
            {
                int iconTexId = (highlightPos != null) ? linkedTex.TextureId : unlinkedTex.TextureId;

                float iconSize = 50f;
                float iconX = x + width + 10f;
                float iconY = y + (height / 2f) - (iconSize / 2f);

                DrawTexture(sh, quadMesh, iconTexId, iconX, iconY, iconSize, iconSize);
            }

            // --- НОВОЕ: 5. СЛОЙ: ИКОНКА СТАТУСА БАССЕЙНА ---
            if (isLookingAtPool)
            {
                // Выбираем текстуру в зависимости от того, принимает бассейн ману или отдает
                int iconTexId = poolIsAccepting ? poolAcceptingTex.TextureId : poolGivingTex.TextureId;

                float iconSize = 50f;
                // Размещаем там же, где была бы иконка привязки
                float iconX = x + width + 10f;
                float iconY = y + (height / 2f) - (iconSize / 2f);

                DrawTexture(sh, quadMesh, iconTexId, iconX, iconY, iconSize, iconSize);
            }

            sh.Uniform("noTexture", 0f);
            sh.Uniform("rgbaIn", new Vec4f(1f, 1f, 1f, 1f));
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

        public override void Dispose()
        {
            bgTex?.Dispose();
            frameTex?.Dispose();
            fillTex?.Dispose();
            linkedTex?.Dispose();
            unlinkedTex?.Dispose();

            poolAcceptingTex?.Dispose();
            poolGivingTex?.Dispose();

            quadMesh?.Dispose();
            fillMesh?.Dispose(); 
            base.Dispose();
        }
    }
}