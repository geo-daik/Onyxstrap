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
        private static readonly HttpClient SessionClient = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(20) };

        private const string USER_ENDPOINT = "https://users.roblox.com/v1/users/authenticated";

        /// <summary>
        /// Verifies a .ROBLOSECURITY token and returns the (userId, username) it belongs to.
        /// </summary>
        public static async Task<(long UserId, string? Username)?> ValidateToken(string securityToken, HttpClient? client = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, USER_ENDPOINT);
                request.Headers.Add("Cookie", $".ROBLOSECURITY={securityToken}");

                using var response = await (client ?? SessionClient).SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                var user = JsonSerializer.Deserialize<UserAuthenticated>(await response.Content.ReadAsStringAsync());

                if (user is null || user.Id == 0)
                    return null;

                return (user.Id, user.Name);
            }
            catch (Exception)
            {
                App.Logger.WriteLine("RobloxAuth::ValidateToken", "Unable to validate the session.");
                return null;
            }
        }

        /// <summary>
        /// Mints a one-time authentication ticket for the given session token.
        /// The ticket is what the website hands to the client via the gameinfo
        /// launch parameter.
        /// </summary>
        public static async Task<string?> MintAuthenticationTicket(string securityToken, HttpClient? client = null, CancellationToken cancellationToken = default)
        {
            const string LOG_IDENT = "RobloxAuth::MintAuthenticationTicket";

            string? csrfToken = null;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, AUTH_ENDPOINT);
                    request.Headers.Add("Cookie", $".ROBLOSECURITY={securityToken}");
                    request.Headers.Add("Referer", "https://www.roblox.com/");

                    // the first request gets CSRF-challenged; retry with the token it handed us
                    if (csrfToken is not null)
                        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

                    using var response = await (client ?? SessionClient).SendAsync(request, cancellationToken);

                    if (response.StatusCode == HttpStatusCode.Forbidden && attempt == 0
                        && response.Headers.TryGetValues("x-csrf-token", out var csrfValues))
                    {
                        csrfToken = csrfValues.FirstOrDefault();
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
                catch (Exception)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Unable to request a launch ticket.");
                    return null;
                }
            }

            return null;
        }

        /// <summary>Replaces only the ticket, preserving the original join parameters.</summary>
        public static async Task<string> BuildAccountLaunchArgs(string launchArgs, string securityToken, HttpClient? client = null)
        {
            if (string.IsNullOrEmpty(launchArgs)) return launchArgs;
            var parts = launchArgs.Split('+');
            if (!parts[0].Equals("roblox-player:1", StringComparison.OrdinalIgnoreCase)
                || parts.Count(p => p.StartsWith("gameinfo:", StringComparison.OrdinalIgnoreCase)) != 1)
                throw new InvalidOperationException("Unsupported launch link. Launch was cancelled.");
            string? ticket = await MintAuthenticationTicket(securityToken, client);
            if (string.IsNullOrWhiteSpace(ticket) || ticket.IndexOfAny(new[] { '+', '\r', '\n', '"' }) >= 0)
                throw new InvalidOperationException("Could not authenticate the selected account. Launch was cancelled.");
            int index = Array.FindIndex(parts, p => p.StartsWith("gameinfo:", StringComparison.OrdinalIgnoreCase));
            parts[index] = "gameinfo:" + ticket;
            return string.Join("+", parts);
        }

        /// <summary>
        /// Fetches the headshot avatar URL for a user from Roblox's thumbnail API.
        /// </summary>
        public static async Task<string?> GetAvatarUrl(long userId)
        {
            const string LOG_IDENT = "RobloxAuth::GetAvatarUrl";

            if (userId <= 0)
                return null;

            try
            {
                var response = await Http.GetJson<ThumbnailResponse>(
                    $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png&isCircular=false"
                );

                return response?.Data?.FirstOrDefault()?.ImageUrl;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to fetch avatar for user {userId}: {ex.Message}");
                return null;
            }
        }

        internal static bool IsAllowedLoginUri(string? value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort
            && string.IsNullOrEmpty(uri.UserInfo)
            && (uri.Host.Equals("roblox.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".roblox.com", StringComparison.OrdinalIgnoreCase));

        internal static void RequireSameUser(long expected, long actual)
        {
            if (expected <= 0 || expected != actual)
                throw new InvalidOperationException("You signed in to a different Roblox account. The saved account was not changed.");
        }

        private class UserAuthenticated
        {
            [JsonPropertyName("id")]
            public long Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        private class ThumbnailResponse
        {
            [JsonPropertyName("data")]
            public List<ThumbnailItem>? Data { get; set; }
        }

        private class ThumbnailItem
        {
            [JsonPropertyName("imageUrl")]
            public string? ImageUrl { get; set; }
        }
    }
}
