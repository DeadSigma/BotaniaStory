using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace BotaniaStory
{
    [HarmonyPatch(typeof(BlockLootVessel), nameof(BlockLootVessel.GetDrops))]
    public static class BlockLootVesselPatch
    {
        [HarmonyPostfix]
        public static void Postfix(
            BlockLootVessel __instance,
            IWorldAccessor world,
            IPlayer byPlayer,
            ref ItemStack[] __result)
        {
            if (__instance.Code?.Domain != "game" ||
                __instance.Code?.Path != "lootvessel-farming")
            {
                return;
            }

            // Целый сосуд не дополняется лутом
            if (__result?.Length == 1 &&
                __result[0]?.Block == __instance)
            {
                return;
            }

            if (world.Rand.NextDouble() >= 0.05)
            {
                return;
            }

            Item seed = world.GetItem(
                new AssetLocation("botaniastory", "overgrowthseed")
            );

            if (seed == null)
            {
                return;
            }

            var drops = new List<ItemStack>(__result ?? Array.Empty<ItemStack>())
            {
                new ItemStack(seed, 1)
            };

            __result = drops.ToArray();
        }
    }
}