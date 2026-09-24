using System.Security.Cryptography;

namespace Onyxstrap
{
    /// <summary>
    /// Onyxstrap's account vault: saved Roblox accounts with per-account FastFlag sets,
    /// plus reusable named presets. Tokens are DPAPI-encrypted at rest (current user only).
    /// </summary>
    public class AccountManager : JsonManager<OnyxAccountsData>
    {
        public override string ClassName => nameof(AccountManager);

        public override string LOG_IDENT_CLASS => ClassName;

        public override string FileName => "Accounts.json";

        public IEnumerable<OnyxAccount> Accounts => Prop.Accounts;

        public OnyxAccount? ActiveAccount =>
            Prop.Accounts.FirstOrDefault(x => x.Id == Prop.ActiveAccountId);

        public bool HasAccounts => Prop.Accounts.Count > 0;

        #region DPAPI

        private static string ProtectToken(string token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(token);
            return Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
        }

        private static string? UnprotectToken(string? protectedToken)
        {
            if (String.IsNullOrEmpty(protectedToken))
                return null;

            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedToken), null, DataProtectionScope.CurrentUser));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to decrypt account token: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Accounts

        public OnyxAccount? GetAccount(string id) => Prop.Accounts.FirstOrDefault(x => x.Id == id);

        public string? GetToken(OnyxAccount account) => UnprotectToken(account.ProtectedToken);

        public void AddAccount(string name, string securityToken, long userId = 0, string? avatarUrl = null)
        {
            var account = new OnyxAccount
            {
                Name = name,
                UserId = userId,
                AvatarUrl = avatarUrl,
                ProtectedToken = ProtectToken(securityToken),
                FastFlags = new Dictionary<string, object>(App.FastFlags.Prop)
            };

            Prop.Accounts.Add(account);

            if (Prop.ActiveAccountId is null)
                Prop.ActiveAccountId = account.Id;

            Save();
        }

        public void RemoveAccount(string id)
        {
            var account = GetAccount(id);
            if (account is null)
                return;

            Prop.Accounts.Remove(account);

            if (Prop.ActiveAccountId == id)
                Prop.ActiveAccountId = Prop.Accounts.FirstOrDefault()?.Id;

            Save();
        }

        public void RenameAccount(string id, string name)
        {
            var account = GetAccount(id);
            if (account is null)
                return;

            account.Name = name;
            Save();
        }

        /// <summary>
        /// Switches the active account: syncs current flags into the previously active
        /// account, then applies the newly selected account's own FastFlag set.
        /// </summary>
        public void SetActive(string id)
        {
            if (GetAccount(id) is null)
                return;

            SyncActiveFlags();

            Prop.ActiveAccountId = id;

            ApplyAccountFlags(ActiveAccount!);

            Save();
        }

        /// <summary>
        /// Writes the account's FastFlag set into the live FastFlag manager.
        /// </summary>
        public void ApplyAccountFlags(OnyxAccount account)
        {
            App.FastFlags.Prop = new Dictionary<string, object>(account.FastFlags);
            App.FastFlags.Save();
        }

        /// <summary>
        /// Copies the current FastFlag set into the active account's record.
        /// Called from <see cref="FastFlagManager.Save"/> so flag edits always
        /// end up attached to whichever account is active.
        /// </summary>
        public void SyncActiveFlags()
        {
            if (ActiveAccount is not OnyxAccount account || !App.FastFlags.Loaded)
                return;

            if (account.FastFlags.Count == App.FastFlags.Prop.Count && account.FastFlags.All(pair => App.FastFlags.Prop.Contains(pair)))
                return;

            account.FastFlags = new Dictionary<string, object>(App.FastFlags.Prop);
            Save();
        }

        #endregion

        #region Presets

        public void SavePreset(string name)
        {
            Prop.Presets[name] = new Dictionary<string, object>(App.FastFlags.Prop);
            Save();
        }

        public void ApplyPreset(string name)
        {
            if (!Prop.Presets.TryGetValue(name, out var flags))
                return;

            App.FastFlags.Prop = new Dictionary<string, object>(flags);
            App.FastFlags.Save();
        }

        public void DeletePreset(string name)
        {
            if (!Prop.Presets.ContainsKey(name))
                return;

            Prop.Presets.Remove(name);
            Save();
        }

        #endregion

        private static string LOG_IDENT => nameof(AccountManager);
    }
}
