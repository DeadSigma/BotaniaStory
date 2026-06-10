using BotaniaStory.blockentity;
using BotaniaStory.client.particles;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.renderers
{
    public class RunicAltarRenderer : IRenderer, IDisposable
    {
        private ICoreClientAPI capi;
        private BlockPos pos;
        public BlockEntityRunicAltar be; 

        // Настройки высоты эффектов
        private float CubeBaseHeight = 1.5f; // Высота летящих кубиков
        private float StarBaseHeight = 1.0f; // Высота звезды по центру 

        // Данные для рендера предметов
        private MultiTextureMeshRef[] meshRefs = new MultiTextureMeshRef[17];
        private bool[] isItem = new bool[17];
        private float[] itemHeights = new float[17];
        public List<RunicLightningFlash> Flashes = new List<RunicLightningFlash>();

        // Данные для рендера эффектов (кубы и звезда)
        private MeshRef cubeMeshRef;
        private LoadedTexture effectTexture;
        private Matrixf modelMat = new Matrixf();
        private const int StarSpikes = 16;
        private Vec3f[] starAxis;
        private float[] starAngle;
        private float[] starPhase;

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public RunicAltarRenderer(BlockPos pos, ICoreClientAPI capi, BlockEntityRunicAltar be)
        {
            this.pos = pos;
            this.capi = capi;
            this.be = be;

            // Инициализация эффектов (Звезда и летающие кубики)
            AssetLocation texLoc = new AssetLocation("botaniastory", "textures/block/runic_altar_cube.png");
            effectTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(texLoc, ref effectTexture);

            MeshData cubeData = CubeMeshUtil.GetCube();
            cubeData.Flags = new int[cubeData.GetVerticesCount()]; // Отключаем ветер
            byte[] rgba = new byte[cubeData.GetVerticesCount() * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = 255; // Заливаем белым для RgbaTint
            cubeData.Rgba = rgba;

            cubeMeshRef = capi.Render.UploadMesh(cubeData);

            // Генерация лучей звезды
            starAxis = new Vec3f[StarSpikes];
            starAngle = new float[StarSpikes];
            starPhase = new float[StarSpikes];
            Random rnd = new Random(pos.X * 7919 + pos.Y * 104729 + pos.Z * 1299709);
            for (int s = 0; s < StarSpikes; s++)
            {
                starAxis[s] = new Vec3f(
                    (float)(rnd.NextDouble() * 2 - 1),
                    (float)(rnd.NextDouble() * 2 - 1),
                    (float)(rnd.NextDouble() * 2 - 1)).Normalize();
                starAngle[s] = (float)(rnd.NextDouble() * Math.PI * 2);
                starPhase[s] = (float)(rnd.NextDouble() * Math.PI * 2);
            }
        }

        public void AddLightning(Vec3d startPos)
        {
            Flashes.Add(new RunicLightningFlash(capi, startPos));
        }

        public void UpdateMeshes()
        {
            if (capi == null || be == null) return;

            for (int i = 0; i < 16; i++)
            {
                meshRefs[i]?.Dispose();
                meshRefs[i] = null;

                ItemSlot slot = be.inventory[i];
                if (slot == null || slot.Empty || slot.Itemstack == null) continue;

                MeshData mesh = null;
                bool currentIsItem = slot.Itemstack.Class == EnumItemClass.Item;

                if (!currentIsItem && slot.Itemstack.Block != null)
                {
                    mesh = capi.TesselatorManager.GetDefaultBlockMesh(slot.Itemstack.Block)?.Clone();
                }
                else if (currentIsItem && slot.Itemstack.Item != null)
                {
                    capi.Tesselator.TesselateItem(slot.Itemstack.Item, out mesh);
                }

                if (mesh != null)
                {
                    isItem[i] = currentIsItem;

                    float scale = 0.3f;
                    float hFix = 0.0f;

                    if (currentIsItem)
                    {
                        if (slot.Itemstack.Item.Code.Path.StartsWith("rune-"))
                        {
                            scale = 0.3f;
                            hFix = 0.0f;
                        }
                        else
                        {
                            scale = 0.75f;
                            hFix = 0.2f;
                        }
                    }

                    itemHeights[i] = hFix;

                    mesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), scale, scale, scale);
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
                    MeshData baseMesh = capi.TesselatorManager.GetDefaultBlockMesh(rockBlock);
                    if (baseMesh != null)
                    {
                        MeshData rockMesh = baseMesh.Clone();
                        rockMesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.25f, 0.25f, 0.25f);
                        rockMesh.Translate(-0.5f, -0.5f, -0.5f);
                        meshRefs[16] = capi.Render.UploadMultiTextureMesh(rockMesh);
                        isItem[16] = false;
                    }
                }
            }
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
           
            // ЭТАП 1: OPAQUE (Предметы и Молнии)
            
            if (stage == EnumRenderStage.Opaque)
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
                prog.NormalShaded = 1;
                prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                float time = capi.World.ElapsedMilliseconds / 1000f;
                int itemCount = 0;
                for (int i = 0; i < 16; i++) if (meshRefs[i] != null) itemCount++;

                for (int i = 0; i < 17; i++)
                {
                    if (meshRefs[i] == null) continue;

                    modelMat.Identity();

                    if (i < 16)
                    {
                        float angle = (time * 0.5f) + (i * (GameMath.TWOPI / Math.Max(1, itemCount)));
                        float offsetX = (float)Math.Cos(angle) * 0.8f;
                        float offsetZ = (float)Math.Sin(angle) * 0.8f;

                        float heightFix = itemHeights[i];
                        float offsetY = 1.3f + heightFix + (float)Math.Sin(time * 2f + (i * 1.2f)) * 0.1f;

                        modelMat.Translate(pos.X - camPos.X + 0.5f + offsetX, pos.Y - camPos.Y + offsetY, pos.Z - camPos.Z + 0.5f + offsetZ).RotateY(-angle);
                    }
                    else
                    {
                        float offsetY = 1.35f + (float)Math.Sin(time * 1.5f) * 0.05f;
                        modelMat.Translate(pos.X - camPos.X + 0.5f, pos.Y - camPos.Y + offsetY, pos.Z - camPos.Z + 0.5f).RotateY(time * 0.5f);
                    }

                    prog.ModelMatrix = modelMat.Values;
                    rpi.BindTexture2d(isItem[i] ? capi.ItemTextureAtlas.AtlasTextures[0].TextureId : capi.BlockTextureAtlas.AtlasTextures[0].TextureId);
                    rpi.RenderMultiTextureMesh(meshRefs[i], "tex", 0);
                }
                prog.Stop();

                // Рендер молний (работает автономно по вызовам AddLightning)
                if (Flashes.Count > 0)
                {
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
                    rpi.GlToggleBlend(false);
                }
            }

            
            // ЭТАП 2: AFTER OIT (Свечение, Кубы, Звезда)
            
            else if (stage == EnumRenderStage.AfterOIT && cubeMeshRef != null)
            {
                IRenderAPI render = capi.Render;
                Vec3d cameraPos = capi.World.Player.Entity.CameraPos;

                double cx = pos.X - cameraPos.X + 0.5;
                double cz = pos.Z - cameraPos.Z + 0.5;

                IStandardShaderProgram prog = render.PreparedStandardShader(pos.X, pos.Y, pos.Z);
                prog.Tex2D = effectTexture.TextureId;
                prog.NormalShaded = 0;
                prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
                prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
                prog.RgbaGlowIn = new Vec4f(1f, 1f, 1f, 1f);
                prog.ViewMatrix = render.CameraMatrixOriginf;
                prog.ProjectionMatrix = render.CurrentProjectionMatrix;

                double time = (capi.World.ElapsedMilliseconds / 1000.0) * 8.0;

                // ЛЕТАЮЩИЕ КУБИКИ
                render.GlToggleBlend(true, EnumBlendMode.Glow);
                render.GLDepthMask(false);
                prog.ExtraGlow = 0;

                int cubes = 2;
                int iters = 15;

                for (int curIter = iters; curIter > 0; curIter--)
                {
                    double pastTime = time - 1.3 * (iters - curIter);
                    float offsetPerCube = 360f / cubes;
                    float alpha = curIter < iters ? (float)curIter / iters * 0.4f : 1f;

                    for (int i = 0; i < cubes; i++)
                    {
                        float offset = offsetPerCube * i;
                        float deg = (float)((pastTime / 0.2 % 360) + offset);
                        float rad = deg * GameMath.DEG2RAD;

                        float radiusX = 0.35f + 0.05f * GameMath.Sin((float)pastTime / 6f);
                        float radiusZ = 0.35f + 0.05f * GameMath.Cos((float)pastTime / 6f);

                        float x = radiusX * GameMath.Cos(rad);
                        float z = radiusZ * GameMath.Sin(rad);
                        float y = GameMath.Cos((float)(pastTime + 50 * i) / 5f) / 10f;

                        prog.RgbaTint = new Vec4f(alpha, alpha, alpha, 1f);

                        modelMat.Identity();
                        modelMat.Translate(cx, pos.Y - cameraPos.Y + CubeBaseHeight + y, cz);
                        modelMat.Translate(x, 0, z);

                        float xRotate = (float)GameMath.Sin(pastTime * 0.2) / 2f;
                        float yRotate = Math.Max(0.6f, (float)GameMath.Sin(pastTime * 0.1) / 2f + 0.5f);
                        float zRotate = (float)GameMath.Cos(pastTime * 0.2) / 2f;
                        Vec3f axis = new Vec3f(xRotate, yRotate, zRotate).Normalize();
                        Mat4f.Rotate(modelMat.Values, modelMat.Values, rad, new float[] { axis.X, axis.Y, axis.Z });

                        modelMat.Scale(0.03125f, 0.03125f, 0.03125f);
                        modelMat.Translate(0.5f, 0.5f, 0.5f);

                        prog.ModelMatrix = modelMat.Values;
                        render.RenderMesh(cubeMeshRef);
                    }
                }

                // ЗВЕЗДА ПО ЦЕНТРУ
                // Проверяем, заполнена ли мана в BlockEntity алтаря. 
                if (be != null && be.TargetMana > 0 && be.CurrentMana >= be.TargetMana)
                {
                    prog.ExtraGlow = 255;
                    prog.RgbaTint = new Vec4f(0f, 0.894f, 0.843f, 1f);

                    float starY = (float)(pos.Y - cameraPos.Y + StarBaseHeight);
                    float spin = (float)(time * 0.05);
                    float globalPulse = 0.85f + 0.15f * GameMath.Sin((float)time / 8f);

                    for (int s = 0; s < StarSpikes; s++)
                    {
                        float len = (0.18f + 0.10f * GameMath.Sin((float)time / 5f + starPhase[s])) * globalPulse;
                        float thick = 0.012f;

                        modelMat.Identity();
                        modelMat.Translate(cx, starY, cz);
                        Mat4f.Rotate(modelMat.Values, modelMat.Values, spin, new float[] { 0.282f, 0.940f, 0.188f });
                        Mat4f.Rotate(modelMat.Values, modelMat.Values, starAngle[s], new float[] { starAxis[s].X, starAxis[s].Y, starAxis[s].Z });
                        modelMat.Scale(len, thick, thick);
                        modelMat.Translate(1f, 0f, 0f);

                        prog.ModelMatrix = modelMat.Values;
                        render.RenderMesh(cubeMeshRef);
                    }
                }

                prog.Stop();
                render.GLDepthMask(true);
                render.GlToggleBlend(false);
            }
        }

        public void Dispose()
        {
            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            capi?.Event.UnregisterRenderer(this, EnumRenderStage.AfterOIT);

            cubeMeshRef?.Dispose();

            foreach (var flash in Flashes) flash.Dispose();
            for (int i = 0; i < 17; i++) meshRefs[i]?.Dispose();
        }
    }
}