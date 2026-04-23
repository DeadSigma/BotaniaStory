using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace botaniastory
{
    public class LexiconHologramSystem : ModSystem, IRenderer
    {
        private ICoreClientAPI capi;
        public bool isActive = false;
        private string currentStructure = null;

        // --- ПЕРЕМЕННЫЕ ДЛЯ 3D И ЛОГИКИ ---
        private Dictionary<int, MeshRef> hologramMeshRefs = new Dictionary<int, MeshRef>();
        private int structureSizeX, structureSizeY, structureSizeZ;

        private int offsetY = 0; // ПЕРЕМЕННАЯ СМЕЩЕНИЯ
        private float renderOffsetY = 0f; // Для отрисовки (можно дробные)

        private BlockSchematic loadedSchematic = null; // Храним саму схему для проверки блоков
        private BlockPos lockedPos = null;             // Зафиксированная позиция голограммы
        private long tickListenerId;                   // ID таймера проверки
        private GuiDialogStructureTracker trackerHud;
        private Dictionary<AssetLocation, int> requiredBlocksCount = new Dictionary<AssetLocation, int>();
        public double RenderOrder => 0.5;
        public int RenderRange => 50;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "lexiconHologram");
            api.Event.MouseDown += OnMouseDown;

            // Запускаем проверку структуры раз в 1 секунду (1000 мс)
            tickListenerId = api.Event.RegisterGameTickListener(OnCheckStructureTick, 1000);
        }

        public void StartVisualization(string structureCode)
        {
            currentStructure = structureCode;
            isActive = true;
            lockedPos = null; // Сбрасываем фиксацию при запуске новой
            loadedSchematic = null;

            if (structureCode == "terraaltar")

            {
                offsetY = -1; // Опускаем алтарь на 1 блок в землю
                renderOffsetY = -0.9f;
            }
            else
            {
                offsetY = 0;  // Остальные структуры ставим как обычно
                renderOffsetY = 0f;
            }

            BuildHologramMesh(structureCode);

        }

        private void BuildHologramMesh(string structureCode)
        {
            // Очищаем старые меши из словаря
            foreach (var mesh in hologramMeshRefs.Values) mesh.Dispose();
            hologramMeshRefs.Clear();

            AssetLocation loc = new AssetLocation("botaniastory", "config/schematics/" + structureCode + ".json");
            IAsset asset = capi.Assets.TryGet(loc);

            if (asset == null)
            {
                capi.ShowChatMessage($"[Lexicon] Ошибка: Файл структуры {structureCode}.json не найден!");
                isActive = false;
                return;
            }

            string error = "";
            BlockSchematic schematic = BlockSchematic.LoadFromString(asset.ToText(), ref error);
            if (schematic == null)
            {
                capi.ShowChatMessage($"[Lexicon] Ошибка загрузки структуры: {error}");
                isActive = false;
                return;
            }

            if (schematic.BlockCodes == null)
            {
                capi.ShowChatMessage($"[Lexicon] Ошибка: В схеме {structureCode} отсутствуют данные о блоках (BlockCodes).");
                isActive = false;
                return;
            }

            loadedSchematic = schematic;
            structureSizeX = schematic.SizeX;
            structureSizeY = schematic.SizeY;
            structureSizeZ = schematic.SizeZ;

            Dictionary<int, MeshData> meshesByPage = new Dictionary<int, MeshData>();
            requiredBlocksCount.Clear();

            for (int i = 0; i < schematic.Indices.Count; i++)
            {
                int index = (int)schematic.Indices[i];
                int rawBlockId = schematic.BlockIds[i];

                if (rawBlockId == 0) continue;

                if (schematic.BlockCodes.TryGetValue(rawBlockId, out AssetLocation blockLoc))
                {
                    if (!requiredBlocksCount.ContainsKey(blockLoc)) requiredBlocksCount[blockLoc] = 0;
                    requiredBlocksCount[blockLoc]++;

                    Block block = capi.World.GetBlock(blockLoc);
                    if (block == null || block.BlockId == 0) continue;

                    int atlasPage = 0;
                    if (block.Textures != null && block.Textures.Count > 0)
                    {
                        var firstTex = block.Textures.Values.FirstOrDefault();
                        if (firstTex != null && firstTex.Baked != null)
                        {
                            var pos = capi.BlockTextureAtlas.Positions[firstTex.Baked.TextureSubId];
                            if (pos != null) atlasPage = pos.atlasNumber;
                        }
                    }

                    MeshData cachedMesh = capi.TesselatorManager.GetDefaultBlockMesh(block);
                    if (cachedMesh == null) continue;

                    MeshData blockMesh = cachedMesh.Clone();
                   // blockMesh.CustomInts = null;
                    // blockMesh.CustomFloats = null;

                    int x = index & 0x3FF;
                    int z = (index >> 10) & 0x3FF;
                    int y = (index >> 20) & 0x3FF;

                    blockMesh.Translate(new Vec3f(x, y, z));

                    if (!meshesByPage.ContainsKey(atlasPage))
                    {
                        meshesByPage[atlasPage] = blockMesh;
                    }
                    else
                    {
                        meshesByPage[atlasPage].AddMeshData(blockMesh);
                    }
                }
            }

            foreach (var kvp in meshesByPage)
            {
                if (kvp.Value.VerticesCount > 0)
                {
                    hologramMeshRefs[kvp.Key] = capi.Render.UploadMesh(kvp.Value);
                }
            }

            if (hologramMeshRefs.Count == 0)
            {
                capi.ShowChatMessage("[Lexicon] Ошибка: Меш пустой.");
                isActive = false;
            }
        }

        // === НОВЫЙ МЕТОД ДЛЯ ОТКЛЮЧЕНИЯ ГОЛОГРАММЫ ===
        public void StopVisualization()
        {
            isActive = false;
            currentStructure = null;
            lockedPos = null;
            loadedSchematic = null;

            if (trackerHud != null)
            {
                trackerHud.TryClose();
                trackerHud.Dispose();
                trackerHud = null;
            }

            // Очищаем словарь мешей
            foreach (var mesh in hologramMeshRefs.Values)
            {
                mesh.Dispose();
            }
            hologramMeshRefs.Clear();
        }

        // --- НОВАЯ ЛОГИКА ПКМ (ФИКСАЦИЯ) ---
        private void OnMouseDown(MouseEvent args)
        {
            if (!isActive || args.Button != EnumMouseButton.Right) return;

            ItemSlot activeSlot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            bool isEmptyHand = activeSlot.Empty;

            // Убедись, что Domain ("botaniastory") совпадает с доменом  предмета книги!
            bool isBook = !isEmptyHand && activeSlot.Itemstack.Collectible.Code.Domain == "botaniastory";

            bool isSneaking = capi.World.Player.Entity.Controls.Sneak;

            if (lockedPos == null)
            {
                // 1. УСТАНОВКА (Книгой или пустой рукой)
                if ((isEmptyHand || isBook) && capi.World.Player.CurrentBlockSelection != null)
                {
                    lockedPos = capi.World.Player.CurrentBlockSelection.Position.AddCopy(capi.World.Player.CurrentBlockSelection.Face);

                    // Блокируем клик ТОЛЬКО в момент установки. 
                    // Это нужно, чтобы книга не открылась на весь экран прямо во время прицеливания.
                    args.Handled = true;
                }
            }
            else
            {
                // 2. ВЗАИМОДЕЙСТВИЕ (Голограмма УЖЕ стоит)

                // Если в руке книга — мы просто выходим из метода (return).
                // args.Handled остаётся false, и движок Vintage Story САМ открывает твою книгу!
                if (isBook) return;

                // Если рука пустая — управляем голограммой
                if (isEmptyHand)
                {
                    if (isSneaking)
                    {
                        // ПОЛНАЯ ОТМЕНА (Shift + ПКМ пустой рукой)
                        isActive = false;
                        currentStructure = null;
                        lockedPos = null;
                        loadedSchematic = null;
                    }
                    else
                    {
                        // ПРОСТО ПЕРЕНОС (Обычный ПКМ пустой рукой)
                        lockedPos = null;
                    }

                    if (capi.World.Player.CurrentBlockSelection != null)
                    {
                        args.Handled = true; // Блокируем клик пустой рукой, чтобы не ломать/не взаимодействовать с блоками
                    }
                }
            }
        }

        // --- ПРОВЕРКА ПОСТРОЙКИ ---
        private void OnCheckStructureTick(float dt)
        {
            if (!isActive || loadedSchematic == null || lockedPos == null) return;
            // Считаем прогресс
            bool isComplete = CheckAndUpdateProgress(lockedPos);

            if (isComplete)
            {
                capi.ShowChatMessage($"[Lexicon] Отлично! Структура {currentStructure} успешно построена!");
                StopVisualization(); // Вызываем метод очистки
            }
        }

        private bool CheckAndUpdateProgress(BlockPos basePos)
        {
            int startX = basePos.X - (structureSizeX / 2);
            int startY = basePos.Y + offsetY;
            int startZ = basePos.Z - (structureSizeZ / 2);

            // Словарь для подсчета правильно установленных блоков
            Dictionary<AssetLocation, int> placedBlocksCount = new Dictionary<AssetLocation, int>();
            foreach (var key in requiredBlocksCount.Keys) placedBlocksCount[key] = 0;

            int totalRequired = 0;
            int totalPlaced = 0;

            for (int i = 0; i < loadedSchematic.Indices.Count; i++)
            {
                int index = (int)loadedSchematic.Indices[i];
                int rawExpectedId = loadedSchematic.BlockIds[i];

                if (rawExpectedId == 0) continue; // Воздух

                totalRequired++;

                if (!loadedSchematic.BlockCodes.TryGetValue(rawExpectedId, out AssetLocation blockLoc)) continue;
                Block expectedBlock = capi.World.GetBlock(blockLoc);
                if (expectedBlock == null || expectedBlock.BlockId == 0) continue;

                int x = index & 0x3FF;
                int z = (index >> 10) & 0x3FF;
                int y = (index >> 20) & 0x3FF;

                BlockPos worldPos = new BlockPos(startX + x, startY + y, startZ + z);
                Block worldBlock = capi.World.BlockAccessor.GetBlock(worldPos);

                // Если блок в мире совпадает с ожидаемым
                if (worldBlock.BlockId == expectedBlock.BlockId)
                {
                    placedBlocksCount[blockLoc]++;
                    totalPlaced++;
                }
            }

            // --- ОБНОВЛЯЕМ ИНТЕРФЕЙС ---
            UpdateTrackerHud(placedBlocksCount);

            // Если собраны все блоки, возвращаем true
            return totalPlaced == totalRequired;
        }
        private void UpdateTrackerHud(Dictionary<AssetLocation, int> placed)
        {
            if (trackerHud == null)
            {
                trackerHud = new GuiDialogStructureTracker(capi);
                trackerHud.TryOpen();
            }

            // Создаём список данных для перестроения интерфейса
            List<StructureTrackerItemData> itemsData = new List<StructureTrackerItemData>();

            foreach (var kvp in requiredBlocksCount)
            {
                AssetLocation loc = kvp.Key;
                int reqCount = kvp.Value;
                int placedCount = placed[loc];

                // Достаем блок, чтобы получить его иконку и имя
                Block block = capi.World.GetBlock(loc);
                if (block == null || block.BlockId == 0) continue; // Пропускаем неизвестные

                itemsData.Add(new StructureTrackerItemData
                {
                    Block = block,
                    RequiredCount = reqCount,
                    PlacedCount = placedCount
                });
            }

            // Вызываем метод полного перестроения ХУДа, передавая список
            trackerHud.Rebuild(itemsData, currentStructure);
        }
        private bool IsStructureBuilt(BlockPos basePos)
        {
            int startX = basePos.X - (structureSizeX / 2);
            int startY = basePos.Y + offsetY;
            int startZ = basePos.Z - (structureSizeZ / 2);

            for (int i = 0; i < loadedSchematic.Indices.Count; i++)
            {
                int index = (int)loadedSchematic.Indices[i];
                int rawExpectedId = loadedSchematic.BlockIds[i];

                if (rawExpectedId == 0) continue; // Воздух пропускаем

                // Находим ожидаемый блок в реестре клиента по его AssetLocation
                if (!loadedSchematic.BlockCodes.TryGetValue(rawExpectedId, out AssetLocation blockLoc)) continue;
                Block expectedBlock = capi.World.GetBlock(blockLoc);

                if (expectedBlock == null || expectedBlock.BlockId == 0) continue;

                int x = index & 0x3FF;
                int z = (index >> 10) & 0x3FF;
                int y = (index >> 20) & 0x3FF;

                BlockPos worldPos = new BlockPos(startX + x, startY + y, startZ + z);
                Block worldBlock = capi.World.BlockAccessor.GetBlock(worldPos);

                // Сравниваем реальные ID блоков в мире клиента
                if (worldBlock.BlockId != expectedBlock.BlockId)
                {
                    return false;
                }
            }
            return true;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!isActive || hologramMeshRefs.Count == 0) return;

            BlockPos targetPos = lockedPos ?? capi.World.Player.CurrentBlockSelection?.Position.AddCopy(capi.World.Player.CurrentBlockSelection.Face);

            if (targetPos != null)
            {
                IRenderAPI render = capi.Render;
                Vec3d camPos = capi.World.Player.Entity.CameraPos;

                IStandardShaderProgram prog = render.PreparedStandardShader(targetPos.X, targetPos.Y, targetPos.Z);
                prog.ViewMatrix = render.CameraMatrixOriginf;
                prog.ProjectionMatrix = render.CurrentProjectionMatrix;
                prog.RgbaTint = new Vec4f(1.0f, 1.0f, 1.0f, 0.4f);

                // --- ИСПРАВЛЕНИЕ НЕВИДИМОСТИ ---
                // Сбрасываем порог альфа-теста, чтобы предыдущие блоки (например, листва)
                // не заставляли видеокарту отбрасывать наши полупрозрачные пиксели.
                prog.AlphaTest = 0.05f;
                prog.ExtraGlow = 1; // Делает голограмму чуть ярче, чтобы она выделялась

                float[] modelMatrix = Mat4f.Create();
                Mat4f.Identity(modelMatrix);
                Mat4f.Translate(modelMatrix, modelMatrix,
                    (float)(targetPos.X - camPos.X),
                    (float)(targetPos.Y + renderOffsetY - camPos.Y),
                    (float)(targetPos.Z - camPos.Z));
                Mat4f.Translate(modelMatrix, modelMatrix, -structureSizeX / 2f + 0.5f, 0, -structureSizeZ / 2f + 0.5f);
                prog.ModelMatrix = modelMatrix;

                render.GlToggleBlend(true);

                // --- Цикл рендера по страницам ---
                foreach (var kvp in hologramMeshRefs)
                {
                    int atlasPage = kvp.Key;
                    MeshRef mesh = kvp.Value;

                    prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[atlasPage].TextureId;
                    render.RenderMesh(mesh);
                }

                render.GlToggleBlend(false);
                prog.Stop();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            capi?.Event.UnregisterGameTickListener(tickListenerId); // Обязательно убиваем таймер при выходе
           
        }
    }
}