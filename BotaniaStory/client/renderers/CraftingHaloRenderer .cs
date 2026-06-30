using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using BotaniaStory.items;

namespace BotaniaStory.client.renderers
{
    public class CraftingHaloRenderer : IRenderer
    {
        readonly ICoreClientAPI capi;
        readonly LoadedTexture glowTex;
        readonly LoadedTexture glowTexAuto;
        MeshRef ringRef;
        readonly Matrixf modelMat = new();
        readonly Matrixf itemMat = new();
        readonly Dictionary<string, MeshRef> itemMeshCache = [];
        float accumSec;
        bool ringWasActive;

        // геометрия кольца
        const float R_OUT = 6.0f;
        const float R_IN = 3.0f * 0.8f;
        const float Y_INNER = 0.0f;
        const float Y_OUTER = 1.5f;
        const float Y_OUTER_SEL = 3.0f;
        const int STEPS = 30;
        const float YAW_OFFSET_DEG = 0f;

        // предметы в слотах
        const float ITEM_RADIUS = 2.6f;
        const float ITEM_Y = 1.5f;
        const float ITEM_SCALE = 0.6f;

        public double RenderOrder => 0.5;
        public int RenderRange => 100;

        public CraftingHaloRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;

            glowTex = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/halo_glow.png"), ref glowTex);

            glowTexAuto = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/gui/halo_glow_automatic.png"), ref glowTexAuto);
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            accumSec += dt;

            EntityPlayer ply = capi.World?.Player?.Entity;
            ItemStack held = capi.World?.Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack;
            bool holdingHalo = ply != null && held?.Collectible is ItemCraftingHalo;

            // Меню только что открылось -> якорим сегмент 0 (верстак) к текущему взгляду
            if (holdingHalo && !ringWasActive)
                ItemCraftingHalo.SetAnchorYaw(capi, ply.Pos.Yaw);
            ringWasActive = holdingHalo;

            if (!holdingHalo) return;

            float ringRotDeg = ItemCraftingHalo.GetRingRotDeg(ply);
            int selected = ItemCraftingHalo.GetLookedAtSegment(ply);
            float pulse = ((float)Math.Sin(accumSec * 4.0) * 0.5f + 0.5f) * 0.4f + 0.3f;

            // Обычное или автоматическое гало -> своя текстура свечения.
            bool isAuto = held.Collectible.Code.Path.Contains("auto");
            int activeTexId = isAuto ? glowTexAuto.TextureId : glowTex.TextureId;

            RenderGlowRing(ply, selected, pulse, activeTexId, ringRotDeg);
            RenderSlotItems(ply, held, ringRotDeg);
        }

        // кольцо
        void RenderGlowRing(EntityPlayer ply, int selected, float pulse, int activeTexId, float ringRotDeg)
        {
            BuildRing(selected, pulse, ringRotDeg);
            if (ringRef == null) return;

            IRenderAPI rpi = capi.Render;
            Vec3d cam = ply.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);
            rpi.GLDepthMask(false);

            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.Tex2D = activeTexId;
            prog.RgbaTint = new Vec4f(1, 1, 1, 1);   // нейтральный тинт: цвет даёт текстура
            prog.NormalShaded = 0;
            prog.ExtraGlow = 0;                       // glow выключен
            prog.AlphaTest = 0.01f;
            prog.RgbaAmbientIn = new Vec3f(1, 1, 1);  // умножающий fullbright: кольцо видно и ночью
            prog.RgbaFogIn = new Vec4f(1, 1, 1, 0);
            prog.FogMinIn = 0f;
            prog.FogDensityIn = 0f;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ModelMatrix = modelMat
                .Identity()
                .Translate((float)(ply.Pos.X - cam.X), (float)(ply.Pos.Y - cam.Y), (float)(ply.Pos.Z - cam.Z))
                .Values;
            rpi.RenderMesh(ringRef);
            prog.Stop();

