using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;


using Newtonsoft.Json;

public static class MainAccountStoreHelper
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinOK");

    private static readonly string FilePath =
        Path.Combine(Dir, "main_accounts.json");

    public static MainAccountStore Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new MainAccountStore();

            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<MainAccountStore>(json) ?? new MainAccountStore();
        }
        catch
        {
            return new MainAccountStore();
        }
    }

    public static void Save(MainAccountStore store)
    {
        Directory.CreateDirectory(Dir);
        string json = JsonConvert.SerializeObject(store, Formatting.Indented);
        File.WriteAllText(FilePath, json, Encoding.UTF8);
    }

    // =========================
    // 对外：新增/更新一条账号（用户名+密码）
    // =========================
    public static void Upsert(string username, string plainPassword)
    {
        var store = Load();

        var item = store.Accounts.FirstOrDefault(a =>
            string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase));

        if (item == null)
        {
            item = new MainAccountItem { Username = username };
            store.Accounts.Add(item);
        }

        item.PasswordProtected = Protect(plainPassword);
        item.LastLoginTime = DateTime.Now;

        store.LastAccount = username;

        // 可选：按最近登录排序
        store.Accounts = store.Accounts
            .OrderByDescending(a => a.LastLoginTime)
            .ToList();

        Save(store);
    }

    // =========================
    // 对外：读取某个账号的明文密码（用于自动填充）
    // =========================
    public static string GetPassword(string username)
    {
        var store = Load();
        var item = store.Accounts.FirstOrDefault(a =>
            string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase));

        if (item == null) return null;
        if (string.IsNullOrEmpty(item.PasswordProtected)) return null;

        return Unprotect(item.PasswordProtected);
    }

    // =========================
    // 对外：删除账号
    // =========================
    public static void Remove(string username)
    {
        var store = Load();
        store.Accounts.RemoveAll(a =>
            string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(store.LastAccount, username, StringComparison.OrdinalIgnoreCase))
            store.LastAccount = store.Accounts.FirstOrDefault()?.Username;

        Save(store);
    }

    // =========================
    // DPAPI 加密/解密（本机）
    // =========================
    private static string Protect(string plain)
    {
        if (plain == null) plain = "";
        var bytes = Encoding.UTF8.GetBytes(plain);
        var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(enc);
    }

    private static string Unprotect(string protectedBase64)
    {
        var enc = Convert.FromBase64String(protectedBase64);
        var bytes = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
