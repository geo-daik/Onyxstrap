using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

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

            public AccountsViewModel ViewModel { get; init; } = null!;

            public ICommand SetActiveCommand => new RelayCommand(() => ViewModel.SetActiveAccount(Id));
            public ICommand LaunchCommand => new RelayCommand(() => ViewModel.LaunchAccount(Id));
            public ICommand RenameCommand => new RelayCommand(() => ViewModel.RenameAccount(Id));
            public ICommand RemoveCommand => new RelayCommand(() => ViewModel.RemoveAccount(Id));
        }

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
                    ViewModel = this
                })
            );

            PresetNames = new ObservableCollection<string>(App.Accounts.Prop.Presets.Keys.OrderBy(x => x));

            OnPropertyChanged(nameof(HasNoAccounts));
            OnPropertyChanged(nameof(HasPresets));
            OnPropertyChanged(nameof(ActiveAccountName));
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
