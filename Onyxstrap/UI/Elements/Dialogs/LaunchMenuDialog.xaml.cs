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
using Onyxstrap.UI.ViewModels.Settings;
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

            PopulateAccounts();
        }

        private class AccountMenuItem
        {
            public string Id { get; init; } = "";
            public string Name { get; init; } = "";
            public ImageSource? Avatar { get; init; }
            public bool IsActive { get; init; }
        }

        /// <summary>
        /// Lists saved accounts on the menu so a session can be started as any
        /// of them in one click. Hidden entirely when no accounts are saved.
        /// </summary>
        private void PopulateAccounts()
        {
            var accounts = App.Accounts.Accounts.ToList();

            if (accounts.Count == 0)
                return;

            AccountsSection.Visibility = Visibility.Visible;

            AccountsList.ItemsSource = accounts.Select(account => new AccountMenuItem
            {
                Id = account.Id,
                Name = account.Name,
                Avatar = AccountsViewModel.AvatarCache.TryGetValue(account.Id, out var avatar) ? avatar : null,
                IsActive = account.Id == App.Accounts.Prop.ActiveAccountId
            }).ToList();
        }

        private void AccountLaunch_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not string id)
                return;

            if (App.Accounts.ActiveAccount?.Id != id)
                App.Accounts.SetActive(id);

            CloseAction = NextAction.LaunchRoblox;
            Close();
        }
    }
}
