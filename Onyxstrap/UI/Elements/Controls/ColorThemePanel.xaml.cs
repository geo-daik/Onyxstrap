using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Onyxstrap.UI.ViewModels.Settings;

namespace Onyxstrap.UI.Elements.Controls
{
    public partial class ColorThemePanel : UserControl
    {
        private ColorThemeViewModel Model => (ColorThemeViewModel)DataContext;
        public ColorThemePanel()
        {
            InitializeComponent();
            DataContext = new ColorThemeViewModel(() =>
            {
                if (Window.GetWindow(this) is Base.WpfUiWindow window) window.ApplyTheme();
                for (DependencyObject? parent = this; parent is not null; parent = System.Windows.Media.VisualTreeHelper.GetParent(parent))
                {
                    if (parent is FrameworkElement { DataContext: AppearanceViewModel appearance })
                    {
                        appearance.OnPropertyChanged(nameof(appearance.Theme));
                        appearance.OnPropertyChanged(nameof(appearance.AccentTheme));
                        break;
                    }
                }
            });
        }
        private void SwatchClick(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is AccentTheme accent) Model.Accent = accent;
        }
        private void PickAppColor(object sender, RoutedEventArgs e) => PickColor(false);
        private void PickPlayerColor(object sender, RoutedEventArgs e) => PickColor(true);
        private void PickColor(bool player)
        {
            var color = ThemeColors.Parse(player ? Model.PlayerHex : Model.AccentHex, System.Windows.Media.Colors.MediumPurple);
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true,
                Color = System.Drawing.Color.FromArgb(color.R, color.G, color.B) };
            var window = Window.GetWindow(this);
            var owner = window is null ? null : new DialogOwner(new WindowInteropHelper(window).Handle);
            if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK) return;
            string hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            if (player) { Model.PlayerHex = hex; Model.PlayerMode = SpotifyColorMode.Custom; }
            else Model.AccentHex = hex;
        }
        private sealed record DialogOwner(nint Handle) : System.Windows.Forms.IWin32Window;
    }
}
