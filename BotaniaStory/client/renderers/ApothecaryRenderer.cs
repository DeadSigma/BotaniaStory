using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.renderers
{
    public class ApothecaryRenderer : IRenderer, IDisposable
    {
        // Параметры рендера задаются отдельно для нужных предметов
        public class ItemRenderTransform
        {
            public float Scale;
            public float RotX;
            public float RotY;
            public float RotZ;

            public ItemRenderTransform(float scale = 0.3f, float rotX = GameMath.PIHALF, float rotY = 0f, float rotZ = 0f)
            {
                Scale = scale;
                RotX = rotX;
                RotY = rotY;
                RotZ = rotZ;
            }
        }

        private Dictionary<string, ItemRenderTransform> customTransforms = new Dictionary<string, ItemRenderTransform>();

        private float spreadLevel = 7f;
        private float itemScale = 1f;
        private float heightOffset = 0.70f;

        private ICoreClientAPI capi;
        private BlockPos pos;

        private MultiTextureMeshRef[] meshRefs = new MultiTextureMeshRef[16];
        private bool[] isItem = new bool[16];

        private float[] xDir = new float[16];
        private float[] zDir = new float[16];
        private float[] yRots = new float[16];

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public ApothecaryRenderer(BlockPos pos, ICoreClientAPI capi)
        {
            this.pos = pos;
            this.capi = capi;

            // Разброс закрепляется за позицией блока
            Random rand = new Random(pos.GetHashCode());

            for (int i = 0; i < 16; i++)
            {
                xDir[i] = (float)(rand.NextDouble() * 2 - 1);
                zDir[i] = (float)(rand.NextDouble() * 2 - 1);
                yRots[i] = (float)(rand.NextDouble() * GameMath.TWOPI);
            }

            customTransforms["шаблон"] = new ItemRenderTransform(
                0.2f,
                GameMath.PIHALF,
                0f,
                0f
            );

            customTransforms["точное_имя_айтема"] = new ItemRenderTransform(
                2.15f,
                GameMath.PIHALF,
                0f,
                0f
            );

            customTransforms["точное_имя_айтема"] = new ItemRenderTransform(
                0.3f,
                0f,
                0f,
                GameMath.PIHALF
            );
        }

        // Меши пересобираются при изменении содержимого
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
                    string itemCode = stack.Collectible.Code.Domain + ":" + stack.Collectible.Code.Path;

                    float finalScale = this.itemScale;
                    float finalRotX = 0f;
                    float finalRotY = 0f;
                    float finalRotZ = 0f;
                    float finalHeight = this.heightOffset;
                    float finalSpread = this.spreadLevel;

                    Vec3f finalCenter = new Vec3f(0.5f, 0.5f, 0.5f);

                    // Индивидуальные параметры применяются раньше общих правил
                    if (customTransforms.TryGetValue(itemCode, out ItemRenderTransform transform))
                    {
                        finalScale = transform.Scale;
                        finalRotX = transform.RotX;
                        finalRotY = transform.RotY;
                        finalRotZ = transform.RotZ;

                    }

                    if (itemCode.Contains("game:gear-rusty"))
                    {
                        finalScale = 0.55f;
                        finalHeight += -0.2f;
                        finalSpread = 5f;
                    }

                    if (itemCode.Contains("botaniastory:overgrowthseed"))
                    {
                        finalScale = 0.55f;
                        finalHeight += -0.45f;
                        finalSpread = 5f;
                        finalRotX = GameMath.PIHALF;
                    }

                    if (itemCode.Contains("mysticalflower"))
                    {
                        finalScale = 0.35f;
                        finalHeight += 0.08f;
                        finalRotY = GameMath.PI;
                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);

                        finalSpread = 7f;
                    }

                    if (itemCode.Contains("manaitem-manapowder"))
                    {
                        finalScale = 0.35f;
                        finalHeight += -0.3f;

                        finalSpread = 7f;
                    }

                    if (itemCode.Contains("flower"))
                    {
                        finalScale = 0.35f;
                        finalHeight += 0.08f;
                        finalRotX = GameMath.PIHALF;
                        finalRotY = GameMath.PI;
                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);

                        finalSpread = 7f;
                    }
                    if (itemCode.Contains("rune"))
                    {
                        finalScale = 0.25f;
                        finalHeight += 0.08f;
                        finalRotX = GameMath.PIHALF;
                        finalRotY = GameMath.PI;
                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);

                        finalSpread = 5f;
                    }

                    if (itemCode.Contains("gray-free") || itemCode.Contains("blue-free")
                        || itemCode.Contains("lightgray-free") || itemCode.Contains("red-free")
                        || itemCode.Contains("wilddaisy") || itemCode.Contains("redtopgrass") || itemCode.Contains("mugwort")
                        || itemCode.Contains("cowparsley") || itemCode.Contains("orangemallow") || itemCode.Contains("catmint"))
                    {
                        finalScale = 0.3f;
                        finalHeight += 0.02f;
                        finalRotX = GameMath.PIHALF;
                        finalRotY = GameMath.PI;
                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);

                        finalSpread = 3f;
                    }

                    if (itemCode.Contains("magenta-free") || itemCode.Contains("brown-free") || itemCode.Contains("lime-free") || itemCode.Contains("orange-free") || itemCode.Contains("black-free") || itemCode.Contains("green-free") || itemCode.Contains("yellow-free"))
                    {
                        finalScale = 0.3f;
                        finalHeight += 0.02f;
                        finalRotX = GameMath.PIHALF;
                        finalRotY = GameMath.PI;
                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);

                        finalSpread = 5f;
                    }

                    else if (itemCode.Contains("petal"))
                    {
                        finalScale = 0.10f;
                        finalHeight += -0.35f;
                        finalRotX = GameMath.PIHALF;
                    }

                    else if (itemCode.Contains("charcoal"))
                    {
                        finalScale = 0.85f;
                    }

                    else if (itemCode.Contains("seeds"))
                    {
                        finalScale = 0.40f;
                        finalHeight += -0.35f;
                        finalRotX = GameMath.PIHALF * 3;
                    }

                    else if (itemCode.Contains("root"))
                    {
                        finalHeight += -0.35f;
                        finalRotX = GameMath.PIHALF;
                        finalSpread = 1f;
                    }

                    else if (itemCode.Contains("treeseed-birch"))
                    {
                        finalScale = 0.6f;
                        finalHeight += -0.09f;
                    }
                    else if (itemCode.Contains("treeseed-greenspirecypress"))
                    {
                        finalScale = 0.6f;
                        finalHeight += -0.09f;
                    }
                    else if (itemCode.Contains("treeseed-baldcypress"))
                    {
                        finalScale = 0.6f;
                        finalHeight += -0.09f;
                    }
                    else if (itemCode.Contains("treeseed-acacia"))
                    {
                        finalScale = 0.6f;
                        finalHeight += -0.09f;
                    }
                    else if (itemCode.Contains("treeseed-ebony"))
                    {
                        finalScale = 0.6f;
                        finalHeight += -0.09f;
                    }
                    else if (itemCode.Contains("treeseed-purpleheart"))
                    {
                        finalScale = 0.6f;
                        finalHeight += -0.09f;
                    }
                    else if (itemCode.Contains("treeseed-maple"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.02f;
                        finalRotX = GameMath.PIHALF;

                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);
                    }
                    else if (itemCode.Contains("treeseed-crimsonkingmaple"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.02f;
                        finalRotX = GameMath.PIHALF;

                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);
                    }
                    // Остальные семена деревьев обрабатываются общим правилом
                    else if (itemCode.Contains("treeseed"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.02f;
                        finalRotX = GameMath.PIHALF;

                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);
                    }

                    // Масштаб и поворот применяются относительно центра предмета
                    mesh.Scale(finalCenter, finalScale, finalScale, finalScale);

                    mesh.Rotate(finalCenter, finalRotX, finalRotY, finalRotZ);

                    mesh.Rotate(finalCenter, 0, yRots[i], 0);

                    float currentMaxOffset = (finalSpread / 10f) * 0.45f;
                    float finalX = xDir[i] * currentMaxOffset;
                    float finalZ = zDir[i] * currentMaxOffset;

                    mesh.Translate(finalX, finalHeight + (i * 0.001f), finalZ);

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
            prog.DontWarpVertices = 1;
            prog.AddRenderFlags = 0;

            // Затенение по нормалям отключается для лежащих предметов
            prog.NormalShaded = 0;

            // Освещение берётся из блока аптекаря
            Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            prog.RgbaLightIn = lightrgbs;

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
