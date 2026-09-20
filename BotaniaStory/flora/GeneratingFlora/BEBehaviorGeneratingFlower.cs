using BotaniaStory.blockentity;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public abstract class BEBehaviorGeneratingFlower : BlockEntityBehavior, ILinkableToSpreader
    {
        public int CurrentMana = 0;
        public int MaxMana = 10000;

        protected delegate void FlowerWork(ref bool dirty);

        protected virtual bool IsPassiveFlower => false;
        protected virtual bool IsAffectedByEnchantedSoil => true;
        protected virtual int EnchantedSoilWorkMultiplier => 2;

        public BlockPos LinkedSpreader { get; set; } = null;

        public BEBehaviorGeneratingFlower(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
        }

        protected bool IsOnEnchantedSoil()
        {
            if (!IsAffectedByEnchantedSoil || this.Api?.World == null) return false;

            Block block = this.Api.World.BlockAccessor.GetBlock(this.Blockentity.Pos.DownCopy());
            if (block?.Code == null || block.Code.Domain != "botaniastory") return false;

            string path = block.Code.Path;
            return path.StartsWith("enchantedsoil") || path.StartsWith("enchantedfarmland");
        }

        protected int GetWorkIterations()
        {
            return IsOnEnchantedSoil() ? EnchantedSoilWorkMultiplier : 1;
        }

        // Рабочий цикл повторяется на зачарованной почве
        protected void RunFlowerWork(FlowerWork work, ref bool dirty)
        {
            int iterations = GetWorkIterations();

            for (int i = 0; i < iterations; i++)
            {
                work(ref dirty);
            }
        }

        protected bool IsPassiveDecayPrevented()
        {
            return IsPassiveFlower && IsOnEnchantedSoil();
        }

        public void FindSpreader()
        {
            int radius = 6;

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos checkPos = this.Blockentity.Pos.AddCopy(x, y, z);

                        if (this.Api.World.BlockAccessor.GetBlockEntity(checkPos) is BlockEntityManaSpreader)
                        {
                            LinkedSpreader = checkPos.Copy();
                            return;
                        }
                    }
                }
            }
        }

        protected void ProcessManaTransfer(ref bool dirty)
        {
            if (LinkedSpreader != null)
            {
                BlockEntity be = this.Api.World.BlockAccessor.GetBlockEntity(LinkedSpreader);
                if (!(be is BlockEntityManaSpreader))
                {
                    LinkedSpreader = null;
                    dirty = true;
                }
            }

            if (LinkedSpreader != null && CurrentMana > 0)
            {
                BlockEntity be = this.Api.World.BlockAccessor.GetBlockEntity(LinkedSpreader);
                if (be is BlockEntityManaSpreader spreader)
                {
                    int space = spreader.MaxMana - spreader.CurrentMana;
                    int toMove = Math.Min(CurrentMana, space);

                    if (toMove > 0)
                    {
                        spreader.CurrentMana += toMove;
                        spreader.MarkDirty(false);
                        CurrentMana -= toMove;
                        dirty = true;
                    }
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("mana", CurrentMana);

            if (LinkedSpreader != null)
            {
                tree.SetInt("lx", LinkedSpreader.X);
                tree.SetInt("ly", LinkedSpreader.Y);
                tree.SetInt("lz", LinkedSpreader.Z);
                tree.SetBool("hasSpreader", true);
            }
            else
            {
                tree.SetBool("hasSpreader", false);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            CurrentMana = tree.GetInt("mana");

            if (tree.GetBool("hasSpreader"))
            {
                LinkedSpreader = new BlockPos(
                    tree.GetInt("lx"),
                    tree.GetInt("ly"),
                    tree.GetInt("lz")
                );
            }
            else
            {
                LinkedSpreader = null;
            }
        }
    }
}
