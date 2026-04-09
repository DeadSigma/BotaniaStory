using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using OpenTK.Graphics.OpenGL; // Необходим для работы с буферами кадров

namespace BotaniaStory
{
    public class TextureAnimatorSystem : ModSystem
    {
        private ICoreClientAPI capi;
        private long tickListenerId;

        // Контейнер для хранения данных об анимации
        private class AnimationData
        {
            public string AnimLoc;
            public string BaseLoc;
            public int NumFrames;
            public float TimePerFrame;

            public float FrameTime = 0;
            public int CurrentFrame = 0;

            public TextureAtlasPosition AnimPos;
            public TextureAtlasPosition BasePos;
            public LoadedTexture AnimTexture;
            public LoadedTexture BaseTexture;
        }

        private List<AnimationData> animations = new List<AnimationData>();

        // Этот код нужен только на стороне клиента (для рендера)
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            // ЗДЕСЬ ТЫ МОЖЕШЬ ДОБАВЛЯТЬ ЛЮБЫЕ АНИМИРОВАННЫЕ БЛОКИ
            animations.Add(new AnimationData()
            {
                AnimLoc = "botaniastory:entity/spark_anim", // Лента кадров (128x896)
                BaseLoc = "botaniastory:entity/spark_base", // Базовый кадр в модели (128x128)
                NumFrames = 7,                             // Количество кадров
                TimePerFrame = 0.1f                        // Задержка (100мс)
            });

            // Ждем, пока игра соберет текстурный атлас, чтобы вклиниться в него
            capi.Event.BlockTexturesLoaded += OnBlockTexturesLoaded;
        }

        private void OnBlockTexturesLoaded()
        {
            foreach (var anim in animations)
            {
                AssetLocation animAsset = new AssetLocation(anim.AnimLoc);
                AssetLocation baseAsset = new AssetLocation(anim.BaseLoc);


                // Принудительно загружаем обе текстуры в атлас СУЩНОСТЕЙ игры
                capi.EntityTextureAtlas.GetOrInsertTexture(animAsset, out _, out anim.AnimPos);
                capi.EntityTextureAtlas.GetOrInsertTexture(baseAsset, out _, out anim.BasePos);

                anim.AnimTexture = capi.EntityTextureAtlas.AtlasTextures[anim.AnimPos.atlasNumber];
                anim.BaseTexture = capi.EntityTextureAtlas.AtlasTextures[anim.BasePos.atlasNumber];
            }

            // Запускаем таймер (50мс — оптимально для проверки кадров)
            tickListenerId = capi.Event.RegisterGameTickListener(OnTick, 50);
        }

        private void OnTick(float dt)
        {
            bool didRender = false;

            foreach (var anim in animations)
            {
                anim.FrameTime += dt;
                if (anim.FrameTime >= anim.TimePerFrame)
                {
                    anim.FrameTime -= anim.TimePerFrame;
                    anim.CurrentFrame = (anim.CurrentFrame + 1) % anim.NumFrames;

                    RenderFrameToAtlas(anim);
                    didRender = true;
                }
            }

            // Если хоть одна текстура обновилась, заставляем игру перерисовать мипмапы атласа сущностей
            if (didRender)
            {
                capi.EntityTextureAtlas.RegenMipMaps(0);
            }
        }

        private void RenderFrameToAtlas(AnimationData anim)
        {
            // --- ВЫТЯЖКА ИЗ LibATex ---
            // Эта функция вычисляет нужный кусок ленты и копирует его поверх базовой текстуры

            // 1. Вычисляем координаты текущего кадра на ленте (в UV пространстве)
            float frameHeightUV = (anim.AnimPos.y2 - anim.AnimPos.y1) / anim.NumFrames;
            float frameWidthUV = anim.AnimPos.x2 - anim.AnimPos.x1;

            // 2. Переводим UV в точные пиксели (Источник: лента кадров)
            int srcX = (int)MathF.Round(anim.AnimTexture.Width * anim.AnimPos.x1);
            int srcY = (int)MathF.Round(anim.AnimTexture.Height * (anim.AnimPos.y1 + frameHeightUV * anim.CurrentFrame));
            int srcW = (int)MathF.Round(anim.AnimTexture.Width * frameWidthUV);
            int srcH = (int)MathF.Round(anim.AnimTexture.Height * frameHeightUV);

            // 3. Цель в атласе (базовый квадрат)
            int dstX = (int)MathF.Round(anim.BaseTexture.Width * anim.BasePos.x1);
            int dstY = (int)MathF.Round(anim.BaseTexture.Height * anim.BasePos.y1);

            // 4. Магия OpenGL (Копирование пикселей)
            int originBufferId;
            GL.GetInteger(GetPName.FramebufferBinding, out originBufferId);

            int readBuf, drawBuf;
            GL.GenFramebuffers(1, out readBuf);
            GL.GenFramebuffers(1, out drawBuf);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readBuf);
            GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, anim.AnimTexture.TextureId, 0);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawBuf);
            GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, anim.BaseTexture.TextureId, 0);

            GL.BlitFramebuffer(srcX, srcY, srcX + srcW, srcY + srcH, dstX, dstY, dstX + srcW, dstY + srcH, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

            // Очищаем мусор
            GL.DeleteFramebuffer(readBuf);
            GL.DeleteFramebuffer(drawBuf);

            // Возвращаем контроль над рендером игре
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, originBufferId);
        }

        public override void Dispose()
        {
            // Важно освобождать ресурсы при выходе из мира
            if (capi != null)
            {
                capi.Event.UnregisterGameTickListener(tickListenerId);
            }
            base.Dispose();
        }
    }
}