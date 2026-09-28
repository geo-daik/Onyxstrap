using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;

namespace Onyxstrap.UI.ViewModels.Settings
{
    public sealed class ColorThemeViewModel : NotifyPropertyChangedViewModel
    {
        private readonly Action _refresh;
        private string? _accentDraft;
        private string? _playerDraft;
        private string _message = "";
        public ColorThemeViewModel(Action refresh) { _refresh = refresh; }
        public record Swatch(string Name, AccentTheme Value, Brush Brush);
        public IEnumerable<Swatch> Swatches => Enum.GetValues<AccentTheme>().Where(v => v != AccentTheme.Custom)
            .Select(v => new Swatch(v == AccentTheme.OnyxViolet ? "Onyx Violet" : v.ToString(), v, new SolidColorBrush(v.GetColor())));
        public IEnumerable<SpotifyColorMode> PlayerModes => Enum.GetValues<SpotifyColorMode>();
        public AccentTheme Accent
        {
            get => App.Settings.Prop.AccentTheme;
            set { App.Settings.Prop.AccentTheme = value; _accentDraft = null; Changed(); }
        }
        public string AccentHex
        {
            get => _accentDraft ?? ThemeColors.Hex(ThemeColors.Accent(App.Settings.Prop));
            set
            {
                _accentDraft = value;
                if (!ThemeColors.TryParse(value, out var color)) { Message = "Enter six hex digits, such as #8B5CF6. The last valid color is kept."; return; }
                App.Settings.Prop.CustomAccentColor = ThemeColors.Hex(color);
                App.Settings.Prop.AccentTheme = AccentTheme.Custom;
                Changed();
            }
        }
        public string PlayerHex
        {
            get => _playerDraft ?? App.Settings.Prop.SpotifyAccentColor;
            set
            {
                _playerDraft = value;
                if (!ThemeColors.TryParse(value, out var color)) { Message = "Enter six hex digits for the player color. The last valid color is kept."; return; }
                App.Settings.Prop.SpotifyAccentColor = ThemeColors.Hex(color);
                Changed();
            }
        }
        public SpotifyColorMode PlayerMode
        {
            get => App.Settings.Prop.SpotifyColorMode;
            set { App.Settings.Prop.SpotifyColorMode = value; Changed(); }
        }
        public bool Compact
        {
            get => App.Settings.Prop.SpotifyCompactMode;
            set { App.Settings.Prop.SpotifyCompactMode = value; Changed(); }
        }
        public string SelectionLabel => Accent == AccentTheme.Custom ? "Custom color" : Accent == AccentTheme.OnyxViolet ? "Onyx Violet" : Accent.ToString();
        public Brush PreviewBrush => new SolidColorBrush(ThemeColors.Accent(App.Settings.Prop));
        public string Message { get => _message; private set { _message = value; OnPropertyChanged(nameof(Message)); } }
        public ObservableCollection<ColorProfile> Profiles => App.Settings.Prop.ColorProfiles ??= new();
        public ColorProfile? SelectedProfile { get; set; }
        public string ProfileName { get; set; } = "";
        public ICommand SaveProfileCommand => new RelayCommand(SaveProfile);
        public ICommand ApplyProfileCommand => new RelayCommand(ApplyProfile);
        public ICommand DeleteProfileCommand => new RelayCommand(DeleteProfile);
        public ICommand ResetColorsCommand => new RelayCommand(ResetColors);

        private void Changed()
        {
            Message = "Preview updated. Click Save to apply colors to the running player.";
            foreach (string name in new[] { nameof(Accent), nameof(AccentHex), nameof(PlayerHex), nameof(PlayerMode), nameof(Compact), nameof(SelectionLabel), nameof(PreviewBrush) }) OnPropertyChanged(name);
            _refresh();
        }
        internal void SaveProfile()
        {
            string name = (ProfileName ?? "").Trim();
            if (name.Length == 0 || name.Length > 48) { Message = "Give the profile a name of 1–48 characters."; return; }
            var old = Profiles.FirstOrDefault(p => p is not null && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (old is null && Profiles.Count >= 32) { Message = "You can save up to 32 profiles. Remove one to add another."; return; }
            var settings = App.Settings.Prop;
            var profile = new ColorProfile { Name = name, Theme = settings.Theme, Accent = settings.AccentTheme,
                CustomAccent = settings.CustomAccentColor, PlayerMode = settings.SpotifyColorMode,
                PlayerAccent = settings.SpotifyAccentColor, Compact = settings.SpotifyCompactMode };
            if (old is null) Profiles.Add(profile); else Profiles[Profiles.IndexOf(old)] = profile;
            SelectedProfile = profile; OnPropertyChanged(nameof(SelectedProfile));
            Message = $"Profile ‘{name}’ saved in this menu. Click Save below to keep it.";
        }
        internal void ApplyProfile()
        {
            if (SelectedProfile is not { } profile) { Message = "Choose a saved profile first."; return; }
            var settings = App.Settings.Prop;
            settings.Theme = Enum.IsDefined(profile.Theme) ? profile.Theme : Theme.Dark;
            settings.AccentTheme = Enum.IsDefined(profile.Accent) ? profile.Accent : AccentTheme.OnyxViolet;
            settings.CustomAccentColor = ThemeColors.Hex(ThemeColors.Parse(profile.CustomAccent, Colors.MediumPurple));
            settings.SpotifyColorMode = Enum.IsDefined(profile.PlayerMode) ? profile.PlayerMode : SpotifyColorMode.Custom;
            settings.SpotifyAccentColor = ThemeColors.Hex(ThemeColors.Parse(profile.PlayerAccent, Color.FromRgb(30, 215, 96)));
            settings.SpotifyCompactMode = profile.Compact;
            ProfileName = profile.Name; OnPropertyChanged(nameof(ProfileName));
            _accentDraft = _playerDraft = null;
            Changed();
        }
        internal void DeleteProfile()
        {
            if (SelectedProfile is null) return;
            Profiles.Remove(SelectedProfile); SelectedProfile = null;
            OnPropertyChanged(nameof(SelectedProfile));
            Message = "Profile removed from this menu. Click Save to keep the change.";
        }
        private void ResetColors()
        {
            var settings = App.Settings.Prop;
            settings.AccentTheme = AccentTheme.OnyxViolet; settings.CustomAccentColor = "#7C6FD8";
            settings.SpotifyColorMode = SpotifyColorMode.Custom; settings.SpotifyAccentColor = "#1ED760";
            settings.SpotifyCompactMode = false;
            _accentDraft = _playerDraft = null;
            Changed();
        }
    }
}
