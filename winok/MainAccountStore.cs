using System;
using System.Collections.Generic;

public class MainAccountItem
{
    public string Username { get; set; }

    // 密码建议加密后保存（下面会给加密代码）
    public string PasswordProtected { get; set; }

    public DateTime LastLoginTime { get; set; }
}

public class MainAccountStore
{
    public List<MainAccountItem> Accounts { get; set; } = new List<MainAccountItem>();

    // 可选：记住最后一次在 MainForm 登录成功的账号
    public string LastAccount { get; set; }
}
