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
