using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Onyxstrap;
using Onyxstrap.Integrations;
using Onyxstrap.Extensions;
using Onyxstrap.Enums;
using Onyxstrap.UI.Elements.Overlay;
using Onyxstrap.UI.ViewModels.Settings;

internal static class Program
{
    private static int _passed;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name); _passed++;
    }
    [STAThread]
    public static int Main(string[] args)
    {
        typeof(Application).GetField("_resourceAssembly", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, typeof(App).Assembly);
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ThemeType.Dark });
        application.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Onyxstrap;component/UI/Style/Dark.xaml") });
        application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Onyxstrap;component/UI/Style/Default.xaml") });
        application.Resources["Rubik"] = new FontFamily("Segoe UI");
        application.Resources["StringFormatConverter"] = new Onyxstrap.UI.Converters.StringFormatConverter();
        application.Resources["EnumNameConverter"] = new Onyxstrap.UI.Converters.EnumNameConverter();
        application.Resources["RangeConverter"] = new Onyxstrap.UI.Converters.RangeConverter();
        typeof(App).GetProperty(nameof(App.LaunchSettings))!.SetValue(null, new LaunchSettings(Array.Empty<string>()));
        if (args.Length > 1 && args[0] == "--essentials")
        {
            App.Settings.Prop.FavoriteGames.Add(new Onyxstrap.Models.FavoriteGame { Name = "My favorite game", PlaceId = 123456 });
            var page = new Onyxstrap.UI.Elements.Settings.Pages.EssentialsPage();
            var menu = new Onyxstrap.UI.Elements.Settings.MainWindow(false);
            App.Settings.Prop.Theme = Onyxstrap.Enums.Theme.Dark;
            menu.ApplyTheme();
            ((Frame)menu.FindName("RootFrame")).Content = page;
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Pump();
            var snapToggle = (Wpf.Ui.Controls.ToggleSwitch)page.FindName("SnapToggle");
            Check(snapToggle.GetBindingExpression(Wpf.Ui.Controls.ToggleSwitch.IsCheckedProperty) is not null, "refresh preserves snapping binding");
            snapToggle.SetCurrentValue(Wpf.Ui.Controls.ToggleSwitch.IsCheckedProperty, false);
            Check(!App.Settings.Prop.SpotifySnapToEdges, "snapping toggle updates preferences");
            Render((FrameworkElement)menu.Content, 1080, 820, args[1], menu.Background);
            App.Settings.Prop.Theme = Onyxstrap.Enums.Theme.Light;
            menu.ApplyTheme(); Pump();
            Render((FrameworkElement)menu.Content, 1080, 820, args[1].Replace(".png", "-light.png"), menu.Background);
            ScrollViewer? FindScroll(DependencyObject root)
            {
                if (root is ScrollViewer scroll) return scroll;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
                    var found = FindScroll(VisualTreeHelper.GetChild(root, i));
                    if (found is not null) return found;
                }
                return null;
            }
            var pageScroll = FindScroll(page);
            Check(pageScroll is not null, "essentials page supports scrolling to backup controls");
            pageScroll!.ScrollToEnd(); Pump();
            Render((FrameworkElement)menu.Content, 1080, 820, args[1].Replace(".png", "-backup.png"), menu.Background);
            Check(true, "essentials page renders in dark and light themes");
            return 0;
        }
        if (args.Length > 1 && args[0] == "--accounts")
        {
            string fixture = Path.Combine(AppContext.BaseDirectory, "account-preview-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixture);
            try {
                var store = new AccountManager(Path.Combine(fixture, "Accounts.json"));
                store.SaveSession(12345, "Example account", "synthetic-preview-session");
                var page = new Onyxstrap.UI.Elements.Settings.Pages.AccountsPage(store);
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                var menu = new Onyxstrap.UI.Elements.Settings.MainWindow(false);
                App.Settings.Prop.Theme = Onyxstrap.Enums.Theme.Dark;
                menu.ApplyTheme();
                var frame = (Frame)menu.FindName("RootFrame");
                frame.Content = page;
                Pump();
                Render((FrameworkElement)menu.Content, 1080, 720, args[1], menu.Background);
                App.Settings.Prop.Theme = Onyxstrap.Enums.Theme.Light;
                menu.ApplyTheme(); Pump();
                Render((FrameworkElement)menu.Content, 1080, 720, args[1].Replace(".png", "-light.png"), menu.Background);
                Check(true, "accounts page renders with a synthetic saved account");
            }
            finally { Directory.Delete(fixture, true); }
            return 0;
        }
        if (args.Length > 0 && args[0] == "--live-read")
        {
            var live = Task.Run(() => new SpotifyTrackReader().GetPlaybackAsync()).GetAwaiter().GetResult();
            Console.WriteLine($"Live Spotify: track={live.HasTrack}, playing={live.IsPlaying}, album={!string.IsNullOrEmpty(live.Album)}, artworkBytes={live.Artwork?.Length ?? 0}, position={live.Position.TotalSeconds:F0}, duration={live.Duration.TotalSeconds:F0}");
            var liveWindow = new SpotifyOverlayWindow();
            liveWindow.UpdatePlayback(live);
            if (args.Length > 1) Render((FrameworkElement)liveWindow.Content, 380, 270, args[1]);
            liveWindow.Close();
            return 0;
        }
        Check(OverlayShortcut.Default.Label == "Right Shift", "default shortcut remains Right Shift");
        var heldKeys = new HashSet<int> { 0xA1 };
        Check(OverlayShortcut.Default.IsDown(heldKeys.Contains), "right Shift triggers default");
        heldKeys = new() { 0xA0 };
        Check(!OverlayShortcut.Default.IsDown(heldKeys.Contains), "left Shift does not trigger right Shift binding");
        Check(OverlayShortcut.TryCreate(0x4D, 6, out var custom) && custom.Label == "Ctrl + Shift + M", "custom chord has readable label");
        heldKeys = new() { 0x4D, 0xA2, 0xA1 };
        Check(custom.IsDown(heldKeys.Contains), "custom chord accepts either side of modifier keys");
        heldKeys.Remove(0xA2);
        Check(!custom.IsDown(heldKeys.Contains), "missing required modifier cannot trigger shortcut");
        heldKeys.Add(0xA3); heldKeys.Add(0xA4);
        Check(!custom.IsDown(heldKeys.Contains), "extra modifier cannot trigger shortcut");
        heldKeys.Remove(0xA4); heldKeys.Add(0x5B);
        Check(!custom.IsDown(heldKeys.Contains), "Windows shortcuts do not trigger overlay");
        Check(OverlayShortcut.TryCreate(0x78, 0, out var functionKey) && functionKey.Label == "F9", "function key binding supported");
        Check(!OverlayShortcut.TryCreate(0x53, 3, out _) && !OverlayShortcut.TryCreate(0x1B, 0, out _), "existing Discord shortcut and Escape are reserved");
        Check(OverlayShortcut.FromSettings(0, -1) == OverlayShortcut.Default, "invalid saved binding falls back to Right Shift");
        Check(OverlayShortcut.TryCreate(0xA1, 4, out var shiftOnly) && shiftOnly == OverlayShortcut.Default, "standalone modifier normalizes its own modifier flag");
        var persisted = System.Text.Json.JsonSerializer.Deserialize<Onyxstrap.Models.Persistable.Settings>(
            System.Text.Json.JsonSerializer.Serialize(new Onyxstrap.Models.Persistable.Settings { SpotifyOverlayKey = custom.VirtualKey, SpotifyOverlayModifiers = custom.Modifiers }))!;
        Check(OverlayShortcut.FromSettings(persisted.SpotifyOverlayKey, persisted.SpotifyOverlayModifiers) == custom, "custom shortcut survives settings save and reload");
        var legacy = System.Text.Json.JsonSerializer.Deserialize<Onyxstrap.Models.Persistable.Settings>("{}")!;
        Check(OverlayShortcut.FromSettings(legacy.SpotifyOverlayKey, legacy.SpotifyOverlayModifiers) == OverlayShortcut.Default, "existing settings keep default shortcut without migration");
        string shortcutFile = Path.Combine(Path.GetTempPath(), "onyxstrap-shortcut-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(shortcutFile, "{\"SpotifyOverlayKey\":77,\"SpotifyOverlayModifiers\":6}");
            using var controller = new SpotifyOverlayController(Environment.ProcessId, shortcutFile);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(SpotifyOverlayController);
            void ReloadShortcut()
            {
                type.GetField("_nextShortcutRead", flags)!.SetValue(controller, DateTime.MinValue);
                type.GetField("_settingsWriteTime", flags)!.SetValue(controller, DateTime.MinValue);
                type.GetMethod("ReadSavedShortcut", flags)!.Invoke(controller, null);
            }
            OverlayShortcut Current() => (OverlayShortcut)type.GetField("_shortcut", flags)!.GetValue(controller)!;
            ReloadShortcut();
            Check(Current() == custom, "running controller loads saved custom shortcut");
            var controlledWindow = (SpotifyOverlayWindow)type.GetField("_window", flags)!.GetValue(controller)!;
            Check(((TextBlock)controlledWindow.FindName("ShortcutLabel")).Text == custom.Label, "live settings refresh updates player badge");
            File.WriteAllText(shortcutFile, "{"); ReloadShortcut();
            Check(Current() == custom, "partial settings save preserves working shortcut");
            File.WriteAllText(shortcutFile, "{\"SpotifyOverlayKey\":120,\"SpotifyOverlayModifiers\":0}"); ReloadShortcut();
            Check(Current() == functionKey, "running controller switches binding after save without restart");
            Check((bool)type.GetField("_waitForShortcutRelease", flags)!.GetValue(controller)!, "new binding waits for release before toggling");
            File.WriteAllText(shortcutFile, "{}"); ReloadShortcut();
            Check(Current() == OverlayShortcut.Default, "removing custom setting restores default during play");
            File.WriteAllText(shortcutFile, "{\"SpotifyAccentColor\":\"#FF8800\",\"SpotifyCompactMode\":true}"); ReloadShortcut();
            Check(controlledWindow.Width == 340 && ((SolidColorBrush)controlledWindow.Resources["PlayerAccent"]).Color == Color.FromRgb(255, 136, 0), "saved color and compact mode update running player");
        }
        finally { File.Delete(shortcutFile); }
        Check(ThemeColors.TryParse(" #ff8800 ", out var orange) && ThemeColors.Hex(orange) == "#FF8800", "custom hex colors normalize correctly");
        Check(!ThemeColors.TryParse("#00FF0000", out _) && !ThemeColors.TryParse("##ABCDEF", out _) && !ThemeColors.TryParse("oops", out _), "invalid and transparent custom colors rejected");
        foreach (var c in new[] { Colors.Black, Colors.White, Colors.Red, Colors.Blue, Colors.Yellow })
            Check(ThemeColors.Contrast(c, ThemeColors.TextOn(c)) >= 4.5, "button text contrast for " + c);
        int refreshCount = 0;
        var colorVm = new ColorThemeViewModel(() => refreshCount++);
        Check(colorVm.Swatches.Count() == 20, "twenty preset colors available");
        colorVm.AccentHex = "#12ABCD";
        Check(App.Settings.Prop.AccentTheme == AccentTheme.Custom && App.Settings.Prop.CustomAccentColor == "#12ABCD" && refreshCount > 0, "custom color updates settings and preview");
        colorVm.AccentHex = "invalid";
        Check(App.Settings.Prop.CustomAccentColor == "#12ABCD", "invalid draft keeps last valid color");
        colorVm.PlayerMode = SpotifyColorMode.AlbumArt; colorVm.Compact = true;
        colorVm.ProfileName = "Night"; colorVm.SaveProfile();
        Check(colorVm.Profiles.Count == 1, "profile captures current theme");
        colorVm.Accent = AccentTheme.Coral; colorVm.Compact = false; colorVm.ApplyProfile();
        Check(colorVm.Accent == AccentTheme.Custom && colorVm.Compact && colorVm.PlayerMode == SpotifyColorMode.AlbumArt, "profile restores accent player mode and compact layout");
        colorVm.ProfileName = "night"; colorVm.SaveProfile();
        Check(colorVm.Profiles.Count == 1, "same profile name updates existing profile");
        var profileRoundTrip = System.Text.Json.JsonSerializer.Deserialize<Onyxstrap.Models.Persistable.Settings>(System.Text.Json.JsonSerializer.Serialize(App.Settings.Prop))!;
        Check(profileRoundTrip.ColorProfiles.Count == 1 && profileRoundTrip.ColorProfiles[0].Compact && profileRoundTrip.ColorProfiles[0].CustomAccent == "#12ABCD", "theme profile survives save and reload");
        colorVm.DeleteProfile(); Check(colorVm.Profiles.Count == 0, "profile removal works");
        colorVm.ResetColorsCommand.Execute(null);
        Check(colorVm.Accent == AccentTheme.OnyxViolet && !colorVm.Compact && colorVm.PlayerHex == "#1ED760", "reset restores original palette and layout");
        var toggle = new OverlayToggle();
        Check(!toggle.Update(false, true), "overlay starts hidden");
        Check(toggle.Update(true, true), "Right Shift opens overlay");
        Check(toggle.Update(true, true), "holding key does not repeatedly toggle");
        toggle.Update(false, true);
        Check(!toggle.Update(true, true), "second press closes overlay");
        toggle.Update(false, false); toggle.Update(true, false);
        Check(!toggle.Update(true, true), "held key from another app cannot open overlay");
        toggle.Update(false, true); toggle.Update(true, true);
        Check(!toggle.Update(false, false) && toggle.IsOpen, "Alt Tab hides without losing open state");
        Check(toggle.Update(false, true), "returning to Roblox restores overlay");
        toggle.Close(); Check(!toggle.Update(false, true), "close button resets open state");
        var paused = new SpotifyPlayback("Night Drive", "Onyx Sessions", false, true, true, true);
        Check(paused.HasTrack && paused.PlayingTrack is null, "paused song retained for overlay but absent from Discord");
        Check((paused with { IsPlaying = true }).PlayingTrack == "Onyx Sessions — Night Drive", "playing state formats track for Discord");
        var vm = new IntegrationsViewModel(); vm.SpotifyOverlayEnabled = true; vm.ActivityTrackingEnabled = false;
        vm.SetSpotifyShortcut(custom);
        Check(vm.SpotifyShortcutLabel == "Ctrl + Shift + M" && App.Settings.Prop.SpotifyOverlayKey == 0x4D, "menu edits saved shortcut fields");
        vm.SetSpotifyShortcut(OverlayShortcut.Default);
        Check(vm.SpotifyOverlayEnabled, "overlay independent of activity tracking and Discord");
        var window = new SpotifyOverlayWindow();
        Check(window.ShowActivated && !window.ShowInTaskbar, "player accepts input without adding a taskbar entry");
        window.UpdateShortcut(custom.Label);
        Check(((TextBlock)window.FindName("ShortcutLabel")).Text == custom.Label && ((Button)window.FindName("CloseButton")).ToolTip.ToString()!.Contains(custom.Label), "player badge and close tooltip follow selected shortcut");
        window.UpdateShortcut(OverlayShortcut.Default.Label);
        window.UpdatePlayback(paused);
        var play = (Button)window.FindName("PlayPauseButton");
        Check((string)play.Content == "\uE768" && play.IsEnabled, "paused track exposes Play control");
        window.UpdatePlayback(paused with { IsPlaying = true, CanPrevious = false });
        Check((string)play.Content == "\uE769" && !((Button)window.FindName("PreviousButton")).IsEnabled, "playing state shows Pause and respects Spotify controls");
        SpotifyCommand? command = null; window.CommandRequested += c => command = c;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(command == SpotifyCommand.TogglePlayPause, "playback button routes to Spotify command");
        window.SetBusy(true); Check(!play.IsEnabled, "pending command prevents duplicate clicks");
        window.SetBusy(false); Check(play.IsEnabled, "controls restored after command");
        window.UpdatePlayback(SpotifyPlayback.Empty);
        Check(!play.IsEnabled && ((TextBlock)window.FindName("TrackArtist")).Text.Contains("Open Spotify"), "missing Spotify shows useful empty state");
        window.Left = 2000; window.Top = 2000; window.FitWithin(new Rect(50, 50, 1000, 600));
        Check(window.Left == 1050 - window.Width && window.Top == 650 - window.Height, "dragged player clamped inside game bounds");
        var timeline = SpotifyTrackReader.NormalizeTimeline(TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(210), TimeSpan.FromSeconds(2));
        Check(timeline.Position.TotalSeconds == 32 && timeline.Duration.TotalSeconds == 200, "timeline respects start offset and elapsed playback");
        Check(SpotifyTrackReader.NormalizeTimeline(TimeSpan.FromSeconds(300), TimeSpan.Zero, TimeSpan.FromSeconds(200), TimeSpan.FromSeconds(5)).Position.TotalSeconds == 200, "timeline clamps end of song");
        Check(SpotifyTrackReader.NormalizeTimeline(TimeSpan.FromSeconds(-2), TimeSpan.Zero, TimeSpan.FromSeconds(200), TimeSpan.FromSeconds(-1)).Position == TimeSpan.Zero, "timeline clamps negative position and clock skew");
        Check(SpotifyTrackReader.NormalizeTimeline(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero).Duration == TimeSpan.Zero, "invalid timeline has no duration");
        var song = paused with { IsPlaying = true, Album = "After Hours • Test artwork", Artwork = CreateArtwork(), Position = TimeSpan.FromSeconds(82), Duration = TimeSpan.FromSeconds(218) };
        window.UpdatePlayback(song);
        var artwork = (Image)window.FindName("AlbumArtwork");
        Check(artwork.Source is BitmapImage && ((BitmapImage)artwork.Source).IsFrozen, "cover artwork decoded and detached from stream");
        var source = artwork.Source;
        window.UpdatePlayback(song with { Position = TimeSpan.FromSeconds(83) });
        Check(ReferenceEquals(source, artwork.Source), "unchanged artwork is not decoded every refresh");
        Check(((TextBlock)window.FindName("ElapsedTime")).Text == "1:23" && ((TextBlock)window.FindName("DurationTime")).Text == "3:38", "playback times are readable");
        Check(Math.Abs(((ProgressBar)window.FindName("TrackProgress")).Value - 83d / 218) < .001, "progress follows reported timeline");
        Check(((TextBlock)window.FindName("TrackAlbum")).Text == song.Album, "album name shown");
        window.UpdatePlayback(song with { Artwork = new byte[] { 0, 1, 2 }, Position = TimeSpan.FromSeconds(999) });
        Check(artwork.Source is null && ((UIElement)window.FindName("ArtworkPlaceholder")).Visibility == Visibility.Visible, "corrupt artwork uses placeholder");
        Check(((ProgressBar)window.FindName("TrackProgress")).Value == 1, "display clamps progress beyond duration");
        window.UpdatePlayback(song with { Artwork = null, Album = "", Duration = TimeSpan.Zero });
        Check(artwork.Source is null && ((TextBlock)window.FindName("TrackAlbum")).Text == "", "track with no artwork clears old cover and album");
        Check(((TextBlock)window.FindName("DurationTime")).Text == "--:--" && ((ProgressBar)window.FindName("TrackProgress")).Value == 0, "unknown timeline has no fabricated progress");
        window.UpdatePlayback(song);
        window.UpdatePlayback(SpotifyPlayback.Empty);
        Check(artwork.Source is null, "disconnect clears previous cover");
        window.UpdatePlayback(song);
        Render((FrameworkElement)window.Content, 380, 270, args.Length > 0 ? args[0] : "overlay-preview.png");
        window.UpdatePlayback(paused with { Title = new string('界', 120), Artist = new string('W', 180) });
        ((FrameworkElement)window.Content).Measure(new Size(380, 270));
        Check(((FrameworkElement)window.Content).DesiredSize.Width <= 380, "long song information stays inside player");
        var look = new Onyxstrap.Models.Persistable.Settings { AccentTheme = AccentTheme.Custom, CustomAccentColor = "#12ABCD", SpotifyColorMode = SpotifyColorMode.MatchApp };
        window.UpdateAppearance(look);
        Check(((SolidColorBrush)window.Resources["PlayerAccent"]).Color == ThemeColors.Parse("#12ABCD", Colors.Black), "player can match custom app accent");
        look.SpotifyCompactMode = true; window.UpdateAppearance(look); window.UpdatePlayback(song);
        Check(window.Height == 194 && window.Width == 340 && ((TextBlock)window.FindName("TrackAlbum")).Visibility == Visibility.Collapsed, "compact player uses smaller layout and keeps song info");
        Render((FrameworkElement)window.Content, 340, 194, (args.Length > 0 ? args[0] : "overlay-preview.png").Replace(".png", "-compact.png"));
        look.SpotifyColorMode = SpotifyColorMode.AlbumArt; window.UpdateAppearance(look);
        var sampled = ((SolidColorBrush)window.Resources["PlayerAccent"]).Color;
        Check(sampled != ThemeColors.Parse("#1ED760", Colors.Black), "album artwork drives player color");
        Check(ThemeColors.Contrast(sampled, ((SolidColorBrush)window.Resources["PlayerBackground"]).Color) >= 3, "album accent remains visible against player");
        Render((FrameworkElement)window.Content, 340, 194, (args.Length > 0 ? args[0] : "overlay-preview.png").Replace(".png", "-album.png"));
        window.UpdatePlayback(SpotifyPlayback.Empty);
        Check(((SolidColorBrush)window.Resources["PlayerAccent"]).Color == ThemeColors.Parse("#1ED760", Colors.Black), "missing artwork restores fallback color");
        look.SpotifyColorMode = SpotifyColorMode.Custom; look.SpotifyAccentColor = "#000000"; window.UpdateAppearance(look);
        Check(ThemeColors.Contrast(((SolidColorBrush)window.Resources["PlayerAccent"]).Color, ((SolidColorBrush)window.Resources["PlayerBackground"]).Color) >= 3, "black custom color adjusted for visible controls");
        look.SpotifyCompactMode = false; window.UpdateAppearance(look);
        Check(window.Width == 380 && window.Height == 270 && ((TextBlock)window.FindName("TrackAlbum")).Visibility == Visibility.Visible, "full player layout can be restored");
        window.Close();
        if (args.Length > 1)
        {
            App.Settings.Prop.EnableActivityTracking = true;
            App.Settings.Prop.UseDiscordRichPresence = true;
            var menu = new Onyxstrap.UI.Elements.Settings.MainWindow(false);
            var frame = menu.GetFrame();
            var integrationPage = new Onyxstrap.UI.Elements.Settings.Pages.IntegrationsPage();
            frame.Content = integrationPage;
            Check(((IntegrationsViewModel)integrationPage.DataContext).SpotifyOverlayEnabled, "menu preserves enabled overlay setting");
            Pump();
            var shortcutButton = (Button)integrationPage.FindName("ShortcutButton");
            Check(shortcutButton.Content.ToString() == "Right Shift", "shortcut picker displays saved binding");
            shortcutButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(shortcutButton.Content.ToString() == "Press shortcut…", "shortcut picker enters recording state");
            typeof(Onyxstrap.UI.Elements.Settings.Pages.IntegrationsPage).GetMethod("AcceptShortcut", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(integrationPage, new object[] { System.Windows.Input.Key.M, System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift });
            Check(shortcutButton.Content.ToString() == "Ctrl + Shift + M", "recorded chord updates picker binding");
            ((IntegrationsViewModel)integrationPage.DataContext).SetSpotifyShortcut(OverlayShortcut.Default);
            Check(shortcutButton.Content.ToString() == "Right Shift", "picker binding remains live after recording");
            Render((FrameworkElement)menu.Content, 1080, 720, args[1], menu.Background);
            App.Settings.Prop.Theme = Onyxstrap.Enums.Theme.Light;
            menu.ApplyTheme();
            Pump();
            Render((FrameworkElement)menu.Content, 1080, 720, args[1].Replace(".png", "-light.png"), menu.Background);
            App.Settings.Prop.Theme = Onyxstrap.Enums.Theme.Dark;
            App.Settings.Prop.AccentTheme = AccentTheme.Teal;
            menu.ApplyTheme(); Pump();
            var panel = new Onyxstrap.UI.Elements.Controls.ColorThemePanel();
            var panelVm = (ColorThemeViewModel)panel.DataContext;
            panelVm.PlayerMode = SpotifyColorMode.AlbumArt;
            panelVm.Compact = true;
            panelVm.ProfileName = "Evening"; panelVm.SaveProfile();
            Render(panel, 650, 660, args[1].Replace(".png", "-colors.png"), menu.Background);
            App.Settings.Prop.Theme = Onyxstrap.Enums.Theme.Light; menu.ApplyTheme(); Pump();
            var lightPanel = new Onyxstrap.UI.Elements.Controls.ColorThemePanel();
            Render(lightPanel, 650, 660, args[1].Replace(".png", "-colors-light.png"), menu.Background);
            // No Show/Close: preview generation must not launch Roblox or persist settings.
        }
        Console.WriteLine($"{_passed} overlay checks passed.");
        return 0;
    }
    private static byte[] CreateArtwork()
    {
        // Synthetic artwork for rendering and decoding tests, not a Spotify cover.
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(61, 34, 108), Color.FromRgb(235, 106, 117), 60), null, new Rect(0, 0, 256, 256));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(252, 190, 133)), null, new Point(170, 94), 44, 44);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(31, 35, 68)), null, new Rect(0, 163, 256, 93));
            for (int i = 0; i < 8; i++)
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(133, 91, 153)), 2), new Point(0, 175 + i * 12), new Point(256, 175 + i * 12));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(249, 192, 157)), 4), new Point(130, 163), new Point(30, 256));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(249, 192, 157)), 4), new Point(150, 163), new Point(230, 256));
        }
        var bitmap = new RenderTargetBitmap(256, 256, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }

    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    internal static void Render(FrameworkElement element, int width, int height, string path, Brush? background = null)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width * 2, height * 2, 192, 192, PixelFormats.Pbgra32);
        if (background is not null)
        {
            var canvas = new DrawingVisual();
            using (var drawing = canvas.RenderOpen()) drawing.DrawRectangle(background, null, new Rect(0, 0, width, height));
            bitmap.Render(canvas);
        }
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path); encoder.Save(output);
    }
}
