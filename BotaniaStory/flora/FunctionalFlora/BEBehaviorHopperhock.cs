using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Linq;
using BotaniaStory.items;

namespace BotaniaStory.blockentity
{
    public class BEBehaviorHopperhock : BlockEntityBehavior, ILinkableToPool
    {
        private long tickListenerId;
        public InventoryGeneric FilterInventory;
        public BlockPos LinkedPool { get; set; } = null;

        public BEBehaviorHopperhock(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (FilterInventory == null)
            {
                FilterInventory = new InventoryGeneric(2, "hopperhock-filters-" + this.Blockentity.Pos, api);
            }
            else
            {
                // Если инвентарь был загружен из сохранения, привязываем API
                FilterInventory.Api = api;
            }

            if (api.Side == EnumAppSide.Server)
            {
                tickListenerId = this.Blockentity.RegisterGameTickListener(OnTick, 4000);
            }
        }

        private void OnTick(float dt)
        {
            int currentRadius = 3;
            BlockEntityManaPool pool = null;

            if (LinkedPool != null && this.Api.World.BlockAccessor.GetBlockEntity(LinkedPool) is BlockEntityManaPool p)
            {
                pool = p;
                if (pool.CurrentMana >= 20) currentRadius = 10;
            }

            List<IInventory> adjacentInventories = GetAdjacentInventories();
            if (adjacentInventories.Count == 0) return;

            Vec3d centerPos = this.Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5);
            Entity[] entities = this.Api.World.GetEntitiesAround(centerPos, currentRadius, currentRadius, (e) => e is EntityItem);

            foreach (Entity entity in entities)
            {
                if (entity is EntityItem entityItem && entityItem.Alive)
                {
                    ItemStack stackToMove = entityItem.Itemstack;
                    if (stackToMove == null || stackToMove.StackSize <= 0) continue;

                    string itemCodeStr = stackToMove.Collectible.Code.ToString();
                    bool isAllowed = true;

                    // Проверяем Белый список (Слот 0)
                    ItemStack whiteLeaf = FilterInventory[0].Itemstack;
                    if (whiteLeaf != null)
                    {
                        isAllowed = false;

                        if (whiteLeaf.Item is ItemFilterScroll whiteFilter)
                        {
                            if (whiteFilter.AllowsItem(whiteLeaf, itemCodeStr))
                            {
                                isAllowed = true;
                            }
                        }
                    }

                    // Проверяем Черный список (Слот 1)
                    ItemStack blackLeaf = FilterInventory[1].Itemstack;
                    if (blackLeaf != null && isAllowed)
                    {
                        if (blackLeaf.Item is ItemFilterScroll blackFilter)
                        {
                            if (!blackFilter.AllowsItem(blackLeaf, itemCodeStr))
                            {
                                isAllowed = false;
                            }
                        }
                    }

                    if (!isAllowed) continue;

                    int initialStackSize = stackToMove.StackSize;

                    foreach (IInventory inv in adjacentInventories)
                    {
                        if (TryInsertItem(inv, stackToMove))
                        {
                            int itemsMoved = initialStackSize - stackToMove.StackSize;

                            if (pool != null && pool.CurrentMana >= 20 * itemsMoved)
                            {
                                pool.ConsumeMana(20 * itemsMoved);
                            }

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
            for (int i = 0; i < targetInv.Count; i++)
            {
                ItemSlot slot = targetInv[i];
                if (slot.Empty || stack.StackSize <= 0) continue;

                if (slot.Itemstack.Equals(this.Api.World, stack, "name"))
                {
                    int maxStackSize = slot.Itemstack.Collectible.MaxStackSize;

                    if (slot.Itemstack.StackSize < maxStackSize)
                    {
                        int spaceLeft = maxStackSize - slot.Itemstack.StackSize;
                        int amountToMove = Math.Min(spaceLeft, stack.StackSize);

                        slot.Itemstack.StackSize += amountToMove;
                        stack.StackSize -= amountToMove;
                        slot.MarkDirty();
                        itemMoved = true;

                        if (stack.StackSize <= 0) return true;
                    }
                }
            }

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
                        break;
                    }
                }
            }
            return itemMoved;
        }

        // СОХРАНЕНИЕ, ЗАГРУЗКА И ВЫПАДЕНИЕ
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (LinkedPool != null)
            {
                tree.SetInt("poolX", LinkedPool.X);
                tree.SetInt("poolY", LinkedPool.Y);
                tree.SetInt("poolZ", LinkedPool.Z);
            }

            // Создаем отдельное поддерево, чтобы избежать конфликтов с другими инвентарями
            ITreeAttribute filterInvTree = new TreeAttribute();
            FilterInventory?.ToTreeAttributes(filterInvTree);
            tree["filterInv"] = filterInvTree;
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

            // Инициализируем инвентарь, если он еще не создан
            if (FilterInventory == null)
            {
                FilterInventory = new InventoryGeneric(2, "hopperhock-filters-" + this.Blockentity.Pos, worldForResolving.Api);
            }

            // Загружаем предметы и обязательно их "резолвим", чтобы игра узнала что это за предметы
            ITreeAttribute filterInvTree = tree.GetTreeAttribute("filterInv");
            if (filterInvTree != null)
            {
                FilterInventory.FromTreeAttributes(filterInvTree);
                FilterInventory.ResolveBlocksOrItems();
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (tickListenerId != 0) this.Blockentity.UnregisterGameTickListener(tickListenerId);

            // Выбрасываем фильтры при разрушении блока
            if (this.Api.Side == EnumAppSide.Server && FilterInventory != null)
            {
                FilterInventory.DropAll(this.Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }
    }
}