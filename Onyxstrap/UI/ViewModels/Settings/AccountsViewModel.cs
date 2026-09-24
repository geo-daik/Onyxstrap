using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Onyxstrap.UI.Elements.Dialogs;

namespace Onyxstrap.UI.ViewModels.Settings
{
    public class AccountsViewModel : ObservableObject
    {
        public class AccountEntry
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public long UserId { get; set; } = 0;
            public bool IsActive { get; set; } = false;
            public ImageSource? Avatar { get; set; }

            public bool HasAvatar => Avatar is not null;

            public AccountsViewModel ViewModel { get; init; } = null!;

            public ICommand SetActiveCommand => new RelayCommand(() => ViewModel.SetActiveAccount(Id));
            public ICommand LaunchCommand => new RelayCommand(() => ViewModel.LaunchAccount(Id));
            public ICommand RenameCommand => new RelayCommand(() => ViewModel.RenameAccount(Id));
            public ICommand RemoveCommand => new RelayCommand(() => ViewModel.RemoveAccount(Id));
        }

        // frozen avatar images keyed by account id, shared across page reloads
        private static readonly Dictionary<string, ImageSource> AvatarCache = new();

        private static readonly HashSet<long> AvatarFetchFailed = new();

        private static bool _isFetchingAvatars = false;

        private ObservableCollection<AccountEntry> _accountEntries = new();

        public ObservableCollection<AccountEntry> AccountEntries
        {
            get => _accountEntries;
            private set => SetProperty(ref _accountEntries, value);
        }

        private ObservableCollection<string> _presetNames = new();

        public ObservableCollection<string> PresetNames
        {
            get => _presetNames;
            private set => SetProperty(ref _presetNames, value);
        }

        public bool HasNoAccounts => _accountEntries.Count == 0;

        public bool HasPresets => _presetNames.Count > 0;

        public string ActiveAccountName => App.Accounts.ActiveAccount?.Name ?? "";

        public ICommand AddAccountCommand => new RelayCommand(AddAccount);

        public ICommand SavePresetCommand => new RelayCommand(SavePreset);

        public ICommand ApplyPresetCommand => new RelayCommand<string>(ApplyPreset);

        public ICommand DeletePresetCommand => new RelayCommand<string>(DeletePreset);

        public AccountsViewModel()
        {
            Refresh();
        }

        public void Refresh()
        {
            AccountEntries = new ObservableCollection<AccountEntry>(
                App.Accounts.Accounts.Select(account => new AccountEntry
                {
                    Id = account.Id,
                    Name = account.Name,
                    UserId = account.UserId,
                    IsActive = account.Id == App.Accounts.Prop.ActiveAccountId,
                    Avatar = AvatarCache.TryGetValue(account.Id, out var avatar) ? avatar : null,
                    ViewModel = this
                })
            );

            PresetNames = new ObservableCollection<string>(App.Accounts.Prop.Presets.Keys.OrderBy(x => x));

            OnPropertyChanged(nameof(HasNoAccounts));
            OnPropertyChanged(nameof(HasPresets));
            OnPropertyChanged(nameof(ActiveAccountName));

            FetchMissingAvatars();
        }

        /// <summary>
        /// Downloads avatar headshots for accounts that don't have one cached yet,
        /// in the background, then refreshes the page so they pop in.
        /// </summary>
        private static async void FetchMissingAvatars()
        {
            if (_isFetchingAvatars)
                return;

            var pending = App.Accounts.Accounts
                .Where(account => account.UserId > 0 && !AvatarCache.ContainsKey(account.Id) && !AvatarFetchFailed.Contains(account.UserId))
                .ToList();

            if (pending.Count == 0)
                return;

            _isFetchingAvatars = true;

            try
            {
                foreach (var account in pending)
                {
                    string? url = await RobloxAuth.GetAvatarUrl(account.UserId);

                    if (String.IsNullOrEmpty(url))
                    {
                        AvatarFetchFailed.Add(account.UserId);
                        continue;
                    }

                    account.AvatarUrl = url;

                    byte[] bytes = await App.HttpClient.GetByteArrayAsync(url);

                    var image = new BitmapImage();
                    using (var stream = new MemoryStream(bytes))
                    {
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                    }

                    image.Freeze();

                    AvatarCache[account.Id] = image;
                }

                App.Accounts.Save();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountsViewModel::FetchMissingAvatars", $"Failed to fetch avatars: {ex.Message}");
            }
            finally
            {
                _isFetchingAvatars = false;
            }

            Application.Current?.Dispatcher.Invoke(RefreshStatic);
        }

        private static void RefreshStatic()
        {
            // refresh whichever page instance is currently showing, if any
            if (Application.Current?.Windows.OfType<UI.Elements.Settings.MainWindow>().FirstOrDefault() is not UI.Elements.Settings.MainWindow window)
                return;

            if (window.RootFrame?.Content is UI.Elements.Settings.Pages.AccountsPage page)
                page.ReloadViewModel();
        }

        private void AddAccount()
        {
            var dialog = new AddAccountDialog();
            dialog.ShowDialog();

            Refresh();
        }

        private void SetActiveAccount(string id)
        {
            App.Accounts.SetActive(id);
            Refresh();
        }

        private void LaunchAccount(string id)
        {
            if (App.Accounts.ActiveAccount?.Id != id)
                App.Accounts.SetActive(id);

            LaunchHandler.LaunchRoblox(LaunchMode.Player);
        }

        private async void RenameAccount(string id)
        {
            var account = App.Accounts.GetAccount(id);
            if (account is null)
                return;

            var dialog = new TextPromptDialog(Strings.Accounts_Rename, Strings.Dialog_AddAccount_NameLabel, account.Name);
            dialog.ShowDialog();

            if (dialog.TextInput is null)
                return;

            App.Accounts.RenameAccount(id, dialog.TextInput);
            Refresh();
        }

        private void RemoveAccount(string id)
        {
            var account = App.Accounts.GetAccount(id);
            if (account is null)
                return;

            var result = Frontend.ShowMessageBox(
                string.Format(Strings.Dialog_RemoveAccount_Confirm, account.Name),
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            App.Accounts.RemoveAccount(id);
            Refresh();
        }

        private async void SavePreset()
        {
            var dialog = new TextPromptDialog(Strings.Accounts_SavePreset, Strings.Dialog_SavePreset_NameLabel, "");
            dialog.ShowDialog();

            if (String.IsNullOrWhiteSpace(dialog.TextInput))
                return;

            string name = dialog.TextInput.Trim();

            if (App.Accounts.Prop.Presets.ContainsKey(name))
            {
                var overwrite = Frontend.ShowMessageBox(
                    string.Format(Strings.Dialog_SavePreset_ConfirmOverwrite, name),
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo
                );

                if (overwrite != MessageBoxResult.Yes)
                    return;
            }

            // make sure unsaved editor changes are captured in the preset
            App.FastFlags.Save();

            App.Accounts.SavePreset(name);
            Refresh();
        }

        private void ApplyPreset(string? name)
        {
            if (String.IsNullOrEmpty(name))
                return;

            App.Accounts.ApplyPreset(name);
            Refresh();
        }

        private void DeletePreset(string? name)
        {
            if (String.IsNullOrEmpty(name))
                return;

            App.Accounts.DeletePreset(name);
            Refresh();
        }
    }
}
