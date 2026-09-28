using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Onyxstrap.Integrations;

namespace Onyxstrap.UI.Elements.Overlay
{
    public partial class SpotifyOverlayWindow : Window
    {
        internal event Action<SpotifyCommand>? CommandRequested;
        internal event Action? HideRequested;
        internal event Action? DragCompleted;
        internal bool IsDragging { get; private set; }
        private SpotifyPlayback _playback = SpotifyPlayback.Empty;
        private bool _busy;
        private byte[]? _artworkBytes;
        private SpotifyColorMode _colorMode;
        private Color _customColor = Color.FromRgb(30, 215, 96);
        private Color _appColor = Color.FromRgb(124, 111, 216);
        private Color? _albumColor;

        public SpotifyOverlayWindow()
        {
            InitializeComponent();
            UpdateAppearance(App.Settings.Prop);
            SourceInitialized += (_, _) =>
            {
                nint handle = new WindowInteropHelper(this).Handle;
                // Keep the player out of Alt-Tab while allowing mouse interaction.
                SetWindowLongW(handle, -20, GetWindowLongW(handle, -20) | 0x00000080);
            };
        }

        internal void UpdateAppearance(Models.Persistable.Settings settings)
        {
            _colorMode = settings.SpotifyColorMode;
            _customColor = ThemeColors.Parse(settings.SpotifyAccentColor, Color.FromRgb(30, 215, 96));
            _appColor = ThemeColors.Accent(settings);
            bool compact = settings.SpotifyCompactMode;
            Width = compact ? 340 : 380;
            Height = compact ? 194 : 270;
            PlayerSurface.Padding = new Thickness(compact ? 14 : 18);
            HeaderRow.Height = new GridLength(compact ? 22 : 28);
            MediaRow.Height = new GridLength(compact ? 68 : 110);
            ProgressRow.Height = new GridLength(compact ? 32 : 48);
            ControlsRow.Height = new GridLength(compact ? 42 : 46);
            double cover = compact ? 52 : 86;
            ArtworkFrame.Width = ArtworkFrame.Height = cover;
            ArtworkClip.Rect = new Rect(0, 0, cover, cover);
            ArtworkColumn.Width = new GridLength(compact ? 66 : 102);
            MediaGrid.Margin = compact ? new Thickness(0, 8, 0, 8) : new Thickness(0, 12, 0, 10);
            TrackTitle.FontSize = compact ? 15 : 18;
            TrackArtist.FontSize = compact ? 12 : 13;
            TrackAlbum.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            ApplyPlayerColors();
        }

        private void ApplyPlayerColors()
        {
            Color accent = _colorMode switch
            {
                SpotifyColorMode.MatchApp => _appColor,
                SpotifyColorMode.AlbumArt => _albumColor ?? _customColor,
                _ => _customColor
            };
            Color background = ThemeColors.Mix(Color.FromRgb(18, 19, 22), accent, .065);
            // Keep even a black custom accent visible on a dark player.
            Color visible = accent;
            for (int i = 0; i < 12 && ThemeColors.Contrast(visible, background) < 3; i++)
                visible = ThemeColors.Mix(visible, Colors.White, .12);
            Resources["PlayerAccent"] = new SolidColorBrush(visible);
            Resources["PlayerAccentText"] = new SolidColorBrush(ThemeColors.TextOn(visible));
            Resources["PlayerBackground"] = new SolidColorBrush(background);
            Resources["PlayerBorder"] = new SolidColorBrush(ThemeColors.Mix(background, visible, .3));
            Resources["PlayerButtonBackground"] = new SolidColorBrush(ThemeColors.Mix(background, visible, .10));
            Resources["PlayerArtBackground"] = new SolidColorBrush(ThemeColors.Mix(background, visible, .17));
        }

        internal void UpdateShortcut(string label)
        {
            ShortcutLabel.Text = label;
            ShortcutLabel.ToolTip = label;
            CloseButton.ToolTip = $"Hide ({label})";
        }

