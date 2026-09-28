using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Onyxstrap.UI.Elements.Settings.Pages
{
    public partial class EssentialsPage
    {
        public EssentialsPage() { InitializeComponent(); DataContext = App.Settings.Prop; }
        private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();
        private void Refresh()
        {
            App.Settings.Prop.FavoriteGames ??= new();
            Favorites.ItemsSource = App.Settings.Prop.FavoriteGames;
            EmptyLabel.Visibility = App.Settings.Prop.FavoriteGames.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SnapToggle.GetBindingExpression(Wpf.Ui.Controls.ToggleSwitch.IsCheckedProperty)?.UpdateTarget();
        }
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try { GameFavorites.Add(App.Settings.Prop.FavoriteGames, GameName.Text, GameLink.Text); GameName.Clear(); GameLink.Clear(); Refresh(); Status.Text = "Favorite added. Click Save to keep it."; }
            catch (InvalidDataException ex) { Status.Text = ex.Message; }
        }
        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not FavoriteGame game) return;
            App.Settings.Prop.FavoriteGames.Remove(game); Refresh(); Status.Text = "Favorite removed. Click Save to keep the change.";
        }
        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not long id) return;
            try {
                var start = new ProcessStartInfo(Paths.Process);
                start.ArgumentList.Add(GameFavorites.LaunchUri(id));
                using var process = Process.Start(start);
                Status.Text = "Launch requested. Roblox will use your current session.";
            }
            catch (Exception) { Status.Text = "Could not launch this game. Try opening it from Roblox."; }
        }
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "Onyxstrap preferences (*.json)|*.json", FileName = "Onyxstrap-preferences.json", AddExtension = true };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try { PreferenceBackup.Write(dialog.FileName, PreferenceBackup.Export(App.Settings.Prop)); Status.Text = "Backup exported. Account sessions and custom commands are excluded."; }
            catch (Exception) { Status.Text = "Could not export the backup. Choose another location and try again."; }
        }
        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Onyxstrap preferences (*.json)|*.json", CheckFileExists = true };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try {
                if (new FileInfo(dialog.FileName).Length > 1024 * 1024) throw new InvalidDataException("The backup is too large.");
                var imported = PreferenceBackup.Import(File.ReadAllText(dialog.FileName), App.Settings.Prop);
                var answer = Frontend.ShowMessageBox($"Load {imported.FavoriteGames.Count} favorites, {imported.ColorProfiles.Count} color profiles, and the saved preferences? Current matching preferences will be replaced. Click Save afterward to keep them.", MessageBoxImage.Question, MessageBoxButton.YesNo);
                if (answer != MessageBoxResult.Yes) return;
                PreferenceBackup.Apply(imported, App.Settings.Prop);
                Refresh(); (Window.GetWindow(this) as MainWindow)?.ApplyTheme();
                Status.Text = "Backup loaded. Click Save, then reopen settings to refresh all controls.";
            }
            catch (Exception) { Status.Text = "Could not load this backup. Use a valid Onyxstrap preferences export; your settings were not changed."; }
        }
    }
}
