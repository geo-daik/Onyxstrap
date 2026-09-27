using Onyxstrap.Models.RobloxApi;
using DiscordRPC;

namespace Onyxstrap.Integrations
{
    public class DiscordRichPresence : IDisposable
    {
        private readonly DiscordRpcClient? _rpcClient;
        private readonly Action<DiscordRPC.RichPresence?> _publish;
        private readonly ActivityWatcher _activityWatcher;
        private readonly Queue<Message> _messageQueue = new();

        private DiscordRPC.RichPresence? _currentPresence;
        private DiscordRPC.RichPresence? _originalPresence;

        private FixedSizeList<ThumbnailCacheEntry> _thumbnailCache = new FixedSizeList<ThumbnailCacheEntry>(20);

        private ulong? _smallImgBeingFetched = null;
        private ulong? _largeImgBeingFetched = null;
        private CancellationTokenSource? _fetchThumbnailsToken;

        private bool _visible = true;

        private string? _spotifyTrack;
        private readonly object _presenceLock = new();
        private bool _disposed;
        private int _gameRevision;
        private bool _spotifyEnabled = App.Settings.Prop.ShowSpotifyOnRichPresence;

        /// <summary>
        /// Runtime toggle for showing the current Spotify track in the presence
        /// (flipped live with the Ctrl+Alt+S bind or the settings toggle).
        /// </summary>
        public bool SpotifyEnabled
        {
            get { lock (_presenceLock) return _spotifyEnabled; }
            set
            {
                lock (_presenceLock)
                {
                    if (_disposed || _spotifyEnabled == value) return;
                    _spotifyEnabled = value;
                    if (!value) _spotifyTrack = null;
                    UpdatePresence();
                }
            }
        }

        public DiscordRichPresence(ActivityWatcher activityWatcher) : this(activityWatcher, null) { }

        internal DiscordRichPresence(ActivityWatcher activityWatcher, Action<DiscordRPC.RichPresence?>? publish)
        {
            const string LOG_IDENT = "DiscordRichPresence";

            _activityWatcher = activityWatcher;

            _activityWatcher.OnGameJoin += OnGameChanged;
            _activityWatcher.OnGameLeave += OnGameChanged;
            _activityWatcher.OnGameMetadataChanged += OnGameChanged;
            _activityWatcher.OnRPCMessage += OnRPCMessage;

            if (publish is not null)
            {
                _publish = publish;
                return;
            }
            _rpcClient = new DiscordRpcClient("1005469189907173486");
            _publish = presence =>
            {
                if (presence is null) _rpcClient.ClearPresence();
                else _rpcClient.SetPresence(presence);
            };

            _rpcClient.OnReady += (_, e) =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Discord is ready");
                UpdatePresence();
            };

            _rpcClient.OnPresenceUpdate += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, "Presence updated");

