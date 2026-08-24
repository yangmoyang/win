using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Globalization;

namespace winok
{
    public partial class LoginForm : Form
    {
        public LoginResult LoginResult
        {
            get; private set;
        }
        public string banben = "V2.9";

        public LoginForm()
        {
            InitializeComponent();
        }
       
        private void LoginForm_Load(object sender, EventArgs e)
        {
            tabControl1.TabPages.Remove(tabPage1);
            this.Text = "登录  " + banben;
            var accounts = AccountStoreHelper.LoadAccounts();

            cboAccount.DisplayMember = "Username";
            cboAccount.DataSource = accounts;

            if (accounts.Count > 0)
            {
                cboAccount.SelectedIndex = 0;
                // txtPassword.Text = accounts[0].Password;
            }
            //_accountStore2 = AccountStoreHelper.Load();
            //Console.WriteLine( _accountStore2.LastAccount+" "+_accountStore2.Accounts.Count.ToString());


            //cboAccount.Items.Clear();
            //int xu = 0;
            //foreach (var acc in _accountStore2.Accounts)
            //{
            //    xu++;
            //    if (xu == 1)
            //    {
            //        textBox1.Text = acc;
            //    }
            //    if (xu == 2)
            //    {
            //        textBox2.Text = acc;
            //    }

            //}

            //// 自动选中上次登录账号
            //if (!string.IsNullOrEmpty(_accountStore.LastAccount))
            //{
            //    cboAccount.Text = _accountStore.LastAccount;
            //}
            splitContainer1.SplitterDistance = 40;
        }
        public class ApiResponse
        {
            public int code { get; set; }
            public string msg { get; set; }
            public DateTime expire_at { get; set; }
        }

        public async Task<ApiResponse> dengluAsync(string username,string password)
        {
            string url = "https://tg.hkd.one/api/v1/auth/denglu";

            var body = new
            {
                username = username,
                password = password
            };

            string json = JsonConvert.SerializeObject(body);

            using (var client = new HttpClient())
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await client.PostAsync(url, content);
                string respJson = await resp.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject<ApiResponse>(respJson);
            }
        }
        public async Task<ApiResponse> RegisterAsync(string username, string password)
        {
            string url = "https://tg.hkd.one/api/v1/auth/reg";

            var body = new
            {
                username = username,
                password = password
            };

            string json = JsonConvert.SerializeObject(body);

            using (var client = new HttpClient())
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await client.PostAsync(url, content);
                string respJson = await resp.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject<ApiResponse>(respJson);
            }
        }

        public async Task<ApiResponse> chongAsync(string username, string card)
        {
            string url = "https://tg.hkd.one/api/v1/auth/chongzhi";

            var body = new
            {
                username = username,
                card = card
            };

            string json = JsonConvert.SerializeObject(body);

            using (var client = new HttpClient())
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await client.PostAsync(url, content);
                string respJson = await resp.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject<ApiResponse>(respJson);
            }
        }
        public static bool IsValidAccount(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return Regex.IsMatch(input, @"^[A-Za-z0-9]{4,8}$");
    }

    private async void button2_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;
            string username = textBox4.Text.Trim();
            string password = textBox3.Text.Trim();
            if (!IsValidAccount(username) || !IsValidAccount(password))
            {
                MessageBox.Show("用户名或者密码格式不符");
                button2.Enabled = true;
                return;
            }

            ApiResponse fh =await  RegisterAsync(username,password);

            if (fh.code == 0)
            {
                MessageBox.Show(fh.msg);

            }
            else
            {
                MessageBox.Show(fh.msg);
            }
            button2.Enabled = true;
            //Console.WriteLine(fh.msg);
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            button1.Enabled = false;
            string username = cboAccount.Text.Trim();
            string password = textBox2.Text.Trim();
            //if (!IsValidAccount(username) || !IsValidAccount(password))
            //{
            //    MessageBox.Show("用户名或者密码格式不符");
            //    button1.Enabled = true;
            //    return;
            //}

            ApiResponse fh = await dengluAsync(username, password);

            if (fh.code == 0)
            {
                MessageBox.Show(fh.msg);


            }
            else
            {
                DateTime dt = DateTime.Parse(fh.expire_at.ToString(), null, DateTimeStyles.AdjustToUniversal);
                string local = dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                // MessageBox.Show(fh.msg+" 到期时间:"+ local);
                if (DateTime.Now >= fh.expire_at)
                {

                    MessageBox.Show("账号已到期，程序即将退出", "提示");
                    Application.Exit();
                }
                AccountStoreHelper.SaveAccount(
    username,
    "12345"
);



                LoginResult = new LoginResult
                {
                    ExpireAt = fh.expire_at,
                    banben = banben,
                    Username = username,
                }
               ;
                this.DialogResult = DialogResult.OK;
                this.Close();
                //  MessageBox.Show(fh.msg);
            }
            button1.Enabled = true;
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            button3.Enabled = false;
            string username = textBox6.Text.Trim();
            string card = textBox5.Text.Trim();
            //if (!IsValidAccount(username) )
            //{
            //    MessageBox.Show("用户名格式不符");
            //    button3.Enabled = true;
            //    return;
            //}
            if (card.Length < 15&& card.Length>30)
            {
                MessageBox.Show("卡号不正确");
                button3.Enabled = true;
                return;
            }


            ApiResponse fh = await chongAsync(username, card);

            if (fh.code == 0)
            {
                MessageBox.Show(fh.msg);

            }
            else
            {
                MessageBox.Show(fh.msg + ":" + fh.expire_at.ToShortDateString());

                this.DialogResult = DialogResult.OK;
                this.Close();

                //  MessageBox.Show(fh.msg);
            }
            button3.Enabled = true;
        }
    }
}
