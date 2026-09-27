using Windows.Win32;
using Windows.Win32.Foundation;

using Onyxstrap.AppData;
using Onyxstrap.Integrations;
using Onyxstrap.Models;

namespace Onyxstrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;
        
        private readonly NotifyIconWrapper? _notifyIcon;

        public readonly ActivityWatcher? ActivityWatcher;

        public readonly DiscordRichPresence? RichPresence;
        private readonly SpotifyOverlayController? _spotifyOverlay;
        private readonly CancellationTokenSource _stop = new();
        private Task? _activityTask;
        private Task? _rebrandTask;

        public Watcher()
        {
            const string LOG_IDENT = "Watcher";

            if (!_lock.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher instance already exists");
                return;
            }

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (String.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new ApplicationException("Roblox player is not been installed");

                using var gameClientProcess = Process.Start(path);

                _watcherData = new() { ProcessId = gameClientProcess.Id };
#else
                throw new Exception("Watcher data not specified");
#endif
            }
            else
            {
                _watcherData = JsonSerializer.Deserialize<WatcherData>(Encoding.UTF8.GetString(Convert.FromBase64String(watcherDataArg)));
            }

            if (_watcherData is null)
                throw new Exception("Watcher data is invalid");

            if (App.Settings.Prop.EnableActivityTracking)
            {
                ActivityWatcher = new(_watcherData.LogFile);

                if (App.Settings.Prop.UseDisableAppPatch)
                {
                    ActivityWatcher.OnAppClose += delegate
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                        CloseProcess(_watcherData.ProcessId);
                    };
                }

                if (App.Settings.Prop.UseDiscordRichPresence)
                    RichPresence = new(ActivityWatcher);
            }

            _notifyIcon = new(this);
            if (App.Settings.Prop.EnableSpotifyOverlay)
            {
                try { _spotifyOverlay = new SpotifyOverlayController(_watcherData.ProcessId); }
                catch (Exception ex) { App.Logger.WriteException("Watcher::SpotifyOverlay", ex); }
            }
        }

        public void KillRobloxProcess() => CloseProcess(_watcherData!.ProcessId, true);

        public void CloseProcess(int pid, bool force = false)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.WriteLine(LOG_IDENT, $"Killing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                if (force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public async Task Run()
        {
            if (!_lock.IsAcquired || _watcherData is null)
                return;

            if (App.Settings.Prop.RebrandGameWindow)
                _rebrandTask = Task.Run(RebrandGameWindowIcons);

            _activityTask = ActivityWatcher?.StartAsync();

            bool spotifyHotkeyWasDown = false;
            var spotify = new SpotifyTrackReader();
            Task<string?>? trackRead = null;
            DateTime nextTrackRead = DateTime.MinValue;

            while (!_stop.IsCancellationRequested && IsProcessRunning(_watcherData.ProcessId))
            {
                if (RichPresence is not null)
                {
                    bool down = PInvoke.GetAsyncKeyState(0x11) < 0
                                && PInvoke.GetAsyncKeyState(0x12) < 0
                                && PInvoke.GetAsyncKeyState(0x53) < 0;

                    if (down && !spotifyHotkeyWasDown)
                    {
                        RichPresence.SpotifyEnabled = !RichPresence.SpotifyEnabled;
                        App.Logger.WriteLine("Watcher::Run", $"Spotify presence toggled {(RichPresence.SpotifyEnabled ? "on" : "off")} (bind)");
                    }
                    spotifyHotkeyWasDown = down;

                    if (trackRead?.IsCompleted == true)
                    {
                        RichPresence.SetSpotifyTrack(RichPresence.SpotifyEnabled ? await trackRead : null);
                        trackRead = null;
                        nextTrackRead = DateTime.UtcNow.AddSeconds(2);
                    }
                    if (RichPresence.SpotifyEnabled && trackRead is null && DateTime.UtcNow >= nextTrackRead)
                        trackRead = spotify.GetTrackAsync();
                    else if (!RichPresence.SpotifyEnabled)
                        RichPresence.SetSpotifyTrack(null);
                }

                // Sample the shortcut independently of slow media/API requests.
                await Task.Delay(50);
            }

            ActivityWatcher?.Dispose();
            if (_activityTask is not null)
                await _activityTask;

            if (_watcherData.AutoclosePids is not null)
            {
                foreach (int pid in _watcherData.AutoclosePids)
                    CloseProcess(pid);
            }

            if (App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Process, "-settings -testmode");
        }

        private static bool IsProcessRunning(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch (ArgumentException) { return false; }
        }

        // kept referenced so the handle stays valid while game windows use it
        private System.Drawing.Icon? _rebrandIcon;

        /// <summary>
        /// Swaps the icon of any Roblox player window that appears during the
        /// session to the Onyxstrap one via WM_SETICON. Purely cosmetic window
        /// messaging - no client files are modified and nothing is injected.
        /// Runs for the whole session so multi-instance launches get covered too.
        /// </summary>
        private void RebrandGameWindowIcons()
        {
            const string LOG_IDENT = "Watcher::RebrandGameWindowIcons";

            try
            {
                _rebrandIcon = App.Settings.Prop.BootstrapperIcon.GetIcon();
                nint hIcon = _rebrandIcon.Handle;

                var rebranded = new HashSet<int>();

                while (!_stop.IsCancellationRequested)
                {
                    var gameProcesses = Utilities.GetProcessesSafe()
                        .Where(x => x.ProcessName == "RobloxPlayerBeta")
                        .ToList();

                    foreach (var process in gameProcesses)
                    {
                        if (rebranded.Contains(process.Id))
                            continue;

                        try
                        {
                            if (process.MainWindowHandle == IntPtr.Zero)
                                continue;

                            PInvoke.SendMessage((HWND)process.MainWindowHandle, PInvoke.WM_SETICON, (WPARAM)(nuint)0, (LPARAM)(nint)hIcon); // ICON_SMALL
                            PInvoke.SendMessage((HWND)process.MainWindowHandle, PInvoke.WM_SETICON, (WPARAM)(nuint)1, (LPARAM)(nint)hIcon); // ICON_BIG

                            rebranded.Add(process.Id);
                            App.Logger.WriteLine(LOG_IDENT, $"Rebranded the game window icon (pid={process.Id})");
                        }
                        catch (Exception)
                        {
                            // the process may have exited between listing and reading
                        }
                    }

                    if (gameProcesses.Count == 0 && !IsProcessRunning(_watcherData!.ProcessId))
                        return;

                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to rebrand the game window icon: {ex.Message}");
            }
        }

        public void Dispose()
        {
            App.Logger.WriteLine("Watcher::Dispose", "Disposing Watcher");

            _stop.Cancel();
            _spotifyOverlay?.Dispose();
            ActivityWatcher?.Dispose();
            _notifyIcon?.Dispose();
            RichPresence?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
