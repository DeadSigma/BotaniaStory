using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

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

            // Увеличили высоту списка, чтобы влезли новые переключатели
            ElementBounds listBounds = ElementBounds.Fixed(0, 30, 340, 680);
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

            // 2. Громкость книги
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-volume"), font, ElementBounds.Fixed(0, y, 140, 30));
            SingleComposer.AddSlider(OnVolumeChanged, ElementBounds.Fixed(150, y, 190, 30), "sliderVolume");
            y += 40;

            // 3. Громкость цветов
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-flowervolume"), font, ElementBounds.Fixed(0, y, 140, 30));
            SingleComposer.AddSlider(OnFlowerVolumeChanged, ElementBounds.Fixed(150, y, 190, 30), "sliderFlowerVolume");
            y += 40;

            // 4. Громкость распространителей
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-spreadervolume"), font, ElementBounds.Fixed(0, y, 140, 30));
            SingleComposer.AddSlider(OnSpreaderVolumeChanged, ElementBounds.Fixed(150, y, 190, 30), "sliderSpreaderVolume");
            y += 40;

            // 5. Громкость посоха
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-wandvolume"), font, ElementBounds.Fixed(0, y, 140, 30));
            SingleComposer.AddSlider(OnWandVolumeChanged, ElementBounds.Fixed(150, y, 190, 30), "sliderWandVolume");
            y += 40;

            // 8. Колесико мыши
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-mousewheel"), font, ElementBounds.Fixed(0, y + 5, 200, 30));
            SingleComposer.AddSwitch(OnMouseWheelToggled, ElementBounds.Fixed(280, y, 60, 30), "switchMouseWheel");
            y += 40;

            // 9. Назад на ПКМ
            SingleComposer.AddStaticText(Lang.Get("botaniastory:dialog-settings-rightclickback"), font, ElementBounds.Fixed(0, y + 5, 200, 30));
            SingleComposer.AddSwitch(OnRightClickBackToggled, ElementBounds.Fixed(280, y, 60, 30), "switchRightClickBack");
            y += 40;

            // --- КНОПКА ДЕБАГГЕРА ---
            SingleComposer.AddButton(Lang.Get("botaniastory:dialog-settings-debuger"), OnToggleDebugger, ElementBounds.Fixed(0, y, 340, 30), font, EnumButtonStyle.Normal);
            y += 40;

            // Кнопка сброса
            SingleComposer.AddButton(Lang.Get("botaniastory:dialog-settings-reset"), OnResetSettings, ElementBounds.Fixed(0, y, 340, 30), font, EnumButtonStyle.Normal);
            y += 40;

            // Кнопка Сохранить
            SingleComposer.AddButton(Lang.Get("botaniastory:dialog-settings-save"), OnSaveAndClose, ElementBounds.Fixed(0, y, 340, 30), font, EnumButtonStyle.Normal);

            SingleComposer.EndChildElements().Compose();

           

            SingleComposer.GetSlider("sliderVolume").SetValues(config.Volume, 0, 100, 1);
            SingleComposer.GetSlider("sliderFlowerVolume")?.SetValues(config.FlowerVolume, 0, 100, 1);
            SingleComposer.GetSlider("sliderSpreaderVolume")?.SetValues(config.SpreaderVolume, 0, 100, 1);
            SingleComposer.GetSwitch("switchMouseWheel").On = config.MouseWheelPaging; 
            SingleComposer.GetSwitch("switchRightClickBack").On = config.RightClickBack;
            SingleComposer.GetSlider("sliderWandVolume")?.SetValues(config.WandVolume, 0, 100, 1);

        }

        private bool OnZoomOut() { if (config.BookScale > 0.6f) { mainDialog.UpdateScale(config.BookScale - 0.1f); SingleComposer.GetDynamicText("scaleText").SetNewText($"{Math.Round(config.BookScale, 1)}x"); } return true; }
        private bool OnZoomIn() { if (config.BookScale < 1.6f) { mainDialog.UpdateScale(config.BookScale + 0.1f); SingleComposer.GetDynamicText("scaleText").SetNewText($"{Math.Round(config.BookScale, 1)}x"); } return true; }

        private bool OnVolumeChanged(int value) { config.Volume = value; return true; }
        private bool OnFlowerVolumeChanged(int value) { config.FlowerVolume = value; return true; }
        private bool OnSpreaderVolumeChanged(int value) { config.SpreaderVolume = value; return true; }

        private void OnMouseWheelToggled(bool on) { config.MouseWheelPaging = on; }
        private void OnRightClickBackToggled(bool on) { config.RightClickBack = on; }
        private bool OnToggleDebugger()
        {
            mainDialog.ToggleDebugger();
            return true;
        }
        private bool OnWandVolumeChanged(int value) { config.WandVolume = value; return true; }



        private bool OnResetSettings()
        {
            config.BookScale = 0.9f;
            config.Volume = 50;
            config.FlowerVolume = 50;
            config.SpreaderVolume = 50;
            config.MouseWheelPaging = true; 
            config.RightClickBack = true;
            config.WandVolume = 50;


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