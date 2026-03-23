using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ApothecaryRenderer : IRenderer, IDisposable
    {
        // ==========================================
        // НАСТРОЙКИ (Подогнаны под Аптекарь)
        // ==========================================
        private float spreadLevel = 6f;      // Разброс лепестков по воде
        private float itemScale = 0.3f;      // Размер лепестков
        private float heightOffset = 0.36f;  // Точная высота уровня воды!
        // ==========================================

        private ICoreClientAPI capi;
        private BlockPos pos;

        private MultiTextureMeshRef[] meshRefs = new MultiTextureMeshRef[16];
        private bool[] isItem = new bool[16];

        private float[] xOffsets = new float[16];
        private float[] zOffsets = new float[16];
        private float[] yRots = new float[16];

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public ApothecaryRenderer(BlockPos pos, ICoreClientAPI capi)
        {
            this.pos = pos;
            this.capi = capi;

            Random rand = new Random(pos.GetHashCode());
            float maxOffset = (spreadLevel / 10f) * 0.45f;

            for (int i = 0; i < 16; i++)
            {
                xOffsets[i] = (float)(rand.NextDouble() * 2 - 1) * maxOffset;
                zOffsets[i] = (float)(rand.NextDouble() * 2 - 1) * maxOffset;
                yRots[i] = (float)(rand.NextDouble() * GameMath.TWOPI);
            }
        }

        public void SetContents(InventoryBase inv)
        {
            for (int i = 0; i < 16; i++)
            {
                meshRefs[i]?.Dispose();
                meshRefs[i] = null;

                ItemSlot slot = inv[i];
                if (slot.Empty) continue;

                ItemStack stack = slot.Itemstack;
                isItem[i] = stack.Class == EnumItemClass.Item;
                MeshData mesh;

                if (stack.Class == EnumItemClass.Block)
                {
                    mesh = capi.TesselatorManager.GetDefaultBlockMesh(stack.Block)?.Clone();
                }
                else
                {
                    capi.Tesselator.TesselateItem(stack.Item, out mesh);
                }

                if (mesh != null)
                {
                    Vec3f center = new Vec3f(0.5f, 0.5f, 0.5f);

                    mesh.Scale(center, itemScale, itemScale, itemScale);

                    if (isItem[i])
                    {
                        mesh.Rotate(center, GameMath.PIHALF, 0, 0); // Кладем плашмя
                    }

                    mesh.Rotate(center, 0, yRots[i], 0); // Случайный поворот
                    mesh.Translate(xOffsets[i], heightOffset + (i * 0.001f), zOffsets[i]); // Смещение

                    meshRefs[i] = capi.Render.UploadMultiTextureMesh(mesh);
                }
            }
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();

            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.NormalShaded = 1;
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);

            Matrixf modelMat = new Matrixf().Identity().Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);
            prog.ModelMatrix = modelMat.Values;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            for (int i = 0; i < 16; i++)
            {
                if (meshRefs[i] == null) continue;

                if (isItem[i]) rpi.BindTexture2d(capi.ItemTextureAtlas.AtlasTextures[0].TextureId);
                else rpi.BindTexture2d(capi.BlockTextureAtlas.AtlasTextures[0].TextureId);

                rpi.RenderMultiTextureMesh(meshRefs[i], "tex", 0);
            }

            prog.Stop();
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            for (int i = 0; i < 16; i++)
            {
                meshRefs[i]?.Dispose();
            }
        }
    }
}