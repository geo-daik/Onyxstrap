using System.Security.Cryptography;

namespace Onyxstrap
{
    /// <summary>
    /// Swaps the .ROBLOSECURITY session inside the Roblox player's own cookie
    /// store (%LOCALAPPDATA%\Roblox\LocalStorage\RobloxCookies.dat).
    /// The file is JSON wrapping a DPAPI blob (current user) containing
    /// Netscape-format cookie lines - the same store the client itself
    /// maintains, so this is data-file editing, not injection.
    /// </summary>
    public static class RobloxCookieStore
    {
        private static string CookieFilePath => Path.Combine(Paths.LocalAppData, "Roblox", "LocalStorage", "RobloxCookies.dat");

        public static bool SwapSecurityCookie(string securityToken)
        {
            const string LOG_IDENT = "RobloxCookieStore::SwapSecurityCookie";

            try
            {
                string contents;

                if (File.Exists(CookieFilePath))
                {
                    var json = JsonSerializer.Deserialize<RobloxCookieFile>(File.ReadAllText(CookieFilePath));

                    if (json?.CookiesData is null)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Cookie store is missing CookiesData");
                        return false;
                    }

                    string plain = Encoding.UTF8.GetString(
                        ProtectedData.Unprotect(Convert.FromBase64String(json.CookiesData), null, DataProtectionScope.CurrentUser));

                    // netscape format: #HttpOnly_.roblox.com\tTRUE\t/\tTRUE\t0\t.ROBLOSECURITY\t<value>
                    var pattern = new Regex(@"(^\#HttpOnly_\.roblox\.com(?:\t[^\t]*){5}\t\.ROBLOSECURITY\t).*$", RegexOptions.Multiline);
                    string updated;

                    if (pattern.IsMatch(plain))
                        updated = pattern.Replace(plain, $"${{1}}{securityToken}", 1);
                    else
                        updated = plain.TrimEnd() + $"\n#HttpOnly_.roblox.com\tTRUE\t/\tTRUE\t0\t.ROBLOSECURITY\t{securityToken}";

                    json.CookiesData = Convert.ToBase64String(ProtectedData.Protect(
                        Encoding.UTF8.GetBytes(updated), null, DataProtectionScope.CurrentUser));

                    contents = JsonSerializer.Serialize(json);

                    // verify the replacement actually landed before claiming success
                    if (!contents.Contains(securityToken, StringComparison.Ordinal))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Swap verification failed - cookie line was not replaced");
                        return false;
                    }
                }
                else
                {
                    // no cookie store yet (fresh client) - create one with the session in it
                    var json = new RobloxCookieFile
                    {
                        CookiesData = Convert.ToBase64String(ProtectedData.Protect(
                            Encoding.UTF8.GetBytes($"#HttpOnly_.roblox.com\tTRUE\t/\tTRUE\t0\t.ROBLOSECURITY\t{securityToken}"),
                            null, DataProtectionScope.CurrentUser))
                    };

                    contents = JsonSerializer.Serialize(json);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(CookieFilePath)!);
                File.WriteAllText(CookieFilePath, contents);

                App.Logger.WriteLine(LOG_IDENT, "Swapped the Roblox client session to the active account");

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to swap the client session: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fully switches the client's stored session to the given account:
        /// the .ROBLOSECURITY cookie in RobloxCookies.dat plus the identity
        /// (UserId/Username/DisplayName) in LocalStorage\appStorage.json,
        /// which is what the app shell actually reads on boot.
        /// </summary>
        public static bool ApplyAccountSession(OnyxAccount account, string securityToken)
        {
            const string LOG_IDENT = "RobloxCookieStore::ApplyAccountSession";

            bool cookieSwapped = SwapSecurityCookie(securityToken);

            try
            {
                string storagePath = Path.Combine(Paths.LocalAppData, "Roblox", "LocalStorage", "appStorage.json");

                if (!File.Exists(storagePath))
                {
                    App.Logger.WriteLine(LOG_IDENT, "appStorage.json does not exist yet, skipping identity update");
                    return cookieSwapped;
                }

                var storage = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(storagePath));

                if (storage is null)
                    return cookieSwapped;

                storage["UserId"] = JsonSerializer.SerializeToElement(account.UserId);
                storage["Username"] = JsonSerializer.SerializeToElement(account.Name);
                storage["DisplayName"] = JsonSerializer.SerializeToElement(account.Name);

                File.WriteAllText(storagePath, JsonSerializer.Serialize(storage));

                App.Logger.WriteLine(LOG_IDENT, $"Updated appStorage identity to {account.Name} ({account.UserId})");

                return cookieSwapped;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to update appStorage identity: {ex.Message}");
                return cookieSwapped;
            }
        }

        private class RobloxCookieFile
        {
            [JsonPropertyName("CookiesVersion")]
            public string CookiesVersion { get; set; } = "1";

            [JsonPropertyName("CookiesData")]
            public string? CookiesData { get; set; }
        }
    }
}
