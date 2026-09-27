using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Onyxstrap;
using Onyxstrap.Integrations;
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
        Check(vm.SpotifyOverlayEnabled, "overlay independent of activity tracking and Discord");
        var window = new SpotifyOverlayWindow();
        Check(window.ShowActivated && !window.ShowInTaskbar, "player accepts input without adding a taskbar entry");
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
            Render((FrameworkElement)menu.Content, 1080, 720, args[1], menu.Background);
            App.Settings.Prop.Theme = Onyxstrap.Enums.Theme.Light;
            menu.ApplyTheme();
            Pump();
            Render((FrameworkElement)menu.Content, 1080, 720, args[1].Replace(".png", "-light.png"), menu.Background);
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
