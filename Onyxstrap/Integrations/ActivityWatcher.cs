namespace Onyxstrap.Integrations
{
    public class ActivityWatcher : IDisposable
    {
        private const string GameMessageEntry                = "[FLog::CreatorOutput] [OnyxstrapRPC]";
        private const string LegacyGameMessageEntry          = "[FLog::Output] [BloxstrapRPC]";
        private const string CreatorGameMessageEntry         = "[FLog::CreatorOutput] [BloxstrapRPC]";
        private const string GameJoiningEntry                = "[FLog::Output] ! Joining game";

        // these entries are technically volatile!
        // they only get printed depending on their configured FLog level, which could change at any time
        // while levels being changed is fairly rare, please limit the number of varying number of FLog types you have to use, if possible

        private const string GameTeleportingEntry            = "[FLog::UgcExperienceController] UgcExperienceController: doTeleport: joinScriptUrl";
        private const string GameJoiningUniverseEntry        = "[FLog::GameJoinLoadTime] Report game_join_loadtime:";
        private const string GameJoiningUDMUXEntry           = "[FLog::Network] UDMUX Address = ";
        private const string GameJoinedEntry                 = "[FLog::Network] Replicator created: ";
        private const string GameDisconnectedEntry           = "[FLog::Network] Time to disconnect replication data:";
        private const string GameLeavingEntry                = "[FLog::SingleSurfaceApp] leaveUGCGameInternal";

        private const string GameJoiningEntryPattern         = @"! Joining game '([0-9a-f\-]{36})' place ([0-9]+) at ([0-9\.]+)";
        private const string GameJoinReferralPattern         = @"referral_page:([^,]+)";
        private const string GameTeleportJoinTypePattern     = @"JoinTypeId""%3a(\d+)%2c";
        private const string GameJoiningUniversePattern      = @"universeid:([0-9]+).*userid:([0-9]+)";
        private const string GameJoiningUDMUXPattern         = @"UDMUX Address = ([0-9\.]+), Port = [0-9]+ \| RCC Server Address = ([0-9\.]+), Port = [0-9]+";
        private const string GameJoinedEntryPattern          = @"serverId: ([0-9\.]+)\|[0-9]+";
        private const string GameMessageEntryPattern         = @"\[(?:OnyxstrapRPC|BloxstrapRPC)\] (.*)";

        private int _logEntriesRead = 0;
        private bool _teleportMarker = false;
        private bool _reservedTeleportMarker = false;
        
        public event EventHandler<string>? OnLogEntry;
        public event EventHandler? OnGameJoin;
        public event EventHandler? OnGameLeave;
        public event EventHandler? OnGameMetadataChanged;
        public event EventHandler? OnLogOpen;
        public event EventHandler? OnAppClose;
        public event EventHandler<Message>? OnRPCMessage;

        private DateTime LastRPCRequest;

        public string LogLocation = null!;

        public bool InGame = false;
        
        public ActivityData Data { get; private set; } = new();

        /// <summary>
        /// Ordered by newest to oldest
        /// </summary>
        public List<ActivityData> History = new();

        public volatile bool IsDisposed = false;
        private readonly CancellationTokenSource _stop = new();

        public ActivityWatcher(string? logFile = null)
        {
            if (!String.IsNullOrEmpty(logFile))
                LogLocation = logFile;
        }

        public async Task StartAsync()
        {
            const string LOG_IDENT = "ActivityWatcher::Start";
            try
            {
                DateTime earliestLog = DateTime.Now.AddSeconds(-15);
                string logDirectory = Path.Combine(Paths.LocalAppData, @"Roblox\logs");
                FileStream? stream = null;
                while (!IsDisposed && stream is null)
                {
                    if (string.IsNullOrEmpty(LogLocation) && Directory.Exists(logDirectory))
                    {
                        var candidate = new DirectoryInfo(logDirectory).GetFiles("*.log")
                            .Where(x => x.Name.Contains("Player", StringComparison.OrdinalIgnoreCase) && x.CreationTime >= earliestLog)
                            .OrderByDescending(x => x.CreationTime).FirstOrDefault();
                        if (candidate is not null) LogLocation = candidate.FullName;
                    }
                    if (!string.IsNullOrEmpty(LogLocation))
                    {
                        try { stream = new FileStream(LogLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
                        catch (IOException) { } // Created can fire before the file becomes readable.
                    }
                    if (stream is null) await Task.Delay(250, _stop.Token);
                }
                if (stream is null) return;
                using var reader = new StreamReader(stream);
                OnLogOpen?.Invoke(this, EventArgs.Empty);
                App.Logger.WriteLine(LOG_IDENT, $"Opened {LogLocation}");
                var pending = new StringBuilder();
                var buffer = new char[4096];
                while (!IsDisposed)
                {
                    int count = await reader.ReadAsync(buffer.AsMemory(), _stop.Token);
                    if (count == 0)
                    {
                        await Task.Delay(250, _stop.Token);
                        continue;
                    }
                    for (int i = 0; i < count; i++)
                    {
                        if (buffer[i] != '\n')
                        {
                            pending.Append(buffer[i]);
                            if (pending.Length > 1024 * 1024) pending.Clear();
                            continue;
                        }
                        string line = pending.ToString().TrimEnd('\r');
                        pending.Clear();
                        try { ReadLogEntry(line); }
                        catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
                    }
                }
            }
            catch (OperationCanceledException) when (IsDisposed) { }
            catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
        }

        private void ReadLogEntry(string entry)
        {
            const string LOG_IDENT = "ActivityWatcher::ReadLogEntry";

            OnLogEntry?.Invoke(this, entry);

            _logEntriesRead += 1;

            // debug stats to ensure that the log reader is working correctly
            // if more than 1000 log entries have been read, only log per 100 to save on spam
            if (_logEntriesRead <= 1000 && _logEntriesRead % 50 == 0)
                App.Logger.WriteLine(LOG_IDENT, $"Read {_logEntriesRead} log entries");
            else if (_logEntriesRead % 100 == 0)
                App.Logger.WriteLine(LOG_IDENT, $"Read {_logEntriesRead} log entries");

            // get the log message from the read line
            int logMessageIdx = entry.IndexOf("[FLog::", StringComparison.Ordinal);
            if (logMessageIdx == -1)
            {
                // likely a log message that spanned multiple lines
                return;
            }

            string logMessage = entry[logMessageIdx..];

            if (logMessage.StartsWith(GameLeavingEntry))
            {
                App.Logger.WriteLine(LOG_IDENT, "User is back into the desktop app");
                
                if (InGame)
                {
                    Data.TimeLeft = DateTime.Now;
                    History.Insert(0, Data);
                    InGame = false;
                    Data = new();
                    OnGameLeave?.Invoke(this, EventArgs.Empty);
                }
                OnAppClose?.Invoke(this, EventArgs.Empty);

                if (Data.PlaceId != 0 && !InGame)
                {
                    App.Logger.WriteLine(LOG_IDENT, "User appears to be leaving from a cancelled/errored join");
                    Data = new();
                }

                return;
            }

            if (!InGame && Data.PlaceId != 0 && logMessage.StartsWith(GameDisconnectedEntry))
            {
                Data = new();
                return;
            }

            if (Data.PlaceId != 0 && logMessage.StartsWith(GameJoiningUniverseEntry))
            {
                var match = Regex.Match(logMessage, GameJoiningUniversePattern);

                if (!match.Success || !long.TryParse(match.Groups[1].Value, out _) || !long.TryParse(match.Groups[2].Value, out _))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to assert format for game join universe entry");
                    App.Logger.WriteLine(LOG_IDENT, logMessage);
                    return;
                }

                Data.UniverseId = Int64.Parse(match.Groups[1].Value);
                Data.UserId = Int64.Parse(match.Groups[2].Value);

                var loadTimeMatch = Regex.Match(logMessage, GameJoinReferralPattern);

                if (loadTimeMatch.Groups.Count == 2)
                {
                    string referral = loadTimeMatch.Groups[1].Value;

                    if (referral.Contains("RequestPrivateGame", StringComparison.OrdinalIgnoreCase) || referral.Contains("GameDetailPageJSHybridEvent", StringComparison.OrdinalIgnoreCase))
                        Data.ServerType = ServerType.Private;
                }

                if (History.Any())
                {
                    var lastActivity = History.First();

                    if (Data.UniverseId == lastActivity.UniverseId && Data.IsTeleport)
                        Data.RootActivity = lastActivity.RootActivity ?? lastActivity;
                }
                if (InGame) OnGameMetadataChanged?.Invoke(this, EventArgs.Empty);
                return;
            }


            if (!InGame && Data.PlaceId == 0)
            {
                // We are not in a game, nor are in the process of joining one
                
                if (logMessage.StartsWith(GameJoiningEntry))
                {
                    Match match = Regex.Match(logMessage, GameJoiningEntryPattern);

                    if (!match.Success || !long.TryParse(match.Groups[2].Value, out _))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to assert format for game join entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    InGame = false;
                    Data.PlaceId = long.Parse(match.Groups[2].Value);
                    Data.JobId = match.Groups[1].Value;
                    Data.MachineAddress = match.Groups[3].Value;

                    if (App.Settings.Prop.ShowServerDetails && Data.MachineAddressValid)
                        _ = Data.QueryServerLocation();

                    if (_teleportMarker)
                    {
                        Data.IsTeleport = true;
                        _teleportMarker = false;
                    }

                    if (_reservedTeleportMarker)
                    {
                        Data.ServerType = ServerType.Reserved;
                        _reservedTeleportMarker = false;
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Joining Game ({Data})");
                }
            }
            else if (!InGame && Data.PlaceId != 0)
            {
                // We are not confirmed to be in a game, but we are in the process of joining one

                if (logMessage.StartsWith(GameJoiningUDMUXEntry))
                {
                    var match = Regex.Match(logMessage, GameJoiningUDMUXPattern);

                    if (!match.Success || match.Groups[2].Value != Data.MachineAddress)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to assert format for game join UDMUX entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    Data.MachineAddress = match.Groups[1].Value;

                    if (App.Settings.Prop.ShowServerDetails)
                        _ = Data.QueryServerLocation();

                    App.Logger.WriteLine(LOG_IDENT, $"Server is UDMUX protected ({Data})");
                }
                else if (logMessage.StartsWith(GameJoinedEntry))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Joined Game ({Data})");

                    InGame = true;
                    Data.TimeJoined = DateTime.Now;

                    OnGameJoin?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (InGame && Data.PlaceId != 0)
            {
                // We are confirmed to be in a game

                if (logMessage.StartsWith(GameDisconnectedEntry))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Disconnected from Game ({Data})");

                    Data.TimeLeft = DateTime.Now;
                    History.Insert(0, Data);

                    InGame = false;
                    Data = new();

                    OnGameLeave?.Invoke(this, EventArgs.Empty);
                }
                else if (logMessage.StartsWith(GameTeleportingEntry))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Initiating teleport to server ({Data})");
                    _teleportMarker = true;

                    var joinTypeMatch = Regex.Match(logMessage, GameTeleportJoinTypePattern);
                    if (joinTypeMatch.Success && int.TryParse(joinTypeMatch.Groups[1].Value, out int joinTypeId))
                    {
                        var joinType = (ServerSessionJoinType)joinTypeId;
                        App.Logger.WriteLine(LOG_IDENT, $"Teleport JoinTypeId: {joinTypeId}");

                        if (joinType is ServerSessionJoinType.NewGamePrivateGame or ServerSessionJoinType.SpecificPrivateGame)
                        {
                            _reservedTeleportMarker = true;
                            App.Logger.WriteLine(LOG_IDENT, "Detected reserved server teleport");
                        }
                    }
                }
                else if (logMessage.StartsWith(GameMessageEntry) || logMessage.StartsWith(CreatorGameMessageEntry) || logMessage.StartsWith(LegacyGameMessageEntry) || logMessage.StartsWith("[FLog::Output] [OnyxstrapRPC]"))
                {
                    var match = Regex.Match(logMessage, GameMessageEntryPattern);

                    if (!match.Success)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to assert format for RPC message entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    string messagePlain = match.Groups[1].Value;
                    Message? message;

                    App.Logger.WriteLine(LOG_IDENT, $"Received message: '{messagePlain}'");

                    if ((DateTime.Now - LastRPCRequest).TotalSeconds <= 1)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Dropping message as ratelimit has been hit");
                        return;
                    }

                    try
                    {
                        message = JsonSerializer.Deserialize<Message>(messagePlain);
                    }
                    catch (Exception)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization threw an exception)");
                        return;
                    }

                    if (message is null)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                        return;
                    }

                    if (string.IsNullOrEmpty(message.Command))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (Command is empty)");
                        return;
                    }

                    if (message.Command == "SetLaunchData")
                    {
                        string? data;

                        try
                        {
                            data = message.Data.Deserialize<string>();
                        }
                        catch (Exception)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization threw an exception)");
                            return;
                        }

                        if (data is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (data.Length > 200)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Data cannot be longer than 200 characters");
                            return;
                        }

                        Data.RPCLaunchData = data;
                    }

                    OnRPCMessage?.Invoke(this, message);

                    LastRPCRequest = DateTime.Now;
                }
            }
        }

        public void Dispose()
        {
            IsDisposed = true;
            _stop.Cancel();
            GC.SuppressFinalize(this);
        }
    }
}