            _rpcClient.OnError += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"An RPC error occurred - {e.Message}");

            _rpcClient.OnConnectionEstablished += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, "Established connection with Discord RPC");

            //spams log as it tries to connect every ~15 sec when discord is closed so not now
            //_rpcClient.OnConnectionFailed += (_, e) =>
            //    App.Logger.WriteLine(LOG_IDENT, "Failed to establish connection with Discord RPC");

            _rpcClient.OnClose += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"Lost connection to Discord RPC - {e.Reason} ({e.Code})");

            _rpcClient.Initialize();
        }

        private void OnGameChanged(object? sender, EventArgs e) => _ = RefreshGameAsync();

        private async Task RefreshGameAsync()
        {
            try { await SetCurrentGame(); }
            catch (Exception ex) { App.Logger.WriteException("DiscordRichPresence::RefreshGame", ex); }
        }

        private void OnRPCMessage(object? sender, Message message) => ProcessRPCMessage(message);

        public void ProcessRPCMessage(Message message, bool implicitUpdate = true)
        {
            lock (_presenceLock)
            {
                if (_disposed) return;

                const string LOG_IDENT = "DiscordRichPresence::ProcessRPCMessage";

                if (message.Command != "SetRichPresence" && message.Command != "SetLaunchData")
                    return;

                if (_currentPresence is null || _originalPresence is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Presence is not set, enqueuing message");
                    if (_messageQueue.Count >= 100) _messageQueue.Dequeue();
                    _messageQueue.Enqueue(message);
                    return;
                }

                // a lot of repeated code here, could this somehow be cleaned up?

                if (message.Command == "SetLaunchData")
                {
                    _currentPresence.Buttons = GetButtons();
                }
                else if (message.Command == "SetRichPresence")
                {
                    var previous = _currentPresence.Clone();
                    try { ProcessSetRichPresence(message, implicitUpdate); }
                    catch (Exception ex)
                    {
                        _currentPresence = previous;
                        App.Logger.WriteException(LOG_IDENT, ex);
                        return;
                    }
                }

                if (implicitUpdate)
                    UpdatePresence();

            }
        }

        private void AddToThumbnailCache(ulong id, string? url)
        {
            if (url != null)
                _thumbnailCache.Add(new ThumbnailCacheEntry { Id = id, Url = url });
        }

        private async Task UpdatePresenceIconsAsync(ulong? smallImg, ulong? largeImg, bool implicitUpdate, CancellationToken token)
        {
            DiscordRPC.RichPresence? presence;
            lock (_presenceLock) presence = _currentPresence;
            try
            {
                var requests = new List<ThumbnailRequest>();
                foreach (var id in new[] { smallImg, largeImg })
                    if (id.HasValue)
                        requests.Add(new ThumbnailRequest { TargetId = id.Value, Type = "Asset", Size = "512x512", IsCircular = false });

                var urls = await Thumbnails.GetThumbnailUrlsAsync(requests, token);
                lock (_presenceLock)
                {
                    if (_disposed || token.IsCancellationRequested || !ReferenceEquals(presence, _currentPresence) || presence is null)
                        return;
                    int i = 0;
                    if (smallImg.HasValue && i < urls.Length)
                    {
                        string? url = urls[i++];
                        AddToThumbnailCache(smallImg.Value, url);
                        if (url is not null) presence.Assets.SmallImageKey = url;
                    }
                    if (largeImg.HasValue && i < urls.Length)
                    {
                        string? url = urls[i];
                        AddToThumbnailCache(largeImg.Value, url);
                        if (url is not null) presence.Assets.LargeImageKey = url;
                    }
                    _smallImgBeingFetched = _largeImgBeingFetched = null;
                    if (implicitUpdate) UpdatePresence();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { App.Logger.WriteException("DiscordRichPresence::UpdateIcons", ex); }
        }

        private void ProcessSetRichPresence(Message message, bool implicitUpdate)
        {
            const string LOG_IDENT = "DiscordRichPresence::ProcessSetRichPresence";
            Models.OnyxstrapRPC.RichPresence? presenceData;

            Debug.Assert(_currentPresence is not null);
            Debug.Assert(_originalPresence is not null);

            if (_fetchThumbnailsToken != null)
            {
                _fetchThumbnailsToken.Cancel();
                _fetchThumbnailsToken.Dispose();
                _fetchThumbnailsToken = null;
            }

            try
            {
                presenceData = message.Data.Deserialize<Models.OnyxstrapRPC.RichPresence>();
            }
            catch (Exception)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization threw an exception)");
                return;
            }

            if (presenceData is null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                return;
            }

            if (presenceData.Details is not null)
            {
                if (presenceData.Details.Length > 128)
                    App.Logger.WriteLine(LOG_IDENT, $"Details cannot be longer than 128 characters");
                else if (presenceData.Details == "<reset>")
                    _currentPresence.Details = _originalPresence.Details;
                else
                    _currentPresence.Details = PresenceText.Limit(presenceData.Details);
            }

            if (presenceData.State is not null)
            {
                if (presenceData.State.Length > 128)
                    App.Logger.WriteLine(LOG_IDENT, $"State cannot be longer than 128 characters");
                else if (presenceData.State == "<reset>")
                    _currentPresence.State = _originalPresence.State;
                else
                    _currentPresence.State = PresenceText.Limit(presenceData.State);
            }

            if (presenceData.TimestampStart == 0)
                _currentPresence.Timestamps.Start = null;
            else if (presenceData.TimestampStart is not null && presenceData.TimestampStart <= 253402300799UL)
                _currentPresence.Timestamps.StartUnixMilliseconds = presenceData.TimestampStart * 1000;

            if (presenceData.TimestampEnd == 0)
                _currentPresence.Timestamps.End = null;
            else if (presenceData.TimestampEnd is not null && presenceData.TimestampEnd <= 253402300799UL)
                _currentPresence.Timestamps.EndUnixMilliseconds = presenceData.TimestampEnd * 1000;

            // set these to start fetching
            ulong? smallImgFetch = null;
            ulong? largeImgFetch = null;

            // only set small image if account display is disabled, doesnt make sense to override it if it is true
            if (presenceData.SmallImage is not null && !App.Settings.Prop.ShowAccountOnRichPresence)
            {
                if (presenceData.SmallImage.Clear)
                {
                    _currentPresence.Assets.SmallImageKey = "";
                    _smallImgBeingFetched = null;
                }
                else if (presenceData.SmallImage.Reset)
                {
                    _currentPresence.Assets.SmallImageText = _originalPresence.Assets.SmallImageText;
                    _currentPresence.Assets.SmallImageKey = _originalPresence.Assets.SmallImageKey;
                    _smallImgBeingFetched = null;
                }
                else
                {
                    if (presenceData.SmallImage.AssetId is not null)
                    {
                        ThumbnailCacheEntry? entry = _thumbnailCache.FirstOrDefault(x => x.Id == presenceData.SmallImage.AssetId);

                        if (entry == null)
                        {
                            smallImgFetch = presenceData.SmallImage.AssetId;
                        }
                        else
                        {
                            _currentPresence.Assets.SmallImageKey = entry.Url;
                            _smallImgBeingFetched = null;
                        }
                    }

                    if (presenceData.SmallImage.HoverText is not null)
                        _currentPresence.Assets.SmallImageText = PresenceText.Limit(presenceData.SmallImage.HoverText);
                }
            }

            if (presenceData.LargeImage is not null)
            {
                if (presenceData.LargeImage.Clear)
                {
                    _currentPresence.Assets.LargeImageKey = "";
                    _largeImgBeingFetched = null;
                }
                else if (presenceData.LargeImage.Reset)
                {
                    _currentPresence.Assets.LargeImageText = _originalPresence.Assets.LargeImageText;
                    _currentPresence.Assets.LargeImageKey = _originalPresence.Assets.LargeImageKey;
                    _largeImgBeingFetched = null;
                }
                else
                {
                    if (presenceData.LargeImage.AssetId is not null)
                    {
                        ThumbnailCacheEntry? entry = _thumbnailCache.FirstOrDefault(x => x.Id == presenceData.LargeImage.AssetId);

                        if (entry == null)
                        {
                            largeImgFetch = presenceData.LargeImage.AssetId;
                        }
                        else
                        {
                            _currentPresence.Assets.LargeImageKey = entry.Url;
                            _largeImgBeingFetched = null;
                        }
                    }

                    if (presenceData.LargeImage.HoverText is not null)
                        _currentPresence.Assets.LargeImageText = PresenceText.Limit(presenceData.LargeImage.HoverText);
                }
            }

            if (smallImgFetch != null)
                _smallImgBeingFetched = smallImgFetch;
            if (largeImgFetch != null)
                _largeImgBeingFetched = largeImgFetch;

            if (_smallImgBeingFetched != null || _largeImgBeingFetched != null)
            {
                _fetchThumbnailsToken = new CancellationTokenSource();
                _ = UpdatePresenceIconsAsync(_smallImgBeingFetched, _largeImgBeingFetched, true, _fetchThumbnailsToken.Token);
            }
        }

        /// <summary>
        /// Overrides the presence state line with the current Spotify track
        /// (or restores the game state when stopped/toggled off).
        /// </summary>
        public void SetSpotifyTrack(string? track)
        {
            lock (_presenceLock)
            {
                if (_disposed) return;

                string? normalized = PresenceText.Limit(track, 115);
                if (_spotifyTrack == normalized) return;
                _spotifyTrack = normalized;
                UpdatePresence();

            }
        }

        public void SetVisibility(bool visible)
        {
            lock (_presenceLock)
            {
                if (_disposed) return;

                App.Logger.WriteLine("DiscordRichPresence::SetVisibility", $"Setting presence visibility ({visible})");

                _visible = visible;

                UpdatePresence();

            }
        }

        public async Task<bool> SetCurrentGame()
        {
            const string LOG_IDENT = "DiscordRichPresence::SetCurrentGame";
            ActivityData activity;
            int revision;
            lock (_presenceLock)
            {
                if (_disposed) return false;
                revision = ++_gameRevision;
                _fetchThumbnailsToken?.Cancel();
                _fetchThumbnailsToken?.Dispose();
                _fetchThumbnailsToken = null;
                _smallImgBeingFetched = _largeImgBeingFetched = null;
                _currentPresence = _originalPresence = null;
                if (!_activityWatcher.InGame)
                {
                    _messageQueue.Clear();
                    UpdatePresence();
                    return true;
                }
                activity = _activityWatcher.Data;
                UpdatePresence();
            }

            string icon = "roblox";
            string smallImage = "roblox";
            string smallImageText = "Roblox";
            string gameName = "Playing Roblox";
            string creator = "In a game";
            try
            {
                if (activity.UniverseDetails is null && activity.UniverseId > 0)
                {
                    await UniverseDetails.FetchSingle(activity.UniverseId);
                    activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
                }
                if (activity.UniverseDetails is { } details)
                {
                    gameName = details.Data.Name ?? gameName;
                    icon = details.Thumbnail?.ImageUrl ?? icon;
                    if (details.Data.Creator is { } author)
                        creator = $"by {author.Name}" + (author.HasVerifiedBadge ? " ☑️" : "");
                }
            }
            catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }

            if (App.Settings.Prop.ShowAccountOnRichPresence && activity.UserId > 0)
            {
                try
                {
                    var user = await UserDetails.Fetch(activity.UserId);
                    smallImage = user.Thumbnail?.ImageUrl ?? smallImage;
                    smallImageText = $"Playing on {user.Data.DisplayName} (@{user.Data.Name})";
                }
                catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
            }

            lock (_presenceLock)
            {
                // The captured ActivityData survives a teleport; compare with the watcher's current object.
                if (_disposed || revision != _gameRevision || !_activityWatcher.InGame || !ReferenceEquals(activity, _activityWatcher.Data))
                    return false;

                string status = activity.ServerType switch
                {
                    ServerType.Private => "In a private server",
                    ServerType.Reserved => "In a reserved server",
                    _ => creator
                };
                _currentPresence = new DiscordRPC.RichPresence
                {
                    Details = PresenceText.Limit(gameName),
                    State = PresenceText.Limit(status),
                    Timestamps = new Timestamps { Start = (activity.RootActivity?.TimeJoined ?? activity.TimeJoined).ToUniversalTime() },
                    Buttons = GetButtons(),
                    Assets = new Assets
                    {
                        LargeImageKey = icon,
                        LargeImageText = PresenceText.Limit(gameName),
                        SmallImageKey = smallImage,
                        SmallImageText = PresenceText.Limit(smallImageText)
                    }
                };
                _originalPresence = _currentPresence.Clone();
                while (_messageQueue.Count > 0)
                    ProcessRPCMessage(_messageQueue.Dequeue(), false);
                UpdatePresence();
            }
            return true;
        }

        public Button[] GetButtons()
        {
            var buttons = new List<Button>();

            var data = _activityWatcher.Data;

            if (!App.Settings.Prop.HideRPCButtons)
            {
                bool show = false;

                if (data.ServerType == ServerType.Public)
                    show = true;
                else if (data.ServerType == ServerType.Reserved && !String.IsNullOrEmpty(data.RPCLaunchData))
                    show = true;

                if (show)
                {
                    buttons.Add(new Button
                    {
                        Label = "Join server",
                        Url = data.GetInviteDeeplink()
                    });
                }
            }

            buttons.Add(new Button
            {
                Label = "See game page",
                Url = $"https://www.roblox.com/games/{data.PlaceId}"
            });

            return buttons.ToArray();
        }

        public void UpdatePresence()
        {
            lock (_presenceLock)
            {
                if (_disposed) return;

                try
                {
                    if (!_visible || _currentPresence is null)
                    {
                        _publish(null);
                        return;
                    }
                    // Keep the game's state untouched so pause/off and game RPC updates restore correctly.
                    var outgoing = _currentPresence.Clone();
                    outgoing.State = PresenceText.State(outgoing.State, _spotifyEnabled, _spotifyTrack);
                    _publish(outgoing);
                }
                catch (Exception ex) { App.Logger.WriteException("DiscordRichPresence::UpdatePresence", ex); }

            }
        }

        public void Dispose()
        {
            lock (_presenceLock)
            {
                if (_disposed) return;
                _disposed = true;
                ++_gameRevision;
                _activityWatcher.OnGameJoin -= OnGameChanged;
                _activityWatcher.OnGameLeave -= OnGameChanged;
                _activityWatcher.OnGameMetadataChanged -= OnGameChanged;
                _activityWatcher.OnRPCMessage -= OnRPCMessage;
                _fetchThumbnailsToken?.Cancel();
                _fetchThumbnailsToken?.Dispose();
                _messageQueue.Clear();
                _rpcClient?.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
