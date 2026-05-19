using BotaniaStory.blockentity;
using BotaniaStory.Flora.GeneratingFlora;
using BotaniaStory.items;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

namespace BotaniaStory.client.ui
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

        private enum AltarIconState { None, NeedsLivingrock, NeedsWand }
        private AltarIconState currentAltarIcon = AltarIconState.None;

        private DummySlot livingrockSlot;
        private DummySlot wandSlot;

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

            ElementBounds dialogBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, 75, 300, 80);
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

            int displayTargetMana = 0;
            showHud = false;
            isLookingAtPool = false;
            currentAltarIcon = AltarIconState.None;

            BlockEntity be = null;
            if (sel != null)
            {
                be = capi.World.BlockAccessor.GetBlockEntity(sel.Position);
            }

            // === ТЕПЕРЬ ВСЕ ПРОВЕРКИ ТОЛЬКО ЕСЛИ В РУКЕ ПОСОХ ===
            if (hasWand && sel != null)
            {
                showConnectionStatus = true;

                // 1. ГЕНЕРИРУЮЩИЕ ЦВЕТЫ (Дневноцвет, Эндофлейм, Роза Аркана и др. через Behaviors)
                BEBehaviorGeneratingFlower genFlower = GetInterface<BEBehaviorGeneratingFlower>(be);
                if (genFlower != null)
                {
                    displayMana = genFlower.CurrentMana;
                    displayMaxMana = genFlower.MaxMana;
                    highlightPos = genFlower.LinkedSpreader;
                    displayName = be.Block.GetPlacedBlockName(capi.World, sel.Position);
                    showHud = true;
                }
                // 2. РАСПРОСТРАНИТЕЛЬ МАНЫ
                else if (be is BlockEntityManaSpreader spreader)
                {
                    displayMana = spreader.CurrentMana;
                    displayMaxMana = spreader.MaxMana;
                    highlightPos = spreader.TargetPos;
                    displayName = spreader.Block.GetPlacedBlockName(capi.World, sel.Position);
                    showHud = true;
                }
                // 3. ФУНКЦИОНАЛЬНЫЕ ЦВЕТЫ (Вороток, Чистая Маргаритка и т.д.)
                else if (GetInterface<ILinkableToPool>(be) != null)
                {
                    var linkableFlower = GetInterface<ILinkableToPool>(be);
                    displayMana = 0;
                    displayMaxMana = 1;

                    if (linkableFlower.LinkedPool != null &&
                        capi.World.BlockAccessor.GetBlockEntity(linkableFlower.LinkedPool) is BlockEntityManaPool)
                    {
                        highlightPos = linkableFlower.LinkedPool;
                    }
                    else
                    {
                        highlightPos = null;
                    }

                    displayName = be.Block.GetPlacedBlockName(capi.World, sel.Position);
                    showHud = true;
                }
                // 4. БАССЕЙН МАНЫ
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
                // 5. РУНИЧЕСКИЙ АЛТАРЬ
                else if (be is BlockEntityRunicAltar altar)
                {
                    displayMana = altar.CurrentMana;
                    displayMaxMana = altar.MaxBufferMana; 
                    displayTargetMana = altar.TargetMana; 

                    highlightPos = null;
                    showConnectionStatus = false;
                    isLookingAtPool = false;

                    displayName = altar.Block.GetPlacedBlockName(capi.World, sel.Position);
                    currentAltarIcon = AltarIconState.None;

                    // Проверяем, нужна ли иконка
                    if (altar.TargetMana > 0)
                    {
                        if (altar.HasLivingrock)
                        {
                            currentAltarIcon = AltarIconState.NeedsWand; // Посох Леса
                        }
                        else if (altar.CurrentMana >= altar.TargetMana)
                        {
                            currentAltarIcon = AltarIconState.NeedsLivingrock; // Жизнекамень
                        }
                    }
                    showHud = true;
                }
            }

            if (showHud)
            {
                if (highlightPos != null)
                    capi.World.HighlightBlocks(player, 2, new List<BlockPos>() { highlightPos });
                else
                    capi.World.HighlightBlocks(player, 2, new List<BlockPos>());

                string textToDisplay = "";

                if (!string.IsNullOrEmpty(displayName))
                {
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
                }

                Composers["botaniaHud"].GetDynamicText("manaText").SetNewText(textToDisplay);

                if (!IsOpened()) TryOpen();
            }
            else
            {
                capi.World.HighlightBlocks(player, 2, new List<BlockPos>());
                if (IsOpened()) TryClose();
            }
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            if (!showHud) return;

            IShaderProgram sh = capi.Render.CurrentActiveShader;
            if (sh == null) return;

            // 1. Получаем текущий масштаб интерфейса игрока
            float scale = RuntimeEnv.GUIScale;

            // 2. Умножаем базовые размеры и отступы на масштаб
            float width = 290f * scale;
            float height = 15f * scale;
            float x = capi.Render.FrameWidth / 2f - width / 2f;
            float y = capi.Render.FrameHeight / 2f + (55f * scale); // отступ 55 

            float fillRatio = (float)displayMana / displayMaxMana;
            fillRatio = Math.Clamp(fillRatio, 0f, 1f);

            // обновляем текстурную сетку ТОЛЬКО когда изменилось количество маны (ради оптимизации)
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
            float fillWidth = width * fillRatio;
            DrawTexture(sh, fillMesh, fillTex.TextureId, x, y, fillWidth, height);

            // 3. СЛОЙ: РАМКА ПОВЕРХ ВСЕГО (frameTex)
            DrawTexture(sh, quadMesh, frameTex.TextureId, x, y, width, height);

            // 4. СЛОЙ: ИКОНКА СТАТУСА ПРИВЯЗКИ
            if (showConnectionStatus)
            {
                int iconTexId = (highlightPos != null) ? linkedTex.TextureId : unlinkedTex.TextureId;

                float iconSize = 50f * scale;
                float iconX = x + width + (10f * scale);
                float iconY = y + (height / 2f) - (iconSize / 2f);

                DrawTexture(sh, quadMesh, iconTexId, iconX, iconY, iconSize, iconSize);
            }

            // 5. СЛОЙ: ИКОНКА СТАТУСА БАССЕЙНА ---
            if (isLookingAtPool)
            {
                int iconTexId = poolIsAccepting ? poolAcceptingTex.TextureId : poolGivingTex.TextureId;

                float iconHeight = 50f * scale;
                float iconWidth = iconHeight * (18f / 9f);

                float iconX = x + width + (10f * scale);
                float iconY = y + (height / 2f) - (iconHeight / 2f);

                DrawTexture(sh, quadMesh, iconTexId, iconX, iconY, iconWidth, iconHeight);
            }

            sh.Uniform("noTexture", 0f);
            sh.Uniform("rgbaIn", new Vec4f(1f, 1f, 1f, 1f));

            // 6. СЛОЙ ДЛЯ РУНИЧЕСКОГО АЛТАРЯ 
            if (currentAltarIcon != AltarIconState.None)
            {
                if (livingrockSlot == null)
                {
                    Block lrBlock = capi.World.GetBlock(new AssetLocation("botaniastory", "livingrock"));
                    if (lrBlock != null) livingrockSlot = new DummySlot(new ItemStack(lrBlock));
                    else
                    {
                        Item lrItem = capi.World.GetItem(new AssetLocation("botaniastory", "livingrock"));
                        if (lrItem != null) livingrockSlot = new DummySlot(new ItemStack(lrItem));
                    }
                }

                if (wandSlot == null)
                {
                    Item wandItem = capi.World.GetItem(new AssetLocation("botaniastory", "wandoftheforest-white-white"));
                    if (wandItem != null) wandSlot = new DummySlot(new ItemStack(wandItem));
                }

                ItemSlot slotToRender = null;
                if (currentAltarIcon == AltarIconState.NeedsLivingrock) slotToRender = livingrockSlot;
                else if (currentAltarIcon == AltarIconState.NeedsWand) slotToRender = wandSlot;

                if (slotToRender != null && !slotToRender.Empty)
                {
                    float iconSize = 45f * scale;
                    float iconX = x + width + (55f * scale);
                    float iconY = y + (height / 2f) - (iconSize / 2f);

                    sh.Uniform("noTexture", 1f);
                    sh.Uniform("rgbaIn", new Vec4f(1f, 1f, 1f, 1f));

                    float plusSize = 20f * scale;
                    float plusThickness = 4f * scale;
                    float plusX = iconX - plusSize - (15f * scale);
                    float plusY = iconY + (iconSize / 2f) - (plusSize / 2f);

                    DrawTexture(sh, quadMesh, bgTex.TextureId, plusX, plusY + (plusSize / 2f) - (plusThickness / 2f), plusSize, plusThickness);
                    DrawTexture(sh, quadMesh, bgTex.TextureId, plusX + (plusSize / 2f) - (plusThickness / 2f), plusY, plusThickness, plusSize);

                    sh.Uniform("noTexture", 0f);

                    // Отрисовка предмета тоже должна учитывать масштаб
                    capi.Render.RenderItemstackToGui(
                        slotToRender,
                        iconX + (25f * scale),
                        iconY + (20f * scale),
                        120,
                        40f * scale, // Размер предмета
                        ColorUtil.WhiteArgb,
                        deltaTime,
                        true,
                        false,
                        false
                    );
                }
            }
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

            quadMesh?.Dispose();
            fillMesh?.Dispose(); 
            base.Dispose();
        }
        // Универсальный искатель интерфейсов
        private T GetInterface<T>(BlockEntity be) where T : class
        {
            if (be == null) return null;

            // Сначала ищем в самой сущности
            if (be is T entityInterface) return entityInterface;

            // Затем перебираем поведения
            foreach (var behavior in be.Behaviors)
            {
                if (behavior is T behaviorInterface) return behaviorInterface;
            }

            return null;
        }
    }
}