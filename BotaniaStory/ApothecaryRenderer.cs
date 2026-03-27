using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ApothecaryRenderer : IRenderer, IDisposable
    {
        // Вспомогательный класс для настроек рендера
        public class ItemRenderTransform
        {
            public float Scale;
            public float RotX; // В радианах! (GameMath.PIHALF = 90 градусов)
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

        // ==========================================
        // ПОЛЯ КЛАССА (Переменные)
        // ==========================================
        private Dictionary<string, ItemRenderTransform> customTransforms = new Dictionary<string, ItemRenderTransform>();

        private float spreadLevel = 7f;      // Разброс лепестков по воде
        private float itemScale = 1f;      // Базовый размер айтемов
        private float heightOffset = 0.70f;  // Точная высота уровня воды!

        private ICoreClientAPI capi;
        private BlockPos pos;

        private MultiTextureMeshRef[] meshRefs = new MultiTextureMeshRef[16];
        private bool[] isItem = new bool[16];

        private float[] xDir = new float[16];
        private float[] zDir = new float[16];
        private float[] yRots = new float[16];

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        // ==========================================
        // ЕДИНСТВЕННЫЙ КОНСТРУКТОР
        // ==========================================
        public ApothecaryRenderer(BlockPos pos, ICoreClientAPI capi)
        {
            this.pos = pos;
            this.capi = capi;

            Random rand = new Random(pos.GetHashCode());

            for (int i = 0; i < 16; i++)
            {
                // Сохраняем просто случайный вектор от -1 до 1
                xDir[i] = (float)(rand.NextDouble() * 2 - 1);
                zDir[i] = (float)(rand.NextDouble() * 2 - 1);
                yRots[i] = (float)(rand.NextDouble() * GameMath.TWOPI);
            }

            // Настройки кастомных предметов (добавляй свои сюда)
            // 1. Настройка для белого лепестка из твоего мода
            customTransforms["точное_имя_айтема"] = new ItemRenderTransform(
                0.2f,             // Масштаб (Scale): 0.4f делает предмет чуть больше базового (0.3f). Буква 'f' означает тип float.
                GameMath.PIHALF,  // Поворот по оси X (RotX): Наклоняем на 90 градусов (PI / 2), чтобы лепесток лежал плашмя на воде.
                0f,               // Поворот по оси Y (RotY): Не крутим вокруг вертикальной оси (оставляем 0).
                0f                // Поворот по оси Z (RotZ): Не наклоняем вбок (оставляем 0).
            );

            // 2. Настройка для  mysticalpetal-white
            customTransforms["точное_имя_айтема"] = new ItemRenderTransform(
                2.15f,            // Масштаб: 
                GameMath.PIHALF,  // Поворот по оси X:
                0f,               // Поворот по Y: 0
                0f                // Поворот по Z: 0
            );

            // 3. Настройка для какого-то нестандартного предмета, у которого оси перепутаны
            customTransforms["точное_имя_айтема"] = new ItemRenderTransform(
                0.3f,             // Масштаб: Оставляем стандартный (0.3f).
                0f,               // Поворот по оси X: Здесь мы НЕ наклоняем его по X (0 градусов)...
                0f,               // Поворот по оси Y: 0
                GameMath.PIHALF   // Поворот по оси Z: ...зато наклоняем на 90 градусов по оси Z! Это нужно, если в Blockbench моделька была сделана иначе, и её нужно "повалить" на другой бок.
            );
        }

        // ==========================================
        // МЕТОДЫ
        // ==========================================
        public void SetContents(InventoryBase inv)
        {
            for (int i = 0; i < 16; i++)
            {
                // Очищаем старый меш
                meshRefs[i]?.Dispose();
                meshRefs[i] = null;

                ItemSlot slot = inv[i];
                if (slot.Empty) continue;

                ItemStack stack = slot.Itemstack;
                isItem[i] = stack.Class == EnumItemClass.Item;
                MeshData mesh;

                // Получаем модельку
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

                    // ==========================================
                    // 1. БАЗА (Для всех предметов)
                    // ==========================================
                    float finalScale = this.itemScale;
                    float finalRotX = 0f;
                    float finalRotY = 0f;
                    float finalRotZ = 0f;
                    float finalHeight = this.heightOffset;
                    float finalSpread = this.spreadLevel;

                    // БАЗОВЫЙ ЦЕНТР: Ровно посередине блока
                    Vec3f finalCenter = new Vec3f(0.5f, 0.5f, 0.5f);

                    // ==========================================
                    // 2. ФИЛЬТРЫ 
                    // ==========================================

                    // СНАЧАЛА проверяем наш словарь кастомных настроек!
                    if (customTransforms.TryGetValue(itemCode, out ItemRenderTransform transform))
                    {
                        finalScale = transform.Scale;
                        finalRotX = transform.RotX;
                        finalRotY = transform.RotY;
                        finalRotZ = transform.RotZ;

                        // Если для кастомных предметов тоже нужно смещать центр вниз, раскомментируй строку ниже:
                        // finalCenter = new Vec3f(0.5f, 0.05f, 0.5f); 
                    }

                    if (itemCode.Contains("mysticalflower"))
                    {
                        finalScale = 0.35f;
                        finalHeight += 0.08f;
                        finalRotX = GameMath.PIHALF;
                        finalRotY = GameMath.PI;
                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);

                        finalSpread = 7f; // <---РАЗБРОС ДЛЯ ЦВЕТК
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

                    if (itemCode.Contains("gray-free") || itemCode.Contains("blue-free") 
                        || itemCode.Contains("lightgray") || itemCode.Contains("red-free") 
                        || itemCode.Contains("wilddaisy") || itemCode.Contains("redtopgrass") || itemCode.Contains("mugwort") 
                        || itemCode.Contains("cowparsley") || itemCode.Contains("orangemallow") || itemCode.Contains("catmint"))
                    {
                        finalScale = 0.3f;
                        finalHeight += 0.02f;
                        finalRotX = GameMath.PIHALF;
                        finalRotY = GameMath.PI;
                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);

                        finalSpread = 3f; // <--- СТАВИМ СВОЙ РАЗБРОС ДЛЯ ЦВЕТКА (например, 2f - ближе к центру)
                    }

                    //слегка смещён вовнутрь
                    if (itemCode.Contains("magenta-free") || itemCode.Contains("brown-free") || itemCode.Contains("lime-free") || itemCode.Contains("orange-free") || itemCode.Contains("black-free") || itemCode.Contains("green-free") || itemCode.Contains("yellow-free"))
                    {
                        finalScale = 0.3f;
                        finalHeight += 0.02f;
                        finalRotX = GameMath.PIHALF;
                        finalRotY = GameMath.PI;
                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);

                        finalSpread = 5f; // <--- СТАВИМ СВОЙ РАЗБРОС ДЛЯ ЦВЕТКА (например, 2f - ближе к центру)
                    }

                    // Если в словаре предмета нет, применяем стандартные правила по группам
                    else if (itemCode.Contains("petal"))
                    {
                        finalScale = 0.10f;
                        finalHeight += -0.35f;
                        finalRotX = GameMath.PIHALF;
                    }

                    // Обрабатываем семена березы отдельно, так как у тебя для них не было поворота
                    else if (itemCode.Contains("treeseed-birch"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.3f;
                        // Стоит прямо, без RotX
                    }
                    else if (itemCode.Contains("treeseed-greenspirecypress"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.1f;
                        // Стоит прямо, без RotX
                    }
                    else if (itemCode.Contains("treeseed-baldcypress"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.1f;
                        // Стоит прямо, без RotX
                    }
                    else if (itemCode.Contains("treeseed-acacia"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.26f;
                        // Стоит прямо, без RotX
                    }
                    else if (itemCode.Contains("treeseed-ebony"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.1f;
                        // Стоит прямо, без RotX
                    }
                    else if (itemCode.Contains("treeseed-purpleheart"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.3f;
                        // Стоит прямо, без RotX
                    }
                    else if (itemCode.Contains("treeseed-maple"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.47f;
                        finalRotX = GameMath.PIHALF;
                        // Лежит на боку, повёрнут на 90 градусов по X

                        // ИСПРАВЛЕНИЕ СМЕЩЕНИЯ: Опускаем точку вращения в самый низ модельки!
                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);
                    }
                    else if (itemCode.Contains("treeseed-crimsonkingmaple"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.47f;
                        // Лежит на боку, повёрнут на 90 градусов по X
                        finalRotX = GameMath.PIHALF;

                        // ИСПРАВЛЕНИЕ СМЕЩЕНИЯ: Опускаем точку вращения в самый низ модельки!
                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);
                    }
                    // Это условие поймает ВСЕ остальные семена деревьев (pine, maple, kapok и т.д.)
                    else if (itemCode.Contains("treeseed"))
                    {
                        finalScale = 0.6f;
                        finalHeight += 0.43f;
                        // Лежит на боку, повёрнут на 90 градусов по X
                        finalRotX = GameMath.PIHALF;

                        // ИСПРАВЛЕНИЕ СМЕЩЕНИЯ: Опускаем точку вращения в самый низ модельки!
                        finalCenter = new Vec3f(0.5f, 0.05f, 0.5f);
                    }
                    // ==========================================
                    // 3. ПРИМЕНЯЕМ ВСЕ НАСТРОЙКИ (с использованием finalCenter!)
                    // ==========================================

                    // Масштабируем относительно нашего центра
                    mesh.Scale(finalCenter, finalScale, finalScale, finalScale);

                    // Поворачиваем относительно нашего центра ВСЕ объекты (и предметы, и блоки)
                    mesh.Rotate(finalCenter, finalRotX, finalRotY, finalRotZ);

                    // Случайный разброс по оси Y
                    mesh.Rotate(finalCenter, 0, yRots[i], 0);

                    // Итоговое смещение (используем finalHeight)
                    // Рассчитываем итоговое смещение с учетом индивидуального spread предмета
                    float currentMaxOffset = (finalSpread / 10f) * 0.45f;
                    float finalX = xDir[i] * currentMaxOffset;
                    float finalZ = zDir[i] * currentMaxOffset;

                    // Сдвигаем модель
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