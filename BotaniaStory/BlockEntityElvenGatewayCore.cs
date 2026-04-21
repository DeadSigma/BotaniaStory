using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BlockEntityElvenGatewayCore : BlockEntity
    {
        public bool IsActive { get; private set; }

        public void OnInteract(IPlayer byPlayer)
        {
            // Обрабатываем только на сервере, чтобы избежать рассинхрона
            if (Api.Side == EnumAppSide.Client) return;

            IsActive = !IsActive;

            // Меняем блок визуально
            AssetLocation newCode = Block.CodeWithVariant("state", IsActive ? "on" : "off");
            Block nextBlock = Api.World.GetBlock(newCode);

            if (nextBlock != null)
            {
                Api.World.BlockAccessor.ExchangeBlock(nextBlock.Id, Pos);
            }

            // Поиск пилонов
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
                            pylon.LinkedTarget = IsActive ? this.Pos : null;
                            pylon.MarkDirty(true); // Отправляет изменения (LinkedTarget) на клиент
                        }
                    }
                }
            }

            MarkDirty(true); // Синхронизирует состояние самого ядра
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            IsActive = tree.GetBool("isActive");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("isActive", IsActive);
        }
    }
}