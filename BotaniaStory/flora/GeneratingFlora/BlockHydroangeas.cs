using BotaniaStory.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BlockHydroangeas : BlockBotaniaFlower
    {
        public override bool TryPlaceBlock(
     IWorldAccessor world,
     IPlayer byPlayer,
     ItemStack itemstack,
     BlockSelection blockSel,
     ref string failureCode)
        {
            Block fluid = world.BlockAccessor.GetBlock(
                blockSel.Position,
                BlockLayersAccess.Fluid
            );

            if (fluid.IsLiquid() && fluid.LiquidCode == "water")
            {
                failureCode = "__ignore__";

                if (world.Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(
                        capi,
                        "hydroangeaswater",
                        Lang.Get("botaniastory:error-hydroangeas-water")
                    );
                }

                return false;
            }

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }
    }
}