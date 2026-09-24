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

        public AddAccountDialog()
        {
            InitializeComponent();

            AccountNameTextBox.TextChanged += (_, _) => UpdateOKState();
            AccountTokenTextBox.PasswordChanged += (_, _) => UpdateOKState();
        }

        private void UpdateOKState()
        {
            OKButton.IsEnabled = !String.IsNullOrWhiteSpace(AccountNameTextBox.Text) && !String.IsNullOrWhiteSpace(AccountTokenTextBox.Password);
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Wait;
            OKButton.IsEnabled = false;

            // validate the token with roblox before accepting it
            var validation = RobloxAuth.ValidateToken(SecurityToken).GetAwaiter().GetResult();

            if (validation is null)
            {
                Cursor = Cursors.Arrow;
                OKButton.IsEnabled = true;

                Frontend.ShowMessageBox(Strings.Dialog_AddAccount_InvalidToken, MessageBoxImage.Warning);
                return;
            }

            // auto-fill the display name if the user left it empty
            if (String.IsNullOrWhiteSpace(AccountName))
                AccountNameTextBox.Text = validation.Value.Username ?? "";

            App.Accounts.AddAccount(AccountNameTextBox.Text.Trim(), SecurityToken, validation.Value.UserId);

            Result = MessageBoxResult.OK;
            Close();
        }
    }
}
