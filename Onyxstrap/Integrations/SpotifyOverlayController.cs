using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Onyxstrap.UI.Elements.Overlay;

namespace Onyxstrap.Integrations
{
    internal sealed class SpotifyOverlayController : IDisposable
    {
        private readonly int _processId;
        private readonly string _settingsPath;
        private readonly SpotifyTrackReader _spotify = new();
        private readonly OverlayToggle _toggle = new();
        private readonly SpotifyOverlayWindow _window = new();
        private readonly DispatcherTimer _timer;
        private readonly string _positionPath = Path.Combine(Paths.Base, "SpotifyOverlay.json");
        private OverlayPosition _position = new();
        private nint _gameWindow;
        private Rect _gameBounds = Rect.Empty;
        private DateTime _nextRead;
        private DateTime _nextBoundsRead;
        private OverlayShortcut _shortcut;
        private DateTime _nextShortcutRead;
        private DateTime _settingsWriteTime;
        private bool _waitForShortcutRelease;
        private bool _reading;
        private Task _readTask = Task.CompletedTask;
        private bool _sending;
        private bool _disposed;

        public SpotifyOverlayController(int processId, string? settingsPath = null)
        {
            _processId = processId;
            _settingsPath = settingsPath ?? App.Settings.FileLocation;
            _shortcut = OverlayShortcut.FromSettings(App.Settings.Prop.SpotifyOverlayKey, App.Settings.Prop.SpotifyOverlayModifiers);
            _window.UpdateShortcut(_shortcut.Label);
            try
            {
                if (File.Exists(_positionPath))
                    _position = JsonSerializer.Deserialize<OverlayPosition>(File.ReadAllText(_positionPath)) ?? new();
            }
            catch (Exception ex) { App.Logger.WriteException("SpotifyOverlay::LoadPosition", ex); }
            if (!double.IsFinite(_position.X) || !double.IsFinite(_position.Y)) _position = new();
            new WindowInteropHelper(_window).EnsureHandle();
            _window.HideRequested += CloseOverlay;
            _window.DragCompleted += SavePosition;
            _window.CommandRequested += SendCommand;
            _timer = new DispatcherTimer(DispatcherPriority.Input, _window.Dispatcher) { Interval = TimeSpan.FromMilliseconds(30) };
            _timer.Tick += Tick;
            _timer.Start();
        }

        private void Tick(object? sender, EventArgs e)
        {
            if (_disposed) return;
            nint foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out uint pid);
            nint overlayHandle = new WindowInteropHelper(_window).Handle;
            bool overlayFocused = foreground == overlayHandle;
            if (pid == _processId && !overlayFocused) _gameWindow = foreground;
            bool gameFocused = (pid == _processId || overlayFocused) && _gameWindow != 0 && !IsIconic(_gameWindow);
            ReadSavedShortcut();
            bool down = _shortcut.IsDown(key => GetAsyncKeyState(key) < 0);
            if (_waitForShortcutRelease)
            {
                _waitForShortcutRelease = down;
                down = false;
            }
            bool show = _toggle.Update(down, gameFocused);
            if (!show)
            {
                if (_window.IsVisible)
                {
                    _window.Hide();
                    if (overlayFocused && !_toggle.IsOpen && !IsIconic(_gameWindow)) SetForegroundWindow(_gameWindow);
                }
                return;
            }

