using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BotaniaStory.items
{
    public class ItemFilterScroll : ItemRollable, IContainedMeshSource
    {
        public const string PlayerPatternPrefix = "@player:";

        public bool IsBlacklist => Code?.Path.Contains("black") == true;

        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling)
        {
            bool isSneak = byEntity.Controls.Sneak;
            bool isCtrl = byEntity.Controls.CtrlKey;

            if (isSneak && !isCtrl)
            {
                handling = EnumHandHandling.PreventDefault;

                if (api.Side == EnumAppSide.Client && byEntity is EntityPlayer)
                {
                    new GuiDialogFilterScroll(api as ICoreClientAPI, slot, IsBlacklist).TryOpen();
                }

                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public bool AllowsItem(ItemStack filterStack, string fullItemCode)
        {
            if (filterStack?.Attributes == null || string.IsNullOrEmpty(fullItemCode))
                return false;

            var attr = filterStack.Attributes;
            string fullCodeLower = fullItemCode.ToLowerInvariant();
            string pathOnly = fullCodeLower;

            int colonIndex = fullCodeLower.IndexOf(':');
            if (colonIndex >= 0)
            {
                pathOnly = fullCodeLower.Substring(colonIndex + 1);
            }

            bool matchFound = false;

            if (attr.HasAttribute("filterList"))
            {
                var exactCodes = (attr["filterList"] as StringArrayAttribute)?.value;
                if (exactCodes != null)
                {
                    foreach (var code in exactCodes)
                    {
                        if (fullCodeLower == code.ToLowerInvariant())
                        {
                            matchFound = true;
                            break;
                        }
                    }
                }
            }

            bool hasItemPatterns = false;

            if (!matchFound && attr.HasAttribute("filterPatterns"))
            {
                var patterns = (attr["filterPatterns"] as StringArrayAttribute)?.value;
                if (patterns != null)
                {
                    foreach (var rawPattern in patterns)
                    {
                        if (string.IsNullOrWhiteSpace(rawPattern)) continue;
                        if (rawPattern.StartsWith(PlayerPatternPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                        hasItemPatterns = true;

                        string pattern = rawPattern.Replace("*", "").Trim().ToLowerInvariant();
                        if (pattern.Length == 0) continue;

                        if (pathOnly.Contains(pattern))
                        {
                            matchFound = true;
                            break;
                        }
                    }
                }
            }

            bool hasList = attr.HasAttribute("filterList")
                && (attr["filterList"] as StringArrayAttribute)?.value?.Length > 0;

            if (!hasList && !hasItemPatterns)
            {
                return IsBlacklist;
            }

            return IsBlacklist ? !matchFound : matchFound;
        }

        public bool AllowsPlayer(ItemStack filterStack, string playerName)
        {
            if (filterStack?.Attributes == null || string.IsNullOrWhiteSpace(playerName))
                return false;

            var patterns = (filterStack.Attributes["filterPatterns"] as StringArrayAttribute)?.value;
            bool hasPlayerNames = false;
            bool matchFound = false;

            if (patterns != null)
            {
                foreach (var rawPattern in patterns)
                {
                    if (string.IsNullOrWhiteSpace(rawPattern)) continue;
                    if (!rawPattern.StartsWith(PlayerPatternPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                    string savedName = rawPattern.Substring(PlayerPatternPrefix.Length).Trim();
                    if (savedName.Length == 0) continue;

                    hasPlayerNames = true;

                    if (string.Equals(savedName, playerName.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        matchFound = true;
                        break;
                    }
                }
            }

            if (!hasPlayerNames)
            {
                return IsBlacklist;
            }

            return IsBlacklist ? !matchFound : matchFound;
        }

        public new MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos = null)
        {
            if (!Attributes.KeyExists("rolledShape")) return null;

            if (atBlockPos != null)
            {
                var ba = api.World.BlockAccessor;

                bool unrolledHere =
                    ba.GetBlock(atBlockPos) is BlockGroundStorage
                    || ba.GetBlockEntity(atBlockPos) is BlockEntityDisplayCase;

                if (unrolledHere) return null;
            }

            var capi = api as ICoreClientAPI;
            AssetLocation loc = AssetLocation.Create(Attributes["rolledShape"].AsString(null), Code.Domain)
                .WithPathPrefixOnce("shapes/")
                .WithPathAppendixOnce(".json");

            Shape shape = capi.Assets.TryGet(loc, true).ToObject<Shape>();

            var textures = new Dictionary<string, AssetLocation>();
            if (shape.Textures != null)
            {
                foreach (var p in shape.Textures) textures[p.Key] = p.Value;
            }

            if (Textures != null)
            {
                foreach (var p in Textures) textures[p.Key] = p.Value.Base;
            }

            var cnts = new ContainedTextureSource(capi, targetAtlas, textures, $"Displayed item {Code}");

            capi.Tesselator.TesselateShape(new TesselationMetaData { TexSource = cnts }, shape, out MeshData mesh);
            return mesh;
        }
    }
}
