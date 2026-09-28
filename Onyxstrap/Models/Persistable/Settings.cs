using System.Collections.ObjectModel;

namespace Onyxstrap.Models.Persistable
{
    public class Settings
    {
        // onyxstrap configuration
        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.OnyxDialog;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconOnyxstrap;
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = "";
        public Theme Theme { get; set; } = Theme.Dark;
        public AccentTheme AccentTheme { get; set; } = AccentTheme.OnyxViolet;
        public string CustomAccentColor { get; set; } = "#7C6FD8";
        public SpotifyColorMode SpotifyColorMode { get; set; } = SpotifyColorMode.Custom;
        public string SpotifyAccentColor { get; set; } = "#1ED760";
        public bool SpotifyCompactMode { get; set; } = false;
        public ObservableCollection<ColorProfile> ColorProfiles { get; set; } = new();
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool DeveloperMode { get; set; } = false;
        public bool CheckForUpdates { get; set; } = false;
        public bool ConfirmLaunches { get; set; } = false;
        public string Locale { get; set; } = "nil";
        public bool UseFastFlagManager { get; set; } = true;
        public bool WPFSoftwareRender { get; set; } = false;
        public bool EnableAnalytics { get; set; } = false;
        public bool RebrandGameWindow { get; set; } = true;
        public bool BackgroundUpdatesEnabled { get; set; } = false;
        public bool DebugDisableVersionPackageCleanup { get; set; } = false;
        public string? SelectedCustomTheme { get; set; } = null;
        public WebEnvironment WebEnvironment { get; set; } = WebEnvironment.Production;

        public ObservableCollection<FavoriteGame> FavoriteGames { get; set; } = new();
        public bool SpotifySnapToEdges { get; set; } = true;

        // integration configuration
        public bool EnableActivityTracking { get; set; } = true;
        public bool UseDiscordRichPresence { get; set; } = true;
        public bool HideRPCButtons { get; set; } = true;
        public bool ShowAccountOnRichPresence { get; set; } = false;
        public bool EnableSpotifyOverlay { get; set; } = true;
        public int SpotifyOverlayKey { get; set; } = 0xA1; // Right Shift
        public int SpotifyOverlayModifiers { get; set; } = 0;
        public bool ShowSpotifyOnRichPresence { get; set; } = false;
        public bool ShowServerDetails { get; set; } = false;
        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = new();

        // mod preset configuration
        public bool UseDisableAppPatch { get; set; } = false;
    }
}
