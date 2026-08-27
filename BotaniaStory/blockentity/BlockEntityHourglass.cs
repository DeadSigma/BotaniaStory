using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.blockentity
{
    public class BlockEntityHourglass : BlockEntity
    {
        public int SandCount = 0;
        public float TimerProgress = 0f;
        public string SandBlockCode = "";

        public bool IsFlipping = false;
        public float FlipProgress = 0f;

        private client.renderers.HourglassRenderer renderer;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // собственный RegisterGameTickListener блок-энтити, а НЕ api.Event.
            RegisterGameTickListener(OnTick, 50);

            if (api.Side == EnumAppSide.Client)
            {
                renderer = new client.renderers.HourglassRenderer((ICoreClientAPI)api, Pos, this);
            }
        }

        private void OnTick(float dt)
        {
            if (SandCount <= 0) return;

            bool isServer = Api.Side == EnumAppSide.Server;

            if (IsFlipping)
            {
                // Анимация переворота длится 0.5 секунды
                FlipProgress += dt / 0.5f;
                if (FlipProgress >= 1.0f)
                {
                    IsFlipping = false;
                    FlipProgress = 0f;

                    if (isServer)
                    {
                        TriggerAdjacentDroppers();
                        MarkDirty(true);
                    }
                }
            }
            else
            {
                TimerProgress += dt / SandCount;
                if (TimerProgress >= 1.0f)
                {
                    TimerProgress = 0f;
                    IsFlipping = true;

                    if (isServer) MarkDirty(true);
                }
            }
        }

        private void TriggerAdjacentDroppers()
        {
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                BlockPos adjPos = Pos.AddCopy(facing);
                if (Api.World.BlockAccessor.GetBlockEntity(adjPos) is BlockEntityMechanicalDropper dropper)
                {
                    dropper.DoDropFromHourglass();
                }
            }
        }

        public bool TryAddSand(ItemSlot slot)
        {
            if (SandCount >= 64) return false;
            if (slot?.Itemstack == null) return false;

            string incomingCode = slot.Itemstack.Collectible.Code.ToString();
            if (SandCount > 0 && SandBlockCode != incomingCode) return false;

            SandBlockCode = incomingCode;

            int spaceLeft = 64 - SandCount;
            int amountToAdd = System.Math.Min(spaceLeft, slot.StackSize);
            if (amountToAdd <= 0) return false;

            SandCount += amountToAdd;
            slot.TakeOut(amountToAdd);
            slot.MarkDirty();

            if (Api.Side == EnumAppSide.Server) MarkDirty(true);
            return true;
        }

        public bool TryTakeSand(IPlayer byPlayer)
        {
            if (SandCount <= 0) return false;

            AssetLocation loc = new AssetLocation(SandBlockCode);
            Block sandBlock = Api.World.GetBlock(loc);
            Item sandItem = Api.World.GetItem(loc);

            ItemStack stackToGive = null;
            if (sandBlock != null) stackToGive = new ItemStack(sandBlock, SandCount);
            else if (sandItem != null) stackToGive = new ItemStack(sandItem, SandCount);

            if (stackToGive != null && Api.Side == EnumAppSide.Server)
            {
                if (!byPlayer.InventoryManager.TryGiveItemstack(stackToGive, true))
                {
                    Api.World.SpawnItemEntity(stackToGive, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }

            SandCount = 0;
            TimerProgress = 0f;
            IsFlipping = false;
            FlipProgress = 0f;
            SandBlockCode = "";

            if (Api.Side == EnumAppSide.Server) MarkDirty(true);
            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            SandCount = tree.GetInt("sandCount");
            TimerProgress = tree.GetFloat("timerProgress");
            SandBlockCode = tree.GetString("sandBlockCode", "");
            IsFlipping = tree.GetBool("isFlipping");
            FlipProgress = tree.GetFloat("flipProgress");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("sandCount", SandCount);
            tree.SetFloat("timerProgress", TimerProgress);
            tree.SetString("sandBlockCode", SandBlockCode);
            tree.SetBool("isFlipping", IsFlipping);
            tree.SetFloat("flipProgress", FlipProgress);
        }

        // base.OnBlockRemoved() / base.OnBlockUnloaded() сами снимают тик-слушатели, зарегистрированные через RegisterGameTickListener этого BE.
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            renderer?.Dispose();
            renderer = null;
        }

        // Этого метода не хватало - при выгрузке чанка утекал и слушатель, и рендерер
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            renderer?.Dispose();
            renderer = null;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            return true;
        }
    }
}