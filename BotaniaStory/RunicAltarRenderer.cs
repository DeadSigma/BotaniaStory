using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class RunicAltarRenderer : IRenderer, IDisposable
    {
        private ICoreClientAPI capi;
        private BlockPos pos;
        private BlockEntityRunicAltar be;

        private MultiTextureMeshRef[] meshRefs = new MultiTextureMeshRef[17];
        private bool[] isItem = new bool[17];

        // Список активных ванильных молний
        public List<RunicLightningFlash> Flashes = new List<RunicLightningFlash>();

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public RunicAltarRenderer(BlockPos pos, ICoreClientAPI capi, BlockEntityRunicAltar be)
        {
            this.pos = pos;
            this.capi = capi;
            this.be = be;
        }

        public void AddLightning(Vec3d startPos)
        {
            Flashes.Add(new RunicLightningFlash(capi, startPos));
        }

        public void UpdateMeshes()
        {
            for (int i = 0; i < 16; i++)
            {
                meshRefs[i]?.Dispose();
                meshRefs[i] = null;

                ItemSlot slot = be.inventory[i];
                if (slot.Empty) continue;

                MeshData mesh = null;
                if (slot.Itemstack.Class == EnumItemClass.Block)
                    mesh = capi.TesselatorManager.GetDefaultBlockMesh(slot.Itemstack.Block)?.Clone();
                else
                    capi.Tesselator.TesselateItem(slot.Itemstack.Item, out mesh);

                if (mesh != null)
                {
                    isItem[i] = slot.Itemstack.Class == EnumItemClass.Item;
                    mesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.3f, 0.3f, 0.3f);
                    mesh.Translate(-0.5f, -0.5f, -0.5f);
                    meshRefs[i] = capi.Render.UploadMultiTextureMesh(mesh);
                }
            }

            meshRefs[16]?.Dispose();
            meshRefs[16] = null;
            if (be.HasLivingrock)
            {
                Block rockBlock = capi.World.GetBlock(new AssetLocation("botaniastory", "livingrock"));
                if (rockBlock != null)
                {
                    MeshData rockMesh = capi.TesselatorManager.GetDefaultBlockMesh(rockBlock).Clone();
                    rockMesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.4f, 0.4f, 0.4f);
                    rockMesh.Translate(-0.5f, -0.5f, -0.5f);
                    meshRefs[16] = capi.Render.UploadMultiTextureMesh(rockMesh);
                    isItem[16] = false;
                }
            }
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (stage == EnumRenderStage.Opaque)
            {
                IRenderAPI rpi = capi.Render;
                Vec3d camPos = capi.World.Player.Entity.CameraPos;

                //  РИСУЕМ ПРЕДМЕТЫ ---
                IStandardShaderProgram prog = rpi.StandardShader;
                prog.Use();

                prog.RgbaAmbientIn = rpi.AmbientColor;
                prog.RgbaFogIn = rpi.FogColor;
                prog.FogMinIn = rpi.FogMin;
                prog.FogDensityIn = rpi.FogDensity;
                prog.RgbaTint = ColorUtil.WhiteArgbVec;
                prog.NormalShaded = 1;
                prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                float time = capi.World.ElapsedMilliseconds / 1000f;
                float radius = 0.8f;
                float orbitHeight = 1.3f;
                float centerHeight = 1.1f;
                float speed = 0.5f;

                int itemCount = 0;
                for (int i = 0; i < 16; i++) if (meshRefs[i] != null) itemCount++;

                for (int i = 0; i < 17; i++)
                {
                    if (meshRefs[i] == null) continue;

                    Matrixf modelMat = new Matrixf().Identity();

                    if (i < 16)
                    {
                        float angle = (time * speed) + (i * (GameMath.TWOPI / Math.Max(1, itemCount)));
                        float offsetX = (float)Math.Cos(angle) * radius;
                        float offsetZ = (float)Math.Sin(angle) * radius;
                        float offsetY = orbitHeight + (float)Math.Sin(time * 2f + (i * 1.2f)) * 0.1f;

                        modelMat.Translate(pos.X - camPos.X + 0.5f + offsetX, pos.Y - camPos.Y + offsetY, pos.Z - camPos.Z + 0.5f + offsetZ)
                                .RotateY(-angle);
                    }
                    else
                    {
                        float offsetY = centerHeight + (float)Math.Sin(time * 1.5f) * 0.05f;
                        modelMat.Translate(pos.X - camPos.X + 0.5f, pos.Y - camPos.Y + offsetY, pos.Z - camPos.Z + 0.5f)
                                .RotateY(time * 0.5f);
                    }

                    prog.ModelMatrix = modelMat.Values;
                    rpi.BindTexture2d(isItem[i] ? capi.ItemTextureAtlas.AtlasTextures[0].TextureId : capi.BlockTextureAtlas.AtlasTextures[0].TextureId);
                    rpi.RenderMultiTextureMesh(meshRefs[i], "tex", 0);
                }

                prog.Stop(); 

                //  РИСУЕМ МАГИЧЕСКИЕ МОЛНИИ ---
                if (Flashes.Count > 0)
                {
                    // Включаем полупрозрачность вручную!
                    rpi.GlToggleBlend(true, EnumBlendMode.Standard);

                    IShaderProgram linesProg = capi.Shader.GetProgramByName("lines");
                    if (linesProg != null)
                    {
                        linesProg.Use();
                        linesProg.UniformMatrix("projection", rpi.CurrentProjectionMatrix);
                        linesProg.UniformMatrix("view", rpi.CameraMatrixOriginf);

                        for (int i = Flashes.Count - 1; i >= 0; i--)
                        {
                            var flash = Flashes[i];
                            flash.Render(dt, linesProg);
                            if (!flash.Alive)
                            {
                                flash.Dispose();
                                Flashes.RemoveAt(i);
                            }
                        }
                        linesProg.Stop();
                    }

                    // Выключаем полупрозрачность, чтобы не сломать рендер мира
                    rpi.GlToggleBlend(false);
                }
            }
        }

        public void Dispose()
        {
            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            foreach (var flash in Flashes) flash.Dispose();
            for (int i = 0; i < 17; i++) meshRefs[i]?.Dispose();
        }
    }
}