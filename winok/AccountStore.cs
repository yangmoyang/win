using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

public static class AccountStoreHelper
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinOK");

    private static readonly string FilePath =
        Path.Combine(Dir, "accounts2.json");

    // =========================
    // 单个账号模型
    // =========================
    public class AccountInfo
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public DateTime LastLoginTime { get; set; }
    }

    // =========================
    // 容器模型
    // =========================
    private class AccountStore
    {
        public List<AccountInfo> Accounts { get; set; } = new List<AccountInfo>();
    }

    // =========================
    // 读取全部账号（给 LoginForm 用）
    // =========================
    public static List<AccountInfo> LoadAccounts()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new List<AccountInfo>();

            string json = File.ReadAllText(FilePath);

            // ① 先尝试按新格式解析
            try
            {
                var store = JsonConvert.DeserializeObject<AccountStore>(json);
                if (store?.Accounts != null)
                    return store.Accounts
                        .OrderByDescending(x => x.LastLoginTime)
                        .ToList();
            }
            catch
            {
                // 忽略，走旧格式迁移
            }

            // ② 旧格式：单账号
            try
            {
                var old = JsonConvert.DeserializeObject<AccountInfo>(json);
                if (old != null)
                {
                    return new List<AccountInfo>
                {
                    new AccountInfo
                    {
                        Username = old.Username,
                        Password = old.Password,
                        LastLoginTime = old.LastLoginTime
                    }
                };
                }
            }
            catch { }

            // ③ 旧格式：string 数组
            try
            {
                var list = JsonConvert.DeserializeObject<List<string>>(json);
                if (list != null)
                {
                    return list.Select(u => new AccountInfo
                    {
                        Username = u,
                        LastLoginTime = DateTime.Now
                    }).ToList();
                }
            }
            catch { }

            return new List<AccountInfo>();
        }
        catch
        {
            return new List<AccountInfo>();
        }
    }


    // =========================
    // 保存账号（登录成功后调用）
    // =========================
    public static void SaveAccount(string username, string password)
    {
        try
        {
            Directory.CreateDirectory(Dir);

            var store = new AccountStore
            {
                Accounts = LoadAccounts()
            };


            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                store = JsonConvert.DeserializeObject<AccountStore>(json)
                        ?? new AccountStore();
            }

            // 查找是否已有该账号
            var acc = store.Accounts
                .FirstOrDefault(x => x.Username == username);

            if (acc == null)
            {
                acc = new AccountInfo
                {
                    Username = username
                };
                store.Accounts.Add(acc);
            }

            acc.Password = password;
            acc.LastLoginTime = DateTime.Now;

            // 只保留最近 10 个账号（可选）
            store.Accounts = store.Accounts
                .OrderByDescending(x => x.LastLoginTime)
                .Take(10)
                .ToList();

            string output = JsonConvert.SerializeObject(store, Formatting.Indented);
            File.WriteAllText(FilePath, output);
        }
        catch
        {
            // 可以写日志
        }
    }

    // =========================
    // 清空账号列表（可选）
    // =========================
    public static void Clear()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }
}
