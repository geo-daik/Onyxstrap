namespace Onyxstrap.Utility
{
    /// <summary>
    /// Import/export of the user's FastFlag set as a JSON file.
    /// Shared by the Engine Settings page and the flag editor.
    /// </summary>
    public static class FastFlagBackup
    {
        public static void Export(string path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(App.FastFlags.Prop, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <returns>The number of flags imported.</returns>
        public static int Import(string path)
        {
            var imported = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path))
                ?? throw new InvalidDataException("File contained no JSON object");

            foreach (var pair in imported)
                App.FastFlags.SetValue(pair.Key, pair.Value);

            App.FastFlags.Save();

            return imported.Count;
        }
    }
}
