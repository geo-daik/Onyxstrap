namespace Onyxstrap.Models
{
    /// <summary>
    /// A saved Roblox account for Onyxstrap's account switcher.
    /// The session token is stored DPAPI-encrypted (current user), never as plaintext.
    /// </summary>
    public class OnyxAccount
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; } = "";

        public long UserId { get; set; } = 0;

        /// <summary>
        /// Base64 of a DPAPI-protected (.ROBLOSECURITY) blob.
        /// </summary>
        public string? ProtectedToken { get; set; }

        /// <summary>
        /// This account's own FastFlag set - applied on launch when the account is active.
        /// </summary>
        public Dictionary<string, object> FastFlags { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? AvatarUrl { get; set; }
    }

    public class OnyxAccountsData
    {
        public string? ActiveAccountId { get; set; }

        public List<OnyxAccount> Accounts { get; set; } = new();

        /// <summary>
        /// Named FastFlag presets that can be applied to any account.
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> Presets { get; set; } = new();
    }
}
