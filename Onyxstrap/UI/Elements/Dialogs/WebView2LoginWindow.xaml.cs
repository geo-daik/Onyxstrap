using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace Onyxstrap.UI.Elements.Dialogs
{
    public partial class WebView2LoginWindow
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;
        public string? SecurityToken { get; private set; }
        public long UserId { get; private set; }
        public string Username { get; private set; } = "";
        private Microsoft.Web.WebView2.Wpf.WebView2? _webView;
        private DispatcherTimer? _timer;
        private readonly CancellationTokenSource _cancel = new();
        private readonly string _profile = Path.Combine(Paths.Temp, "AccountLogin", Guid.NewGuid().ToString("N"));
        private bool _closed, _checking, _loaded;
        public WebView2LoginWindow()
        {
            InitializeComponent();
            Closed += OnClosed;
        }
        private async void WebView2LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _profile);
                if (_closed) { await DeleteProfile(); return; }
                _webView = new Microsoft.Web.WebView2.Wpf.WebView2();
                WebViewHost.Children.Clear();
                WebViewHost.Children.Add(_webView);
                await _webView.EnsureCoreWebView2Async(environment);
                if (_closed) { _webView.Dispose(); await DeleteProfile(); return; }
                var core = _webView.CoreWebView2;
                core.Settings.IsPasswordAutosaveEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.NavigationStarting += (_, args) => args.Cancel = !RobloxAuth.IsAllowedLoginUri(args.Uri);
                core.NewWindowRequested += (_, args) => args.Handled = true;
                core.DownloadStarting += (_, args) => args.Cancel = true;
                core.NavigationCompleted += OnNavigationCompleted;
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += OnTick;
                core.Navigate("https://www.roblox.com/login");
                _timer.Start();
            }
            catch (Exception)
            {
                if (_closed) return;
                Frontend.ShowMessageBox("Unable to open the Roblox login browser. Check that Microsoft Edge WebView2 Runtime is installed, then try again.", MessageBoxImage.Warning);
                Close();
            }
        }
        private async void OnTick(object? sender, EventArgs e) => await CheckForSession();
        private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) => await CheckForSession();
        private async Task CheckForSession()
        {
            if (_closed || _checking || SecurityToken is not null || _webView?.CoreWebView2 is null) return;
            _checking = true;
            try
            {
                var cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
                if (_closed) return;
                var cookie = cookies.FirstOrDefault(c => c.Name == ".ROBLOSECURITY" && c.Domain.TrimStart('.').Equals("roblox.com", StringComparison.OrdinalIgnoreCase));
                if (cookie is null || string.IsNullOrWhiteSpace(cookie.Value)) return;
                var identity = await RobloxAuth.ValidateToken(cookie.Value, cancellationToken: _cancel.Token);
                if (_closed || identity is null || string.IsNullOrWhiteSpace(identity.Value.Username)) return;
                SecurityToken = cookie.Value;
                UserId = identity.Value.UserId;
                Username = identity.Value.Username;
                Result = MessageBoxResult.OK;
                Close();
            }
            catch (Exception) { /* A navigation can invalidate an in-flight cookie request. */ }
            finally { _checking = false; }
        }
        private async void OnClosed(object? sender, EventArgs e)
        {
            _closed = true;
            _cancel.Cancel();
            if (_timer is not null) { _timer.Stop(); _timer.Tick -= OnTick; }
            try
            {
                if (_webView?.CoreWebView2 is not null) {
                    _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                    _webView.CoreWebView2.CookieManager.DeleteAllCookies();
                }
            }
            catch (Exception) { }
            finally { _webView?.Dispose(); WebViewHost.Children.Clear(); }
            await DeleteProfile();
        }
        private async Task DeleteProfile()
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                try { if (Directory.Exists(_profile)) Directory.Delete(_profile, true); return; }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                await Task.Delay(500);
            }
            App.Logger.WriteLine("AccountLogin", "Temporary browser profile cleanup was blocked by Windows.");
        }
    }
}
