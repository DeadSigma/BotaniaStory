using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BlockDaybloom : BlockPlant
    {
        // Клик ПКМ по цветку для проверки его статуса
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityDaybloom be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityDaybloom;
            
            if (be != null && world.Side == EnumAppSide.Client)
            {
                var clientApi = world.Api as ICoreClientAPI;
                
                // Формируем понятный текст статуса
                string status = be.LinkedSpreader != null 
                    ? $"Привязан к Распространителю на координатах: {be.LinkedSpreader.X}, {be.LinkedSpreader.Y}, {be.LinkedSpreader.Z}" 
                    : "Ждет Распространитель (в радиусе 6 блоков)...";
                
                clientApi?.ShowChatMessage($"[Дневноцвет] Мана: {be.CurrentMana} / {be.MaxMana} | Статус: {status}");
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}