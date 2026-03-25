using BotaniaStory.Flora.GeneratingFlora;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

// Если Дневноцвет лежит в другой папке - раскомментируй строку ниже:
// using BotaniaStory.GeneratingFlora; 

namespace BotaniaStory
{
    public class BotaniaWandHud : HudElement
    {
        // 1. Меши для графической отрисовки
        private MeshRef quadMesh;
        private MeshRef borderMesh;
        private Matrixf modelMat = new Matrixf();

        // Универсальные переменные для отрисовки ЛЮБОГО блока
        private int displayMana = 0;
        private int displayMaxMana = 1;
        private BlockPos highlightPos = null;
        private string displayName = "";
        private bool showHud = false;

        public BotaniaWandHud(ICoreClientAPI capi) : base(capi)
        {
            quadMesh = capi.Render.UploadMesh(QuadMeshUtil.GetQuad());
            borderMesh = capi.Render.UploadMesh(LineMeshUtil.GetRectangle(ColorUtil.WhiteArgb));

            // Сместили Y с 30 на 60, чтобы убрать окно из-под самого прицела
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
            bool hasWand = !slot.Empty && slot.Itemstack.Item is ItemWandOfTheForest;
            var sel = player.CurrentBlockSelection;

            showHud = false; // По умолчанию скрываем интерфейс

            if (hasWand && sel != null)
            {
                BlockEntity be = capi.World.BlockAccessor.GetBlockEntity(sel.Position);

                // ПРОВЕРЯЕМ: Это Дневноцвет?
                if (be is BlockEntityDaybloom flower)
                {
                    displayMana = flower.CurrentMana;
                    displayMaxMana = flower.MaxMana;
                    highlightPos = flower.LinkedSpreader;
                    displayName = "Дневноцвет";
                    showHud = true;
                }
                // ПРОВЕРЯЕМ: Это Эндофлейм?
                else if (be is BlockEntityEndoflame endoflame)
                {
                    displayMana = endoflame.CurrentMana;
                    displayMaxMana = endoflame.MaxMana;
                    highlightPos = endoflame.LinkedSpreader;
                    displayName = "Эндофлейм";
                    showHud = true;
                }
                // ПРОВЕРЯЕМ: А может это Распространитель?
                else if (be is BlockEntityManaSpreader spreader)
                {
                    displayMana = spreader.CurrentMana;
                    displayMaxMana = spreader.MaxMana;
                    highlightPos = spreader.TargetPos;
                    displayName = "Распространитель маны";
                    showHud = true;
                }
            }

            // Если нашли магический блок - показываем HUD
            // Если нашли магический блок - показываем HUD
            if (showHud)
            {
                if (highlightPos != null)
                    capi.World.HighlightBlocks(player, 2, new List<BlockPos>() { highlightPos });
                else
                    capi.World.HighlightBlocks(player, 2, new List<BlockPos>());

                string statusText = highlightPos != null ? "[Привязан]" : "[Нет связи]";
                Composers["botaniaHud"].GetDynamicText("manaText").SetNewText($"{displayName}\n\n{displayMana} / {displayMaxMana} {statusText}");

                // ВАЖНО: Открываем HUD только если он сейчас закрыт!
                if (!IsOpened())
                {
                    TryOpen();
                }
            }
            else
            {
                // Если смотрим на траву или обычный камень
                capi.World.HighlightBlocks(player, 2, new List<BlockPos>());

                // ВАЖНО: Закрываем HUD только если он открыт!
                if (IsOpened())
                {
                    TryClose();
                }
            }
        }

        // ==========================================
        // ГРАФИЧЕСКАЯ ОТРИСОВКА (Изменено на универсальные переменные)
        // ==========================================
        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            if (!showHud) return; // Рисуем только если нужно!

            IShaderProgram sh = capi.Render.CurrentActiveShader;
            if (sh == null) return;

            sh.Uniform("noTexture", 1f);

            float width = 140f;
            float height = 12f;
            float x = capi.Render.FrameWidth / 2f - width / 2f;
            float y = capi.Render.FrameHeight / 2f + 55f;

            // Используем УНИВЕРСАЛЬНЫЕ переменные
            float fillRatio = (float)displayMana / displayMaxMana;
            fillRatio = Math.Clamp(fillRatio, 0f, 1f);

            DrawMesh(sh, quadMesh, x, y, width, height, new Vec4f(0f, 0f, 0f, 0.6f));

            float fillWidth = width * fillRatio;
            DrawMesh(sh, quadMesh, x, y, fillWidth, height, new Vec4f(0.1f, 0.8f, 1.0f, 0.9f));

            float borderPx = 2f;
            DrawMesh(sh, borderMesh, x - borderPx, y - borderPx, width + borderPx * 2f, height + borderPx * 2f, new Vec4f(1f, 1f, 1f, 0.5f));

            sh.Uniform("noTexture", 0f);
            sh.Uniform("rgbaIn", new Vec4f(1f, 1f, 1f, 1f));
        }

        // Метод отрисовки графических примитивов (Адаптирован из GuiQuadDrawer.cs)
        private void DrawMesh(IShaderProgram sh, MeshRef mesh, float x, float y, float w, float h, Vec4f color)
        {
            modelMat.Set(capi.Render.CurrentModelviewMatrix)
                    .Translate(x, y, 50f)
                    .Scale(w, h, 0f)
                    .Translate(0.5f, 0.5f, 0f)
                    .Scale(0.5f, 0.5f, 0f);

            sh.UniformMatrix("modelViewMatrix", modelMat.Values);
            sh.Uniform("rgbaIn", color);
            capi.Render.RenderMesh(mesh);
        }

        public override void Dispose()
        {
            borderMesh?.Dispose();
            quadMesh?.Dispose();
            base.Dispose();
        }
    }
}