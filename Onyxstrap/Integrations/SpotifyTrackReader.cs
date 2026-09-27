using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Onyxstrap.Integrations
{
    internal enum SpotifyCommand { Previous, TogglePlayPause, Next }

    internal record SpotifyPlayback(string Title, string Artist, bool IsPlaying,
        bool CanToggle, bool CanPrevious, bool CanNext)
    {
        public string Album { get; init; } = "";
        public byte[]? Artwork { get; init; }
        public TimeSpan Position { get; init; }
        public TimeSpan Duration { get; init; }
        public static SpotifyPlayback Empty { get; } = new("", "", false, false, false, false);
        public bool HasTrack => !string.IsNullOrWhiteSpace(Title);
        public string? PlayingTrack => !IsPlaying || !HasTrack ? null
            : string.IsNullOrWhiteSpace(Artist) ? Title : $"{Artist} — {Title}";
    }

    internal sealed class SpotifyTrackReader
    {
        private GlobalSystemMediaTransportControlsSessionManager? _manager;
        private DateTime _retryAfter;
        private string? _artworkKey;
        private byte[]? _artwork;
        private DateTime _artworkRetryAfter;

        private async Task<GlobalSystemMediaTransportControlsSession?> GetSessionAsync()
        {
            if (DateTime.UtcNow < _retryAfter) return null;
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync()
                .AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            var sessions = _manager.GetSessions().Where(s => IsSpotify(s.SourceAppUserModelId)).ToList();
            return sessions.FirstOrDefault(s => s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                ?? sessions.FirstOrDefault(s => s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                ?? sessions.FirstOrDefault();
        }

        public async Task<SpotifyPlayback> GetPlaybackAsync(bool includeArtwork = true)
        {
            try
            {
                var session = await GetSessionAsync();
                if (session is null)
                {
                    _artworkKey = null;
                    _artwork = null;
                    return SpotifyPlayback.Empty;
                }
                var media = await session.TryGetMediaPropertiesAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
                var playback = session.GetPlaybackInfo();
                var controls = playback?.Controls;
                bool playing = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                var position = TimeSpan.Zero;
                var duration = TimeSpan.Zero;
                try
                {
                    var timeline = session.GetTimelineProperties();
                    if (timeline is not null)
                    {
                        var elapsed = playing ? DateTimeOffset.UtcNow - timeline.LastUpdatedTime : TimeSpan.Zero;
                        (position, duration) = NormalizeTimeline(timeline.Position, timeline.StartTime, timeline.EndTime, elapsed);
                    }
                }
                catch { /* A missing timeline should not hide the song or controls. */ }
                byte[]? artwork = null;
                if (includeArtwork)
                {
                    string key = string.Join("\0", session.SourceAppUserModelId, media.Title, media.Artist, media.AlbumTitle);
                    artwork = await GetArtworkAsync(key, media.Thumbnail);
                }
                return new SpotifyPlayback(media.Title?.Trim() ?? "", media.Artist?.Trim() ?? "",
                    playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    controls?.IsPlayPauseToggleEnabled == true || (playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                        ? controls?.IsPauseEnabled == true : controls?.IsPlayEnabled == true),
                    controls?.IsPreviousEnabled == true,
                    controls?.IsNextEnabled == true)
                {
                    Album = media.AlbumTitle?.Trim() ?? "",
                    Artwork = artwork,
                    Position = position,
                    Duration = duration
                };
            }
            catch (Exception ex)
            {
                OnError(ex);
                return SpotifyPlayback.Empty;
            }
        }

        public async Task<string?> GetTrackAsync() => (await GetPlaybackAsync(includeArtwork: false)).PlayingTrack;

        internal static (TimeSpan Position, TimeSpan Duration) NormalizeTimeline(
            TimeSpan position, TimeSpan start, TimeSpan end, TimeSpan elapsed)
        {
            double duration = Math.Max(0, end.TotalSeconds - start.TotalSeconds);
            double seconds = position.TotalSeconds - start.TotalSeconds + Math.Max(0, elapsed.TotalSeconds);
            return (TimeSpan.FromSeconds(Math.Clamp(seconds, 0, duration)), TimeSpan.FromSeconds(duration));
        }

        private async Task<byte[]?> GetArtworkAsync(string key, IRandomAccessStreamReference? thumbnail)
        {
            if (_artworkKey != key)
            {
                _artworkKey = key;
                _artwork = null;
                _artworkRetryAfter = DateTime.MinValue;
            }
            if (_artwork is not null || DateTime.UtcNow < _artworkRetryAfter) return _artwork;
            _artworkRetryAfter = DateTime.UtcNow.AddSeconds(10);
            if (thumbnail is null) return null;
            try
            {
                using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var stream = await thumbnail.OpenReadAsync().AsTask(timeout.Token);
                if (stream.Size == 0 || stream.Size > 4 * 1024 * 1024) return null;
                using var reader = new DataReader(stream);
                uint count = await reader.LoadAsync((uint)stream.Size).AsTask(timeout.Token);
                if (count != stream.Size) return null;
                var bytes = new byte[count];
                reader.ReadBytes(bytes);
                _artwork = bytes;
            }
            catch { /* Retry later without interrupting playback metadata. */ }
            return _artwork;
        }

        public async Task<bool> SendCommandAsync(SpotifyCommand command)
        {
            try
            {
                var session = await GetSessionAsync();
                if (session is null) return false;
                var playback = session.GetPlaybackInfo();
                var controls = playback?.Controls;
                var operation = command switch
                {
                    SpotifyCommand.Previous when controls?.IsPreviousEnabled == true => session.TrySkipPreviousAsync(),
                    SpotifyCommand.Next when controls?.IsNextEnabled == true => session.TrySkipNextAsync(),
                    SpotifyCommand.TogglePlayPause when controls?.IsPlayPauseToggleEnabled == true => session.TryTogglePlayPauseAsync(),
                    SpotifyCommand.TogglePlayPause when playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing && controls?.IsPauseEnabled == true => session.TryPauseAsync(),
                    SpotifyCommand.TogglePlayPause when playback?.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing && controls?.IsPlayEnabled == true => session.TryPlayAsync(),
                    _ => null
                };
                return operation is not null && await operation.AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                OnError(ex);
                return false;
            }
        }

        private void OnError(Exception ex)
        {
            _manager = null;
            _retryAfter = DateTime.UtcNow.AddSeconds(5);
            App.Logger.WriteLine("SpotifyTrackReader", $"Unable to access Spotify playback: {ex.Message}");
        }

        internal static bool IsSpotify(string appId) =>
            appId.Equals("Spotify.exe", StringComparison.OrdinalIgnoreCase) ||
            appId.Equals("Spotify", StringComparison.OrdinalIgnoreCase) ||
            appId.StartsWith("SpotifyAB.SpotifyMusic_", StringComparison.OrdinalIgnoreCase);
    }
}
