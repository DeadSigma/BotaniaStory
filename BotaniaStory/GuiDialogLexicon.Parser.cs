using botaniastory;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace botaniastory
{
    public partial class GuiDialogLexicon
    {
        private RichTextComponentBase[] ParseLexiconText(string rawText, CairoFont baseFont)
        {
            if (string.IsNullOrEmpty(rawText)) return new RichTextComponentBase[0];

            var components = new List<RichTextComponentBase>();

            string[] parts = rawText.Split(new string[] { "<link=" }, System.StringSplitOptions.None);

            if (!string.IsNullOrEmpty(parts[0]))
            {
                components.Add(new RichTextComponent(capi, parts[0], baseFont));
            }

            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                int closeBracket = part.IndexOf('>');
                int closeTag = part.IndexOf("</link>");

                if (closeBracket != -1 && closeTag != -1 && closeBracket < closeTag)
                {
                    string targetId = part.Substring(0, closeBracket).Trim();
                    string linkText = part.Substring(closeBracket + 1, closeTag - closeBracket - 1);
                    string remainingText = part.Substring(closeTag + 7);

                    CairoFont linkFont = baseFont.Clone().WithColor(new double[] { 0.1, 0.5, 0.1, 1 }).WithWeight(Cairo.FontWeight.Bold);

                    var linkComp = new LinkTextComponent(capi, linkText, linkFont, (comp) => {
                        if (targetId.StartsWith("search:"))
                        {
                            string searchQuery = targetId.Substring(7);

                            var handbookSys = capi.ModLoader.Systems.FirstOrDefault(s => s.GetType().Name == "ModSystemSurvivalHandbook");
                            if (handbookSys != null)
                            {
                                var dialogField = handbookSys.GetType().GetField("dialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (dialogField != null)
                                {
                                    var dialog = dialogField.GetValue(handbookSys);
                                    if (dialog != null)
                                    {
                                        dynamic dynDialog = dialog;
                                        dynDialog.TryOpen();
                                        dynDialog.Search(searchQuery);
                                        return;
                                    }
                                }
                            }
                        }
                        else if (targetId.StartsWith("category:"))
                        {
                            string catId = targetId.Substring(9);
                            OpenSpecificCategory(catId);
                        }
                        else if (targetId.Contains(":"))
                        {
                            string cleanId = targetId.Replace("handbook://item-", "")
                                                     .Replace("handbook://block-", "")
                                                     .Replace("handbooksearch://", "");

                            AssetLocation loc = new AssetLocation(cleanId);

                            if (loc.Domain == "botaniastory")
                            {
                                string chapterId = BookDataManager.GetChapterForBlock(cleanId);
                                if (chapterId != null)
                                {
                                    OpenSpecificChapter(chapterId);
                                }
                                return;
                            }

                            var handbookSys = capi.ModLoader.Systems.FirstOrDefault(s => s.GetType().Name == "ModSystemSurvivalHandbook");
                            if (handbookSys != null)
                            {
                                var dialogField = handbookSys.GetType().GetField("dialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (dialogField != null)
                                {
                                    var dialog = dialogField.GetValue(handbookSys);
                                    if (dialog != null)
                                    {
                                        dynamic dynDialog = dialog;
                                        dynDialog.TryOpen();

                                        bool pageOpened = false;

                                        if (capi.World.GetItem(loc) != null) pageOpened = dynDialog.OpenDetailPageFor($"item-{loc.Path}");
                                        else if (capi.World.GetBlock(loc) != null) pageOpened = dynDialog.OpenDetailPageFor($"block-{loc.Path}");

                                        if (!pageOpened)
                                        {
                                            string searchWord = loc.Path;
                                            Item item = capi.World.GetItem(loc);
                                            if (item != null) searchWord = new ItemStack(item).GetName();
                                            else
                                            {
                                                Block block = capi.World.GetBlock(loc);
                                                if (block != null) searchWord = new ItemStack(block).GetName();
                                            }
                                            dynDialog.Search(searchWord);
                                        }
                                        return;
                                    }
                                }
                            }
                        }
                        else
                        {
                            OpenSpecificChapter(targetId);
                        }
                    });

                    linkComp.Href = targetId;
                    components.Add(linkComp);

                    if (!string.IsNullOrEmpty(remainingText))
                    {
                        components.Add(new RichTextComponent(capi, remainingText, baseFont));
                    }
                }
                else
                {
                    components.Add(new RichTextComponent(capi, "<link=" + part, baseFont));
                }
            }

            return components.ToArray();
        }
    }
}