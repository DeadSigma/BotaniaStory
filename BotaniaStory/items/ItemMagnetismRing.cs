using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public enum MagnetLootMode
    {
        Disabled = 0,
        MonstersOnly = 1,
        AllCreatures = 2
    }

    public class ItemMagnetismRing : Item
    {
        private const int ModeDisabled = 0;
        private const int ModeMonsters = 1;
        private const int ModeAll = 2;

        private SkillItem[] toolModes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Variant?["type"] != "looting")
            {
                return;
            }

            if (api is not ICoreClientAPI capi)
            {
                return;
            }

            toolModes = new SkillItem[]
            {
                new SkillItem
                {
                    Code = new AssetLocation("disabled"),
                    Name = Lang.Get("botaniastory:magnet-lootmode-disabled")
                }.WithIcon(
                    capi,
                    LoadModeIcon(
                        capi,
                        "botaniastory:textures/icons/magnetloot-disabled.svg"
                    )
                ),

                new SkillItem
                {
                    Code = new AssetLocation("monsters"),
                    Name = Lang.Get("botaniastory:magnet-lootmode-monsters")
                }.WithIcon(
                    capi,
                    LoadModeIcon(
                        capi,
                        "botaniastory:textures/icons/magnetloot-monsters.svg"
                    )
                ),

                new SkillItem
                {
                    Code = new AssetLocation("all"),
                    Name = Lang.Get("botaniastory:magnet-lootmode-all")
                }.WithIcon(
                    capi,
                    LoadModeIcon(
                        capi,
                        "botaniastory:textures/icons/magnetloot-all.svg"
                    )
                )
            };

            for (int i = 0; i < toolModes.Length; i++)
            {
                toolModes[i].TexturePremultipliedAlpha = false;
            }
        }

        public override SkillItem[] GetToolModes(
            ItemSlot slot,
            IClientPlayer forPlayer,
            BlockSelection blockSel
        )
        {
            if (!IsLootingRing(slot?.Itemstack))
            {
                return null;
            }

            return toolModes;
        }

        public override int GetToolMode(
            ItemSlot slot,
            IPlayer byPlayer,
            BlockSelection blockSelection
        )
        {
            ItemStack stack = slot?.Itemstack;

            if (!IsLootingRing(stack))
            {
                return ModeDisabled;
            }

            int mode;

            if (stack.Attributes.HasAttribute("toolMode"))
            {
                mode = stack.Attributes.GetInt(
                    "toolMode",
                    ModeMonsters
                );
            }
            else if (stack.Attributes.HasAttribute("magnetLootMode"))
            {
                mode = stack.Attributes.GetInt(
                    "magnetLootMode",
                    ModeMonsters
                );
            }
            else
            {
                mode = ModeMonsters;
            }

            if (mode < ModeDisabled)
            {
                mode = ModeDisabled;
            }
            else if (mode > ModeAll)
            {
                mode = ModeAll;
            }

            return mode;
        }

        public override void SetToolMode(
            ItemSlot slot,
            IPlayer byPlayer,
            BlockSelection blockSelection,
            int toolMode
        )
        {
            if (!IsLootingRing(slot?.Itemstack))
            {
                return;
            }

            if (toolMode < ModeDisabled)
            {
                toolMode = ModeDisabled;
            }
            else if (toolMode > ModeAll)
            {
                toolMode = ModeAll;
            }

            slot.Itemstack.Attributes.SetInt(
                "toolMode",
                toolMode
            );

            if (slot.Itemstack.Attributes.HasAttribute("magnetLootMode"))
            {
                slot.Itemstack.Attributes.RemoveAttribute(
                    "magnetLootMode"
                );
            }

            slot.MarkDirty();
        }

        public static bool IsLootingRing(ItemStack stack)
        {
            return stack?.Collectible is ItemMagnetismRing ring
                && ring.Variant?["type"] == "looting";
        }

        public static MagnetLootMode GetLootMode(
            ItemStack stack
        )
        {
            if (!IsLootingRing(stack))
            {
                return MagnetLootMode.Disabled;
            }

            int mode;

            if (stack.Attributes.HasAttribute("toolMode"))
            {
                mode = stack.Attributes.GetInt(
                    "toolMode",
                    ModeMonsters
                );
            }
            else if (stack.Attributes.HasAttribute("magnetLootMode"))
            {
                mode = stack.Attributes.GetInt(
                    "magnetLootMode",
                    ModeMonsters
                );
            }
            else
            {
                mode = ModeMonsters;
            }

            if (mode < ModeDisabled)
            {
                mode = ModeDisabled;
            }
            else if (mode > ModeAll)
            {
                mode = ModeAll;
            }

            return (MagnetLootMode)mode;
        }

        private LoadedTexture LoadModeIcon(
            ICoreClientAPI capi,
            string path
        )
        {
            return capi.Gui.LoadSvgWithPadding(
                new AssetLocation(path),
                48,
                48,
                5,
                ColorUtil.WhiteArgb
            );
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (toolModes != null)
            {
                for (int i = 0; i < toolModes.Length; i++)
                {
                    toolModes[i]?.Dispose();
                }

                toolModes = null;
            }

            base.OnUnloaded(api);
        }
    }
}