using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory.Patches
{
    public class EnchantedSoilTillingSystem : ModSystem
    {
        private const string HarmonyId = "botaniastory.enchantedsoil";
        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            if (!Harmony.HasAnyPatches(HarmonyId))
            {
                harmony = new Harmony(HarmonyId);
                harmony.PatchAll();
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            base.Dispose();
        }
    }

    [HarmonyPatch(typeof(ItemHoe))]
    public static class ItemHoeEnchantedSoilPatch
    {
        public const string SoilCode = "enchantedsoil";
        public const string FarmlandCode = "enchantedfarmland-dry";

        public static bool IsEnchantedSoil(Block block)
        {
            return block?.Code != null
                && block.Code.Domain == "botaniastory"
                && block.Code.PathStartsWith(SoilCode);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnHeldInteractStart")]
        public static void Postfix_OnHeldInteractStart(
            ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel,
            EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null) return;
            if (handHandling != EnumHandHandling.NotHandled) return;     
            if (byEntity.Controls.ShiftKey && byEntity.Controls.CtrlKey) return;

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (IsEnchantedSoil(block))
            {
                handHandling = EnumHandHandling.PreventDefault;
            }
        }

        // Подменяем результат вспашки. Проверка клеймов и таймер 0.6с остаются ванильными (в OnHeldInteractStep)
        [HarmonyPrefix]
        [HarmonyPatch("DoTill")]
        public static bool Prefix_DoTill(
            float secondsUsed, ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return true;

            IWorldAccessor world = byEntity.World;
            BlockPos pos = blockSel.Position;
            Block block = world.BlockAccessor.GetBlock(pos);

            if (!IsEnchantedSoil(block)) return true;                     // обычная земля - ванильная логика

            Block farmland = world.GetBlock(new AssetLocation("botaniastory", FarmlandCode));
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (farmland == null || byPlayer == null) return false;

            if (block.Sounds != null) world.PlaySoundAt(block.Sounds.Place, pos, 0.4, null);

            world.BlockAccessor.SetBlock(farmland.BlockId, pos);
            slot.Itemstack?.Collectible.DamageItem(world, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot);

            if (slot.Empty)
            {
                world.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"),
                    byEntity.Pos.X, byEntity.Pos.InternalY, byEntity.Pos.Z);
            }

            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityFarmland bef)
            {
                bef.OnCreatedFromSoil(block);
            }

            world.BlockAccessor.MarkBlockDirty(pos);
            return false;
        }
    }
}