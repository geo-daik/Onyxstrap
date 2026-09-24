namespace Onyxstrap
{
    /// <summary>
    /// Roblox authentication helpers for the account switcher.
    /// Uses Roblox's own public auth endpoints only - credentials are never
    /// sent anywhere other than roblox.com.
    /// </summary>
    public static class RobloxAuth
    {
        private const string AUTH_ENDPOINT = "https://auth.roblox.com/v1/authentication-ticket/";
        private const string USER_ENDPOINT = "https://users.roblox.com/v1/users/authenticated";

        /// <summary>
        /// Verifies a .ROBLOSECURITY token and returns the (userId, username) it belongs to.
        /// </summary>
        public static async Task<(long UserId, string? Username)?> ValidateToken(string securityToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, USER_ENDPOINT);
                request.Headers.Add("Cookie", $".ROBLOSECURITY={securityToken}");

                using var response = await App.HttpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return null;

                var user = JsonSerializer.Deserialize<UserAuthenticated>(await response.Content.ReadAsStringAsync());

                if (user is null || user.Id == 0)
                    return null;

                return (user.Id, user.Name);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RobloxAuth::ValidateToken", $"Failed to validate token: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Mints a one-time authentication ticket for the given session token.
        /// The ticket is what the website hands to the client via the gameinfo
        /// launch parameter.
        /// </summary>
        public static async Task<string?> MintAuthenticationTicket(string securityToken)
        {
            const string LOG_IDENT = "RobloxAuth::MintAuthenticationTicket";

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, AUTH_ENDPOINT);
                    request.Headers.Add("Cookie", $".ROBLOSECURITY={securityToken}");
                    request.Headers.Add("Referer", "https://www.roblox.com/");

                    using var response = await App.HttpClient.SendAsync(request);

                    if (response.StatusCode == HttpStatusCode.Forbidden && attempt == 0
                        && response.Headers.TryGetValues("x-csrf-token", out var csrfValues))
                    {
                        // first request gets CSRF-challenged; retry with the token it handed us
                        request.Headers.Add("X-CSRF-TOKEN", csrfValues.FirstOrDefault());
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Authentication ticket request failed ({(int)response.StatusCode})");
                        return null;
                    }

                    if (response.Headers.TryGetValues("rbx-authentication-ticket", out var ticketValues))
                        return ticketValues.FirstOrDefault();

                    App.Logger.WriteLine(LOG_IDENT, "Response did not contain an authentication ticket");
                    return null;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to mint authentication ticket: {ex.Message}");
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Rebuilds a roblox-player launch URI so that it authenticates with a
        /// freshly minted one-time ticket for the given account: the gameinfo
        /// parameter carries the ticket, and the join request is pointed at the
        /// PlaceLauncher service for the same place. Falls back to only swapping
        /// the gameinfo segment if the original join URL can't be parsed.
        /// </summary>
        public static async Task<string> BuildAccountLaunchArgs(string launchArgs, string securityToken)
        {
            const string LOG_IDENT = "RobloxAuth::BuildAccountLaunchArgs";

            if (String.IsNullOrEmpty(launchArgs))
                return launchArgs;

            string? ticket = await MintAuthenticationTicket(securityToken);

            if (String.IsNullOrEmpty(ticket))
            {
                App.Logger.WriteLine(LOG_IDENT, "No ticket available, keeping original launch arguments");
                return launchArgs;
            }

            string? launcherUrl = ExtractParam(launchArgs, "placelauncherurl", decode: true);

            if (!String.IsNullOrEmpty(launcherUrl) && TryGetJoinParams(launcherUrl, out long placeId, out string? gameId, out string? accessCode, out string? linkCode))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Rebuilding launch URI for place {placeId} (account switch)");

                string request = "RequestGame";
                string extra = "";

                if (!String.IsNullOrEmpty(accessCode))
                {
                    request = "RequestPrivateGame";
                    extra += $"&accessCode={accessCode}";
                }
                else if (!String.IsNullOrEmpty(linkCode))
                {
                    request = "RequestPrivateGame";
                    extra += $"&linkCode={linkCode}";
                }

                if (!String.IsNullOrEmpty(gameId))
                    extra += $"&gameId={gameId}";

                string placeLauncherUrl = $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request={request}&placeId={placeId}{extra}&isPlayTogetherGame=false";

                string browserTrackerId = ExtractParam(launchArgs, "browsertrackerid", decode: false) ?? "";
                string locale = ExtractParam(launchArgs, "robloxLocale", decode: false) ?? "en_us";
                string gameLocale = ExtractParam(launchArgs, "gameLocale", decode: false) ?? "en_us";
                string channel = ExtractParam(launchArgs, "channel", decode: false) ?? "";

                string rebuilt = $"1+launchmode:play+gameinfo:{ticket}+launchtime:{DateTimeOffset.Now.ToUnixTimeMilliseconds()}+" +
                    $"placelauncherurl:{Uri.EscapeDataString(placeLauncherUrl)}" +
                    (String.IsNullOrEmpty(browserTrackerId) ? "" : $"+browsertrackerid:{browserTrackerId}") +
                    $"+robloxLocale:{locale}+gameLocale:{gameLocale}+channel:{channel}+LaunchExp:InApp";

                // preserve the protocol prefix the original arguments came with
                int plusIndex = launchArgs.IndexOf('+');
                if (launchArgs.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase) || launchArgs.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase))
                    rebuilt = launchArgs[..plusIndex] + rebuilt;

                return rebuilt;
            }

            App.Logger.WriteLine(LOG_IDENT, "Could not parse join URL, swapping gameinfo only");

            return Regex.Replace(
                launchArgs,
                "gameinfo:[^+]*",
                $"gameinfo:{ticket}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );
        }

        private static string? ExtractParam(string launchArgs, string key, bool decode)
        {
            Match match = Regex.Match(launchArgs, $@"[+^]{Regex.Escape(key)}:([^+]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!match.Success || match.Groups.Count < 2)
                return null;

            string value = match.Groups[1].Value;

            return decode ? Uri.UnescapeDataString(value) : value;
        }

        private static bool TryGetJoinParams(string joinUrl, out long placeId, out string? gameId, out string? accessCode, out string? linkCode)
        {
            placeId = 0;
            gameId = accessCode = linkCode = null;

            Match placeMatch = Regex.Match(joinUrl, @"[?&]placeId=(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!placeMatch.Success || !long.TryParse(placeMatch.Groups[1].Value, out placeId))
                return false;

            gameId = Regex.Match(joinUrl, @"[?&]gameId=([^&\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) is Match m && m.Success ? m.Groups[1].Value : null;
            accessCode = Regex.Match(joinUrl, @"[?&]accessCode=([^&\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) is Match m2 && m2.Success ? m2.Groups[1].Value : null;
            linkCode = Regex.Match(joinUrl, @"[?&]linkCode=([^&\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) is Match m3 && m3.Success ? m3.Groups[1].Value : null;

            return true;
        }

        private class UserAuthenticated
        {
            [JsonPropertyName("id")]
            public long Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }
    }
}