        internal void UpdatePlayback(SpotifyPlayback playback)
        {
            _playback = playback;
            TrackTitle.Text = playback.HasTrack ? playback.Title : "Your music, in game";
            TrackArtist.Text = playback.HasTrack ? playback.Artist : "Open Spotify and play a song";
            TrackTitle.ToolTip = playback.HasTrack ? playback.Title : null;
            TrackArtist.ToolTip = playback.HasTrack ? playback.Artist : null;
            TrackAlbum.Text = playback.HasTrack ? playback.Album : "";
            TrackAlbum.ToolTip = string.IsNullOrEmpty(TrackAlbum.Text) ? null : TrackAlbum.Text;
            UpdateArtwork(playback.HasTrack ? playback.Artwork : null);
            bool hasTimeline = playback.HasTrack && playback.Duration > TimeSpan.Zero;
            double duration = hasTimeline ? playback.Duration.TotalSeconds : 0;
            double position = Math.Clamp(playback.Position.TotalSeconds, 0, duration);
            TrackProgress.Value = hasTimeline ? position / duration : 0;
            ElapsedTime.Text = hasTimeline ? FormatTime(TimeSpan.FromSeconds(position)) : "--:--";
            DurationTime.Text = hasTimeline ? FormatTime(playback.Duration) : "--:--";
            PlaybackStatus.Text = !playback.HasTrack ? "NOT CONNECTED" : playback.IsPlaying ? "NOW PLAYING" : "PAUSED";
            PlayPauseButton.Content = playback.IsPlaying ? "\uE769" : "\uE768";
            PlayPauseButton.ToolTip = playback.IsPlaying ? "Pause" : "Play";
            SetBusy(_busy);
        }

        private void UpdateArtwork(byte[]? artwork)
        {
            if (ReferenceEquals(_artworkBytes, artwork)) return;
            _artworkBytes = artwork;
            AlbumArtwork.Source = null;
            _albumColor = null;
            if (artwork is { Length: > 0 and <= 4194304 })
            {
                try
                {
                    using var stream = new MemoryStream(artwork, writable: false);
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 256;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    AlbumArtwork.Source = image;
                    _albumColor = ThemeColors.FromArtwork(image);
                }
                catch { /* Keep the music placeholder for unsupported/corrupt artwork. */ }
            }
            ArtworkPlaceholder.Visibility = AlbumArtwork.Source is null ? Visibility.Visible : Visibility.Collapsed;
            ApplyPlayerColors();
        }

        private static string FormatTime(TimeSpan time) => time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{(int)time.TotalMinutes}:{time.Seconds:00}";

        internal void SetBusy(bool busy)
        {
            _busy = busy;
            PlayPauseButton.IsEnabled = !busy && _playback.CanToggle;
            PreviousButton.IsEnabled = !busy && _playback.CanPrevious;
            NextButton.IsEnabled = !busy && _playback.CanNext;
        }

        internal void ShowCommandError() => PlaybackStatus.Text = "TRY IN SPOTIFY";
        internal void FitWithin(Rect bounds)
        {
            if (IsDragging || bounds.IsEmpty) return;
            Left = Math.Clamp(Left, bounds.Left, Math.Max(bounds.Left, bounds.Right - Width));
            Top = Math.Clamp(Top, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - Height));
        }

        private void DragHeader(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            IsDragging = true;
            try { DragMove(); }
            catch (InvalidOperationException) { }
            finally { IsDragging = false; DragCompleted?.Invoke(); }
        }

        private void CloseClick(object sender, RoutedEventArgs e) => HideRequested?.Invoke();
        private void PreviousClick(object sender, RoutedEventArgs e) => CommandRequested?.Invoke(SpotifyCommand.Previous);
        private void PlayPauseClick(object sender, RoutedEventArgs e) => CommandRequested?.Invoke(SpotifyCommand.TogglePlayPause);
        private void NextClick(object sender, RoutedEventArgs e) => CommandRequested?.Invoke(SpotifyCommand.Next);

        [DllImport("user32.dll")] private static extern int GetWindowLongW(nint hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLongW(nint hwnd, int index, int value);
    }
}
