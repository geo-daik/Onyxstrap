using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
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

        public SpotifyOverlayWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) =>
            {
                nint handle = new WindowInteropHelper(this).Handle;
                // Keep the player out of Alt-Tab while allowing mouse interaction.
                SetWindowLongW(handle, -20, GetWindowLongW(handle, -20) | 0x00000080);
            };
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
                }
                catch { /* Keep the music placeholder for unsupported/corrupt artwork. */ }
            }
            ArtworkPlaceholder.Visibility = AlbumArtwork.Source is null ? Visibility.Visible : Visibility.Collapsed;
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
