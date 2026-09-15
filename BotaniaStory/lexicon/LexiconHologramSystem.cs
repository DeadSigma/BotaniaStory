using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using BotaniaStory.systems;

namespace BotaniaStory.lexicon
{
    public class LexiconHologramSystem : ModSystem, IRenderer
    {
        private ICoreClientAPI capi;
        public bool isActive = false;
        private string currentStructure = null;

        private Dictionary<int, MeshRef> hologramMeshRefs = new Dictionary<int, MeshRef>();
        private int structureSizeX, structureSizeY, structureSizeZ;

        private int offsetY = 0;
        private float renderOffsetY = 0f;
        private int currentFacing = 0;
        private float renderRotationY = 0f;
        private BlockSchematic loadedSchematic = null;
        private BlockPos lockedPos = null;
        private long tickListenerId;
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

            // Проверка структуры раз в секунду
            tickListenerId = api.Event.RegisterGameTickListener(OnCheckStructureTick, 1000);
        }

        public void StartVisualization(string structureCode)
        {
            currentStructure = structureCode;
            isActive = true;
            lockedPos = null;
            loadedSchematic = null;

            if (structureCode == "terraaltar")
            {
                // Алтарь уходит на один блок вниз
                offsetY = -1;
                renderOffsetY = -0.9f;
            }
            else if (structureCode == "alfheimgates" || structureCode == "gaiarutial")
            {
                offsetY = 0;
                renderOffsetY = 0f;
            }
            else
            {
                offsetY = 0;
                renderOffsetY = 0f;
            }

            BuildHologramMesh(structureCode);
        }

        private void BuildHologramMesh(string structureCode)
        {
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

            foreach (var mesh in hologramMeshRefs.Values)
            {
                mesh.Dispose();
            }
            hologramMeshRefs.Clear();
        }

        private void OnMouseDown(MouseEvent args)
        {
            if (!isActive || args.Button != EnumMouseButton.Right) return;

            ItemSlot activeSlot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            bool isEmptyHand = activeSlot.Empty;
            bool isBook = !isEmptyHand && activeSlot.Itemstack.Collectible.Code.Domain == "botaniastory";
            bool isSneaking = capi.World.Player.Entity.Controls.Sneak;

            if (lockedPos == null)
            {
                if ((activeSlot.Empty || isBook) && capi.World.Player.CurrentBlockSelection != null)
                {
                    // Фиксируем позицию с текущим поворотом
                    lockedPos = capi.World.Player.CurrentBlockSelection.Position.AddCopy(capi.World.Player.CurrentBlockSelection.Face);
                    args.Handled = true;
                }
            }
            else
            {
                if (isBook) return;

                if (isEmptyHand)
                {
                    if (isSneaking)
                    {
                        // Shift + ПКМ - закрыть голограмму
                        isActive = false;
                        currentStructure = null;
                        lockedPos = null;
                        loadedSchematic = null;
                    }
                    else
                    {
                        // ПКМ - выбрать новое место
                        lockedPos = null;
                    }

                    if (capi.World.Player.CurrentBlockSelection != null)
                    {
                        args.Handled = true;
                    }
                }
            }
        }

        private void OnCheckStructureTick(float dt)
        {
            if (!isActive || loadedSchematic == null || lockedPos == null) return;

            bool isComplete = CheckAndUpdateProgress(lockedPos);

            if (isComplete)
            {
                StopVisualization();
            }
        }

        private bool CheckAndUpdateProgress(BlockPos basePos)
        {
            int startX = basePos.X - (structureSizeX / 2);
            int startY = basePos.Y + offsetY;
            int startZ = basePos.Z - (structureSizeZ / 2);

            Dictionary<AssetLocation, int> placedBlocksCount = new Dictionary<AssetLocation, int>();
            foreach (var key in requiredBlocksCount.Keys) placedBlocksCount[key] = 0;

            int totalRequired = 0;
            int totalPlaced = 0;

            for (int i = 0; i < loadedSchematic.Indices.Count; i++)
            {
                int index = (int)loadedSchematic.Indices[i];
                int rawExpectedId = loadedSchematic.BlockIds[i];

                if (rawExpectedId == 0) continue;

                totalRequired++;

                if (!loadedSchematic.BlockCodes.TryGetValue(rawExpectedId, out AssetLocation blockLoc)) continue;
                Block expectedBlock = capi.World.GetBlock(blockLoc);
                if (expectedBlock == null || expectedBlock.BlockId == 0) continue;

                int x = index & 0x3FF;
                int z = (index >> 10) & 0x3FF;
                int y = (index >> 20) & 0x3FF;

                int offsetX = x - (structureSizeX / 2);
                int offsetZ = z - (structureSizeZ / 2);

                int finalOffsetX = offsetX;
                int finalOffsetZ = offsetZ;

                // Поворот координат вокруг центра
                switch (currentFacing)
                {
                    case 0:
                        finalOffsetX = offsetX;
                        finalOffsetZ = offsetZ;
                        break;
                    case 1:
                        finalOffsetX = offsetZ;
                        finalOffsetZ = -offsetX;
                        break;
                    case 2:
                        finalOffsetX = -offsetX;
                        finalOffsetZ = -offsetZ;
                        break;
                    case 3:
                        finalOffsetX = -offsetZ;
                        finalOffsetZ = offsetX;
                        break;
                }

                BlockPos worldPos = new BlockPos(basePos.X + finalOffsetX, startY + y, basePos.Z + finalOffsetZ);
                Block worldBlock = capi.World.BlockAccessor.GetBlock(worldPos);

                if (worldBlock.BlockId == expectedBlock.BlockId)
                {
                    placedBlocksCount[blockLoc]++;
                    totalPlaced++;
                }
            }

            UpdateTrackerHud(placedBlocksCount);
            return totalPlaced == totalRequired;
        }

