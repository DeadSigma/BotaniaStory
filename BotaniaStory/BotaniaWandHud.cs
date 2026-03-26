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
        private MeshRef borderMesh;
        private Matrixf modelMat = new Matrixf();

        private int displayMana = 0;
        private int displayMaxMana = 1;
        private BlockPos highlightPos = null;
        private string displayName = "";
        private bool showHud = false;
        private bool showConnectionStatus = true;

        public BotaniaWandHud(ICoreClientAPI capi) : base(capi)
        {
            quadMesh = capi.Render.UploadMesh(QuadMeshUtil.GetQuad());
            borderMesh = capi.Render.UploadMesh(LineMeshUtil.GetRectangle(ColorUtil.WhiteArgb));

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

            // Проверяем, в руках ли Посох Леса ИЛИ Посох Связывания (оба должны показывать HUD!)
            bool hasWand = !slot.Empty && (slot.Itemstack.Item is ItemWandOfTheForest );
            var sel = player.CurrentBlockSelection;

            showHud = false;

            if (hasWand && sel != null)
            {
                BlockEntity be = capi.World.BlockAccessor.GetBlockEntity(sel.Position);
                showConnectionStatus = true;

                // 1. ПРОВЕРКА ВСЕХ ЦВЕТОВ РАЗОМ
                if (be is BlockEntityGeneratingFlower flower)
                {
                    displayMana = flower.CurrentMana;
                    displayMaxMana = flower.MaxMana;
                    highlightPos = flower.LinkedSpreader;

                    // Универсальное получение локализованного имени прямо из игры!
                    displayName = flower.Block.GetPlacedBlockName(capi.World, sel.Position);
                    showHud = true;
                }
                // 2. РАСПРОСТРАНИТЕЛЬ
                else if (be is BlockEntityManaSpreader spreader)
                {
                    displayMana = spreader.CurrentMana;
                    displayMaxMana = spreader.MaxMana;
                    highlightPos = spreader.TargetPos;
                    displayName = spreader.Block.GetPlacedBlockName(capi.World, sel.Position);
                    showHud = true;
                }
                // 3. БАССЕЙН МАНЫ
                else if (be is BlockEntityManaPool pool)
                {
                    displayMana = pool.CurrentMana;
                    displayMaxMana = pool.MaxMana;
                    highlightPos = null;
                    showConnectionStatus = false; // Бассейн ни к чему не привязан, статус ему не нужен
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

                string statusText = "";
                if (showConnectionStatus)
                {
                    statusText = highlightPos != null ? "[Привязан]" : "[Нет связи]";
                }

                // Делим реальную ману на 1000. Буква 'f' нужна, чтобы превратить число в дробное.
                // Формат "0.##" убирает лишние нули: 1000 станет "1", а 1440 станет "1.44".
                string visualMana = (displayMana / 1000f).ToString("0.##");
                string visualMax = (displayMaxMana / 1000f).ToString("0.##");

                Composers["botaniaHud"].GetDynamicText("manaText").SetNewText($"{displayName}\n\n{visualMana} / {visualMax} {statusText}");

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

            sh.Uniform("noTexture", 1f);

            float width = 140f;
            float height = 12f;
            float x = capi.Render.FrameWidth / 2f - width / 2f;
            float y = capi.Render.FrameHeight / 2f + 55f;

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