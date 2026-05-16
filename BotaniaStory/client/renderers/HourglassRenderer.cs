using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.renderers
{
    public class HourglassRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private BlockPos pos;
        private blockentity.BlockEntityHourglass be;

        private MeshRef hourglassBaseMeshRef;

        // Разделяем песок на данные и загруженные меши для динамического обрезания
        private MeshData originalSandMesh;
        private MeshData topSandData;
        private MeshData bottomSandData;
        private MeshRef topSandMeshRef;
        private MeshRef bottomSandMeshRef;

        private string loadedSandCode = "";

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public HourglassRenderer(ICoreClientAPI capi, BlockPos pos, blockentity.BlockEntityHourglass be)
        {
            this.capi = capi;
            this.pos = pos;
            this.be = be;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "botania_hourglass");
        }

        private void EnsureMeshLoaded()
        {
            if (hourglassBaseMeshRef == null)
            {
                MeshData baseMesh = capi.TesselatorManager.GetDefaultBlockMesh(be.Block).Clone();
                if (baseMesh.Rgba != null)
                {
                    for (int i = 0; i < baseMesh.Rgba.Length; i++) baseMesh.Rgba[i] = 255;
                }
                hourglassBaseMeshRef = capi.Render.UploadMesh(baseMesh);
            }

            if (string.IsNullOrEmpty(be.SandBlockCode)) return;

            if (originalSandMesh == null || loadedSandCode != be.SandBlockCode)
            {
                Block sandBlock = capi.World.GetBlock(new AssetLocation(be.SandBlockCode));
                if (sandBlock == null) return;

                originalSandMesh = capi.TesselatorManager.GetDefaultBlockMesh(sandBlock).Clone();
                if (originalSandMesh.Rgba != null)
                {
                    for (int i = 0; i < originalSandMesh.Rgba.Length; i++) originalSandMesh.Rgba[i] = 255;
                }

                // Создаем две независимые копии меша для верхней и нижней колбы
                topSandData = originalSandMesh.Clone();
                bottomSandData = originalSandMesh.Clone();

                topSandMeshRef?.Dispose();
                bottomSandMeshRef?.Dispose();

                // Загружаем их в видеокарту
                topSandMeshRef = capi.Render.UploadMesh(topSandData);
                bottomSandMeshRef = capi.Render.UploadMesh(bottomSandData);

                loadedSandCode = be.SandBlockCode;
            }
        }

        // Кадрирование песка
        private void UpdateSandMesh(MeshData target, float height)
        {
            height = Math.Max(0.001f, height);

            for (int i = 0; i < originalSandMesh.GetVerticesCount(); i += 4)
            {
                bool isTopFace = true;
                bool isBottomFace = true;

                // Определяем, какая это сторона куба по оригинальному мешу
                for (int j = 0; j < 4; j++)
                {
                    if (originalSandMesh.xyz[(i + j) * 3 + 1] < 0.99f) isTopFace = false;
                    if (originalSandMesh.xyz[(i + j) * 3 + 1] > 0.01f) isBottomFace = false;
                }

                if (isTopFace)
                {
                    // Просто опускаем верхнюю крышку вниз (текстура крышки не меняется)
                    for (int j = 0; j < 4; j++)
                    {
                        target.xyz[(i + j) * 3 + 1] = height;
                    }
                }
                else if (!isBottomFace)
                {
                    // Для боковых сторон: вычисляем UV координаты оригинальной текстуры
                    float vTopAvg = 0, vBottomAvg = 0;
                    int tCount = 0, bCount = 0;
                    for (int j = 0; j < 4; j++)
                    {
                        if (originalSandMesh.xyz[(i + j) * 3 + 1] > 0.5f) { vTopAvg += originalSandMesh.Uv[(i + j) * 2 + 1]; tCount++; }
                        else { vBottomAvg += originalSandMesh.Uv[(i + j) * 2 + 1]; bCount++; }
                    }
                    if (tCount > 0) vTopAvg /= tCount;
                    if (bCount > 0) vBottomAvg /= bCount;

                    // Опускаем верхние точки и обрезаем текстуру так, чтобы она не сжималась
                    for (int j = 0; j < 4; j++)
                    {
                        if (originalSandMesh.xyz[(i + j) * 3 + 1] > 0.5f)
                        {
                            target.xyz[(i + j) * 3 + 1] = height;
                            target.Uv[(i + j) * 2 + 1] = vBottomAvg - (vBottomAvg - vTopAvg) * height;
                        }
                    }
                }
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            EnsureMeshLoaded();
            if (hourglassBaseMeshRef == null) return;

            IRenderAPI render = capi.Render;
            IStandardShaderProgram prog = render.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            prog.RgbaLightIn = capi.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y + 1, pos.Z);
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.NormalShaded = 1;

            int blockAtlasId = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

            prog.ViewMatrix = render.CameraMatrixOriginf;
            prog.ProjectionMatrix = render.CurrentProjectionMatrix;

            // 1. Высчитываем смещение парения
            long timeMs = capi.World.ElapsedMilliseconds;
            float bobbingSpeed = 800f;
            float bobbingAmplitude = 0.08f;
            float yOffset = (float)Math.Sin(timeMs / bobbingSpeed) * bobbingAmplitude;
            // Высота часов
            float hoverHeight = 0.15f;

            // 2. Формируем базовую матрицу
            float pivotY = 0.28414f;
            float[] baseMatrix = Mat4f.Create();
            Mat4f.Identity(baseMatrix);

            // 3. Добавляем и yOffset, и hoverHeight к оси Y
            Mat4f.Translate(baseMatrix, baseMatrix,
                (float)(pos.X - capi.World.Player.Entity.CameraPos.X) + 0.5f,
                (float)(pos.Y - capi.World.Player.Entity.CameraPos.Y) + pivotY + yOffset + hoverHeight, 
                (float)(pos.Z - capi.World.Player.Entity.CameraPos.Z) + 0.5f
            );

            if (be.IsFlipping)
            {
                float angle = be.FlipProgress * (float)Math.PI;
                Mat4f.RotateX(baseMatrix, baseMatrix, angle);
            }

            Mat4f.Translate(baseMatrix, baseMatrix, -0.5f, -pivotY, -0.5f);

            if (stage == EnumRenderStage.Opaque)
            {
                // 1. РИСУЕМ ЗОЛОТО
                prog.AlphaTest = 0.99f;
                prog.Tex2D = blockAtlasId;
                prog.ModelMatrix = baseMatrix;
                render.RenderMesh(hourglassBaseMeshRef);

                // 2. РИСУЕМ ПЕСОК
                if (be.SandCount > 0 && originalSandMesh != null)
                {
                    Block sandBlock = capi.World.GetBlock(new AssetLocation(be.SandBlockCode));
                    int sandAtlasId = capi.BlockTextureAtlas.GetPosition(sandBlock, "up").atlasTextureId;

                    prog.AlphaTest = 0.05f;
                    prog.Tex2D = sandAtlasId;

                    float topFill = be.IsFlipping ? 0f : (1.0f - be.TimerProgress);
                    float bottomFill = be.IsFlipping ? 1f : be.TimerProgress;

                    // ОБНОВЛЯЕМ МЕШИ: физически обрезаем их по высоте и отправляем в видеокарту
                    UpdateSandMesh(topSandData, topFill);
                    render.UpdateMesh(topSandMeshRef, topSandData);

                    UpdateSandMesh(bottomSandData, bottomFill);
                    render.UpdateMesh(bottomSandMeshRef, bottomSandData);

                    // Динамическая высота уже зашита в саму геометрию меша.
                    DrawSand(prog, baseMatrix, 0.395f, 0.314f, 0.395f, 0.210f, 0.1875f, 0.210f, topSandMeshRef, topFill);
                    DrawSand(prog, baseMatrix, 0.395f, 0.066f, 0.395f, 0.210f, 0.1875f, 0.210f, bottomSandMeshRef, bottomFill);
                }

                // 3. РИСУЕМ СТЕКЛО
                render.GlToggleBlend(true, EnumBlendMode.Standard);
                prog.AlphaTest = 0.01f;
                prog.Tex2D = blockAtlasId;
                prog.ModelMatrix = baseMatrix;
                render.RenderMesh(hourglassBaseMeshRef);
                render.GlToggleBlend(false);
            }

            prog.Stop();
        }

        private void DrawSand(IStandardShaderProgram prog, float[] baseMatrix, float x, float y, float z, float w, float h, float l, MeshRef meshRef, float fill)
        {
            if (fill <= 0.001f) return;
            float[] modelMatrix = Mat4f.CloneIt(baseMatrix);
            Mat4f.Translate(modelMatrix, modelMatrix, x, y, z);
            Mat4f.Scale(modelMatrix, modelMatrix, new float[] { w, h, l });
            prog.ModelMatrix = modelMatrix;
            capi.Render.RenderMesh(meshRef); // Отрисовываем конкретный (обрезанный) меш
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            topSandMeshRef?.Dispose();
            bottomSandMeshRef?.Dispose();
            hourglassBaseMeshRef?.Dispose();
        }
    }
}