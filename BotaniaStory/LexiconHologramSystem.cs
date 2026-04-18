using System.Collections.Generic;
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
        private MeshRef hologramMeshRef = null;
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

            if (isActive)
            {
                capi.ShowChatMessage($"[Lexicon] Режим голограммы активирован для: {structureCode}.");
                capi.ShowChatMessage($"[Lexicon] Кликните ПКМ, чтобы закрепить голограмму на месте и начать стройку.");
            }
        }

        private void BuildHologramMesh(string structureCode)
        {
            hologramMeshRef?.Dispose();
            hologramMeshRef = null;

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

            // === ИСПРАВЛЕНИЕ КРАША (Версия 3.0) ===
            if (schematic.BlockCodes == null)
            {
                capi.ShowChatMessage($"[Lexicon] Ошибка: В схеме {structureCode} отсутствуют данные о блоках (BlockCodes).");
                isActive = false;
                return;
            }

            // Сохраняем схему в память для будущих проверок
            loadedSchematic = schematic;

            structureSizeX = schematic.SizeX;
            structureSizeY = schematic.SizeY;
            structureSizeZ = schematic.SizeZ;

            MeshData combinedMesh = null;
            requiredBlocksCount.Clear();

            // ЕДИНЫЙ ЦИКЛ: и для подсчета HUD, и для сборки 3D-меша
            for (int i = 0; i < schematic.Indices.Count; i++)
            {
                int index = (int)schematic.Indices[i];
                int rawBlockId = schematic.BlockIds[i];

                if (rawBlockId == 0) continue; // Воздух не считаем и не рисуем

                if (schematic.BlockCodes.TryGetValue(rawBlockId, out AssetLocation blockLoc))
                {
                    // --- 1. ЛОГИКА ДЛЯ HUD (Считаем нужное количество) ---
                    if (!requiredBlocksCount.ContainsKey(blockLoc))
                    {
                        requiredBlocksCount[blockLoc] = 0;
                    }
                    requiredBlocksCount[blockLoc]++;

                    // --- 2. ЛОГИКА ДЛЯ ГОЛОГРАММЫ (Строим меш) ---
                    Block block = capi.World.GetBlock(blockLoc);
                    if (block == null || block.BlockId == 0) continue;

                    MeshData blockMesh;
                    capi.Tesselator.TesselateBlock(block, out blockMesh);

                    if (blockMesh != null)
                    {
                        // Высчитываем координаты блока внутри схемы
                        int x = index & 0x3FF;
                        int z = (index >> 10) & 0x3FF;
                        int y = (index >> 20) & 0x3FF;

                        // Сдвигаем меш отдельного блока на его позицию
                        blockMesh.Translate(new Vec3f(x, y, z));

                        // Склеиваем с общим мешом структуры
                        if (combinedMesh == null)
                        {
                            combinedMesh = blockMesh.Clone();
                        }
                        else
                        {
                            combinedMesh.AddMeshData(blockMesh);
                        }
                    }
                }
            }

            // Загружаем готовый меш в видеокарту
            if (combinedMesh != null && combinedMesh.VerticesCount > 0)
            {
                hologramMeshRef = capi.Render.UploadMesh(combinedMesh);
            }
            else
            {
                capi.ShowChatMessage("[Lexicon] Ошибка: Меш пустой.");
                isActive = false;
            }
        }

        // === НОВЫЙ МЕТОД ДЛЯ ОТКЛЮЧЕНИЯ ГОЛОГРАММЫ ===
        public void StopVisualization()
        {
            isActive = false;           // Выключаем отрисовку
            currentStructure = null;    // Сбрасываем имя структуры
            lockedPos = null;           // Сбрасываем позицию
            loadedSchematic = null;     // Очищаем схему

            if (trackerHud != null)
            {
                trackerHud.TryClose();
                trackerHud.Dispose();
                trackerHud = null;
            }
            // Обязательно очищаем память от 3D-модели!
            hologramMeshRef?.Dispose();
            hologramMeshRef = null;
        }

        // --- НОВАЯ ЛОГИКА ПКМ (ФИКСАЦИЯ) ---
        private void OnMouseDown(MouseEvent args)
        {
            if (!isActive || args.Button != EnumMouseButton.Right) return;

            ItemSlot activeSlot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            bool isEmptyHand = activeSlot.Empty;

            // Убедись, что Domain ("botaniastory") совпадает с доменом твоего предмета книги!
            bool isBook = !isEmptyHand && activeSlot.Itemstack.Collectible.Code.Domain == "botaniastory";

            bool isSneaking = capi.World.Player.Entity.Controls.Sneak;

            if (lockedPos == null)
            {
                // 1. УСТАНОВКА (Книгой или пустой рукой)
                if ((isEmptyHand || isBook) && capi.World.Player.CurrentBlockSelection != null)
                {
                    lockedPos = capi.World.Player.CurrentBlockSelection.Position.AddCopy(capi.World.Player.CurrentBlockSelection.Face);
                    capi.ShowChatMessage("[Lexicon] Голограмма закреплена! Разместите блоки согласно проекции.");

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
                        capi.ShowChatMessage("[Lexicon] Визуализация полностью отменена.");
                    }
                    else
                    {
                        // ПРОСТО ПЕРЕНОС (Обычный ПКМ пустой рукой)
                        lockedPos = null;
                        capi.ShowChatMessage("[Lexicon] Голограмма откреплена. Выберите новое место.");
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
                StopVisualization(); // Вызываем твой метод очистки
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
            if (!isActive || hologramMeshRef == null) return;

            // Если позиция зафиксирована - рисуем там. Если нет - рисуем по прицелу игрока.
            BlockPos targetPos = lockedPos ?? capi.World.Player.CurrentBlockSelection?.Position.AddCopy(capi.World.Player.CurrentBlockSelection.Face);

            if (targetPos != null)
            {
                IRenderAPI render = capi.Render;
                Vec3d camPos = capi.World.Player.Entity.CameraPos;

                IStandardShaderProgram prog = render.PreparedStandardShader(targetPos.X, targetPos.Y, targetPos.Z);
                prog.ViewMatrix = render.CameraMatrixOriginf;
                prog.ProjectionMatrix = render.CurrentProjectionMatrix;

                prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;
                prog.RgbaTint = new Vec4f(1.0f, 1.0f, 1.0f, 0.4f);

                float[] modelMatrix = Mat4f.Create();
                Mat4f.Identity(modelMatrix);

                Mat4f.Translate(modelMatrix, modelMatrix,
                    (float)(targetPos.X - camPos.X),
                  (float)(targetPos.Y + renderOffsetY - camPos.Y), 
                    (float)(targetPos.Z - camPos.Z));

                Mat4f.Translate(modelMatrix, modelMatrix, -structureSizeX / 2f + 0.5f, 0, -structureSizeZ / 2f + 0.5f);

                prog.ModelMatrix = modelMatrix;

                render.GlToggleBlend(true);
                render.RenderMesh(hologramMeshRef);
                render.GlToggleBlend(false);

                prog.Stop();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            capi?.Event.UnregisterGameTickListener(tickListenerId); // Обязательно убиваем таймер при выходе
            hologramMeshRef?.Dispose();
        }
    }
}