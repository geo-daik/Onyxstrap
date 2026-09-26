using System.Windows;
using System.Windows.Input;

using Onyxstrap.UI.ViewModels.Settings;
using Wpf.Ui.Mvvm.Contracts;

namespace Onyxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for FastFlagsPage.xaml
    /// </summary>
    public partial class FastFlagsPage
    {
        private bool _initialLoad = false;

        private FastFlagsViewModel _viewModel = null!;

        public FastFlagsPage()
        {
            SetupViewModel();
            InitializeComponent();
        }

        private void SetupViewModel()
        {
            _viewModel = new FastFlagsViewModel();

            _viewModel.OpenFlagEditorEvent += OpenFlagEditor;
            _viewModel.RequestPageReloadEvent += (_, _) => SetupViewModel();

            DataContext = _viewModel;
        }

        private void OpenFlagEditor(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
                    window.Navigate(typeof(FastFlagEditorPage));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // refresh datacontext on page load to synchronize with editor page
            
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            SetupViewModel();
        }

        private bool _suppressManualSuggestions;

        private void ManualName_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ManualNameTextBox.Text))
            {
                ManualSuggestions.ItemsSource = FastFlagCatalog.Names.Take(12).ToList();
                ManualSuggestions.Visibility = Visibility.Visible;
            }
        }

        private void ManualName_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_suppressManualSuggestions)
            {
                _suppressManualSuggestions = false;
                return;
            }

            var suggestions = FastFlagCatalog.Search(ManualNameTextBox.Text);

            if (suggestions.Count == 0)
            {
                ManualSuggestions.Visibility = Visibility.Collapsed;
                return;
            }

            ManualSuggestions.ItemsSource = suggestions;
            ManualSuggestions.Visibility = Visibility.Visible;
        }

        private void ManualName_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ManualSuggestions.Visibility != Visibility.Visible)
                return;

            switch (e.Key)
            {
                case System.Windows.Input.Key.Down:
                    ManualSuggestions.SelectedIndex = Math.Min(ManualSuggestions.SelectedIndex + 1, ManualSuggestions.Items.Count - 1);
                    ManualSuggestions.ScrollIntoView(ManualSuggestions.SelectedItem);
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Up:
                    ManualSuggestions.SelectedIndex = Math.Max(ManualSuggestions.SelectedIndex - 1, 0);
                    ManualSuggestions.ScrollIntoView(ManualSuggestions.SelectedItem);
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Enter:
                case System.Windows.Input.Key.Tab:
                    AcceptManualSuggestion();
                    e.Handled = true;
                    break;

                case System.Windows.Input.Key.Escape:
                    ManualSuggestions.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                    break;
            }
        }

        private void ManualSuggestion_Selected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ManualSuggestions.SelectedItem is string name)
            {
                _suppressManualSuggestions = true;
                ManualNameTextBox.Text = name;
                ManualNameTextBox.CaretIndex = ManualNameTextBox.Text.Length;
            }
        }

        private void ManualSuggestion_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ManualSuggestions.SelectedItem is not null)
                AcceptManualSuggestion();
        }

        private void AcceptManualSuggestion()
        {
            if (ManualSuggestions.SelectedItem is string name)
            {
                _suppressManualSuggestions = true;
                ManualNameTextBox.Text = name;
                ManualNameTextBox.CaretIndex = ManualNameTextBox.Text.Length;
            }

            ManualSuggestions.Visibility = Visibility.Collapsed;
            ManualSuggestions.SelectedIndex = -1;
            ManualValueTextBox.Focus();
        }

        private void AddManualFlag_Click(object sender, RoutedEventArgs e)
        {
            string name = ManualNameTextBox.Text.Trim();
            string value = ManualValueTextBox.Text.Trim();

            if (name.Length == 0 || value.Length == 0)
            {
                Frontend.ShowMessageBox("Type both a flag name and a value first.", MessageBoxImage.Warning);
                return;
            }

            App.FastFlags.SetValue(name, value);
            App.FastFlags.Save();

            ManualFeedback.Text = $"Added {name} = {value}";
            ManualFeedback.Visibility = Visibility.Visible;

            ManualNameTextBox.Clear();
            ManualValueTextBox.Clear();
            ManualSuggestions.Visibility = Visibility.Collapsed;
            ManualNameTextBox.Focus();
        }

        private void ExportFlags_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", FileName = "MyFastFlags.json" };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                FastFlagBackup.Export(dialog.FileName);
                Frontend.ShowMessageBox($"Flags exported to {dialog.FileName}", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Could not export flags:{Environment.NewLine}{ex.Message}", MessageBoxImage.Warning);
            }
        }

        private void ImportFlags_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                int count = FastFlagBackup.Import(dialog.FileName);
                SetupViewModel();
                Frontend.ShowMessageBox($"Imported {count} flags.", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Could not import flags:{Environment.NewLine}{ex.Message}", MessageBoxImage.Warning);
            }
        }

        private void ValidateInt32(object sender, TextCompositionEventArgs e) => e.Handled = e.Text != "-" && !Int32.TryParse(e.Text, out int _);
        
        private void ValidateUInt32(object sender, TextCompositionEventArgs e) => e.Handled = !UInt32.TryParse(e.Text, out uint _);
    }
}
