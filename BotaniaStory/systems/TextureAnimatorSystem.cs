using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using OpenTK.Graphics.OpenGL;

namespace BotaniaStory.systems
{
    public class TextureAnimatorSystem : ModSystem, IRenderer
    {
        private readonly bool DebugGl = false;

        private ICoreClientAPI capi;

        private int readFbo;
        private int drawFbo;
        private bool fbosInitialized;

        public double RenderOrder => 0.1;
        public int RenderRange => 999;

        private class AnimationData
        {
            public AssetLocation AnimLoc;
            public AssetLocation BaseLoc;
            public int NumFrames;
            public float TimePerFrame;

            public float FrameTime;
            public int CurrentFrame;
        }

        private readonly List<AnimationData> animations = new List<AnimationData>();
        private readonly HashSet<int> texturesToUpdate = new HashSet<int>();

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            animations.Add(new AnimationData
            {
                AnimLoc = new AssetLocation("botaniastory:entity/spark_anim"),
                BaseLoc = new AssetLocation("botaniastory:entity/spark_base"),
                NumFrames = 7,
                TimePerFrame = 0.1f
            });

            animations.Add(new AnimationData
            {
                AnimLoc = new AssetLocation("botaniastory:block/alfheim_core_on_anim"),
                BaseLoc = new AssetLocation("botaniastory:block/alfheim_core_on_target"),
                NumFrames = 6,
                TimePerFrame = 0.1f
            });

            animations.Add(new AnimationData
            {
                AnimLoc = new AssetLocation("botaniastory:item/blackholetalisman_anim"),
                BaseLoc = new AssetLocation("botaniastory:item/blackholetalisman_base"),
                NumFrames = 5,
                TimePerFrame = 0.1f
            });

            animations.Add(new AnimationData
            {
                AnimLoc = new AssetLocation("botaniastory:block/alfheim_portal_anim"),
                BaseLoc = new AssetLocation("botaniastory:block/alfheim_portal_target"),
                NumFrames = 16,
                TimePerFrame = 0.05f
            });

