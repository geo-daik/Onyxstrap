using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Onyxstrap;
using Onyxstrap.Integrations;
using Onyxstrap.Models.OnyxstrapRPC;
using Onyxstrap.UI.ViewModels.Settings;
using Presence = DiscordRPC.RichPresence;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
void Log(ActivityWatcher watcher, string message) => typeof(ActivityWatcher)
    .GetMethod("ReadLogEntry", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(watcher, new object[] { "2026-09-27T00:00:00Z,0,0 " + message });
const string join = "[FLog::Output] ! Joining game '11111111-1111-1111-1111-111111111111' place 123 at 127.0.0.1";
const string joined = "[FLog::Network] Replicator created: serverId: 127.0.0.1|1";
const string leave = "[FLog::Network] Time to disconnect replication data: 0";
Message Rpc(string json) => new() { Command = "SetRichPresence", Data = JsonSerializer.Deserialize<JsonElement>(json) };

App.Settings.Prop.ShowServerDetails = false;
App.Settings.Prop.ShowAccountOnRichPresence = false;
App.Settings.Prop.ShowSpotifyOnRichPresence = false;
Check(PresenceText.Limit("x") == "x ", "one-character text accepted");
string emoji = PresenceText.Limit(string.Concat(Enumerable.Repeat("🎵", 100)))!;
Check(Encoding.UTF8.GetByteCount(emoji) <= 128 && !emoji.Contains('\ufffd'), "long Unicode stays valid and within Discord limit");
Check(PresenceText.State("Game", true, "Artist — Song") == "Listening to Artist — Song", "Spotify overlay");
Check(PresenceText.State("Game", true, null) == "Game", "paused Spotify restores game state");
Check(PresenceText.State(null, false, "Track") is null, "null game state restored");
Check(SpotifyTrackReader.IsSpotify("Spotify.exe") && SpotifyTrackReader.IsSpotify("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"), "desktop and Store Spotify recognized");
Check(!SpotifyTrackReader.IsSpotify("chrome.exe") && !SpotifyTrackReader.IsSpotify("NotSpotify.exe"), "other media apps ignored");

using (var watcher = new ActivityWatcher())
{
    Presence? published = null;
    int updates = 0;
    using var rpc = new DiscordRichPresence(watcher, presence => { published = presence; updates++; });
    Log(watcher, join); Log(watcher, joined);
    Check(published?.Details == "Playing Roblox", "presence works without Roblox API metadata");
    rpc.SpotifyEnabled = true;
    rpc.SetSpotifyTrack("Artist — Song");
    Check(published?.State == "Listening to Artist — Song", "track is published");
    int before = updates; rpc.SetSpotifyTrack("Artist — Song");
    Check(updates == before, "unchanged track sends no duplicate update");
    rpc.ProcessRPCMessage(Rpc("{\"state\":\"New game state\"}"));
    Check(published?.State == "Listening to Artist — Song", "game update preserves Spotify overlay");
    rpc.SpotifyEnabled = false;
    Check(published?.State == "New game state", "disable immediately restores latest game state");
    rpc.SpotifyEnabled = true; rpc.SetSpotifyTrack("Song"); rpc.SetSpotifyTrack(null);
    Check(published?.State == "New game state", "pause restores latest game state");
    rpc.SetSpotifyTrack(new string('界', 200));
    Check(Encoding.UTF8.GetByteCount(published!.State!) <= 128, "long track does not crash RPC");
    rpc.SetVisibility(false); rpc.SetSpotifyTrack("Another track");
    Check(published is null, "hidden presence stays hidden during Spotify updates");
    rpc.SetVisibility(true);
    Check(published?.State == "Listening to Another track", "visibility restore retains current track");
    rpc.ProcessRPCMessage(Rpc("{\"timeStart\":18446744073709551615,\"state\":\"Safe\"}"));
    rpc.SpotifyEnabled = false;
    Check(published?.State == "Safe", "overflow timestamp cannot kill RPC processing");
    Log(watcher, leave);
    Check(published is null && !watcher.InGame, "leaving clears Discord presence");
    Log(watcher, join); Log(watcher, joined);
    Check(published?.State == "In a game", "next game has fresh state");
    Log(watcher, "[FLog::SingleSurfaceApp] leaveUGCGameInternal");
    Check(published is null && !watcher.InGame, "desktop return clears presence without disconnect entry");
}
using (var watcher = new ActivityWatcher())
{
    Log(watcher, join); Log(watcher, leave); Log(watcher, join); Log(watcher, joined);
    Check(watcher.InGame, "failed join does not block next join");
    Log(watcher, "[FLog::GameJoinLoadTime] Report game_join_loadtime: universeid:77,userid:88,referral_page:Home");
    Check(watcher.Data.UniverseId == 77 && watcher.Data.UserId == 88, "metadata arriving after joined is recognized");
    Message? received = null; watcher.OnRPCMessage += (_, message) => received = message;
    Log(watcher, "[FLog::CreatorOutput] [BloxstrapRPC] {\"command\":\"SetRichPresence\",\"data\":{\"state\":\"Works\"}}");
    Check(received?.Command == "SetRichPresence", "existing games using BloxstrapRPC work");
}
using (var watcher = new ActivityWatcher())
{
    Log(watcher, "[FLog::Output] ! Joining game '11111111-1111-1111-1111-111111111111' place 999999999999999999999999 at 127.0.0.1");
    Log(watcher, join); Log(watcher, joined);
    Check(watcher.InGame, "invalid numeric log entry does not prevent later joins");
}
using (var watcher = new ActivityWatcher())
{
    var states = new List<Presence?>();
    using var rpc = new DiscordRichPresence(watcher, value => states.Add(value));
    rpc.ProcessRPCMessage(Rpc("{\"details\":\"Queued details\"}"));
    rpc.ProcessRPCMessage(Rpc("{\"state\":\"Queued state\"}"));
    Log(watcher, join); Log(watcher, joined);
    Check(states.Last()?.Details == "Queued details" && states.Last()?.State == "Queued state", "all queued messages drain in order");
}
string logFile = Path.Combine(Path.GetTempPath(), "onyxstrap-regression-" + Guid.NewGuid() + ".log");
try
{
    using var watcher = new ActivityWatcher(logFile);
    Task tail = watcher.StartAsync();
    await Task.Delay(100);
    Check(!tail.IsCompleted, "log tail waits for missing file");
    await File.WriteAllTextAsync(logFile, "timestamp " + join[..40]);
    await Task.Delay(400);
    Check(watcher.Data.PlaceId == 0, "incomplete log line is buffered");
    await File.AppendAllTextAsync(logFile, join[40..] + "\n" + "timestamp " + joined + "\n");
    for (int i = 0; i < 20 && !watcher.InGame; i++) await Task.Delay(100);
    Check(watcher.InGame, "split log line reconstructed correctly");
    watcher.Dispose();
    await tail.WaitAsync(TimeSpan.FromSeconds(3));
    Check(tail.IsCompletedSuccessfully, "log tail cancels cleanly");
}
finally { File.Delete(logFile); }
var vm = new IntegrationsViewModel();
App.Settings.Prop.ShowSpotifyOnRichPresence = true;
vm.DiscordActivityEnabled = false;
Check(!vm.SpotifyRichPresenceEnabled, "Spotify setting follows RPC dependency");
typeof(FastFlagCatalog).GetField("_names", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null,
    new List<string> { "FlagA", "FlagB", "FlagC", "OtherFlagA", "OtherFlagB", "OtherFlagC" });
Check(FastFlagCatalog.Search("Flag", 2).Count == 2, "autocomplete obeys requested result limit");
var track = await new SpotifyTrackReader().GetTrackAsync();
Console.WriteLine("Windows media API smoke check completed; active Spotify track: " + (track is not null));
Console.WriteLine($"{passed} regression checks passed.");
