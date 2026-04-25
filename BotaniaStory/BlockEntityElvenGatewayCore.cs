using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory
{
    public class BlockEntityElvenGatewayCore : BlockEntity
    {
        public bool IsActive { get; private set; }

        // Очередь для хранения предметов, которые портал должен выплюнуть
        private Queue<ItemStack> _returnQueue = new Queue<ItemStack>();
        // Память для хранения неполных рецептов (например, 1 слиток манастали из 2 нужных)
        private Dictionary<string, int> _itemBuffer = new Dictionary<string, int>();

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                // Проверка целостности структуры
                RegisterGameTickListener(CheckStructureTick, 500);

                // Проверка брошенных предметов (быстрое поглощение)
                RegisterGameTickListener(CheckForDroppedItems, 250);

                // Выдача предметов из очереди (2 предмета в секунду)
                RegisterGameTickListener(SpitOutItemsTick, 500);
            }
        }

        private void CheckStructureTick(float dt)
        {
            if (!IsActive) return;

            if (GetPortalOrientation() == "none")
            {
                Deactivate();
            }
        }

        private string GetPortalOrientation()
        {
            if (CheckAxis(true)) return "we";
            if (CheckAxis(false)) return "ns";
            return "none";
        }

        private bool CheckAxis(bool isXAxis)
        {
            int[,] livingwoodOffsets = new int[,]
            {
                { -1, 0 }, { 1, 0 },
                { -2, 1 }, { 2, 1 },
                { -2, 3 }, { 2, 3 },
                { -1, 4 }, { 1, 4 }
            };

            int[,] glimmeringOffsets = new int[,]
            {
                { -2, 2 }, { 2, 2 },
                { 0, 4 }
            };

            for (int i = 0; i < livingwoodOffsets.GetLength(0); i++)
            {
                int dx = isXAxis ? livingwoodOffsets[i, 0] : 0;
                int dy = livingwoodOffsets[i, 1];
                int dz = isXAxis ? 0 : livingwoodOffsets[i, 0];

                BlockPos checkPos = Pos.AddCopy(dx, dy, dz);
                Block block = Api.World.BlockAccessor.GetBlock(checkPos);

                if (block == null || !block.Code.Path.Contains("livingwood-normal") || block.Code.Path.Contains("glimmering"))
                {
                    return false;
                }
            }

            for (int i = 0; i < glimmeringOffsets.GetLength(0); i++)
            {
                int dx = isXAxis ? glimmeringOffsets[i, 0] : 0;
                int dy = glimmeringOffsets[i, 1];
                int dz = isXAxis ? 0 : glimmeringOffsets[i, 0];

                BlockPos checkPos = Pos.AddCopy(dx, dy, dz);
                Block block = Api.World.BlockAccessor.GetBlock(checkPos);

                if (block == null || !block.Code.Path.Contains("glimmering-livingwood"))
                {
                    return false;
                }
            }

            return true;
        }

        public void OnInteract(IPlayer byPlayer)
        {
            if (Api.Side == EnumAppSide.Client) return;

            bool wasActive = IsActive;

            if (IsActive)
                Deactivate();
            else
                Activate();

            if (IsActive != wasActive)
            {
                var sapi = Api as ICoreServerAPI;
                var channel = sapi.Network.GetChannel("botanianetwork");

                channel.BroadcastPacket(new PlayManaSoundPacket()
                {
                    Position = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5),
                    SoundName = "wand_bind"
                });
            }
        }

        public void Activate()
        {
            if (IsActive) return;

            string orientation = GetPortalOrientation();
            if (orientation == "none") return;

            List<BlockEntityPylon> validPylons = new List<BlockEntityPylon>();
            List<BlockEntityManaPool> validPools = new List<BlockEntityManaPool>();
            int radius = 5;

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos pylonPos = Pos.AddCopy(x, y, z);
                        if (Api.World.BlockAccessor.GetBlockEntity(pylonPos) is BlockEntityPylon pylon)
                        {
                            if (pylon.CurrentType == EnumPylonType.Natura)
                            {
                                BlockPos poolPos = pylonPos.DownCopy();
                                if (Api.World.BlockAccessor.GetBlockEntity(poolPos) is BlockEntityManaPool pool)
                                {
                                    validPylons.Add(pylon);
                                    validPools.Add(pool);
                                }
                            }
                        }
                    }
                }
            }

            if (validPylons.Count < 2) return;

            int activationCost = 500000;
            int costPerPool = activationCost / validPools.Count;

            foreach (var pool in validPools)
            {
                if (pool.CurrentMana < costPerPool) return;
            }

            foreach (var pool in validPools)
            {
                pool.ConsumeMana(costPerPool);
            }

            IsActive = true;
            UpdateBlockState("on");

            foreach (var pylon in validPylons)
            {
                pylon.LinkedTarget = this.Pos;
                pylon.MarkDirty(true);
            }

            if (Api.Side == EnumAppSide.Server)
            {
                BlockPos portalPos = Pos.UpCopy();
                Block portalBlock = Api.World.GetBlock(new AssetLocation("botaniastory", "alfheim_portal_dummy-" + orientation));

                if (portalBlock != null)
                {
                    Api.World.BlockAccessor.SetBlock(portalBlock.BlockId, portalPos);
                }
            }

            MarkDirty(true);
        }

        public void Deactivate(bool isBeingBroken = false)
        {
            if (!IsActive) return;

            IsActive = false;

            if (!isBeingBroken)
            {
                UpdateBlockState("off");
            }

            int radius = 12;
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos pylonPos = Pos.AddCopy(x, y, z);
                        if (Api.World.BlockAccessor.GetBlockEntity(pylonPos) is BlockEntityPylon pylon)
                        {
                            if (pylon.LinkedTarget != null && pylon.LinkedTarget.Equals(this.Pos))
                            {
                                pylon.LinkedTarget = null;
                                pylon.MarkDirty(true);
                            }
                        }
                    }
                }
            }

            if (Api.Side == EnumAppSide.Server)
            {
                BlockPos portalPos = Pos.UpCopy();
                Block currentBlock = Api.World.BlockAccessor.GetBlock(portalPos);

                if (currentBlock != null && currentBlock.Code.Path.Contains("alfheim_portal_dummy"))
                {
                    Api.World.BlockAccessor.SetBlock(0, portalPos);
                }
            }

            MarkDirty(true);
        }

        private void UpdateBlockState(string state)
        {
            AssetLocation newCode = Block.CodeWithVariant("state", state);
            Block nextBlock = Api.World.GetBlock(newCode);

            if (nextBlock != null)
            {
                Api.World.BlockAccessor.ExchangeBlock(nextBlock.Id, Pos);

                if (Api.World.BlockAccessor.GetBlockEntity(Pos) is BlockEntityElvenGatewayCore newCore)
                {
                    newCore.IsActive = (state == "on");
                }
            }
        }

        // ==========================================
        // ОБМЕН ПРЕДМЕТОВ С АЛЬФХЕЙМОМ
        // ==========================================

        private void CheckForDroppedItems(float dt)
        {
            if (!IsActive) return;

            Entity[] entities = Api.World.GetEntitiesAround(Pos.ToVec3d().Add(0.5, 1.5, 0.5), 1.5f, 1.5f, e => e is EntityItem);

            foreach (Entity entity in entities)
            {
                if (entity is EntityItem entityItem && entityItem.Itemstack != null)
                {
                    ItemStack stack = entityItem.Itemstack;
                    string fullCode = $"{stack.Collectible.Code.Domain}:{stack.Collectible.Code.Path}";

                    int manaCost = 1000; // Стоимость поглощения ОДНОГО предмета

                    if (fullCode == "botaniastory:managlass") TryAbsorbItem(entityItem, fullCode, "botaniastory:elvenglass-0", 1, 1, manaCost);
                    else if (fullCode == "botaniastory:manaitem-managear") TryAbsorbItem(entityItem, fullCode, "botaniastory:dragonstone", 1, 1, manaCost);
                    else if (fullCode == "game:ingot-manasteel") TryAbsorbItem(entityItem, fullCode, "game:ingot-elementium", 2, 1, manaCost);
                    else if (fullCode == "botaniastory:livingwood-normal") TryAbsorbItem(entityItem, fullCode, "botaniastory:dreamwood-normal", 1, 1, manaCost);
                    else if (fullCode == "botaniastory:manaitem-manaquartz") TryAbsorbItem(entityItem, fullCode, "botaniastory:fairydust", 1, 1, manaCost);
                }
            }
        }

        private void TryAbsorbItem(EntityItem inputEntity, string inputCode, string outputItemCode, int requiredInput, int outputAmount, int manaCost)
        {
            AssetLocation loc = new AssetLocation(outputItemCode);
            Item outputItem = Api.World.GetItem(loc);
            Block outputBlock = outputItem == null ? Api.World.GetBlock(loc) : null;

            if (outputItem == null && outputBlock == null) return;

            bool absorbedAny = false;

            // Поглощаем по ОДНОМУ предмету за раз
            while (inputEntity.Itemstack.StackSize > 0)
            {
                if (!TryConsumeManaForExchange(manaCost)) break; // Если маны на 1 предмет нет — останавливаемся

                inputEntity.Itemstack.StackSize--;
                absorbedAny = true;

                // Добавляем проглоченный предмет в буфер
                if (!_itemBuffer.ContainsKey(inputCode)) _itemBuffer[inputCode] = 0;
                _itemBuffer[inputCode]++;

                // Проверяем, накопилось ли достаточно предметов для крафта
                if (_itemBuffer[inputCode] >= requiredInput)
                {
                    // Списываем нужное количество из буфера
                    _itemBuffer[inputCode] -= requiredInput;

                    // Кладем результат в очередь на выброс
                    if (outputItem != null)
                        _returnQueue.Enqueue(new ItemStack(outputItem, outputAmount));
                    else if (outputBlock != null)
                        _returnQueue.Enqueue(new ItemStack(outputBlock, outputAmount));
                }
            }

            if (absorbedAny)
            {
                if (inputEntity.Itemstack.StackSize <= 0)
                {
                    inputEntity.Die(EnumDespawnReason.Death);
                }
                else
                {
                    inputEntity.WatchedAttributes.SetItemstack("itemstack", inputEntity.Itemstack);
                    inputEntity.WatchedAttributes.MarkAllDirty();
                }

                SpawnCraftingParticles(inputEntity.Pos.XYZ);
                MarkDirty(true); // Сохраняем состояние буфера
            }
        }

        private void SpitOutItemsTick(float dt)
        {
            if (!IsActive) return;

            if (_returnQueue.Count > 0)
            {
                ItemStack stackToSpit = _returnQueue.Dequeue();

                Vec3d spawnPos = Pos.ToVec3d().Add(0.5, 1.5, 0.5);

                // 1. ИСПРАВЛЕНИЕ ОШИБКИ: Добавлено явное приведение типа через "as EntityItem"
                EntityItem spawnedItem = Api.World.SpawnItemEntity(stackToSpit, spawnPos) as EntityItem;

                if (spawnedItem != null)
                {
                    // 2. ИСПРАВЛЕНИЕ ПРЕДУПРЕЖДЕНИЯ: ServerPos заменен на Pos
                    spawnedItem.Pos.Motion = new Vec3d((Api.World.Rand.NextDouble() - 0.5) * 0.05, 0.05, (Api.World.Rand.NextDouble() - 0.5) * 0.05);
                }

                SpawnCraftingParticles(spawnPos);

                if (Api.Side == EnumAppSide.Server)
                {
                    var sapi = Api as ICoreServerAPI;
                    var channel = sapi.Network.GetChannel("botanianetwork");
                    channel.BroadcastPacket(new PlayManaSoundPacket()
                    {
                        Position = spawnPos,
                        SoundName = "alfheim_exchange"
                    });
                }

                MarkDirty(true);
            }
        }

        private bool TryConsumeManaForExchange(int totalCost)
        {
            List<BlockEntityManaPool> linkedPools = new List<BlockEntityManaPool>();
            int radius = 12;

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos pylonPos = Pos.AddCopy(x, y, z);
                        if (Api.World.BlockAccessor.GetBlockEntity(pylonPos) is BlockEntityPylon pylon)
                        {
                            if (pylon.LinkedTarget != null && pylon.LinkedTarget.Equals(this.Pos))
                            {
                                BlockPos poolPos = pylonPos.DownCopy();
                                if (Api.World.BlockAccessor.GetBlockEntity(poolPos) is BlockEntityManaPool pool)
                                {
                                    linkedPools.Add(pool);
                                }
                            }
                        }
                    }
                }
            }

            if (linkedPools.Count == 0) return false;

            int costPerPool = (int)Math.Ceiling((float)totalCost / linkedPools.Count);

            foreach (var pool in linkedPools)
            {
                if (pool.CurrentMana < costPerPool) return false;
            }

            foreach (var pool in linkedPools)
            {
                pool.ConsumeMana(costPerPool);
            }

            return true;
        }

        private void SpawnCraftingParticles(Vec3d pos)
        {
            SimpleParticleProperties particles = new SimpleParticleProperties(
                10, 15,
                ColorUtil.ToRgba(255, 0, 255, 200),
                new Vec3d(pos.X - 0.2, pos.Y, pos.Z - 0.2),
                new Vec3d(pos.X + 0.2, pos.Y + 0.5, pos.Z + 0.2),
                new Vec3f(-1f, 1f, -1f),
                new Vec3f(1f, 2f, 1f),
                1.5f,
                -0.05f,
                0.2f, 0.5f,
                EnumParticleModel.Cube
            );

            Api.World.SpawnParticles(particles);
        }

        // --- СИНХРОНИЗАЦИЯ DAA И ОЧЕРЕДИ ПРЕДМЕТОВ ---

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("isActive", IsActive);

            tree.SetInt("queueCount", _returnQueue.Count);
            int index = 0;
            foreach (var stack in _returnQueue)
            {
                tree.SetItemstack("queueItem_" + index, stack);
                index++;
            }

            // Сохраняем буфер проглоченных ингредиентов
            tree.SetInt("bufferCount", _itemBuffer.Count);
            int bIdx = 0;
            foreach (var kvp in _itemBuffer)
            {
                tree.SetString("bufferKey_" + bIdx, kvp.Key);
                tree.SetInt("bufferVal_" + bIdx, kvp.Value);
                bIdx++;
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            IsActive = tree.GetBool("isActive");

            _returnQueue.Clear();
            int count = tree.GetInt("queueCount", 0);
            for (int i = 0; i < count; i++)
            {
                ItemStack stack = tree.GetItemstack("queueItem_" + i);
                if (stack != null)
                {
                    stack.ResolveBlockOrItem(worldForResolving);
                    _returnQueue.Enqueue(stack);
                }
            }
                
            // Восстанавливаем буфер проглоченных ингредиентов
            _itemBuffer.Clear();
            int bCount = tree.GetInt("bufferCount", 0);
            for (int i = 0; i < bCount; i++)
            {
                string k = tree.GetString("bufferKey_" + i);
                int v = tree.GetInt("bufferVal_" + i);
                if (!string.IsNullOrEmpty(k)) _itemBuffer[k] = v;
            }
        }

        // --- ОЧИСТКА ПРИ РАЗРУШЕНИИ ---

        public override void OnBlockRemoved()
        {
            // Если игрок сломает ядро во время обмена, все ресурсы внутри просто исчезнут.
            // Это наказание за прерывание работы портала :)
            _returnQueue.Clear();
            _itemBuffer.Clear();

            if (IsActive)
            {
                // Вызываем деактивацию, чтобы убрать блок-заполнитель портала и отвязать пилоны
                Deactivate(true);
            }

            base.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
        }
    }
}