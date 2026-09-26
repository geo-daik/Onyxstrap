using System.IO;
using System.Windows;
using System.Windows.Threading;

using Microsoft.Web.WebView2.Core;

namespace Onyxstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// One-time embedded login: opens the real Roblox login page in a WebView2,
    /// waits for the .ROBLOSECURITY session cookie to appear, validates it with
    /// roblox.com, and hands it back to the caller.
    ///
    /// The browser runs in a throwaway profile under Temp; cookies are cleared
    /// and the folder wiped on close so no plaintext token persists on disk.
    /// </summary>
    public partial class WebView2LoginWindow
    {
        public MessageBoxResult Result = MessageBoxResult.Cancel;

        public string? SecurityToken { get; private set; }

        private Microsoft.Web.WebView2.Wpf.WebView2? _webView;

        private DispatcherTimer? _cookieTimer;

        private static string ProfileFolder => Path.Combine(Paths.Temp, "LoginWebView");

        public WebView2LoginWindow()
        {
            InitializeComponent();
        }

        private async void WebView2LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // a fresh profile every time - nothing from a previous login survives
                if (Directory.Exists(ProfileFolder))
                    Directory.Delete(ProfileFolder, true);

                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: ProfileFolder);

                _webView = new Microsoft.Web.WebView2.Wpf.WebView2();
                WebViewHost.Children.Clear();
                WebViewHost.Children.Add(_webView);

                await _webView.EnsureCoreWebView2Async(environment);

                _webView.CoreWebView2.NavigationCompleted += async (_, _) => await CheckForSession();
                _webView.CoreWebView2.NavigationStarting += (_, args) =>
                {
                    // never navigate away from roblox.com (defensive: the login page could be phished via redirects)
                    if (!string.IsNullOrEmpty(args.Uri) && !args.Uri.Contains("roblox.com", StringComparison.OrdinalIgnoreCase))
                        args.Cancel = true;
                };

                _cookieTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                _cookieTimer.Tick += async (_, _) => await CheckForSession();

                _webView.CoreWebView2.Navigate("https://www.roblox.com/login");
                _cookieTimer.Start();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("WebView2LoginWindow", $"WebView2 unavailable: {ex.Message}");

                Frontend.ShowMessageBox(
                    "The embedded browser isn't available (WebView2 runtime missing). Add your account by pasting the .ROBLOSECURITY token instead - see the wiki for instructions.",
                    MessageBoxImage.Warning
                );

                Close();
            }
        }

        private async Task CheckForSession()
        {
            if (SecurityToken is not null || _webView?.CoreWebView2 is null)
                return;

            try
            {
                var cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
                var cookie = cookies.FirstOrDefault(c => c.Name == ".ROBLOSECURITY");

                if (cookie is null || string.IsNullOrEmpty(cookie.Value))
                    return;

                var validation = await RobloxAuth.ValidateToken(cookie.Value);

                if (validation is null)
                    return; // cookie present but the session isn't valid yet

                SecurityToken = cookie.Value;
                Result = MessageBoxResult.OK;

                _cookieTimer?.Stop();
                Close();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("WebView2LoginWindow", $"Session check failed: {ex.Message}");
            }
        }

        private void CleanupProfile()
        {
            try
            {
                _webView?.CoreWebView2.CookieManager.DeleteAllCookies();
            }
            catch
            {
                // best effort
            }

            _cookieTimer?.Stop();

            try
            {
                if (Directory.Exists(ProfileFolder))
                    Directory.Delete(ProfileFolder, true);
            }
            catch (IOException)
            {
                // the folder can be locked right after close; it is wiped on the next open
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
