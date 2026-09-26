namespace Onyxstrap
{
    /// <summary>
    /// Known Roblox engine flag names, for autocomplete and validation hints.
    /// Ships with an embedded baseline and refreshes daily from Roblox's own
    /// client settings endpoint (the same source the player reads).
    /// </summary>
    public static class FastFlagCatalog
    {
        private static List<string>? _names;

        private static readonly SemaphoreSlim Semaphore = new(1, 1);

        private static string CachePath => Path.Combine(Paths.Base, "FlagCatalog.json");

        public static IReadOnlyList<string> Names
        {
            get
            {
                if (_names is null)
                    Load();

                return _names!;
            }
        }

        private static void Load()
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var stream = Resource.GetStream("FFlagNames.json");
                names.UnionWith(JsonSerializer.Deserialize<List<string>>(stream) ?? new List<string>());
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FastFlagCatalog::Load", $"Failed to read embedded catalog: {ex.Message}");
            }

            try
            {
                if (File.Exists(CachePath))
                    names.UnionWith(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(CachePath)) ?? new List<string>());
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FastFlagCatalog::Load", $"Failed to read cached catalog: {ex.Message}");
            }

            _names = names.ToList();

            _ = Task.Run(RefreshAsync);
        }

        /// <summary>
        /// Pulls the current flag list from Roblox's settings endpoint once per
        /// day. Failures are silent - the embedded baseline always works.
        /// </summary>
        private static async Task RefreshAsync()
        {
            if (!await Semaphore.WaitAsync(0))
                return;

            try
            {
                if (File.Exists(CachePath) && File.GetLastWriteTimeUtc(CachePath) > DateTime.UtcNow.AddDays(-1))
                    return;

                var response = await App.HttpClient.GetAsync("https://clientsettings.roblox.com/v2/settings/application/PCClientBootstrapper");
                response.EnsureSuccessStatusCode();

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                if (!document.RootElement.TryGetProperty("applicationSettings", out var settings) || settings.ValueKind != JsonValueKind.Object)
                    return;

                var names = new List<string>();

                foreach (var flag in settings.EnumerateObject())
                    names.Add(flag.Name);

                if (names.Count == 0)
                    return;

                File.WriteAllText(CachePath, JsonSerializer.Serialize(names));

                _names = _names.Union(names).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

                App.Logger.WriteLine("FastFlagCatalog::Refresh", $"Catalog refreshed with {names.Count} flags from Roblox");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FastFlagCatalog::Refresh", $"Refresh failed: {ex.Message}");
            }
            finally
            {
                Semaphore.Release();
            }
        }

        /// <summary>
        /// Searches the catalog: entries starting with the query first, then
        /// entries containing it. Case-insensitive.
        /// </summary>
        public static IReadOnlyList<string> Search(string query, int limit = 12)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<string>();

            var prefix = new List<string>();
            var contains = new List<string>();

            foreach (string name in Names)
            {
                if (prefix.Count >= limit && contains.Count >= limit)
                    break;

                if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    if (prefix.Count < limit && !name.Equals(query, StringComparison.OrdinalIgnoreCase))
                        prefix.Add(name);
                }
                else if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    if (contains.Count < limit)
                        contains.Add(name);
                }
            }

            var results = new List<string>(prefix);
            results.AddRange(contains);
            return results;
        }
    }
}
