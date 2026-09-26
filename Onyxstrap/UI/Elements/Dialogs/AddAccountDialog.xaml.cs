using System;
using System.Windows;
using System.Windows.Input;

using Onyxstrap.Resources;

namespace Onyxstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for AddAccountDialog.xaml
    /// </summary>
    public partial class AddAccountDialog
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;

        public string AccountName => AccountNameTextBox.Text.Trim();

        public string SecurityToken => AccountTokenTextBox.Password.Trim();

        private readonly string? _existingAccountId;

        /// <param name="existingAccountId">When set, the dialog re-authenticates that account instead of adding a new one.</param>
        public AddAccountDialog(string? existingAccountId = null)
        {
            _existingAccountId = existingAccountId;

            InitializeComponent();

            AccountNameTextBox.TextChanged += (_, _) => UpdateOKState();
            AccountTokenTextBox.PasswordChanged += (_, _) => UpdateOKState();
        }

        private void UpdateOKState()
        {
            OKButton.IsEnabled = !String.IsNullOrWhiteSpace(AccountNameTextBox.Text) && !String.IsNullOrWhiteSpace(AccountTokenTextBox.Password);
        }

        /// <summary>
        /// Opens the embedded Roblox login page; the session token is captured
        /// automatically after a successful one-time login.
        /// </summary>
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Wait;

            var loginWindow = new WebView2LoginWindow();
            loginWindow.ShowDialog();

            Cursor = Cursors.Arrow;

            if (loginWindow.Result != MessageBoxResult.OK || String.IsNullOrWhiteSpace(loginWindow.SecurityToken))
                return;

            AccountTokenTextBox.Password = loginWindow.SecurityToken;

            // complete the flow immediately - no need to touch the form
            if (String.IsNullOrWhiteSpace(AccountName))
                OKButton_Click(sender, e);
        }

        private async void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Wait;
            OKButton.IsEnabled = false;

            // validate the token with roblox before accepting it
            var validation = await RobloxAuth.ValidateToken(SecurityToken);

            if (validation is null)
            {
                Cursor = Cursors.Arrow;
                OKButton.IsEnabled = true;

                Frontend.ShowMessageBox(Strings.Dialog_AddAccount_InvalidToken, MessageBoxImage.Warning);
                return;
            }

            if (_existingAccountId is not null)
            {
                App.Accounts.UpdateAccountToken(_existingAccountId, SecurityToken);
            }
            else
            {
                // auto-fill the display name if the user left it empty
                if (String.IsNullOrWhiteSpace(AccountName))
                    AccountNameTextBox.Text = validation.Value.Username ?? "";

                // grab the avatar headshot while we're at it
                string? avatarUrl = await RobloxAuth.GetAvatarUrl(validation.Value.UserId);

                App.Accounts.AddAccount(AccountNameTextBox.Text.Trim(), SecurityToken, validation.Value.UserId, avatarUrl);
            }

            Result = MessageBoxResult.OK;
            Close();
        }
    }
}
