using System.Windows;
using Onyxstrap.UI.Elements.Dialogs;

namespace Onyxstrap.UI.Elements.Settings.Pages
{
    public partial class AccountsPage
    {
        private readonly AccountManager Store;
        public AccountsPage() : this(new AccountManager(Path.Combine(Paths.Base, "Accounts.json"))) { }
        internal AccountsPage(AccountManager store) { Store = store; InitializeComponent(); }
        private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();
        private void Refresh()
        {
            try { var accounts = Store.Read().Accounts; Accounts.ItemsSource = accounts; Status.Text = accounts.Count == 0 ? "No accounts saved yet." : "Select an account below."; }
            catch (Exception) { Status.Text = "Could not read saved accounts. The file has been left unchanged."; }
        }
        private void Add_Click(object sender, RoutedEventArgs e) => SignIn(null);
        private void Reauthenticate_Click(object sender, RoutedEventArgs e)
        {
            if (Accounts.SelectedItem is OnyxAccount account) SignIn(account.Id);
            else Status.Text = "Select an account first.";
        }
        private void SignIn(string? id)
        {
            var dialog = new WebView2LoginWindow { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
            if (dialog.Result != MessageBoxResult.OK || dialog.SecurityToken is null) return;
            try { Store.SaveSession(dialog.UserId, dialog.Username, dialog.SecurityToken, id); Refresh(); }
            catch (InvalidOperationException ex) { Status.Text = ex.Message; }
            catch (Exception) { Status.Text = "Could not save the account. Existing saved accounts were left unchanged."; }
        }
        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (Accounts.SelectedItem is not OnyxAccount account) { Status.Text = "Select an account first."; return; }
            try { Store.Remove(account.Id); Refresh(); }
            catch (Exception) { Status.Text = "Could not remove the saved account. Please try again."; }
        }
        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            if (Accounts.SelectedItem is not OnyxAccount account) { Status.Text = "Select an account first."; return; }
            try {
                var start = new ProcessStartInfo(Paths.Process);
                start.ArgumentList.Add("-player"); start.ArgumentList.Add("-account"); start.ArgumentList.Add(account.Id);
                using var process = Process.Start(start);
                Status.Text = "Launch requested. Onyxstrap will verify the account before starting Roblox.";
            }
            catch (Exception) { Status.Text = "Could not start Onyxstrap. Please try again."; }
        }
    }
}
