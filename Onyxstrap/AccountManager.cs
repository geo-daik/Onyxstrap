using System.Security.Cryptography;

namespace Onyxstrap
{
    internal sealed class AccountManager
    {
        private readonly string _path;
        internal AccountManager(string path) { _path = path; }
        internal OnyxAccountsData Read()
        {
            if (!File.Exists(_path)) return new();
            var data = JsonSerializer.Deserialize<OnyxAccountsData>(File.ReadAllText(_path))
                ?? throw new InvalidDataException("The saved accounts file is invalid. It has been left unchanged.");
            if (data.Accounts is null || data.Accounts.Any(a => a is null || !Guid.TryParse(a.Id, out _) || a.UserId <= 0)
                || data.Accounts.Select(a => a.Id).Distinct().Count() != data.Accounts.Count)
                throw new InvalidDataException("The saved accounts file is invalid. It has been left unchanged.");
            return data;
        }
        internal void SaveSession(long userId, string username, string token, string? existingId = null)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token))
                throw new InvalidDataException("The verified account is incomplete.");
            Change(data => {
                var account = existingId is null ? data.Accounts.FirstOrDefault(a => a.UserId == userId)
                    : data.Accounts.SingleOrDefault(a => a.Id == existingId)
                        ?? throw new InvalidOperationException("This saved account was removed. Reload the accounts page.");
                if (account is not null) RobloxAuth.RequireSameUser(account.UserId, userId);
                else { account = new OnyxAccount { UserId = userId, Name = username }; data.Accounts.Add(account); }
                account.ProtectedToken = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser));
            });
        }
        internal void Remove(string id) => Change(data => {
            data.Accounts.RemoveAll(a => a.Id == id);
            if (data.ActiveAccountId == id) data.ActiveAccountId = null;
        });
        private void Change(Action<OnyxAccountsData> edit)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using var gate = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var data = Read();
            edit(data);
            string temp = _path + ".new-" + Guid.NewGuid().ToString("N");
            try {
                File.WriteAllText(temp, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                if (File.Exists(_path)) File.Replace(temp, _path, null);
                else File.Move(temp, _path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        internal async Task<string> GetVerifiedToken(string id, HttpClient? client = null)
        {
            var account = Read().Accounts.SingleOrDefault(a => a.Id == id)
                ?? throw new InvalidOperationException("The saved account was not found.");
            string token;
            try { token = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(account.ProtectedToken ?? ""), null, DataProtectionScope.CurrentUser)); }
            catch (Exception ex) when (ex is CryptographicException or FormatException) {
                throw new InvalidOperationException("Sign in to this saved account again before launching.");
            }
            var identity = await RobloxAuth.ValidateToken(token, client)
                ?? throw new InvalidOperationException("The saved session could not be verified. Sign in again or check your connection.");
            RobloxAuth.RequireSameUser(account.UserId, identity.UserId);
            return token;
        }
    }
}
