using BotaniaStory.items;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace BotaniaStory.client.ui
{
    public class TerraShattererHud : IRenderer
    {
        private ICoreClientAPI capi;
        private LoadedTexture bgTex;
        private LoadedTexture fgTex;

        // Кэш для цифр, чтобы не убить FPS
        private Dictionary<int, LoadedTexture> levelTextures = new Dictionary<int, LoadedTexture>();
        private CairoFont levelFont;

        public double RenderOrder => 1.1;
        public int RenderRange => 1;

        public TerraShattererHud(ICoreClientAPI capi)
        {
            this.capi = capi;
            bgTex = new LoadedTexture(capi);
            fgTex = new LoadedTexture(capi);

            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/terrashatterer_bg.png"), ref bgTex);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/terrashatterer_fg.png"), ref fgTex);

            // Ванильный шрифт (Белый цвет, размер 14, черная обводка)
            levelFont = CairoFont.WhiteSmallText().WithFontSize(14).WithStroke(new double[] { 0, 0, 0, 1 }, 2);
        }
        // Вспомогательный метод для быстрой отрисовки текста
        private LoadedTexture GetOrCreateLevelTexture(int level)
        {
            if (!levelTextures.TryGetValue(level, out LoadedTexture tex))
            {
                TextTextureUtil textUtil = new TextTextureUtil(capi);

                // Метод сам создает и возвращает готовую текстуру
                tex = textUtil.GenTextTexture(level.ToString(), levelFont);

                levelTextures[level] = tex;
            }
            return tex;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            ItemSlot targetSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
            if (targetSlot == null || targetSlot.Empty || targetSlot.Itemstack?.Collectible == null) return;

            if (!targetSlot.Itemstack.Collectible.Code.Path.Contains("pickaxe-terrashatterer")) return;

            float scale = (float)RuntimeEnv.GUIScale;
            float height = (float)Math.Round(12f * scale);

            // 1. ФОЛЛБЭК КООРДИНАТЫ 
            float exactTooltipWidth = 380f * scale;
            float exactTooltipX = capi.Input.MouseX + (15f * scale);
            float exactTooltipY = capi.Input.MouseY + (50f * scale);

            // 2. КРАЖА КООРДИНАТ У ДВИЖКА
            GuiDialog hudMouseTools = capi.Gui.LoadedGuis.FirstOrDefault(d => d.DebugName == "HudMouseTools");
            GuiComposer compo = hudMouseTools?.Composers["itemstackinfo"];

            if (compo != null)
            {
                GuiElement elem1 = compo.GetElement("itemstackinfo1");
                GuiElement elem2 = compo.GetElement("itemstackinfo2");

                if (elem1 != null && elem2 != null)
                {
                    GuiElement activeElem = elem1;

                    var renderField = elem1.GetType().GetField("Render");
                    if (renderField != null)
                    {
                        bool isElem1Active = (bool)renderField.GetValue(elem1);
                        activeElem = isElem1Active ? elem1 : elem2;
                    }
                    else
                    {
                        var renderProp = elem1.GetType().GetProperty("Render");
                        if (renderProp != null)
                        {
                            bool isElem1Active = (bool)renderProp.GetValue(elem1);
                            activeElem = isElem1Active ? elem1 : elem2;
                        }
                    }

                    if (activeElem.Bounds.OuterWidth > 0 && activeElem.Bounds.renderY > 0)
                    {
                        exactTooltipWidth = (float)activeElem.Bounds.OuterWidth;
                        exactTooltipX = (float)activeElem.Bounds.renderX;
                        exactTooltipY = (float)activeElem.Bounds.renderY;
                    }
                }
            }

            // СВЯЗЬ С НОВОЙ ЛОГИКОЙ КИРКИ
            float currentValue = 0f;
            float maxValue = 1f; // Защита от деления на ноль
            int currentLevel = 0;

            // Проверяем, действительно ли мы смотрим на Землекрушитель
            if (targetSlot.Itemstack.Item is ItemTerraShatterer shatterer)
            {
                currentValue = shatterer.GetCurrentMana(targetSlot.Itemstack);
                maxValue = shatterer.GetMaxMana(targetSlot.Itemstack);

                // Надежно читаем ранг прямо из типа предмета (pickaxe-terrashatterer-0 -> достает 0)
                string rankStr = targetSlot.Itemstack.Item.Variant["rank"];
                int.TryParse(rankStr, out currentLevel);
            }

            float fillRatio = Math.Clamp(currentValue / maxValue, 0f, 1f);

            // 3. ПРИКЛЕИВАЕМ БАР

            float paddingLeft = -0.5f * scale;
            float paddingRight = 1f * scale;
            float gapY = 0f * scale;

            float x = (float)Math.Round(exactTooltipX + paddingLeft);
            float y = (float)Math.Round(exactTooltipY - height - gapY);
            float barWidth = (float)Math.Round(exactTooltipWidth - paddingLeft - paddingRight);

            // 4. ТЕКСТ УРОВНЕЙ
            int nextLevel = currentLevel + 1;

            // Получаем текстуры для цифр
            LoadedTexture currentLevelTex = GetOrCreateLevelTexture(currentLevel);
            LoadedTexture nextLevelTex = GetOrCreateLevelTexture(nextLevel);

            // Зазор от текста до полоски по оси Y (подгони, если нужно выше/ниже)
            float textGapY = 14f * scale;
            float textY = (float)Math.Round(y - textGapY);

            // Левая цифра
            float leftTextX = x;

            // Правая цифра (вычитаем её собственную ширину, чтобы она идеально встала по правому краю бара!)
            float rightTextX = (float)Math.Round(x + barWidth - nextLevelTex.Width);


            // 5. ОТРИСОВКА
            // 
            capi.Render.GlPushMatrix();
            capi.Render.GlTranslate(0f, 0f, 1000f);

            // Фон
            capi.Render.Render2DTexturePremultipliedAlpha(bgTex.TextureId, x, y, barWidth, height, 998f, null);

            // Заливка
            float fillWidth = barWidth * fillRatio;
            ElementBounds scissor = ElementBounds.Fixed(x / scale, y / scale, fillWidth / scale, height / scale).WithEmptyParent();
            scissor.CalcWorldBounds();

            capi.Render.PushScissor(scissor, true);
            capi.Render.Render2DTexturePremultipliedAlpha(fgTex.TextureId, x, y, barWidth, height, 999f, null);
            capi.Render.PopScissor();

            // Рисуем цифры поверх всего (на высоте 999f)
            capi.Render.Render2DTexturePremultipliedAlpha(currentLevelTex.TextureId, leftTextX, textY, currentLevelTex.Width, currentLevelTex.Height, 999f, null);
            capi.Render.Render2DTexturePremultipliedAlpha(nextLevelTex.TextureId, rightTextX, textY, nextLevelTex.Width, nextLevelTex.Height, 999f, null);

            capi.Render.GlPopMatrix();
        }

        public void Dispose()
        {
            bgTex?.Dispose();
            fgTex?.Dispose();

            // Очищаем кэш цифр при закрытии игры
            foreach (var tex in levelTextures.Values)
            {
                tex?.Dispose();
            }
            levelTextures.Clear();
        }
    }
}