using BotaniaStory.blockentity;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.items.lenses
{
    public class ItemMagnetismLens : ItemManaLens
    {
        public override int SpreaderTickIntervalMs => 1000;
        public override Vec3f SpreaderOffset => new Vec3f(0f, 0.12f, -0.47f);
        public override float SpreaderScale => 0.7f;

        public override void OnSpreaderTick(IManaLensHost host, ItemStack stack)
        {
            BlockPos target = host.FindNearestManaReceiver(12);
            host.SetLensTarget(target, true);
        }
    }
}
