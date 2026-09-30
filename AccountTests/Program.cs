using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Onyxstrap;

int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
void Throws(Action action, string name) { bool threw = false; try { action(); } catch { threw = true; } Check(threw, name); }
async Task ThrowsAsync(Func<Task> action, string name) { bool threw = false; try { await action(); } catch { threw = true; } Check(threw, name); }
const string row = "#HttpOnly_.roblox.com\tTRUE\t/\tTRUE\t0\t.ROBLOSECURITY\tfake-old";
byte[] Fixture(string version = "1") => Encoding.UTF8.GetBytes(new JsonObject {
 ["CookiesVersion"] = version, ["UnknownProperty"] = 42,
 ["CookiesData"] = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(row), null, DataProtectionScope.CurrentUser)) }.ToJsonString());
string Plain(byte[] bytes) => Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(JsonNode.Parse(bytes)!["CookiesData"]!.GetValue<string>()), null, DataProtectionScope.CurrentUser));
string updated = RobloxCookieStore.ReplaceCookieRows(row + "\n" + row, "fake-new");
Check(updated.Split('\n').Length == 1 && updated.EndsWith("\tfake-new"), "replace existing cookie and remove duplicates");
Check(RobloxCookieStore.ReplaceCookieRows("example.com\tFALSE\t/\tTRUE\t0\tother\tkeep", "fake-new").Contains("other\tkeep"), "preserve unrelated cookie rows");
Throws(() => RobloxCookieStore.ReplaceCookieRows("unknown-format", "fake-new"), "reject unknown cookie rows");
Throws(() => RobloxCookieStore.ReplaceCookieRows(row, "bad;value"), "reject cookie delimiter injection");
byte[] prepared = RobloxCookieStore.Prepare(Fixture(), "fake-new");
Check(Plain(prepared).EndsWith("fake-new"), "encrypted session verifies after decrypting");
Check(!Encoding.UTF8.GetString(prepared).Contains("fake-new"), "session not written as plaintext");
Check(JsonNode.Parse(prepared)!["UnknownProperty"]!.GetValue<int>() == 42, "preserve unknown store metadata");
Throws(() => RobloxCookieStore.Prepare(Fixture("2"), "fake-new"), "unknown cookie version fails closed");
string dir = Path.Combine(AppContext.BaseDirectory, "fixture-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try {
 string file = Path.Combine(dir, "cookies.json"); byte[] original = Fixture(); File.WriteAllBytes(file, original);
 using (var change = new RobloxCookieStore.SessionChange(file, "fake-new")) {
  Check(Plain(File.ReadAllBytes(file)).EndsWith("fake-new"), "transaction installs prepared encrypted session");
  Throws(() => { using var conflicting = new RobloxCookieStore.SessionChange(file, "fake-other"); }, "parallel switch is blocked");
 }
 Check(File.ReadAllBytes(file).SequenceEqual(original), "failed launch rolls back original bytes");
 using (var change = new RobloxCookieStore.SessionChange(file, "fake-new")) change.Commit();
 Check(Plain(File.ReadAllBytes(file)).EndsWith("fake-new"), "successful launch commits session");
 Check(Directory.GetFiles(dir, "*.onyx-backup-*").Length == 0, "completed transaction removes backup");
 Check(File.ReadAllBytes(file + ".onyx-original").SequenceEqual(original), "commit keeps the user's original session");
 using (var change = new RobloxCookieStore.SessionChange(file, "fake-second")) change.Commit();
 Check(File.ReadAllBytes(file + ".onyx-original").SequenceEqual(original), "switching saved accounts keeps the first original");
 Check(!File.Exists(file + ".onyx-lock"), "completed transaction removes lock file");
 var conflict = new RobloxCookieStore.SessionChange(file, "fake-next"); File.WriteAllBytes(file, Fixture());
 Throws(conflict.Dispose, "rollback never overwrites externally changed session");
 Check(Directory.GetFiles(dir, "*.onyx-backup-*").Length == 1, "external change preserves recovery backup");
 Check(RobloxCookieStore.RestoreOriginal(file) && File.ReadAllBytes(file).SequenceEqual(original) && !File.Exists(file + ".onyx-original"), "normal launch restores original session");
 Check(!RobloxCookieStore.RestoreOriginal(file), "restore is a no-op without a saved original");
 var store = new AccountManager(Path.Combine(dir, "Accounts.json"));
 store.SaveSession(123, "FakeAccount", "fake-session");
 var account = store.Read().Accounts.Single();
 Check(account.UserId == 123 && !File.ReadAllText(Path.Combine(dir,"Accounts.json")).Contains("fake-session"), "saved account uses encrypted token");
 byte[] before = File.ReadAllBytes(Path.Combine(dir,"Accounts.json"));
 Throws(() => store.SaveSession(456, "WrongAccount", "fake-other", account.Id), "reauthentication rejects different user ID");
 Check(before.SequenceEqual(File.ReadAllBytes(Path.Combine(dir,"Accounts.json"))), "wrong-account reauthentication preserves vault bytes");
 store.SaveSession(123, "NewUsername", "fake-renewed", account.Id);
 Check(store.Read().Accounts.Count == 1 && store.Read().Accounts[0].Name == "FakeAccount", "reauthentication preserves saved label");
 using var correct = new HttpClient(new Mock(_ => new(HttpStatusCode.OK) { Content = new StringContent("{\"id\":123,\"name\":\"FakeAccount\"}") }));
 Check(await store.GetVerifiedToken(account.Id, correct) == "fake-renewed", "launch verifies account identity before returning session");
 using var wrong = new HttpClient(new Mock(_ => new(HttpStatusCode.OK) { Content = new StringContent("{\"id\":456,\"name\":\"WrongAccount\"}") }));
 await ThrowsAsync(() => store.GetVerifiedToken(account.Id, wrong), "launch rejects mismatched session identity");
 File.WriteAllText(Path.Combine(dir,"Accounts.json"), "invalid-json");
 Throws(() => store.SaveSession(123,"FakeAccount","fake-session"), "corrupt vault is not replaced");
 Check(File.ReadAllText(Path.Combine(dir,"Accounts.json")) == "invalid-json", "corrupt vault preserved for recovery");
} finally { Directory.Delete(dir,true); }
foreach (string uri in new[] { "https://www.roblox.com/login", "https://roblox.com/", "https://auth.roblox.com/" }) Check(RobloxAuth.IsAllowedLoginUri(uri), "allow Roblox HTTPS host: " + uri);
foreach (string uri in new[] { "https://roblox.com.evil.test", "https://evil.test/?roblox.com", "http://www.roblox.com", "https://evilroblox.com", "https://roblox.com@evil.test/", "https://roblox.com:444/", "javascript:roblox.com" }) Check(!RobloxAuth.IsAllowedLoginUri(uri), "block invalid login destination: " + uri);
using var denied = new HttpClient(new Mock(_ => new(HttpStatusCode.Unauthorized)));
const string launch = "roblox-player:1+launchmode:play+gameinfo:old+placelauncherurl:https%3A%2F%2Fexample.test%2F%3FprivateCode%3Da%26x%3Db+channel:abc";
await ThrowsAsync(() => RobloxAuth.BuildAccountLaunchArgs(launch,"fake",denied), "ticket failure aborts without old-account fallback");
int requests = 0;
using var csrf = new HttpClient(new Mock(request => {
 requests++;
 if (requests == 1) { var response = new HttpResponseMessage(HttpStatusCode.Forbidden); response.Headers.Add("x-csrf-token","fake-csrf"); return response; }
 Check(request.Headers.GetValues("x-csrf-token").Single() == "fake-csrf", "CSRF retry uses challenge token");
 var result = new HttpResponseMessage(HttpStatusCode.OK); result.Headers.Add("rbx-authentication-ticket","fake-ticket"); return result;
}));
Check(await RobloxAuth.BuildAccountLaunchArgs(launch,"fake",csrf) == launch.Replace("gameinfo:old","gameinfo:fake-ticket"), "ticket replacement preserves protocol version and every join parameter");
await ThrowsAsync(() => RobloxAuth.BuildAccountLaunchArgs("roblox-player:1+launchmode:play", "fake", denied), "missing gameinfo fails closed");
await ThrowsAsync(() => RobloxAuth.BuildAccountLaunchArgs(launch+"+gameinfo:duplicate", "fake", denied), "duplicate ticket fields fail closed");
Console.WriteLine($"{passed} account regression checks passed. Only synthetic sessions and mocked HTTP were used.");
sealed class Mock(Func<HttpRequestMessage,HttpResponseMessage> respond) : HttpMessageHandler
{
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
}
