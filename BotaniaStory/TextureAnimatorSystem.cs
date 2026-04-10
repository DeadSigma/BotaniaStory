using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using OpenTK.Graphics.OpenGL;

namespace BotaniaStory
{
    public class TextureAnimatorSystem : ModSystem
    {
        private ICoreClientAPI capi;
        private long tickListenerId;

        private class AnimationData
        {
            public string AnimLoc;
            public string BaseLoc;
            public int NumFrames;
            public float TimePerFrame;

            public float FrameTime = 0;
            public int CurrentFrame = 0;

            // Для атласа сущностей (в мире)
            public TextureAtlasPosition EntityAnimPos;
            public TextureAtlasPosition EntityBasePos;
            public LoadedTexture EntityAnimTexture;
            public LoadedTexture EntityBaseTexture;

            // Для атласа предметов (в инвентаре)
            public TextureAtlasPosition ItemAnimPos;
            public TextureAtlasPosition ItemBasePos;
            public LoadedTexture ItemAnimTexture;
            public LoadedTexture ItemBaseTexture;
        }

        private List<AnimationData> animations = new List<AnimationData>();

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            animations.Add(new AnimationData()
            {
                AnimLoc = "botaniastory:entity/spark_anim",
                BaseLoc = "botaniastory:entity/spark_base",
                NumFrames = 7,
                TimePerFrame = 0.1f
            });

            capi.Event.BlockTexturesLoaded += OnBlockTexturesLoaded;
        }

        private void OnBlockTexturesLoaded()
        {
            foreach (var anim in animations)
            {
                AssetLocation animAsset = new AssetLocation(anim.AnimLoc);
                AssetLocation baseAsset = new AssetLocation(anim.BaseLoc);

                // 1. Загружаем в атлас СУЩНОСТЕЙ (для мира)
                capi.EntityTextureAtlas.GetOrInsertTexture(animAsset, out _, out anim.EntityAnimPos);
                capi.EntityTextureAtlas.GetOrInsertTexture(baseAsset, out _, out anim.EntityBasePos);
                anim.EntityAnimTexture = capi.EntityTextureAtlas.AtlasTextures[anim.EntityAnimPos.atlasNumber];
                anim.EntityBaseTexture = capi.EntityTextureAtlas.AtlasTextures[anim.EntityBasePos.atlasNumber];

                // 2. Загружаем в атлас ПРЕДМЕТОВ (для инвентаря)
                capi.ItemTextureAtlas.GetOrInsertTexture(animAsset, out _, out anim.ItemAnimPos);
                capi.ItemTextureAtlas.GetOrInsertTexture(baseAsset, out _, out anim.ItemBasePos);
                anim.ItemAnimTexture = capi.ItemTextureAtlas.AtlasTextures[anim.ItemAnimPos.atlasNumber];
                anim.ItemBaseTexture = capi.ItemTextureAtlas.AtlasTextures[anim.ItemBasePos.atlasNumber];
            }

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

                    // Обновляем текстуру для модели в мире
                    RenderFrameToAtlas(anim.EntityAnimTexture, anim.EntityAnimPos, anim.EntityBaseTexture, anim.EntityBasePos, anim.NumFrames, anim.CurrentFrame);

                    // Обновляем текстуру для иконки в инвентаре
                    RenderFrameToAtlas(anim.ItemAnimTexture, anim.ItemAnimPos, anim.ItemBaseTexture, anim.ItemBasePos, anim.NumFrames, anim.CurrentFrame);

                    didRender = true;
                }
            }

            if (didRender)
            {
                // Обновляем мипмапы для обоих атласов
                capi.EntityTextureAtlas.RegenMipMaps(0);
                capi.ItemTextureAtlas.RegenMipMaps(0);
            }
        }

        // Вынес логику в универсальный метод, принимающий конкретные текстуры
        private void RenderFrameToAtlas(LoadedTexture srcTex, TextureAtlasPosition srcPos, LoadedTexture dstTex, TextureAtlasPosition dstPos, int numFrames, int currentFrame)
        {
            if (srcTex == null || dstTex == null || srcTex.TextureId == 0 || dstTex.TextureId == 0) return;

            // Считаем координаты
            float frameHeightUV = (srcPos.y2 - srcPos.y1) / numFrames;
            float frameWidthUV = srcPos.x2 - srcPos.x1;

            int srcX = (int)MathF.Round(srcTex.Width * srcPos.x1);
            int srcY = (int)MathF.Round(srcTex.Height * (srcPos.y1 + frameHeightUV * currentFrame));
            int srcW = (int)MathF.Round(srcTex.Width * frameWidthUV);
            int srcH = (int)MathF.Round(srcTex.Height * frameHeightUV);

            int dstX = (int)MathF.Round(dstTex.Width * dstPos.x1);
            int dstY = (int)MathF.Round(dstTex.Height * dstPos.y1);

            // Сохраняем текущий буфер
            GL.GetInteger(GetPName.FramebufferBinding, out int originBufferId);

            // Используем временные буферы
            int readFbo = GL.GenFramebuffer();
            int drawFbo = GL.GenFramebuffer();

            // Чтение
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readFbo);
            GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, srcTex.TextureId, 0);

            // Запись
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawFbo);
            GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, dstTex.TextureId, 0);

            // Проверка готовности (важно для предотвращения InvalidOperation)
            if (GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer) == FramebufferErrorCode.FramebufferComplete &&
                GL.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer) == FramebufferErrorCode.FramebufferComplete)
            {
                GL.BlitFramebuffer(srcX, srcY, srcX + srcW, srcY + srcH, dstX, dstY, dstX + srcW, dstY + srcH, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            }

            // ОЧИСТКА ПЕРЕД УДАЛЕНИЕМ
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

            GL.DeleteFramebuffer(readFbo);
            GL.DeleteFramebuffer(drawFbo);

            // Возвращаем исходный буфер
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, originBufferId);
        }

        public override void Dispose()
        {
            if (capi != null)
            {
                capi.Event.UnregisterGameTickListener(tickListenerId);
            }
            animations.Clear();
            base.Dispose();
        }
    }
}