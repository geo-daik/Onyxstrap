using System.Windows;

using Onyxstrap.UI.ViewModels.Settings;

namespace Onyxstrap.UI.Elements.Settings.Pages
{
    public partial class AccountsPage
    {
        private bool _initialLoad = false;

        private AccountsViewModel _viewModel = null!;

        public AccountsPage()
        {
            SetupViewModel();
            InitializeComponent();
        }

        private void SetupViewModel()
        {
            _viewModel = new AccountsViewModel();
            DataContext = _viewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // refresh the view model on page load to reflect any vault changes
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            SetupViewModel();
        }
    }
}