            capi.Event.BlockTexturesLoaded += OnAtlasesLoaded;
            capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "textureanimator");
        }

        private void OnAtlasesLoaded()
        {
            foreach (var anim in animations)
            {
                InsertSafe(capi.ItemTextureAtlas, anim.AnimLoc);
                InsertSafe(capi.ItemTextureAtlas, anim.BaseLoc);
                InsertSafe(capi.BlockTextureAtlas, anim.AnimLoc);
                InsertSafe(capi.BlockTextureAtlas, anim.BaseLoc);
            }
        }

        private void InsertSafe(ITextureAtlasAPI atlas, AssetLocation loc)
        {
            if (atlas == null) return;
            try { atlas.GetOrInsertTexture(loc, out _, out _); }
            catch (Exception e) { capi.Logger.Warning("[BotaniaStory] texanim insert failed for {0}: {1}", loc, e.Message); }
        }

        /// <summary>
        /// Резолвим позицию и страницу атласа заново каждый раз.
        /// Если что-то не сходится - молча пропускаем кадр вместо записи в чужую текстуру.
        /// </summary>
        private bool TryResolve(ITextureAtlasAPI atlas, AssetLocation loc, out LoadedTexture tex, out TextureAtlasPosition pos)
        {
            tex = null;
            pos = null;

            if (atlas == null) return false;

            List<LoadedTexture> pages = atlas.AtlasTextures;
            if (pages == null || pages.Count == 0) return false;

            if (!atlas.GetOrInsertTexture(loc, out _, out pos) || pos == null)
            {
                pos = null;
                return false;
            }

            if (pos.atlasNumber < 0 || pos.atlasNumber >= pages.Count) { pos = null; return false; }

            tex = pages[pos.atlasNumber];
            if (tex == null || tex.TextureId == 0 || tex.Width <= 0 || tex.Height <= 0)
            {
                tex = null; pos = null; return false;
            }

            // Ключевая защита: id страницы и id из позиции обязаны совпадать, и GL должен подтвердить, что это всё ещё живой текстурный объект.
            if (pos.atlasTextureId != 0 && pos.atlasTextureId != tex.TextureId) { tex = null; pos = null; return false; }
            if (!GL.IsTexture(tex.TextureId)) { tex = null; pos = null; return false; }

            return true;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Before) return;
            if (capi?.World == null) return;

            if (!fbosInitialized)
            {
                readFbo = GL.GenFramebuffer();
                drawFbo = GL.GenFramebuffer();
                fbosInitialized = true;
            }

            texturesToUpdate.Clear();

            foreach (var anim in animations)
            {
                anim.FrameTime += deltaTime;
                if (anim.FrameTime < anim.TimePerFrame) continue;

                anim.FrameTime -= anim.TimePerFrame;
                if (anim.FrameTime > anim.TimePerFrame) anim.FrameTime = 0f; // защита от накопления при лагах
                anim.CurrentFrame = (anim.CurrentFrame + 1) % anim.NumFrames;

                BlitInto(capi.BlockTextureAtlas, anim);
                BlitInto(capi.ItemTextureAtlas, anim);
            }

            RegenerateMipmaps();

            if (DebugGl) capi.Render.CheckGlError("botaniastory:texanim");
        }

        private void BlitInto(ITextureAtlasAPI atlas, AnimationData anim)
        {
            if (!TryResolve(atlas, anim.AnimLoc, out LoadedTexture srcTex, out TextureAtlasPosition srcPos)) return;
            if (!TryResolve(atlas, anim.BaseLoc, out LoadedTexture dstTex, out TextureAtlasPosition dstPos)) return;

            if (RenderFrameToAtlas(srcTex, srcPos, dstTex, dstPos, anim.NumFrames, anim.CurrentFrame))
            {
                texturesToUpdate.Add(dstTex.TextureId);
            }
        }

        private bool RenderFrameToAtlas(LoadedTexture srcTex, TextureAtlasPosition srcPos, LoadedTexture dstTex, TextureAtlasPosition dstPos, int numFrames, int currentFrame)
        {
            float frameHeightUV = (srcPos.y2 - srcPos.y1) / numFrames;
            float frameWidthUV = srcPos.x2 - srcPos.x1;

            int srcX = (int)MathF.Round(srcTex.Width * srcPos.x1);
            int srcY = (int)MathF.Round(srcTex.Height * (srcPos.y1 + frameHeightUV * currentFrame));
            int srcW = (int)MathF.Round(srcTex.Width * frameWidthUV);
            int srcH = (int)MathF.Round(srcTex.Height * frameHeightUV);

            int dstX = (int)MathF.Round(dstTex.Width * dstPos.x1);
            int dstY = (int)MathF.Round(dstTex.Height * dstPos.y1);
            int dstW = (int)MathF.Round(dstTex.Width * (dstPos.x2 - dstPos.x1));
            int dstH = (int)MathF.Round(dstTex.Height * (dstPos.y2 - dstPos.y1));

            if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0) return false;

            // сохраняем состояние движка
            GL.GetInteger(GetPName.DrawFramebufferBinding, out int prevDrawFbo);
            GL.GetInteger(GetPName.ReadFramebufferBinding, out int prevReadFbo);
            bool scissorWasOn = GL.IsEnabled(EnableCap.ScissorTest);
            if (scissorWasOn) GL.Disable(EnableCap.ScissorTest);

            bool blitted = false;

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readFbo);
            GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, srcTex.TextureId, 0);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawFbo);
            GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, dstTex.TextureId, 0);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            if (GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer) == FramebufferErrorCode.FramebufferComplete &&
                GL.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer) == FramebufferErrorCode.FramebufferComplete)
            {
                GL.BlitFramebuffer(
                    srcX, srcY, srcX + srcW, srcY + srcH,
                    dstX, dstY, dstX + dstW, dstY + dstH,
                    ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
                blitted = true;
            }

            // Обязательно отцепляем чужие текстуры: наш FBO не должен держать ссылку на объект, который движок может удалить и переиспользовать
            GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, 0, 0);
            GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, 0, 0);

            // возвращаем состояние движка как было
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, prevReadFbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, prevDrawFbo);
            if (scissorWasOn) GL.Enable(EnableCap.ScissorTest);

            return blitted;
        }

        private void RegenerateMipmaps()
        {
            if (texturesToUpdate.Count == 0) return;

            GL.GetInteger(GetPName.ActiveTexture, out int prevUnit);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.GetInteger(GetPName.TextureBinding2D, out int prevTexOnUnit0);

            foreach (int texId in texturesToUpdate)
            {
                if (texId == 0 || !GL.IsTexture(texId)) continue;
                GL.BindTexture(TextureTarget.Texture2D, texId);
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            }

            GL.BindTexture(TextureTarget.Texture2D, prevTexOnUnit0);
            GL.ActiveTexture((TextureUnit)prevUnit);
        }

        public override void Dispose()
        {
            if (capi != null)
            {
                capi.Event.BlockTexturesLoaded -= OnAtlasesLoaded;
                capi.Event.UnregisterRenderer(this, EnumRenderStage.Before);
            }

            if (fbosInitialized)
            {
                GL.DeleteFramebuffer(readFbo);
                GL.DeleteFramebuffer(drawFbo);
                readFbo = 0;
                drawFbo = 0;
                fbosInitialized = false;
            }

            animations.Clear();
            texturesToUpdate.Clear();
            base.Dispose();
        }
    }
}