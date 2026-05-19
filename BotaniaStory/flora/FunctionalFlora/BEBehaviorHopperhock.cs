using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory.blockentity // Или твой новый namespace
{
    public class BEBehaviorHopperhock : BlockEntityBehavior, ILinkableToPool
    {
        private long tickListenerId;

        // Координаты привязанного бассейна маны
        public BlockPos LinkedPool { get; set; } = null;

        public BEBehaviorHopperhock(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            if (api.Side == EnumAppSide.Server)
            {
                tickListenerId = this.Blockentity.RegisterGameTickListener(OnTick, 4000);
            }
        }

        private void OnTick(float dt)
        {
            int currentRadius = 3; // Радиус без маны, если ты бомж
            BlockEntityManaPool pool = null;

            if (LinkedPool != null && this.Api.World.BlockAccessor.GetBlockEntity(LinkedPool) is BlockEntityManaPool p)
            {
                pool = p;
                if (pool.CurrentMana >= 20) currentRadius = 10; // Потребление маны // Радиус но уже с маной 
            }

            // 1. Ищем инвентари
            List<IInventory> adjacentInventories = GetAdjacentInventories();
            if (adjacentInventories.Count == 0) return;

            // 2. Ищем сущности-предметы
            Vec3d centerPos = this.Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5);
            Entity[] entities = this.Api.World.GetEntitiesAround(centerPos, currentRadius, currentRadius, (e) => e is EntityItem);

            foreach (Entity entity in entities)
            {
                if (entity is EntityItem entityItem && entityItem.Alive)
                {
                    ItemStack stackToMove = entityItem.Itemstack;
                    if (stackToMove == null || stackToMove.StackSize <= 0) continue;

                    int initialStackSize = stackToMove.StackSize;

                    foreach (IInventory inv in adjacentInventories)
                    {
                        if (TryInsertItem(inv, stackToMove))
                        {
                            int itemsMoved = initialStackSize - stackToMove.StackSize;

                            // Потребляем ману
                            if (pool != null && pool.CurrentMana >= 20 * itemsMoved)
                            {
                                pool.ConsumeMana(20 * itemsMoved);
                            }

                            // Если скушали весь стак с земли - удаляем сущность
                            if (stackToMove.StackSize <= 0)
                            {
                                entityItem.Die();
                            }

                            this.Api.World.PlaySoundAt(new AssetLocation("game", "sounds/player/collect"), this.Blockentity.Pos.X + 0.5, this.Blockentity.Pos.Y + 0.5, this.Blockentity.Pos.Z + 0.5, null, true, 16, 0.5f);

                            break;
                        }
                    }
                }
            }
        }

        // Автоматический поиск бассейна при установке
        public void AutoFindPool()
        {
            int searchRadius = 6;
            for (int x = -searchRadius; x <= searchRadius; x++)
            {
                for (int y = -searchRadius; y <= searchRadius; y++)
                {
                    for (int z = -searchRadius; z <= searchRadius; z++)
                    {
                        BlockPos checkPos = this.Blockentity.Pos.AddCopy(x, y, z);
                        if (this.Api.World.BlockAccessor.GetBlockEntity(checkPos) is BlockEntityManaPool)
                        {
                            LinkedPool = checkPos.Copy();
                            this.Blockentity.MarkDirty(true);
                            return;
                        }
                    }
                }
            }
        }

        // Вспомогательные методы вставки и поиска инвентарей
        private List<IInventory> GetAdjacentInventories()
        {
            List<IInventory> inventories = new List<IInventory>();
            BlockFacing[] facings = BlockFacing.ALLFACES;
            foreach (BlockFacing facing in facings)
            {
                BlockPos checkPos = this.Blockentity.Pos.AddCopy(facing);
                BlockEntity be = this.Api.World.BlockAccessor.GetBlockEntity(checkPos);
                if (be is BlockEntityContainer container && container.Inventory != null)
                {
                    inventories.Add(container.Inventory);
                }
            }
            return inventories;
        }

        private bool TryInsertItem(IInventory targetInv, ItemStack stack)
        {
            bool itemMoved = false;

            // Сначала ищем уже существующие неполные стаки такого же предмета
            for (int i = 0; i < targetInv.Count; i++)
            {
                ItemSlot slot = targetInv[i];
                if (slot.Empty || stack.StackSize <= 0) continue;

                // Сравниваем предметы
                if (slot.Itemstack.Equals(this.Api.World, stack, "name"))
                {
                    // Используем Collectible! Это работает и для предметов (Item), и для блоков (Block)
                    int maxStackSize = slot.Itemstack.Collectible.MaxStackSize;

                    if (slot.Itemstack.StackSize < maxStackSize)
                    {
                        int spaceLeft = maxStackSize - slot.Itemstack.StackSize;
                        int amountToMove = Math.Min(spaceLeft, stack.StackSize);

                        slot.Itemstack.StackSize += amountToMove;
                        stack.StackSize -= amountToMove;
                        slot.MarkDirty();
                        itemMoved = true;

                        // Если весь предмет распихали по стакам — сразу выходим
                        if (stack.StackSize <= 0) return true;
                    }
                }
            }

            // Если предмет всё ещё остался (в руках у цветка), ищем для него ПУСТОЙ слот
            if (stack.StackSize > 0)
            {
                for (int i = 0; i < targetInv.Count; i++)
                {
                    ItemSlot slot = targetInv[i];
                    if (slot.Empty)
                    {
                        slot.Itemstack = stack.Clone();
                        stack.StackSize = 0;
                        slot.MarkDirty();
                        itemMoved = true;
                        break; // Предмет положен, прекращаем поиск пустых слотов
                    }
                }
            }

            return itemMoved;
        }

        // СОХРАНЕНИЕ И ЗАГРУЗКА ПРИВЯЗКИ
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (LinkedPool != null)
            {
                tree.SetInt("poolX", LinkedPool.X);
                tree.SetInt("poolY", LinkedPool.Y);
                tree.SetInt("poolZ", LinkedPool.Z);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            if (tree.HasAttribute("poolX"))
            {
                LinkedPool = new BlockPos(tree.GetInt("poolX"), tree.GetInt("poolY"), tree.GetInt("poolZ"));
            }
            else
            {
                LinkedPool = null;
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (tickListenerId != 0) this.Blockentity.UnregisterGameTickListener(tickListenerId);
        }
    }
}