            if (DateTime.UtcNow >= _nextBoundsRead || !_window.IsVisible)
            {
                _nextBoundsRead = DateTime.UtcNow.AddMilliseconds(250);
                if (GetWindowRect(_gameWindow, out var rect))
                {
                    var transform = HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle)?.CompositionTarget?.TransformFromDevice
                        ?? System.Windows.Media.Matrix.Identity;
                    var topLeft = transform.Transform(new Point(rect.Left, rect.Top));
                    var bottomRight = transform.Transform(new Point(rect.Right, rect.Bottom));
                    _gameBounds = new Rect(topLeft, bottomRight);
                }
                if (!_window.IsDragging && !_gameBounds.IsEmpty)
                {
                    _window.Left = _gameBounds.Left + _position.X;
                    _window.Top = _gameBounds.Top + _position.Y;
                    _window.FitWithin(_gameBounds);
                }
            }
            if (!_window.IsVisible)
            {
                _window.Show();
                _window.Activate();
                _nextRead = DateTime.MinValue;
            }
            if (!_reading && !_sending && DateTime.UtcNow >= _nextRead) _readTask = RefreshAsync();
        }

        private void ReadSavedShortcut()
        {
            if (DateTime.UtcNow < _nextShortcutRead) return;
            _nextShortcutRead = DateTime.UtcNow.AddSeconds(1);
            try
            {
                string path = _settingsPath;
                var modified = File.GetLastWriteTimeUtc(path);
                if (modified == _settingsWriteTime || !File.Exists(path)) return;
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                var root = json.RootElement;
                int key = root.TryGetProperty("SpotifyOverlayKey", out var k) ? k.GetInt32() : 0xA1;
                int modifiers = root.TryGetProperty("SpotifyOverlayModifiers", out var m) ? m.GetInt32() : 0;
                var shortcut = OverlayShortcut.FromSettings(key, modifiers);
                if (shortcut != _shortcut)
                {
                    _shortcut = shortcut;
                    _window.UpdateShortcut(shortcut.Label);
                    _waitForShortcutRelease = true;
                }
                _settingsWriteTime = modified;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
            {
                // A save may be in progress. Keep the working binding and retry next second.
            }
        }

        private async Task RefreshAsync()
        {
            _reading = true;
            try
            {
                var playback = await _spotify.GetPlaybackAsync();
                if (!_disposed) _window.UpdatePlayback(playback);
            }
            catch (Exception ex) { App.Logger.WriteException("SpotifyOverlay::Refresh", ex); }
            finally { _reading = false; _nextRead = DateTime.UtcNow.AddSeconds(1); }
        }

        private async void SendCommand(SpotifyCommand command)
        {
            if (_sending || _disposed) return;
            _sending = true;
            _window.SetBusy(true);
            try
            {
                await _readTask;
                if (_disposed) return;
                bool success = await _spotify.SendCommandAsync(command);
                if (_disposed) return;
                if (success) await RefreshAsync();
                else
                {
                    _window.ShowCommandError();
                    _nextRead = DateTime.UtcNow.AddSeconds(3);
                }
            }
            catch (Exception ex) { App.Logger.WriteException("SpotifyOverlay::Command", ex); }
            finally
            {
                _sending = false;
                if (!_disposed) _window.SetBusy(false);
            }
        }

        private void CloseOverlay()
        {
            _toggle.Close();
            _window.Hide();
            if (_gameWindow != 0 && !IsIconic(_gameWindow)) SetForegroundWindow(_gameWindow);
        }

        private void SavePosition()
        {
            if (_gameBounds.IsEmpty) return;
            _window.FitWithin(_gameBounds);
            _position.X = _window.Left - _gameBounds.Left;
            _position.Y = _window.Top - _gameBounds.Top;
            try
            {
                string tempPath = _positionPath + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(_position));
                File.Move(tempPath, _positionPath, true);
            }
            catch (Exception ex) { App.Logger.WriteException("SpotifyOverlay::SavePosition", ex); }
        }

        public void Dispose()
        {
            if (!_window.Dispatcher.CheckAccess())
            {
                _window.Dispatcher.Invoke(Dispose);
                return;
            }
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Tick -= Tick;
            _window.HideRequested -= CloseOverlay;
            _window.DragCompleted -= SavePosition;
            _window.CommandRequested -= SendCommand;
            _window.Close();
        }

        private sealed class OverlayPosition
        {
            public OverlayPosition() { }
            public double X { get; set; } = 28;
            public double Y { get; set; } = 90;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint hwnd);
        [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint hwnd);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);
    }
}