        private void UpdateTrackerHud(Dictionary<AssetLocation, int> placed)
        {
            if (trackerHud == null)
            {
                trackerHud = new GuiDialogStructureTracker(capi);
                trackerHud.TryOpen();
            }

            List<StructureTrackerItemData> itemsData = new List<StructureTrackerItemData>();

            foreach (var kvp in requiredBlocksCount)
            {
                AssetLocation loc = kvp.Key;
                int reqCount = kvp.Value;
                int placedCount = placed[loc];

                Block block = capi.World.GetBlock(loc);
                if (block == null || block.BlockId == 0) continue;

                itemsData.Add(new StructureTrackerItemData
                {
                    Block = block,
                    RequiredCount = reqCount,
                    PlacedCount = placedCount
                });
            }

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

                if (rawExpectedId == 0) continue;

                if (!loadedSchematic.BlockCodes.TryGetValue(rawExpectedId, out AssetLocation blockLoc)) continue;
                Block expectedBlock = capi.World.GetBlock(blockLoc);

                if (expectedBlock == null || expectedBlock.BlockId == 0) continue;

                int x = index & 0x3FF;
                int z = (index >> 10) & 0x3FF;
                int y = (index >> 20) & 0x3FF;

                BlockPos worldPos = new BlockPos(startX + x, startY + y, startZ + z);
                Block worldBlock = capi.World.BlockAccessor.GetBlock(worldPos);

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

            BlockSelection selection = capi.World.Player.CurrentBlockSelection;
            BlockPos targetPos = lockedPos ?? selection?.Position.AddCopy(selection.Face);

            if (targetPos != null)
            {
                if (lockedPos == null)
                {
                    float yaw = capi.World.Player.Entity.Pos.Yaw;

                    // Направление голограммы по взгляду игрока
                    currentFacing = ((int)System.Math.Round(yaw / GameMath.PIHALF) + 1) % 4;
                    if (currentFacing < 0) currentFacing += 4;

                    renderRotationY = currentFacing * GameMath.PIHALF;
                }

                IRenderAPI render = capi.Render;
                Vec3d camPos = capi.World.Player.Entity.CameraPos;

                IStandardShaderProgram prog = render.PreparedStandardShader(targetPos.X, targetPos.Y, targetPos.Z);
                ShaderSanitizer.Sanitize(prog);
                prog.ViewMatrix = render.CameraMatrixOriginf;
                prog.ProjectionMatrix = render.CurrentProjectionMatrix;
                prog.RgbaTint = new Vec4f(1.0f, 1.0f, 1.0f, 0.4f);

                // Низкий порог нужен для полупрозрачных текстур
                prog.AlphaTest = 0.05f;
                prog.ExtraGlow = 1;

                float[] modelMatrix = Mat4f.Create();
                Mat4f.Identity(modelMatrix);

                Mat4f.Translate(modelMatrix, modelMatrix,
                    (float)(targetPos.X - camPos.X) + 0.5f,
                    (float)(targetPos.Y + renderOffsetY - camPos.Y),
                    (float)(targetPos.Z - camPos.Z) + 0.5f);

                Mat4f.RotateY(modelMatrix, modelMatrix, renderRotationY);

                // Центруем схему перед поворотом
                Mat4f.Translate(modelMatrix, modelMatrix, -structureSizeX / 2f, 0, -structureSizeZ / 2f);

                prog.ModelMatrix = modelMatrix;

                render.GlToggleBlend(true);

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
            capi?.Event.UnregisterGameTickListener(tickListenerId);
        }
    }
}
