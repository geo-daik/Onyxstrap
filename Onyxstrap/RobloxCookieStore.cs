using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Onyxstrap
{
    internal static class RobloxCookieStore
    {
        internal static string ReplaceCookieRows(string plaintext, string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.IndexOfAny(new[] { '\r', '\n', '\t', ';' }) >= 0)
                throw new InvalidDataException("The session value is invalid.");
            var result = new List<string>();
            bool replaced = false;
            foreach (string line in plaintext.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length == 0 || (line.StartsWith('#') && !line.StartsWith("#HttpOnly_", StringComparison.Ordinal)))
                { result.Add(line); continue; }
                string[] parts = line.Split('\t');
                if (parts.Length != 7) throw new InvalidDataException("Unsupported Roblox cookie-store format. No account changes were made.");
                string domain = parts[0].Replace("#HttpOnly_", "").TrimStart('.');
                if (domain.Equals("roblox.com", StringComparison.OrdinalIgnoreCase) && parts[5] == ".ROBLOSECURITY")
                {
                    if (replaced) continue;
                    parts[6] = token;
                    result.Add(string.Join("\t", parts));
                    replaced = true;
                }
                else result.Add(line);
            }
            if (!replaced) result.Add("#HttpOnly_.roblox.com\tTRUE\t/\tTRUE\t0\t.ROBLOSECURITY\t" + token);
            return string.Join("\n", result);
        }

        internal static byte[] Prepare(byte[] original, string token)
        {
            var json = JsonNode.Parse(original) as JsonObject ?? throw new InvalidDataException("Invalid Roblox cookie store.");
            if (json["CookiesVersion"]?.GetValue<string>() != "1")
                throw new InvalidDataException("Unsupported Roblox cookie-store version. No session was changed.");
            string data = json["CookiesData"]?.GetValue<string>() ?? throw new InvalidDataException("Unsupported Roblox cookie store.");
            byte[] decoded = ProtectedData.Unprotect(Convert.FromBase64String(data), null, DataProtectionScope.CurrentUser);
            string updated = ReplaceCookieRows(Encoding.UTF8.GetString(decoded), token);
            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(updated), null, DataProtectionScope.CurrentUser);
            // Verify a decrypted round trip, never compare plaintext with ciphertext.
            if (Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser)) != updated)
                throw new InvalidDataException("Unable to verify the prepared session.");
            json["CookiesData"] = Convert.ToBase64String(encrypted);
            return Encoding.UTF8.GetBytes(json.ToJsonString());
        }

        internal sealed class SessionChange : IDisposable
        {
            private readonly string _path;
            private readonly string _backup;
            private readonly FileStream _lock;
            private bool _committed;
            private readonly byte[] _installed;
            internal SessionChange(string path, string token)
            {
                _path = path;
                if (!File.Exists(path)) throw new InvalidDataException("Open Roblox and sign in normally once before using saved-account launch.");
                _lock = new FileStream(path + ".onyx-lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                _backup = path + ".onyx-backup-" + Guid.NewGuid().ToString("N");
                string temp = path + ".onyx-new-" + Guid.NewGuid().ToString("N");
                try
                {
                    byte[] original = File.ReadAllBytes(path);
                    _installed = Prepare(original, token);
                    File.WriteAllBytes(temp, _installed);
                    if (!File.ReadAllBytes(path).SequenceEqual(original)) throw new IOException("Roblox changed its session during the switch. Try again after closing Roblox.");
                    File.Replace(temp, path, _backup);
                }
                catch { if (File.Exists(temp)) File.Delete(temp); _lock.Dispose(); throw; }
            }
            internal void Commit() { _committed = true; }
            public void Dispose()
            {
                try
                {
                    if (!_committed && File.Exists(_backup))
                    {
                        if (File.ReadAllBytes(_path).SequenceEqual(_installed)) File.Move(_backup, _path, true);
                        else throw new IOException("Session changed externally; the original encrypted backup was preserved.");
                    }
                    else if (File.Exists(_backup)) File.Delete(_backup);
                }
                finally { _lock.Dispose(); }
            }
        }
    }
}
