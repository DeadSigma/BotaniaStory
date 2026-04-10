using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using OpenTK.Graphics.OpenGL;

namespace BotaniaStory
{
    // Добавили IRenderer, чтобы система могла легально работать с графикой
    public class TextureAnimatorSystem : ModSystem, IRenderer
    {
        private ICoreClientAPI capi;

        // Кешируем ID буферов, чтобы не пересоздавать их каждый кадр
        private int readFbo;
        private int drawFbo;
        private bool fbosInitialized = false;

        // Настройки рендерера
        public double RenderOrder => 0.1; // Рендерим в самом начале кадра
        public int RenderRange => 999;

        private class AnimationData
        {
            public string AnimLoc;
            public string BaseLoc;
            public int NumFrames;
            public float TimePerFrame;

            public float FrameTime = 0;
            public int CurrentFrame = 0;

            public TextureAtlasPosition EntityAnimPos;
            public TextureAtlasPosition EntityBasePos;
            public LoadedTexture EntityAnimTexture;
            public LoadedTexture EntityBaseTexture;

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

            // Регистрируем наш рендерер на этапе Before, до того как игра начнет рисовать мир
            capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "textureanimator");
        }

        private void OnBlockTexturesLoaded()
        {
            foreach (var anim in animations)
            {
                AssetLocation animAsset = new AssetLocation(anim.AnimLoc);
                AssetLocation baseAsset = new AssetLocation(anim.BaseLoc);

                // Загружаем в атлас сущностей
                capi.EntityTextureAtlas.GetOrInsertTexture(animAsset, out _, out anim.EntityAnimPos);
                capi.EntityTextureAtlas.GetOrInsertTexture(baseAsset, out _, out anim.EntityBasePos);
                anim.EntityAnimTexture = capi.EntityTextureAtlas.AtlasTextures[anim.EntityAnimPos.atlasNumber];
                anim.EntityBaseTexture = capi.EntityTextureAtlas.AtlasTextures[anim.EntityBasePos.atlasNumber];

                // Загружаем в атлас предметов
                capi.ItemTextureAtlas.GetOrInsertTexture(animAsset, out _, out anim.ItemAnimPos);
                capi.ItemTextureAtlas.GetOrInsertTexture(baseAsset, out _, out anim.ItemBasePos);
                anim.ItemAnimTexture = capi.ItemTextureAtlas.AtlasTextures[anim.ItemAnimPos.atlasNumber];
                anim.ItemBaseTexture = capi.ItemTextureAtlas.AtlasTextures[anim.ItemBasePos.atlasNumber];
            }
        }

        // Этот метод теперь вызывается игрой каждый графический кадр
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            // Единожды создаем FBO в безопасном графическом потоке
            if (!fbosInitialized)
            {
                readFbo = GL.GenFramebuffer();
                drawFbo = GL.GenFramebuffer();
                fbosInitialized = true;
            }

            bool didRender = false;

            foreach (var anim in animations)
            {
                anim.FrameTime += deltaTime;
                if (anim.FrameTime >= anim.TimePerFrame)
                {
                    anim.FrameTime -= anim.TimePerFrame;
                    anim.CurrentFrame = (anim.CurrentFrame + 1) % anim.NumFrames;

                    RenderFrameToAtlas(anim.EntityAnimTexture, anim.EntityAnimPos, anim.EntityBaseTexture, anim.EntityBasePos, anim.NumFrames, anim.CurrentFrame);
                    RenderFrameToAtlas(anim.ItemAnimTexture, anim.ItemAnimPos, anim.ItemBaseTexture, anim.ItemBasePos, anim.NumFrames, anim.CurrentFrame);

                    didRender = true;
                }
            }

            if (didRender)
            {
                capi.EntityTextureAtlas.RegenMipMaps(0);
                capi.ItemTextureAtlas.RegenMipMaps(0);
            }
        }

        private void RenderFrameToAtlas(LoadedTexture srcTex, TextureAtlasPosition srcPos, LoadedTexture dstTex, TextureAtlasPosition dstPos, int numFrames, int currentFrame)
        {
            if (srcTex == null || dstTex == null || srcTex.TextureId == 0 || dstTex.TextureId == 0) return;

            float frameHeightUV = (srcPos.y2 - srcPos.y1) / numFrames;
            float frameWidthUV = srcPos.x2 - srcPos.x1;

            int srcX = (int)MathF.Round(srcTex.Width * srcPos.x1);
            int srcY = (int)MathF.Round(srcTex.Height * (srcPos.y1 + frameHeightUV * currentFrame));
            int srcW = (int)MathF.Round(srcTex.Width * frameWidthUV);
            int srcH = (int)MathF.Round(srcTex.Height * frameHeightUV);

            int dstX = (int)MathF.Round(dstTex.Width * dstPos.x1);
            int dstY = (int)MathF.Round(dstTex.Height * dstPos.y1);

            GL.GetInteger(GetPName.FramebufferBinding, out int originBufferId);

            // Используем заранее созданные буферы, а не создаем новые!
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readFbo);
            GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, srcTex.TextureId, 0);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawFbo);
            GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, dstTex.TextureId, 0);

            if (GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer) == FramebufferErrorCode.FramebufferComplete &&
                GL.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer) == FramebufferErrorCode.FramebufferComplete)
            {
                GL.BlitFramebuffer(srcX, srcY, srcX + srcW, srcY + srcH, dstX, dstY, dstX + srcW, dstY + srcH, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            }

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, originBufferId);
        }

        public override void Dispose()
        {
            if (capi != null)
            {
                capi.Event.UnregisterRenderer(this, EnumRenderStage.Before);
            }

            // Удаляем наши буферы только при выходе из игры
            if (fbosInitialized)
            {
                GL.DeleteFramebuffer(readFbo);
                GL.DeleteFramebuffer(drawFbo);
            }

            animations.Clear();
            base.Dispose();
        }
    }
}