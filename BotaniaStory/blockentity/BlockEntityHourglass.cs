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

        // Новые переменные для переворота
        public bool IsFlipping = false;
        public float FlipProgress = 0f;

        private long tickListenerId;
        private client.renderers.HourglassRenderer renderer;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Тикает и на сервере, и на клиенте (для идеальной плавности)
            tickListenerId = api.Event.RegisterGameTickListener(OnTick, 50);

            if (api.Side == EnumAppSide.Client)
            {
                renderer = new client.renderers.HourglassRenderer((ICoreClientAPI)api, Pos, this);
            }
        }

        private void OnTick(float dt)
        {
            if (SandCount <= 0) return;

            if (IsFlipping)
            {
                // Анимация переворота длится 0.5 секунды
                FlipProgress += dt / 0.5f;
                if (FlipProgress >= 1.0f)
                {
                    IsFlipping = false;
                    FlipProgress = 0f;

                    if (Api.Side == EnumAppSide.Server)
                    {
                        // ТУТ СРАБАТЫВАЕТ СИГНАЛ (ВЫБРОС ПРЕДМЕТА И Т.Д.)
                        TriggerAdjacentDroppers(); // Вызываем наш новый метод

                        MarkDirty(true);
                    }
                }
            }
            else
            {
                // Обычное пересыпание песка
                TimerProgress += dt / SandCount;
                if (TimerProgress >= 1.0f)
                {
                    TimerProgress = 0f;
                    IsFlipping = true; // Запускаем переворот

                    if (Api.Side == EnumAppSide.Server) MarkDirty(true);
                }
            }
        }

        // Ищем выбрасыватели вокруг часов и дёргаем их
        private void TriggerAdjacentDroppers()
        {
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                BlockPos adjPos = Pos.AddCopy(facing);
                // Проверяем, является ли соседний блок выбрасывателем
                if (Api.World.BlockAccessor.GetBlockEntity(adjPos) is BlockEntityMechanicalDropper dropper)
                {
                    // Подаем команду на выброс (внутри выбрасывателя проверится энергия)
                    dropper.DoDropFromHourglass();
                }
            }
        }

        public bool TryAddSand(ItemSlot slot)
        {
            if (SandCount >= 64) return false;

            string incomingCode = slot.Itemstack.Collectible.Code.ToString();
            // Проверяем, что тип песка совпадает, если внутри уже что-то есть
            if (SandCount > 0 && SandBlockCode != incomingCode) return false;

            SandBlockCode = incomingCode;

            // Считаем, сколько песка мы можем вместить
            int spaceLeft = 64 - SandCount;
            // Берем минимальное значение: либо сколько осталось места, либо сколько есть в стаке
            int amountToAdd = System.Math.Min(spaceLeft, slot.StackSize);

            SandCount += amountToAdd;
            slot.TakeOut(amountToAdd);
            slot.MarkDirty();

            if (Api.Side == EnumAppSide.Server) MarkDirty(true);
            return true;
        }

        public bool TryTakeSand(IPlayer byPlayer)
        {
            if (SandCount <= 0) return false;

            // Восстанавливаем предмет из сохраненного кода
            AssetLocation loc = new AssetLocation(SandBlockCode);
            Block sandBlock = Api.World.GetBlock(loc);
            Item sandItem = Api.World.GetItem(loc); // На случай, если это окажется предметом, а не блоком

            ItemStack stackToGive = null;
            if (sandBlock != null) stackToGive = new ItemStack(sandBlock, SandCount);
            else if (sandItem != null) stackToGive = new ItemStack(sandItem, SandCount);

            if (stackToGive != null)
            {
                // Пытаемся дать песок в инвентарь игроку
                if (!byPlayer.InventoryManager.TryGiveItemstack(stackToGive, true))
                {
                    // Если инвентарь полон, выбрасываем предмет в мир (рядом с блоком)
                    Api.World.SpawnItemEntity(stackToGive, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }

            // Обнуляем данные часов
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

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (tickListenerId != 0) Api.Event.UnregisterGameTickListener(tickListenerId);
            if (Api.Side == EnumAppSide.Client) renderer?.Dispose();
      
       
        }
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            // Возвращаем true, ибо отрисовываем сами
            return true;
        }
    }

}