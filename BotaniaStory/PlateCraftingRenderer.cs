using OpenTK.Graphics.OpenGL;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class PlateCraftingRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private BlockPos pos;
        private BlockEntityTerrestrialPlate plate;

        private MeshRef quadMeshRef;
        private LoadedTexture particleTexture;
        public Matrixf ModelMat = new Matrixf();

        private MeshRef[] itemMeshRefs = new MeshRef[3];
        private ItemStack[] lastRenderedStacks = new ItemStack[3];

        private float currentProgress = 0f;
        private float rotationAngle = 0f;

        public double RenderOrder => 0.5;
        public int RenderRange => 64;



        public PlateCraftingRenderer(ICoreClientAPI api, BlockPos pos, BlockEntityTerrestrialPlate plate)
        {
            this.capi = api;
            this.pos = pos;
            this.plate = plate;
            LoadGraphics();
        }

        private void LoadGraphics()
        {
            particleTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/particle/mana_particle.png"), ref particleTexture);

            MeshData quad = QuadMeshUtil.GetCustomQuadModelData(-0.5f, -0.5f, 0, 1f, 1f);
            quad.Rgba = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
            quadMeshRef = capi.Render.UploadMesh(quad);
        }

        public void UpdateProgress(float progress)
        {
            currentProgress = progress;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {

            if (currentProgress <= 0 || quadMeshRef == null || particleTexture.TextureId == 0) return;

            // Крутим частицы постоянно. Скорость кручения можно увеличить.
            rotationAngle += deltaTime * 3f;

            IClientPlayer player = capi.World.Player;
            Vec3d camPos = player.Entity.CameraPos;

            IStandardShaderProgram prog = capi.Render.PreparedStandardShader((int)pos.X, (int)pos.Y, (int)pos.Z);

            capi.Render.BindTexture2d(particleTexture.TextureId);
            prog.Uniform("alphaTest", 0f);

            // Настройки свечения
            capi.Render.GlToggleBlend(true, EnumBlendMode.Glow);
            GL.DepthMask(false);

            // Радиус зависит от прогресса. При 0% радиус 1.5 блока. При 100% радиус 0.1 блока.
            float maxRadius = 1.5f;
            float minRadius = 0.1f;
            float currentRadius = maxRadius - ((maxRadius - minRadius) * currentProgress);


            for (int i = 0; i < 3; i++)
            {
                ItemSlot slot = plate.inventory[i];
                if (slot.Empty)
                {
                    itemMeshRefs[i]?.Dispose();
                    itemMeshRefs[i] = null;
                    lastRenderedStacks[i] = null;
                    continue;
                }

                // Если предмет в слоте изменился — пересобираем меш
                if (lastRenderedStacks[i] != slot.Itemstack)
                {
                    itemMeshRefs[i]?.Dispose();
                    capi.Tesselator.TesselateItem(slot.Itemstack.Item, out MeshData mesh);
                    itemMeshRefs[i] = capi.Render.UploadMesh(mesh);
                    lastRenderedStacks[i] = slot.Itemstack;
                }

                if (itemMeshRefs[i] != null)
                {
                    float offsetAngle = rotationAngle * 0.5f + (i * (float)(Math.PI * 2 / 3));
                    float itemRadius = 0.4f; // Расстояние от центра

                    double ix = pos.X + 0.5 + Math.Sin(offsetAngle) * itemRadius;
                    double iy = pos.Y + 0.3 + Math.Sin(rotationAngle + i) * 0.05;
                    double iz = pos.Z + 0.5 + Math.Cos(offsetAngle) * itemRadius;

                    ModelMat.Identity();
                    ModelMat.Translate(ix - camPos.X, iy - camPos.Y, iz - camPos.Z);
                    ModelMat.RotateY(rotationAngle + i); // Предметы сами крутятся
                    ModelMat.Scale(0.3f, 0.3f, 0.3f);

                    prog.ModelMatrix = ModelMat.Values;
                    capi.Render.RenderMesh(itemMeshRefs[i]);
                }
            }

            // Рендерим 3 шара с интервалом в 120 градусов (2 * PI / 3)
            for (int i = 0; i < 3; i++)
            {
                float offsetAngle = rotationAngle + (i * (float)(Math.PI * 2 / 3));

                double px = pos.X + 0.5 + Math.Sin(offsetAngle) * currentRadius;
                double py = pos.Y + 0.2 + (Math.Sin(rotationAngle * 2 + i) * 0.1); // Небольшое колебание по высоте
                double pz = pos.Z + 0.5 + Math.Cos(offsetAngle) * currentRadius;

                // Зеленый цвет для террастали
                Vec4f color = new Vec4f(0.1f, 1.0f, 0.2f, 0.8f);

                prog.RgbaAmbientIn = new Vec3f(color.X, color.Y, color.Z);
                prog.RgbaLightIn = color;
                prog.RgbaGlowIn = color;
                prog.RgbaTint = color;

                ModelMat.Identity();
                ModelMat.Translate(px - camPos.X, py - camPos.Y, pz - camPos.Z);
                ModelMat.RotateY(player.CameraYaw);
                ModelMat.RotateX(player.CameraPitch);

                // Размер частиц немного пульсирует
                float size = 0.5f + (float)(Math.Sin(rotationAngle * 5 + i) * 0.1);
                ModelMat.Scale(size, size, size);

                prog.ModelMatrix = ModelMat.Values;
                prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
                prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;

                capi.Render.RenderMesh(quadMeshRef);
            }

            // Очистка шейдера
            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            prog.Stop();

            GL.DepthMask(true);
            capi.Render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            quadMeshRef?.Dispose();
            particleTexture?.Dispose();
        }
    }
}