            rpi.GLDepthMask(true);
            rpi.GlEnableCullFace();
        }

        // предметы-результаты в слотах
        void RenderSlotItems(EntityPlayer ply, ItemStack halo, float ringRotDeg)
        {
            IWorldAccessor world = capi.World;
            Vec3d cam = ply.CameraPos;
            float segDeg = 360f / ItemCraftingHalo.SEGMENTS;
            float spin = accumSec * 35f;

            for (int s = 0; s < ItemCraftingHalo.SEGMENTS; s++)
            {
                // слот 0 - сам halo (узел-«верстак»); слоты 1..11 - результат рецепта
                ItemStack shown = (s == 0) ? halo : ItemCraftingHalo.GetSegmentOutput(halo, world, s);
                if (shown?.Collectible == null) continue;

                float ang = (s * segDeg + segDeg * 0.5f + YAW_OFFSET_DEG + ringRotDeg) * GameMath.DEG2RAD;
                float bob = (float)Math.Sin(accumSec * 2.0 + s) * 0.06f;
                double lx = Math.Cos(ang) * ITEM_RADIUS;
                double lz = Math.Sin(ang) * ITEM_RADIUS;

                DrawItem(ply, cam, shown, lx, ITEM_Y + bob, lz, ITEM_SCALE, spin);
            }
        }

        // Отрисовать ItemStack в точке (локальные смещения от игрока)
        void DrawItem(EntityPlayer ply, Vec3d cam, ItemStack stack, double lx, double ly, double lz, float scale, float spinDeg)
        {
            IRenderAPI rpi = capi.Render;
            double wx = ply.Pos.X + lx, wy = ply.Pos.Y + ly, wz = ply.Pos.Z + lz;
            bool isBlock = stack.Class == EnumItemClass.Block;

            // Общая матрица: меш в [0..1], крутим и масштабируем вокруг центра
            float[] model = itemMat
                .Identity()
                .Translate((float)(wx - cam.X), (float)(wy - cam.Y), (float)(wz - cam.Z))
                .RotateY(spinDeg * GameMath.DEG2RAD)
                .Scale(scale, scale, scale)
                .Translate(-0.5f, -0.5f, -0.5f)
                .Values;

            if (isBlock)
            {
                // Блоки - инвентарным мешем: учитывает атрибуты (полки) и инвентарную форму
                // (полная кровать/корыто вместо мирового обрубка одной клетки)
                ItemRenderInfo ri = rpi.GetItemStackRenderInfo(new DummySlot(stack), EnumItemRenderTarget.Ground, 0f);
                if (ri?.ModelRef == null) return;

                IStandardShaderProgram prog = rpi.PreparedStandardShader((int)wx, (int)wy, (int)wz);
                prog.Use();
                prog.RgbaTint = new Vec4f(1, 1, 1, 1);
                prog.ExtraGlow = 0;
                prog.AlphaTest = ri.AlphaTest;
                prog.NormalShaded = ri.NormalShaded ? 1 : 0;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ModelMatrix = model;

                rpi.GlDisableCullFace();
                rpi.RenderMultiTextureMesh(ri.ModelRef, "tex");
                rpi.GlEnableCullFace();
                prog.Stop();
                return;
            }

            // Предметы - прежним проверенным путём (Tesselate + BindTexture2d + RenderMesh)
            string key = "i-" + stack.Id;
            if (!itemMeshCache.TryGetValue(key, out MeshRef meshRef))
            {
                capi.Tesselator.TesselateItem(stack.Item, out MeshData md);
                if (md == null) return;
                meshRef = capi.Render.UploadMesh(md);
                itemMeshCache[key] = meshRef;
            }

            IStandardShaderProgram iprog = rpi.PreparedStandardShader((int)wx, (int)wy, (int)wz);
            iprog.Use();
            iprog.RgbaTint = new Vec4f(1, 1, 1, 1);
            iprog.ExtraGlow = 0;
            iprog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            iprog.ViewMatrix = rpi.CameraMatrixOriginf;
            iprog.ModelMatrix = model;
            rpi.BindTexture2d(capi.ItemTextureAtlas.AtlasTextures[0].TextureId);
            rpi.RenderMesh(meshRef);
            iprog.Stop();
        }

        // меш кольца
        void BuildRing(int selected, float pulse, float ringRotDeg)
        {
            int quads = ItemCraftingHalo.SEGMENTS * STEPS;
            int vCount = quads * 4;
            int iCount = quads * 6;

            MeshData mesh = new(vCount, iCount, false, true, true, true)
            {
                xyz = new float[vCount * 3],
                Uv = new float[vCount * 2],
                Rgba = new byte[vCount * 4],
                Flags = new int[vCount],
                Indices = new int[iCount]
            };

            int vp = 0, ip = 0;
            float segDeg = 360f / ItemCraftingHalo.SEGMENTS;

            for (int seg = 0; seg < ItemCraftingHalo.SEGMENTS; seg++)
            {
                bool sel = seg == selected;
                float yOuter = sel ? Y_OUTER_SEL : Y_OUTER;

                // Цвет кольца даёт текстура halo_glow; вершины белые, чтобы её не тонировать
                byte r = 255, g = 255, b = 255;

                float a = Math.Min(1f, pulse + (sel ? 0.3f : 0f));
                byte ab = (byte)(a * 255);

                for (int st = 0; st < STEPS; st++)
                {
                    float a0 = (seg * segDeg) + (st * segDeg / STEPS) + YAW_OFFSET_DEG + ringRotDeg;
                    float a1 = (seg * segDeg) + ((st + 1) * segDeg / STEPS) + YAW_OFFSET_DEG + ringRotDeg;
                    float r0 = a0 * GameMath.DEG2RAD, r1 = a1 * GameMath.DEG2RAD;

                    float cos0 = (float)Math.Cos(r0), sin0 = (float)Math.Sin(r0);
                    float cos1 = (float)Math.Cos(r1), sin1 = (float)Math.Sin(r1);

                    int bidx = vp;
                    float vMax = 0.25f;

                    Put(mesh, vp++, R_IN * cos0, Y_INNER, R_IN * sin0, 0, vMax, r, g, b, ab);
                    Put(mesh, vp++, R_OUT * cos0, yOuter, R_OUT * sin0, 0, 0, r, g, b, ab);
                    Put(mesh, vp++, R_OUT * cos1, yOuter, R_OUT * sin1, 1, 0, r, g, b, ab);
                    Put(mesh, vp++, R_IN * cos1, Y_INNER, R_IN * sin1, 1, vMax, r, g, b, ab);

                    mesh.Indices[ip++] = bidx + 0; mesh.Indices[ip++] = bidx + 1; mesh.Indices[ip++] = bidx + 2;
                    mesh.Indices[ip++] = bidx + 0; mesh.Indices[ip++] = bidx + 2; mesh.Indices[ip++] = bidx + 3;
                }
            }

            mesh.VerticesCount = vCount;
            mesh.IndicesCount = iCount;

            if (ringRef == null) ringRef = capi.Render.UploadMesh(mesh);
            else capi.Render.UpdateMesh(ringRef, mesh);
        }

        static void Put(MeshData m, int i, float x, float y, float z, float u, float v, byte r, byte g, byte b, byte a)
        {
            m.xyz[i * 3 + 0] = x; m.xyz[i * 3 + 1] = y; m.xyz[i * 3 + 2] = z;
            m.Uv[i * 2 + 0] = u; m.Uv[i * 2 + 1] = v;
            m.Rgba[i * 4 + 0] = r; m.Rgba[i * 4 + 1] = g; m.Rgba[i * 4 + 2] = b; m.Rgba[i * 4 + 3] = a;
            m.Flags[i] = 0; // младший байт = glow вершины; держим в нуле
        }

        public void Dispose()
        {
            ringRef?.Dispose();
            glowTex?.Dispose();
            glowTexAuto?.Dispose();
            foreach (MeshRef mr in itemMeshCache.Values) mr?.Dispose();
            itemMeshCache.Clear();
            GC.SuppressFinalize(this);
        }
    }
}