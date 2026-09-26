using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Onyxstrap.UI.ViewModels.Dialogs;
using Onyxstrap.UI.ViewModels.Installer;
using Wpf.Ui.Mvvm.Interfaces;

namespace Onyxstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for LaunchMenuDialog.xaml
    /// </summary>
    public partial class LaunchMenuDialog
    {
        public NextAction CloseAction = NextAction.Terminate;

        public LaunchMenuDialog()
        {
            var viewModel = new LaunchMenuViewModel();
            viewModel.CloseWindowRequest += (_, closeAction) =>
            {
                CloseAction = closeAction;
                Close();
            };

            DataContext = viewModel;

            InitializeComponent();

            try
            {
                var accent = App.Settings.Prop.AccentTheme.GetColor();
                var light = accent.Lerp(Colors.White, 0.45);

                GemGlow.Color = accent;
                WordmarkForeground.GradientStops[0].Color = Color.FromArgb(0xFF, light.R, light.G, light.B);
                WordmarkForeground.GradientStops[1].Color = Color.FromArgb(0xFF, light.R, light.G, light.B);
                TaglineText.Foreground = new SolidColorBrush(Color.FromArgb(0x99, light.R, light.G, light.B));
            }
            catch
            {
                // cosmetic only
            }
        }
    }
}
