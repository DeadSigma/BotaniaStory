using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config; // <-- Добавлено для Lang

namespace botaniastory
{
    public class GuiDialogLexiconSettings : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;
        private LexiconConfig config;
        private GuiDialogLexicon mainDialog;

        public GuiDialogLexiconSettings(ICoreClientAPI capi, LexiconConfig config, GuiDialogLexicon mainDialog) : base(capi)
        {
            this.config = config;
            this.mainDialog = mainDialog;
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-20, 0);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds listBounds = ElementBounds.Fixed(0, 30, 340, 520);
            bgBounds.WithChild(listBounds);

            CairoFont font = CairoFont.WhiteSmallText();

            SingleComposer = capi.Gui.CreateCompo("lexiconSettings", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("botaniastory:dialog-settings-title"), () => TryClose())
                .BeginChildElements(bgBounds);

            int y = 30;

            // 1. Масштаб книги
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-scale"), font, ElementBounds.Fixed(0, y + 5, 140, 30));
            SingleComposer.AddButton("-", OnZoomOut, ElementBounds.Fixed(150, y, 30, 30), font, EnumButtonStyle.Normal);
            SingleComposer.AddDynamicText($"{Math.Round(config.BookScale, 1)}x", font.WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(185, y + 8, 50, 30), "scaleText");
            SingleComposer.AddButton("+", OnZoomIn, ElementBounds.Fixed(240, y, 30, 30), font, EnumButtonStyle.Normal);

            y += 50;

            // 2. Ползунок (Прозрачность)
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-opacity"), font, ElementBounds.Fixed(0, y, 140, 30));
            SingleComposer.AddSlider(OnOpacityChanged, ElementBounds.Fixed(150, y, 190, 30), "sliderOpacity");

            y += 40;

            // 3. Ползунок (Громкость)
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-volume"), font, ElementBounds.Fixed(0, y, 140, 30));
            SingleComposer.AddSlider(OnVolumeChanged, ElementBounds.Fixed(150, y, 190, 30), "sliderVolume");

            y += 40;

            // 4. Переключатель 1
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-darkmode"), font, ElementBounds.Fixed(0, y + 5, 200, 30));
            SingleComposer.AddSwitch(OnDarkModeToggled, ElementBounds.Fixed(280, y, 60, 30), "switchDarkMode");

            y += 40;

            // 5. Переключатель 2
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-pagenumbers"), font, ElementBounds.Fixed(0, y + 5, 200, 30));
            SingleComposer.AddSwitch(OnPageNumbersToggled, ElementBounds.Fixed(280, y, 60, 30), "switchPageNum");

            y += 40;

            // 6. Переключатель 3
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-animation"), font, ElementBounds.Fixed(0, y + 5, 200, 30));
            SingleComposer.AddSwitch(OnAnimToggled, ElementBounds.Fixed(280, y, 60, 30), "switchAnim");

            y += 50;

            // 7. Текст 1
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-owner"), font, ElementBounds.Fixed(0, y + 5, 140, 30));
            SingleComposer.AddTextInput(ElementBounds.Fixed(150, y, 190, 30), OnOwnerNameChanged, font, "inputOwner");

            y += 50;

            // 8. Текст 2
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-booktitle"), font, ElementBounds.Fixed(0, y + 5, 140, 30));
            SingleComposer.AddTextInput(ElementBounds.Fixed(150, y, 190, 30), OnTitleChanged, font, "inputTitle");

            y += 60;

            // 9. Кнопка сброса
            SingleComposer.AddButton(Lang.Get("botaniastory:dialog-settings-reset"), OnResetSettings, ElementBounds.Fixed(0, y, 340, 30), font, EnumButtonStyle.Normal);

            y += 40;

            // 10. Кнопка Сохранить
            SingleComposer.AddButton(Lang.Get("botaniastory:dialog-settings-save"), OnSaveAndClose, ElementBounds.Fixed(0, y, 340, 30), font, EnumButtonStyle.Normal);

            SingleComposer.EndChildElements().Compose();

            SingleComposer.GetSlider("sliderOpacity").SetValues(config.Opacity, 0, 100, 1);
            SingleComposer.GetSlider("sliderVolume").SetValues(config.Volume, 0, 100, 1);
            SingleComposer.GetSwitch("switchDarkMode").On = config.DarkMode;
            SingleComposer.GetSwitch("switchPageNum").On = config.ShowPageNumbers;
            SingleComposer.GetSwitch("switchAnim").On = config.EnableAnimations;
            SingleComposer.GetTextInput("inputOwner").SetValue(config.OwnerName);
            SingleComposer.GetTextInput("inputTitle").SetValue(config.CustomTitle);
        }

        private bool OnZoomOut()
        {
            if (config.BookScale > 0.6f)
            {
                mainDialog.UpdateScale(config.BookScale - 0.1f);
                SingleComposer.GetDynamicText("scaleText").SetNewText($"{Math.Round(config.BookScale, 1)}x");
            }
            return true;
        }

        private bool OnZoomIn()
        {
            if (config.BookScale < 1.6f)
            {
                mainDialog.UpdateScale(config.BookScale + 0.1f);
                SingleComposer.GetDynamicText("scaleText").SetNewText($"{Math.Round(config.BookScale, 1)}x");
            }
            return true;
        }

        private bool OnOpacityChanged(int value) { config.Opacity = value; mainDialog.SaveConfig(); return true; }
        private bool OnVolumeChanged(int value) { config.Volume = value; mainDialog.SaveConfig(); return true; }
        private void OnDarkModeToggled(bool on) { config.DarkMode = on; mainDialog.SaveConfig(); }
        private void OnPageNumbersToggled(bool on) { config.ShowPageNumbers = on; mainDialog.SaveConfig(); }
        private void OnAnimToggled(bool on) { config.EnableAnimations = on; mainDialog.SaveConfig(); }
        private void OnOwnerNameChanged(string text) { config.OwnerName = text; mainDialog.SaveConfig(); }
        private void OnTitleChanged(string text) { config.CustomTitle = text; mainDialog.SaveConfig(); }

        private bool OnResetSettings()
        {
            config.BookScale = 1.0f;
            config.Opacity = 100;
            config.Volume = 50;
            config.DarkMode = false;
            config.ShowPageNumbers = true;
            config.EnableAnimations = true;
            config.OwnerName = Lang.Get("botaniastory:default-owner");
            config.CustomTitle = Lang.Get("botaniastory:default-title");

            mainDialog.UpdateScale(1.0f);
            SingleComposer?.Dispose();
            SetupDialog();
            return true;
        }

        private bool OnSaveAndClose()
        {
            mainDialog.SaveConfig();
            TryClose();
            return true;
        }
    }
}