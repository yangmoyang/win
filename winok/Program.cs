using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using winok.utils;
namespace winok
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool ok = false;

            Task.Run(async () =>
            {
                ok = await TimeCheckHelper.CheckLocalTimeAsync(
                    allowDiffSeconds: 5,  // 允许 5 秒误差
                    onError: msg =>
                    {
                        MessageBox.Show(
                            msg,
                            "时间异常",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                    });
            }).GetAwaiter().GetResult();

            if (!ok)
                return;
           // 先显示登录窗体
            using (var login = new LoginForm())
            {
                if (login.ShowDialog() == DialogResult.OK)
                {
                    // 登录成功，启动主窗体
                    Application.Run(new Form1( login.LoginResult));
                }
            }
            //  Application.Run(new Form1());
            //Application.Run(new LoginForm());
        }
        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var asmName = new AssemblyName(args.Name).Name + ".dll";

            var resourceName = Assembly.GetExecutingAssembly()
                .GetManifestResourceNames()
                .FirstOrDefault(x => x.EndsWith(asmName));

            if (resourceName == null)
                return null;

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                return Assembly.Load(data);
            }
        }
    }
}
