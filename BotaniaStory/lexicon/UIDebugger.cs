using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using System.Globalization;
using botaniastory;

namespace BotaniaStory.lexicon
{
    public class UIDebugger : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;
        private GuiDialogLexicon mainDialog;
        private string selectedKey;

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            if (SingleComposer != null)
            {
                SingleComposer.Bounds.fixedX = 0;
                SingleComposer.Bounds.fixedY = 0;
                SingleComposer.Bounds.CalcWorldBounds();
            }
        }

        public UIDebugger(ICoreClientAPI capi, GuiDialogLexicon mainDialog) : base(capi)
        {
            this.mainDialog = mainDialog;
            selectedKey = mainDialog.ui.Keys.First();
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.LeftMiddle).WithFixedAlignmentOffset(20, 0);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            CairoFont font = CairoFont.WhiteSmallText();

            var compo = capi.Gui.CreateCompo("uiDebugger", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("botaniastory:dialog-debugger-title"), () => TryClose())
                .BeginChildElements(bgBounds);

            int y = 30;

            compo.AddStaticText(Lang.Get("botaniastory:dialog-debugger-scale-global"), font, ElementBounds.Fixed(0, y, 250, 20));
            y += 20;
            compo.AddSlider(OnGlobalScaleChanged, ElementBounds.Fixed(0, y, 250, 20), "globalScaleSlider");
            y += 35;

            compo.AddStaticText(Lang.Get("botaniastory:dialog-debugger-scale-local"), font, ElementBounds.Fixed(0, y, 250, 20));
            y += 20;
            compo.AddSlider(OnLocalScaleChanged, ElementBounds.Fixed(0, y, 250, 20), "localScaleSlider");
            y += 35;

            string[] keys = mainDialog.ui.Keys.ToArray();
            compo.AddDropDown(keys, keys, Array.IndexOf(keys, selectedKey), OnKeySelected, ElementBounds.Fixed(0, y, 250, 30), font, "elementDropdown");
            y += 40;

            y = AddAxisControls(compo, font, Lang.Get("botaniastory:dialog-debugger-axis-x"), 0, y);
            y = AddAxisControls(compo, font, Lang.Get("botaniastory:dialog-debugger-axis-y"), 1, y);
            y = AddAxisControls(compo, font, Lang.Get("botaniastory:dialog-debugger-axis-width"), 2, y);
            y = AddAxisControls(compo, font, Lang.Get("botaniastory:dialog-debugger-axis-height"), 3, y);

            compo.AddDynamicText(Lang.Get("botaniastory:dialog-debugger-coords-updating"), font, ElementBounds.Fixed(0, y + 10, 250, 50), "infoText");
            compo.AddButton(Lang.Get("botaniastory:dialog-debugger-copy"), OnCopyClicked, ElementBounds.Fixed(0, y + 60, 250, 30), font, EnumButtonStyle.Small);

            compo.AddButton(Lang.Get("botaniastory:dialog-debugger-save"), OnSaveConfigClicked, ElementBounds.Fixed(0, y + 95, 250, 30), font, EnumButtonStyle.Small);

            SingleComposer = compo.EndChildElements().Compose();
            UpdateInfoText();

            SingleComposer.GetSlider("globalScaleSlider").SetValues((int)(mainDialog.bookScale * 100), 50, 200, 5);
            UpdateLocalSlider();
        }

        private void UpdateLocalSlider()
        {
            if (mainDialog.ui.ContainsKey(selectedKey))
            {
                SingleComposer.GetSlider("localScaleSlider")?.SetValues((int)(mainDialog.ui[selectedKey][4] * 100), 10, 300, 5);
            }
        }

        private bool OnGlobalScaleChanged(int value) { mainDialog.UpdateScale(value / 100f); return true; }

        private bool OnLocalScaleChanged(int value)
        {
            if (mainDialog.ui.ContainsKey(selectedKey))
            {
                mainDialog.ui[selectedKey][4] = value / 100f;
                UpdateInfoText();
                mainDialog.RecomposeFromDebugger();
            }
            return true;
        }

        private void OnKeySelected(string code, bool selected) { selectedKey = code; UpdateInfoText(); UpdateLocalSlider(); }

        private int AddAxisControls(GuiComposer compo, CairoFont font, string label, int index, int y)
        {
            compo.AddStaticText(label, font, ElementBounds.Fixed(0, y + 5, 80, 30));
            compo.AddButton("-10", () => ChangeValue(index, -10), ElementBounds.Fixed(85, y, 35, 30), font, EnumButtonStyle.Small);
            compo.AddButton("-1", () => ChangeValue(index, -1), ElementBounds.Fixed(125, y, 35, 30), font, EnumButtonStyle.Small);
            compo.AddButton("+1", () => ChangeValue(index, 1), ElementBounds.Fixed(165, y, 35, 30), font, EnumButtonStyle.Small);
            compo.AddButton("+10", () => ChangeValue(index, 10), ElementBounds.Fixed(205, y, 35, 30), font, EnumButtonStyle.Small);
            return y + 35;
        }

        private bool ChangeValue(int index, double amount)
        {
            if (mainDialog.ui.ContainsKey(selectedKey)) { mainDialog.ui[selectedKey][index] += amount; UpdateInfoText(); mainDialog.RecomposeFromDebugger(); }
            return true;
        }

        private void UpdateInfoText()
        {
            if (mainDialog.ui.ContainsKey(selectedKey))
            {
                var val = mainDialog.ui[selectedKey];
                // Используем форматирование из Lang
                SingleComposer.GetDynamicText("infoText")?.SetNewText(Lang.Get("botaniastory:dialog-debugger-info", val[0], val[1], val[2], val[3], val[4]));
            }
        }

        private bool OnCopyClicked()
        {
            StringBuilder sb = new StringBuilder();

            // Начинаем формировать текст именно для нового файла LexiconUIData.cs
            sb.AppendLine("        public static Dictionary<string, double[]> GetDefaultUI()");
            sb.AppendLine("        {");
            sb.AppendLine("            return new Dictionary<string, double[]>()");
            sb.AppendLine("            {");

            string Fmt(double val) => Math.Round(val, 3).ToString(CultureInfo.InvariantCulture);

            foreach (var kvp in mainDialog.ui)
            {
                sb.AppendLine($"                {{ \"{kvp.Key}\", new double[] {{ {Fmt(kvp.Value[0])}, {Fmt(kvp.Value[1])}, {Fmt(kvp.Value[2])}, {Fmt(kvp.Value[3])}, {Fmt(kvp.Value[4])} }} }},");
            }

            sb.AppendLine("            };");
            sb.AppendLine("        }");

            capi.Input.ClipboardText = sb.ToString();
            capi.ShowChatMessage(Lang.Get("botaniastory:msg-debugger-copied"));
            return true;
        }

        private bool OnSaveConfigClicked()
        {
            mainDialog.config.CustomUI = mainDialog.ui;
            mainDialog.SaveConfig();
            capi.ShowChatMessage(Lang.Get("botaniastory:msg-debugger-saved"));
            return true;
        }
    }
}