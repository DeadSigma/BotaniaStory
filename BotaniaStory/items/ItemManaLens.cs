using BotaniaStory.blockentity;
using BotaniaStory.entities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.items.lenses
{
    public class ManaBurstContext
    {
        public int ManaPayload;
        public double Speed;
        public double MaxDistance;
        public int Color;
        public Vec3d Direction;
        public BlockPos TargetPos;
    }

    public abstract class ItemManaLens : Item
    {
        public virtual int SpreaderTickIntervalMs => 0;
        public virtual Vec3f SpreaderOffset => new Vec3f(0f, 0f, -0.44f);
        public virtual Vec3f SpreaderRotation => new Vec3f(0f, 0f, 0f);
        public virtual float SpreaderScale => 0.55f;

        public virtual bool CanAttach(IManaLensHost host, ItemStack stack)
        {
            return true;
        }

        public virtual void OnSpreaderTick(IManaLensHost host, ItemStack stack)
        {
        }

        public virtual void ModifyBurst(IManaLensHost host, ItemStack stack, ManaBurstContext context)
        {
        }

        public virtual void OnBurstCreated(IManaLensHost host, ItemStack stack, EntityManaBurst burst)
        {
            burst.WatchedAttributes.SetString("manaLens", Code.ToString());
        }
    }
}
