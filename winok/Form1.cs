using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.ComponentModel;
using MQTTnet.Protocol;
using System.Net.WebSockets;
using System.Threading;
using WebSocketSharp;
using System.Security.Cryptography;
using System.Net.Security;
using Newtonsoft.Json.Linq;
using System.Collections;
using winok.utils;

namespace winok
{
 
    public partial class Form1 : Form
    {
        private bool _timer1Busy;
        private bool _timer2Busy;
        private const int MaxRichLogChars = 120000;
        private const int RichLogTrimChars = 30000;
        private TabPage _historyBacktestTabPage;
        private Panel _historyBacktestChartPanel;
        private Label _historyBacktestSummaryLabel;
        private Button _historyBacktestButton;
        private HScrollBar _historyBacktestScrollBar;
        private int _historyBacktestVisibleCount = 60;
        private int _historyBacktestStartIndex = 0;
        private bool _historyBacktestDragging = false;
        private int _historyBacktestDragStartX = 0;
        private int _historyBacktestDragStartIndex = 0;

        private readonly LoginResult _login;
        private DateTime _expireTimer;
        public Form1( LoginResult login)
        {
            InitializeComponent();
            ConfigureUiPerformance();
            if (login == null)
            {
                Application.Exit();
            }
            _login = login;
            this.Text = _login.ExpireAt.ToString() + "    [" + _login.banben + "]";
            cboAccount.Text = _login.Username;
            cboAccount.Enabled = false;

           // Text = $"已登录：{_login.Username}";
        }
        //private StrategyContext _ctx = new StrategyContext();
        // 🔥 正在运行的策略实例
        private readonly List<StrategyContext> _strategyList = new List<StrategyContext>();
        // 是否立即启动（true=立即做多/做空，false=等待信号）
        private bool ImmediateBuyEnabled = true;

        // 🔥 连续编号
        private int _strategySeq = 1;


        private static readonly HttpClient httpClient;

        private string currentKey = "";
        private string mqttHost = "";
        private int mqttPort = 0;
        private string mqttUsername = "";
        private string token = "";

        private IMqttClient mqttClient;


        private IMqttClient tradeClient;
        // 防止同一根K重复生成（用K线时间做key）
        private DateTime? _lastSpawnKlineTime = null;

        // 并发上限（建议先小点，防止刷屏刷单）
        private int MaxInstances = 3;

        private WebSocketSharp.WebSocket tradeWsSharp;

      //  private AccountStore _accountStore;
        void LogUI(string text)
        {
            txtResult.AppendText($"{DateTime.Now:HH:mm:ss} {text}\r\n");
        }

        private void ConfigureUiPerformance()
        {
            this.DoubleBuffered = true;
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(panelChart, true, null);

            EnsureHistoryBacktestTab();
        }

        private void EnsureHistoryBacktestTab()
        {
            if (_historyBacktestTabPage != null)
                return;

            _historyBacktestTabPage = new TabPage("历史验证");
            _historyBacktestTabPage.BackColor = Color.Black;

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.FromArgb(35, 35, 35)
            };

            _historyBacktestButton = new Button
            {
                Text = "验证全部历史",
                Width = 110,
                Height = 26,
                Left = 8,
                Top = 4
            };
            _historyBacktestButton.Click += BtnRunHistoryBacktest_Click;

            _historyBacktestSummaryLabel = new Label
            {
                AutoSize = false,
                Left = 128,
                Top = 8,
                Width = 700,
                Height = 20,
                ForeColor = Color.White,
                Text = "加载历史后点击验证"
            };

            _historyBacktestChartPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                TabStop = true
            };
            _historyBacktestChartPanel.Paint += HistoryBacktestChartPanel_Paint;
            _historyBacktestChartPanel.MouseDown += HistoryBacktestChartPanel_MouseDown;
            _historyBacktestChartPanel.MouseMove += HistoryBacktestChartPanel_MouseMove;
            _historyBacktestChartPanel.MouseUp += HistoryBacktestChartPanel_MouseUp;
            _historyBacktestChartPanel.MouseWheel += HistoryBacktestChartPanel_MouseWheel;
            _historyBacktestChartPanel.MouseEnter += (s, e) => _historyBacktestChartPanel.Focus();

            _historyBacktestScrollBar = new HScrollBar
            {
                Dock = DockStyle.Bottom,
                Height = 18,
                Minimum = 0,
                Maximum = 0
            };
            _historyBacktestScrollBar.ValueChanged += HistoryBacktestScrollBar_ValueChanged;

            topPanel.Controls.Add(_historyBacktestButton);
            topPanel.Controls.Add(_historyBacktestSummaryLabel);
            _historyBacktestTabPage.Controls.Add(_historyBacktestChartPanel);
            _historyBacktestTabPage.Controls.Add(_historyBacktestScrollBar);
            _historyBacktestTabPage.Controls.Add(topPanel);
            tabControl1.TabPages.Add(_historyBacktestTabPage);
        }

        private DateTime ParseKlineTime2(string stime)
        {
            // 如果 stime 永远是这个格式，最稳
            return DateTime.ParseExact(stime, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }

        private DateTime ParseKlineTime(KlineItem k)
        {
            // 优先用时间戳（最可靠）
            if (k.ktime > 0)
            {
                return DateTimeOffset
                    .FromUnixTimeSeconds(k.ktime)
                    .LocalDateTime;
            }

            // 兜底：用 stime
            if (!string.IsNullOrEmpty(k.stime)
                && DateTime.TryParse(k.stime, out var dt))
            {
                return dt;
            }

            throw new Exception("无法解析 K 线时间");
        }

        private void InitOrderGrid(DataGridView dgv)
        {
            dgv.AutoGenerateColumns = false;
            dgv.AllowUserToAddRows = false;
            dgv.ReadOnly = true;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            dgv.Columns.Clear();

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Time",
                HeaderText = "成交时间",
                DataPropertyName = "TimeStr",
                Width = 140
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ListingNo",
                HeaderText = "挂货编号",
                DataPropertyName = "listing_no",
                Width = 120
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "BreedNo",
                HeaderText = "商品编号",
                DataPropertyName = "breed_no",
                Width = 80
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Type",
                HeaderText = "购销",
                DataPropertyName = "TypeText",
                Width = 60
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Price",
                HeaderText = "价格",
                DataPropertyName = "price",
                Width = 80
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Num",
                HeaderText = "数量",
                DataPropertyName = "num",
                Width = 60
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DealNum",
                HeaderText = "成交数量",
                DataPropertyName = "deal_num",
                Width = 80
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Ocata",
                HeaderText = "定转",
                DataPropertyName = "ocata_name",
                Width = 80
            });

            // 操作按钮
            dgv.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "Action",
                HeaderText = "操作",
                Text = "撤单",
                UseColumnTextForButtonValue = true,
                Width = 60
            });
            // 操作按钮
            dgv.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "Action2",
                HeaderText = "转",
                Text = "转",
                UseColumnTextForButtonValue = true,
                Width = 60
            });
        }



        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked)
            {
                numericUpDown8.Enabled = false;
                numericUpDown9.Enabled = false;
            }
            else
            {
                numericUpDown8.Enabled = true;
                numericUpDown9.Enabled = true;

            }
        }
        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                textBox6.Enabled = false;
                _Scan.z_zhisun = int.Parse(textBox6.Text);
            }
            else
            {
                textBox6.Enabled = true;
            }
        }
        string _clientId = "";
        string _clientId2 = "";
        public byte[] BuildMqttConnect(string token)
        {
            var body = new List<byte>();

            // ===== 固定可变头 =====
            body.AddRange(new byte[]
            {
0x00,0x04,
    0x4D,0x51,0x54,0x54, // "MQTT"
    0x04,               // protocol level 4
    0xC0,               // flags (username + password, clean session = 0)
    0x00,0x5A            // keepalive
            });

            // ===== clientId =====
            _clientId = Guid.NewGuid().ToString();

            body.AddRange(BitConverter.GetBytes((ushort)_clientId.Length).Reverse());
            body.AddRange(Encoding.ASCII.GetBytes(_clientId));

            // ===== username =====
            string username = "mem_"+ user_id.ToString() + "_pc";
            body.AddRange(BitConverter.GetBytes((ushort)username.Length).Reverse());
            body.AddRange(Encoding.ASCII.GetBytes(username));

            // ===== password(token) =====
            byte[] pwd = Encoding.ASCII.GetBytes(token);
            body.AddRange(BitConverter.GetBytes((ushort)pwd.Length).Reverse());
            body.AddRange(pwd);

            // ===== 固定头 =====
            var frame = new List<byte>();
            frame.Add(0x10); // CONNECT

            int remainingLength = body.Count;
            WriteRemainingLength(frame, remainingLength);

            frame.AddRange(body);
            DumpFrame("MQTT CONNECT", frame);
            return frame.ToArray();
        }
        public byte[] BuildMqttConnect2(string token)
        {
            var body = new List<byte>();

            // ===== 固定可变头 =====
            body.AddRange(new byte[]
            {
0x00,0x04,
    0x4D,0x51,0x54,0x54, // "MQTT"
    0x04,               // protocol level 4
    0xC0,               // flags (username + password, clean session = 0)
    0x00,0x5A            // keepalive
            });

            // ===== clientId =====
            _clientId2 = Guid.NewGuid().ToString();

            body.AddRange(BitConverter.GetBytes((ushort)_clientId2.Length).Reverse());
            body.AddRange(Encoding.ASCII.GetBytes(_clientId2));

            // ===== username =====
            string username = "mem_" + user_id.ToString() + "_pc";
            body.AddRange(BitConverter.GetBytes((ushort)username.Length).Reverse());
            body.AddRange(Encoding.ASCII.GetBytes(username));

            // ===== password(token) =====
            byte[] pwd = Encoding.ASCII.GetBytes(token);
            body.AddRange(BitConverter.GetBytes((ushort)pwd.Length).Reverse());
            body.AddRange(pwd);

            // ===== 固定头 =====
            var frame = new List<byte>();
            frame.Add(0x10); // CONNECT

            int remainingLength = body.Count;
            WriteRemainingLength(frame, remainingLength);

            frame.AddRange(body);
            DumpFrame("MQTT CONNECT", frame);
            return frame.ToArray();
        }


        static void DumpFrame(string title, List<byte> frame)
        {
            var bytes = frame.ToArray();

            var sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
                sb.Append(' ');
            }

            Console.WriteLine(
                $"[{title}] len={bytes.Length}\r\n{sb}\r\n"
            );
        }

        static void WriteRemainingLength(List<byte> frame, int length)
        {
            do
            {
                byte digit = (byte)(length % 128);
                length /= 128;
                if (length > 0)
                    digit |= 0x80;
                frame.Add(digit);
            }
            while (length > 0);
        }
        private SslStream _ssl;

        void Send(byte[] payload, string tag = "SEND")
        {
            DumpHex(tag + " PAYLOAD", payload);

            var frame = new List<byte>();
            frame.Add(0x82); // FIN + binary

            int len = payload.Length;
            if (len <= 125)
            {
                frame.Add((byte)(0x80 | len));
            }
            else
            {
                frame.Add((byte)(0x80 | 126));
                frame.Add((byte)(len >> 8));
                frame.Add((byte)(len & 0xFF));
            }

            byte[] mask = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(mask);
            }
            frame.AddRange(mask);

            for (int i = 0; i < payload.Length; i++)
                payload[i] ^= mask[i % 4];

            frame.AddRange(payload);

            _ssl.Write(frame.ToArray());
            _ssl.Flush();

            DumpHex(tag + " WS FRAME", frame.ToArray());
        }
        static void DumpHex(string title, byte[] data)
        {
            var sb = new StringBuilder();
            foreach (var b in data)
                sb.Append(b.ToString("X2")).Append(' ');

            Console.WriteLine($"[{title}] len={data.Length}\n{sb}\n");
        }

        // ======== 构造下单帧 ========
        private byte[] BuildPlaceOrder()
        {
            string json =
                "{\"instruct\":\"place_order\",\"m_id\":"+user_id.ToString()+",\"b_id\":1,\"num\":1," +
                "\"direction\":1,\"oflag\":1,\"price\":1511,\"bond\":1," +
                "\"oid\":0,\"record_type\":1," +
                "\"clientId\":\"f5730aee-249f-4047-b0d2-53bae9ab42f1\"}";

            var list = new List<byte>();
            list.AddRange(new byte[] { 0xBA, 0x01, 0x00, 0x05 });
            list.AddRange(Encoding.ASCII.GetBytes("trade"));
            list.Add(0x2C);
            list.Add(0x36);
            list.AddRange(Encoding.UTF8.GetBytes(json));
            return list.ToArray();
        }

        public static byte[] BuildzhuanOrderPacket
            (
            int mId,
            int b_id,
            int num,
            int oflag,
            int direction,
            int price,
            int bond,

           long oid,
           int record_type,
            string clientId
        )

        {
            // =========================
            // 1️⃣ 构建 JSON
            // =========================
        string json =
                "{"
                + "\"instruct\":\"place_order\","
                + $"\"m_id\":{mId},"
                 + $"\"b_id\":{b_id},"
                   + $"\"num\":{num},"
                     + $"\"oflag\":{oflag},"
                    + $"\"direction\":{direction},"
                      + $"\"price\":{price},"
                         + $"\"bond\":{bond},"

                + $"\"oid\":{oid},"
                 + $"\"record_type\":{record_type},"

                + $"\"clientId\":\"{clientId}\""
                + "}";

            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] topic = Encoding.ASCII.GetBytes("trade");

            var body = new List<byte>();

            // Topic length
            body.Add((byte)(topic.Length >> 8));
            body.Add((byte)(topic.Length & 0xFF));
            body.AddRange(topic);

            // PacketId（QoS1 必须）
            body.Add((byte)(1 >> 8));
            body.Add((byte)(1 & 0xFF));

            body.AddRange(payload);

            var frame = new List<byte>();
            frame.Add(0x32); // PUBLISH QoS1
            WriteRemainingLength(frame, body.Count);
            frame.AddRange(body);

            return frame.ToArray();
        }

        //chedan
        public static byte[] BuildcancelOrderPacket(
              int mId,
          
              long oid ,
              string clientId
          )
        {
            // =========================
            // 1️⃣ 构建 JSON
            // =========================
            string json =
                "{"
                + "\"instruct\":\"cancel_order\","
                + $"\"m_id\":{mId},"
              
              
                + $"\"oid\":[{oid}],"
             
                + $"\"clientId\":\"{clientId}\""
                + "}";

            byte[] jsonBytes = Encoding.ASCII.GetBytes(json);

            // =========================
            // 2️⃣ topic = "trade"
            // =========================
            byte[] topicBytes = Encoding.ASCII.GetBytes("trade");

            // =========================
            // 3️⃣ 组 BA 01 包
            // =========================
            var packet = new List<byte>();

            // 固定头（你抓包验证过）
            packet.Add(0x32);
            packet.Add(0x76);
          //  packet.Add(0x00);

            // topic length
            packet.Add((byte)(topicBytes.Length >> 8));
            packet.Add((byte)(topicBytes.Length & 0xFF));

            // topic
            packet.AddRange(topicBytes);

            // json length（⚠️ 这是关键）
            packet.Add((byte)(jsonBytes.Length >> 8));
            packet.Add((byte)(jsonBytes.Length & 0xFF));

            // json payload
            packet.AddRange(jsonBytes);

            return packet.ToArray();
        }
        public static byte[] BuildPlaceOrderPayload(
    int mId,
    int num,
    int price,
    string clientId,
    int direction = 1,
    int oflag = 1,
    int bId = 1,
    int bond = 1,
    int recordType = 1,
    ushort packetId = 1   // QoS1 必须
)
        {
            string json =
                "{"
                + "\"instruct\":\"place_order\","
                + $"\"m_id\":{mId},"
                + $"\"b_id\":{bId},"
                + $"\"num\":{num},"
                + $"\"direction\":{direction},"
                + $"\"oflag\":{oflag},"
                + $"\"price\":{price},"
                + $"\"bond\":{bond},"
                + "\"oid\":0,"
                + $"\"record_type\":{recordType},"
                + $"\"clientId\":\"{clientId}\""
                + "}";

            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] topic = Encoding.ASCII.GetBytes("trade");

            var body = new List<byte>();

            // Topic length
            body.Add((byte)(topic.Length >> 8));
            body.Add((byte)(topic.Length & 0xFF));
            body.AddRange(topic);

            // PacketId（QoS1 必须）
            body.Add((byte)(packetId >> 8));
            body.Add((byte)(packetId & 0xFF));

            body.AddRange(payload);

            var frame = new List<byte>();
            frame.Add(0x32); // PUBLISH QoS1
            WriteRemainingLength(frame, body.Count);
            frame.AddRange(body);

            return frame.ToArray();
        }

        /// <summary>
        /// 构建 place_order WS 二进制包
        /// </summary>
        public static byte[] BuildPlaceOrderPacket(
                int mId,
                int num,
                int price,
                string clientId,
                int direction = 1,
                int oflag = 1,
                int bId = 1,
                int bond = 1,
                int recordType = 1
            )
            {
                // =========================
                // 1️⃣ 构建 JSON
                // =========================
                string json =
                    "{"
                    + "\"instruct\":\"place_order\","
                    + $"\"m_id\":{mId},"
                    + $"\"b_id\":{bId},"
                    + $"\"num\":{num},"
                    + $"\"direction\":{direction},"
                    + $"\"oflag\":{oflag},"
                    + $"\"price\":{price},"
                    + $"\"bond\":{bond},"
                    + "\"oid\":0,"
                    + $"\"record_type\":{recordType},"
                    + $"\"clientId\":\"{clientId}\""
                    + "}";

                byte[] jsonBytes = Encoding.ASCII.GetBytes(json);

                // =========================
                // 2️⃣ topic = "trade"
                // =========================
                byte[] topicBytes = Encoding.ASCII.GetBytes("trade");

                // =========================
                // 3️⃣ 组 BA 01 包
                // =========================
                var packet = new List<byte>();

            // 固定头（你抓包验证过）
         
            packet.Add(0xBA);
                packet.Add(0x01);

                // topic length
                packet.Add((byte)(topicBytes.Length >> 8));
                packet.Add((byte)(topicBytes.Length & 0xFF));

                // topic
                packet.AddRange(topicBytes);

                // json length（⚠️ 这是关键）
                packet.Add((byte)(jsonBytes.Length >> 8));
                packet.Add((byte)(jsonBytes.Length & 0xFF));

                // json payload
                packet.AddRange(jsonBytes);

                return packet.ToArray();
            }
       public int user_id=-1;
        public class xiadan_ztc
        {
            public int StrategyId = -1;
            public long oid = -1;
            public long listing_no = -1;
            public string msg = "";
            public int gm_price = -1;
            


        }
        RawWsMqttTradeClient client2 = null;// new RawWsMqttTradeClient("47.57.4.140", 8006, "/mqtt");
        private async Task ConnectTradeWS2()
        {
            Console.WriteLine("lian", tiao_xu);

            byte[] mqttConnect = BuildMqttConnect2(token);   // 你已经有
            byte[] placeOrder = BuildPlaceOrder();         // BA 01 下单包
                                                           //  byte[] placeOrder = BuildPlaceOrderPacket(36025, 1, 1501, _clientId, 1, 1, 1, 1, 1);
                                                           //     (
                                                           //    int mId,
                                                           //    int num,
                                                           //    int price,
                                                           //    string clientId,
                                                           //    int direction = 1,
                                                           //    int oflag = 1,
                                                           //    int bId = 1,
                                                           //    int bond = 1,
                                                           //    int recordType = 1
                                                           //)


            client2 = new RawWsMqttTradeClient("47.57.4.140", 8005, "/mqtt", user_id, _clientId2, mqttConnect);//_clientId,

            _orderService = new OrderService(client2);
            client2.OnUserNotify += msg =>
            {
                BeginInvoke(new Action(() =>
                {
                    if (msg.Contains("重连"))
                    {
                        //client.Dispose();
                       // ConnectTradeWS();
                    }
                    if (msg.Contains("tiao"))
                    {
                        tiao_xu = 0;
                    }
                    Console.WriteLine("jieshoudao:" + msg);
                }));
            };

            _orderService.OnUserNotify += msg =>
            {
                BeginInvoke(new Action(() =>
                {
                    JObject obj;
                    Console.WriteLine("??111");
                    //  txtHex.AppendText(msg);
                    //MessageBox.Show(msg, "提示");
                }));
            };

            _orderService.OnOrderStatus += (oid, status, msg) =>
            {
                BeginInvoke(new Action(() =>
                {
                    UpdateOrderRow(oid, status);
                }));
            };
            //_orderService.OnDisconnected += async reason =>
            //{
            //    BeginInvoke(new Action(() =>
            //    {
            //        AppendLog("WS 断开：" + reason);
            //    }));

            //    await ScheduleReconnectAsync();
            //};

            //_orderService.OnFundUpdate += fund =>
            //{
            //    BeginInvoke(new Action(() =>
            //    {
            //        lblBalance.Text = fund.Balance.ToString("0.00");
            //        lblFreeze.Text = fund.Freeze.ToString("0.00");
            //        lblBond.Text = fund.Bond.ToString("0.00");
            //    }));
            //};

            await client2.ConnectAsync(mqttConnect);


            return;


        }

        int tiao_xu = 0;
        xiadan_ztc _Xdzt = new xiadan_ztc();
        RawWsMqttTradeClient client = null;// new RawWsMqttTradeClient("47.57.4.140", 8006, "/mqtt");

        private async Task ConnectTradeWS()
        {
            Console.WriteLine("lian1:"+ tiao_xu.ToString());
           

            byte[] mqttConnect = BuildMqttConnect(token);   // 你已经有
                                                         byte[] placeOrder = BuildPlaceOrder();         // BA 01 下单包
                                                                                                        //  byte[] placeOrder = BuildPlaceOrderPacket(36025, 1, 1501, _clientId, 1, 1, 1, 1, 1);
                                                                                                        //     (
                                                                                                        //    int mId,
                                                                                                        //    int num,
                                                                                                        //    int price,
                                                                                                        //    string clientId,
                                                                                                        //    int direction = 1,
                                                                                                        //    int oflag = 1,
                                                                                                        //    int bId = 1,
                                                                                                        //    int bond = 1,
                                                                                                        //    int recordType = 1
                                                                                                        //)


            client = new RawWsMqttTradeClient("47.76.96.34", 8006, "/mqtt", user_id, _clientId, mqttConnect);//_clientId,

            _orderService = new OrderService(client);
            //client.OnUserNotify += msg =>
            //{
            //    BeginInvoke(new Action(() =>
            //    {
            //        if (msg.Contains("重连"))
            //        {
            //            client.Dispose();
            //            ConnectTradeWS();
            //        }
            //        if (msg.Contains("tiao")) 
            //        {
            //            tiao_xu = 0;
            //        }
            //        Console.WriteLine("jieshoudao:" + msg);
            //    }));
            //};

            _orderService.OnUserNotify += msg =>
            {
                BeginInvoke(new Action(() =>
                {
                    JObject obj;
                    try
                    {
                        obj = JObject.Parse(msg);
                        Console.WriteLine("msg:" + obj);
                        int code= obj["code"]?.Value<int>() ?? -1;
                        if (code > 0)
                        {
                            if (code == 1000)
                            {
                                string msg2 = obj["msg"]?.ToString();
                                long oid= obj["oid"]?.Value<long>() ?? -1;
                                int m_id = obj["m_id"]?.Value<int>() ?? -1;
                                if (msg2 == "撤单指令发送成功")
                                {
                                    //foreach (StrategyContext n_sc in _strategyList)
                                    //{
                                    //    if (n_sc.StrategyId == xd_bianhao)
                                    //    {
                                    //        if (n_sc.buzhou == 9)
                                    //        {
                                    //            n_sc.buzhou = 10;
                                    //        }
                                    //        if (n_sc.buzhou == 7)
                                    //        {
                                    //            n_sc.buzhou = 4;
                                    //        }

                                    //        xd_bianhao = -1;
                                    //        break;
                                    //    }
                                    //}

                                        }
                                if (msg2 == "下单成功")
                                {
                                  //  if (m_id == user_id)
                                    if (m_id == user_id)
                                        {
                                        bool find = true;
                                        foreach(StrategyContext n_sc in _strategyList)
                                        {
                                            double diff = (DateTime.Now - n_sc.OrderSendTime).TotalSeconds;
                                            Console.WriteLine("dd "+ diff.ToString());
                                            if (diff<4)
                                            {
                                                if (n_sc.buzhou == 1 && n_sc.Oid<1)
                                                {
                                                  n_sc.buzhou = 2;
                                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]"+n_sc.maijia.ToString()+" "+ "订货下单成功 oid:" + oid);
                                                 
                                                    n_sc.Oid = oid;
                                                    n_sc.shitou = true;
                                                    find = false;
                                                    break;
                                                }
                                                if (n_sc.buzhou == 4 && n_sc.oid2 < 1)
                                                {
                                                    n_sc.buzhou = 5;
                                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]"+n_sc.maijia.ToString()+" "  + "转货下单成功 oid2:" + oid);
                                                    xd_bianhao = -1;
                                                    n_sc.oid2 = oid;
                                                    n_sc.shitou = true;
                                                    find = false;
                                                    break;
                                                }

                                                //

                                             //   break;
                                            }


                                        }
                                        if (find)
                                        {
                                            AppendLog("【有手工操作下单】");
                                        }
                                        _shua6 = true;
                                        //   LoadOrdersAsync();
                                    }

                                  
                                  //  txtResult.AppendText(msg+"\r\n");
                                }
                               
                            }
                            if (code == 1002)
                            {
                                string msg2 = obj["msg"]?.ToString();
                                long oid = obj["oid"]?.Value<long>() ?? -1;
                                string instruct = obj["instruct"]?.Value<string>() ?? "";

                                int m_id = obj["m_id"]?.Value<int>() ?? -1;
                                if (m_id == user_id  && instruct == "order_details" && msg2 == "成交明细")
                                {
                                    foreach (StrategyContext n_sc in _strategyList)
                                    {
                                        if (n_sc.Oid == oid )
                                        {
                                            //
                                            //    AppendLog("编号：" + n_sc.StrategyId.ToString() + " 成交明细。" );
                                           // if (n_sc.buzhou == 2)
                                            {
                                              
                                              //  long desc_oid = obj["desc_oid"]?.Value<long>() ?? 0;
                                               // Console.WriteLine("--------------" + desc_oid.ToString());
                                               
                                                if(n_sc.buzhou==2)
                                                AppendLog("[" + n_sc.StrategyId.ToString() + "]"+n_sc.maijia.ToString()+" 订货成交。");
                                                n_sc.buzhou = 3;
                                                //n_sc.listing_no = desc_oid;
                                                n_sc.xiacheng_time = DateTime.Now;
                                               // LoadOrdersAsync2();

                                            }
                                          

                                            break;

                                        }
                                        //if (n_sc.oid2 == oid)
                                        //{
                                        //    //
                                        //    //    AppendLog("编号：" + n_sc.StrategyId.ToString() + " 成交明细。" );
                                        //    if (n_sc.buzhou == 5)
                                        //    {
                                        //        n_sc.buzhou = 4;
                                        //        xd_bianhao = -1;
                                        //        n_sc.xiacheng_time = DateTime.Now;
                                        //        LoadOrdersAsync2();

                                        //    }


                                        //    break;

                                        //}


                                    }
                                }
                          
                            }
                            //if (code == 1000)
                            //{
                            //    string msg2 = obj["msg"]?.ToString();
                            //    txtResult.AppendText(msg2 + "\r\n");
                            //}
                            if (code == 1003)
                            {
                                string msg2 = obj["msg"]?.ToString();
                                int oid = obj["oid"]?.Value<int>() ?? -1;
                                int m_id = obj["m_id"]?.Value<int>() ?? -1;
                                if (m_id == user_id)
                                {
                                    if (msg2 == "撤单成功")
                                    {
                                        bool find = true;
                                        foreach (StrategyContext n_sc in _strategyList)
                                        {
                                            if (n_sc.Oid == oid)
                                            {

                                                n_sc.buzhou = 11;
                                                AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " 订货 " + msg2);
                                                find = false;
                                                break;
                                            }
                                            if (n_sc.oid2 == oid)
                                            {

                                                n_sc.buzhou = 3;
                                                AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " 转货 " + msg2);
                                                find = false;
                                                break;
                                            }
                                        }
                                        if (find)
                                        {
                                            AppendLog("[有手工操作撤单]");
                                        }

                                        }
                                    if (msg2 == "全部成交")
                                    {
                                        foreach (StrategyContext n_sc in _strategyList)
                                        {

                                         
                                                if (n_sc.oid2 == oid)
                                                {
                                                  
                                                        AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " 完成全部流程。");
                                                        n_sc.buzhou = 8;


                                                    break;

                                                }
                                            if (n_sc.Oid == oid)
                                            {
                                               
                                                if(n_sc.buzhou==2)
                                                    AppendLog("[" + n_sc.StrategyId.ToString() + "]" + n_sc.maijia.ToString() + " 订货成交。");
                                                    n_sc.buzhou = 3;


                                                break;

                                            }


                                        }
                                        _shua5 = true;
                                      //  LoadOrdersAsync2();
                                    }
                                      
                                  //  txtResult.AppendText(msg2+" "+oid.ToString() + "\r\n");
                                }
                             
                            }

                        }
                    }
                    catch
                    {
                        if (msg.Contains("重连"))
                        {
                            client.Dispose();
                            ConnectTradeWS();
                        }
                        Console.WriteLine("jieshoudao:" + msg);

                        // return;
                    }
                 
                  //  txtHex.AppendText(msg);
                    //MessageBox.Show(msg, "提示");
                }));
            };

            _orderService.OnOrderStatus += (oid, status, msg) =>
            {
                BeginInvoke(new Action(() =>
                {
                    UpdateOrderRow(oid, status);
                }));
            };
            //_orderService.OnDisconnected += async reason =>
            //{
            //    BeginInvoke(new Action(() =>
            //    {
            //        AppendLog("WS 断开：" + reason);
            //    }));

            //    await ScheduleReconnectAsync();
            //};

            _orderService.OnFundUpdate += fund =>
            {
                BeginInvoke(new Action(() =>
                {
                    lblBalance.Text = fund.Balance.ToString("0.00");
                    lblFreeze.Text = fund.Freeze.ToString("0.00");
                    lblBond.Text = fund.Bond.ToString("0.00");
                }));
            };
       
            await client.ConnectAsync(mqttConnect);
     

            return;
           

        }


        // 当前窗口显示的 K 线数量（滚轮缩放用）
        private int visibleCount = 100;
        private int startIndex = 0;

        // 十字线
        private bool showCross = false;
        private float mouseX = -1;
        private float mouseY = -1;

        // 成交量区高度
        private int volumeZoneHeight = 60;

        // 实时构建 1 分钟 K 线
        private int klinePeriodSeconds = 60;
        private KlineItem buildingKline = null;

        private readonly List<BacktestMarker> _backtestMarkers = new List<BacktestMarker>();
        private double _backtestTotalProfit = 0;
        private int _backtestClosedCount = 0;
        private bool _backtestHasOpenPosition = false;

        // 当前窗口价格范围（所有 PriceToY 都用它）
        private double _viewMaxPrice;
        private double _viewMinPrice;


        // === 实盘策略控制 ===
        private bool liveStrategyRunning = false;
        private int liveOrderId = 0;

        private List<Trade> liveOpenTrades = new List<Trade>();    // 未平仓订单
        private List<Trade> liveClosedTrades = new List<Trade>();  // 已平仓订单

        // 用户设置（UI 输入）
        private double liveTakeProfit = 2;   // 止盈
        private double liveStopLoss = -3;    // 止损
        private string liveMode = "signal";  // signal=信号开仓，immediate=立即开仓
        private int liveLots = 1;            // 手数


        // K 线数据
        private List<KlineItem> klineList = new List<KlineItem>();

        // ================= HttpClient =================
       static Form1()
        {
         //   InitializeComponent();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            httpClient = new HttpClient(handler);

            httpClient.DefaultRequestHeaders.Add("X-Version", "ASC1.0.13");
            httpClient.DefaultRequestHeaders.Add("X-Client", "PC");
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) UniAgri/1.0.13 Chrome/108 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            httpClient.DefaultRequestHeaders.Add("Origin", "app://.");
        }

        public Form1()
        {
            InitializeComponent();

            // 减少闪烁
            ConfigureUiPerformance();

            btnCaptcha.Click += btnCaptcha_Click;
         //   btnLogin.Click += btnLogin_Click;
            //button2.Click += button2_Click;


            panelChart.Paint += panelChart_Paint;
            panelChart.MouseMove += panelChart_MouseMove;
            panelChart.MouseLeave += panelChart_MouseLeave;
            panelChart.MouseWheel += panelChart_MouseWheel;

            // AutoLoginIfSaved();
        }

        // ================= 自动登录 =================
        private async void AutoLoginIfSaved()
        {
            if (string.IsNullOrEmpty(Properties.Settings.Default.Token))
                return;

            token = Properties.Settings.Default.Token;
            mqttHost = Properties.Settings.Default.MqttHost;
            mqttPort = Properties.Settings.Default.MqttPort;
            mqttUsername = Properties.Settings.Default.MqttUsername;

            txtResult.AppendText("检测到已保存的登录信息，正在自动连接...\r\n");
            await AutoLoadKlineAfterLogin();
            await ConnectMQTT();

        }

        private async Task AutoLoadKlineAfterLogin()
        {
            klineList = await GetKlineAsync("1", "1");
            txtResult.AppendText($"[自动加载] 共加载 K 线 {klineList.Count} 根。\r\n");

            if (klineList.Count > 0)
            {

                // klineList.RemoveAt(klineList.Count - 1);
                visibleCount = Math.Min(18, klineList.Count);
                startIndex = Math.Max(0, klineList.Count - visibleCount);
            }
            this.BeginInvoke(new Action(() =>
            {
                panelChart.Invalidate();
            }));
         
        }
        private void FixBuildingKlineFromHistory()
        {
            if (klineList == null || klineList.Count == 0) return;

            var last = klineList.Last();

            DateTime lastDt = UnixToDateTime(last.ktime);
            DateTime now = DateTime.Now;

            // 历史最后一根 K 与当前时间在同一分钟 → 属于未完成的K线
            if (now.Hour == lastDt.Hour && now.Minute == lastDt.Minute)
            {
                // 移除历史最后一根，作为实时 kline 继续构建
                buildingKline = last;

            }
        }
        private DateTime UnixToDateTime(long ts)
        {
            return DateTimeOffset.FromUnixTimeSeconds(ts)
                .ToLocalTime()
                .DateTime;
        }
        /// <summary>
        /// 从历史数据中接管最后一根K线作为 buildingKline，
        /// 保证图上最后一根是“正在走的那根”
        /// </summary>
        private void TakeLastKlineAsBuilding()
        {
            // 清掉旧的 buildingKline，避免残留
            buildingKline = null;

            if (klineList == null || klineList.Count == 0) return;

            // 取出最后一根，作为正在构建的K线
            buildingKline = klineList[klineList.Count - 1];
            klineList.RemoveAt(klineList.Count - 1);
        }




        // ================= 鼠标缩放 =================
        private void panelChart_MouseWheel(object sender, MouseEventArgs e)
        {
            if (klineList == null || klineList.Count == 0) return;

            int oldVisible = visibleCount;
            int maxVisible = klineList.Count;

            if (e.Delta > 0)
                visibleCount = Math.Max(20, visibleCount - 8);    // 放大
            else
                visibleCount = Math.Min(maxVisible, visibleCount + 8); // 缩小

            // 以当前窗口中心为缩放中心（简单一点）
            int centerIndex = startIndex + oldVisible / 2;
            startIndex = centerIndex - visibleCount / 2;

            if (startIndex < 0) startIndex = 0;
            if (startIndex + visibleCount > maxVisible)
                startIndex = Math.Max(0, maxVisible - visibleCount);

            panelChart.Invalidate();
        }

        // ================= 验证码 =================
        private async void btnCaptcha_Click(object sender, EventArgs e)
        {
            btnCaptcha.Enabled = false;
            btnCaptcha.Text = "获取中...";

            try
            {
                await LoadCaptchaAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("请求验证码失败：" + ex.Message);
            }
            finally
            {
                btnCaptcha.Enabled = true;
                btnCaptcha.Text = "获取验证码";
            }
        }

        private async Task LoadCaptchaAsync()
        {
            var req = new HttpRequestMessage(HttpMethod.Post,
              "https://47.76.96.34:8002/captcha")
            //  "https://47.57.4.140:8002/captcha")
            {
                Content = new ByteArrayContent(Array.Empty<byte>())
            };

            var resp = await httpClient.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            string json = await resp.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<CaptchaResponse>(json);

            currentKey = obj.data.key;
            lblKey.Text = "Key: " + currentKey;

            string base64 = obj.data.base64.Replace("data:image/jpeg;base64,", "");
            byte[] bytes = Convert.FromBase64String(base64);

            using (var ms = new MemoryStream(bytes))
            {
                picCaptcha.Image = Image.FromStream(ms);
            }
        }
        bool _shizhan_f = true;
        // ================= 登录 =================
        private async void btnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(cboAccount.Text) ||
                string.IsNullOrEmpty(txtPass.Text) ||
                string.IsNullOrEmpty(txtCode.Text) ||
                string.IsNullOrEmpty(currentKey))
            {
                MessageBox.Show("请填写账号、密码、验证码并获取验证码 key。");
                return;
            }
            btnLogin.Enabled = false;
            try
            {
                await DoLoginAsync();
            }
            finally
            {
                btnLogin.Enabled = true;
            }
         
        }
        // 初始化历史 K线 → 填充到 BindingList<HistoryRow>
        private async void InitHistoryFromKlineList()
        {
            if (klineList == null || klineList.Count == 0)
                return;

            historyTable.Clear();   // BindingList 会自动刷新 UI

            var indicators = CalcJinMayi(klineList);

            // 只显示最近 50 条
            int take = Math.Min(50, klineList.Count);
            int start = klineList.Count - take;

            for (int i = klineList.Count - 1; i >= start; i--)
            {
                var k = klineList[i];
                var ind = indicators[i];

                string sig = "-";
                if (ind.BuySignal)
                    sig = "做多";
                if (ind.SellSignal)
                    sig = "做空";
               

                historyTable.Insert(0,new HistoryRow
                {
                    Time = k.stime.Substring(11, 5),
                    Price = k.close,
                    Signal = sig,
                    Volume = k.vol,
                    close = k.close,
                    open = k.open,
                    high = k.high,
                    low = k.low
                });
            }
        }
        // 交易连接信息（来自 login 返回的 push）
        string tradeHost;
        int tradePort;
        string tradeUsername;
        private async Task ConnectTradeMQTTAsync()
        {
            var factory = new MqttFactory();
            tradeClient = factory.CreateMqttClient();

            tradeClient.ApplicationMessageReceivedAsync += e =>
            {
                var payload = e.ApplicationMessage.Payload;
                string hex = BitConverter.ToString(payload);

                this.BeginInvoke(new Action(() =>
                {
                    txtResult.AppendText(
                        $"[交易WS收到]\r\nTopic: {e.ApplicationMessage.Topic}\r\nHEX: {hex}\r\n\r\n"
                    );
                }));

                return Task.CompletedTask;
            };

            var options = new MqttClientOptionsBuilder()
                .WithClientId(tradeUsername)     // 可随意，但别重复
                .WithWebSocketServer(o =>
                {
                    o.WithUri($"wss://{tradeHost}:{tradePort}/mqtt");
                })
                .WithCredentials(tradeUsername, token)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                .Build();

            await tradeClient.ConnectAsync(options);

            // ===== 关键：发送交易认证包 =====
            string clientId = Guid.NewGuid().ToString();

            byte[] authPacket = BuildTradeAuthPacket(
                clientId,
                tradeUsername,
                token
            );

            await tradeClient.PublishAsync(new MqttApplicationMessage
            {
                Topic = "trade",
                Payload = authPacket,
                QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
            });

            this.BeginInvoke(new Action(() =>
            {
                txtResult.AppendText("✅ 交易 MQTT（8006）认证已发送\r\n");
            }));
        }

        private async Task DoLoginAsync()
        {
            if (string.IsNullOrEmpty(token))
            {
                var bodyObj = new
                {
                    username = _login.Username,
                    password = txtPass.Text,
                    key = currentKey,
                    code = txtCode.Text,
                    mark = "pc"
                };

                var req = new HttpRequestMessage(HttpMethod.Post,
                    "https://47.76.96.34:8002/login")
                {
                    Content = new StringContent(
                        JsonConvert.SerializeObject(bodyObj),
                        Encoding.UTF8,
                        "application/json")
                };

                HttpResponseMessage resp;
                string json;

                try
                {
                    resp = await httpClient.SendAsync(req);
                    json = await resp.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("网络请求失败：\n" + ex.Message);
                    return;
                }
               // txtResult.Text = json;
                LoginResponse obj;

                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        FloatParseHandling = FloatParseHandling.Double
                    };

                    obj = JsonConvert.DeserializeObject<LoginResponse>(json, settings);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("登录返回解析失败：\n" + ex.Message + "\n\n原始数据：\n" + json);
                    return;
                }

                if (obj == null)
                {
                    MessageBox.Show("登录返回为空");
                    return;
                }

                if (obj.code != 0)
                {
                    MessageBox.Show("登录失败：" + obj.msg);
                    return;
                }
                tradeHost = obj.data.push.host;
                tradePort = obj.data.push.port;   // 8006
                tradeUsername = obj.data.push.username;

                user_id = obj.data.id;
                token = obj.data.token;
                mqttHost = obj.data.quotemqtt.host;
                mqttPort = obj.data.quotemqtt.port;
                mqttUsername = obj.data.quotemqtt.username;
                lblBalance.Text = obj.data.balance.balance.ToString("0.00");
            
                lblFreeze.Text = obj.data.balance.freeze.ToString("0.00");
                Properties.Settings.Default.Token = token;
                Properties.Settings.Default.MqttHost = mqttHost;
                Properties.Settings.Default.MqttPort = mqttPort;
                Properties.Settings.Default.MqttUsername = mqttUsername;
                Properties.Settings.Default.Save();
                //txtStrategyResult.Clear();
                AppendLog("登录成功，加载行情中。。。！");
                groupBox2.Enabled = false;
                button2.Enabled = true;
                splitContainer2.Panel1Collapsed = true;
                _lblStatus.Text ="登录成功！";
                timer3.Enabled = true;
                _lblAccount.Text = "账号："+cboAccount.Text;
                tabPage2.Select();
               // MainAccountStoreHelper.Upsert(cboAccount.Text.Trim(), txtPass.Text.Trim());
                // OnLoginSuccess(cboAccount.Text.Trim());
            }
            _orderQueryService = new OrderQueryService(token);
            // await LoadUserInfoAsync();

            klineList = await GetKlineAsync("1", "1");

            // 用最新K线初始化
           InitHistoryFromKlineList();
           // AppendLog("K线初始化完成");
           ConnectTradeWS(); // 8006
                             //  ConnectTradeWS2();
                             //AppendLog("初始化1");
            await ConnectMQTT();
                             //AppendLog("初始化2");
                             // 接管最后一根K线为 buildingKline
                             // TakeLastKlineAsBuilding();
                             // === 把历史 K 线全部写入表格 ===
                             // === 用金蚂蚁指标计算全量信号 ===
            var indicators = CalcJinMayi(klineList);

            // === 只显示最近 20 条 ===
            historyTable.Clear();

            //  int start = Math.Max(0, klineList.Count - 20);
            for (int i = 1; i < klineList.Count; i++)
            {
                var k = klineList[i];
                var ind = indicators[i];

                string signal = "-";
                if (ind.BuySignal) signal = "做多";
                if (ind.SellSignal) signal = "做空";
                if ((klineList.Count - i) < 20)
                {
                    if (ind.BuySignal)
                    {
                        if (label37.Text == "")
                        {
                            label37.Text = "金蚂蚁初始化结果【做多】";
                            cbStartMode.SelectedIndex = 0;
                        }
                    }
                    if (ind.SellSignal)
                    {
                        if (label37.Text == "")
                        {
                            label37.Text = "金蚂蚁初始化结果【做空】";
                            cbStartMode.SelectedIndex = 1;

                        }
                    }
                }
                

                // 直接将数据添加到 BindingList 中（UI 会自动刷新）
                historyTable.Insert(0, new HistoryRow
                {
                    Time = k.stime.Substring(11, 5),
                    Price = k.close,
                    Signal = signal,
                    Volume = k.vol,
                    close = k.close,
                    open = k.open,
                    high = k.high,
                    low = k.low
                });
            }
            //  RefreshHistoryTable();



            if (klineList.Count > 0)
            {
                visibleCount = Math.Min(20, klineList.Count);
                startIndex = Math.Max(0, klineList.Count - visibleCount);
            }
            this.BeginInvoke(new Action(() =>
            {
                panelChart.Invalidate();
            }));
        }

      

private byte[] BuildTradeAuthPacket(
    string clientId,
    string username,
    string token
)
    {
        var list = new List<byte>();

        // ===== 固定头 =====
        list.Add(0xC6);
        list.Add(0x02);
        list.Add(0x00);
        list.Add(0x04);

        // "MQTT"
        list.AddRange(Encoding.ASCII.GetBytes("MQTT"));

        list.Add(0x04);     // Level
        list.Add(0xC0);     // Flags

        // KeepAlive = 90s
        list.Add(0x00);
        list.Add(0x5A);

        // ===== clientId =====
        var clientIdBytes = Encoding.ASCII.GetBytes(clientId);
        list.Add(0x00);
        list.Add((byte)clientIdBytes.Length);
        list.AddRange(clientIdBytes);

        // ===== username =====
        var userBytes = Encoding.ASCII.GetBytes(username);
        list.Add(0x00);
        list.Add((byte)userBytes.Length);
        list.AddRange(userBytes);

        // ===== token =====
        var tokenBytes = Encoding.ASCII.GetBytes(token);
        list.Add(0x01);     // token 类型
        list.Add((byte)tokenBytes.Length);
        list.AddRange(tokenBytes);

        return list.ToArray();
    }
        private MqttClientOptions mqttOptions;
        private bool isReconnecting = false;
        private bool isManualClose = false;
        // ================= MQTT =================
        private async Task ConnectMQTT()
        {
            if (mqttClient != null && mqttClient.IsConnected)
            {
                txtResult.AppendText("MQTT 已连接，忽略重复连接请求。\r\n");
                return;
            }


            if (string.IsNullOrEmpty(mqttHost) ||
                string.IsNullOrEmpty(mqttUsername) ||
                string.IsNullOrEmpty(token))
            {
                txtResult.AppendText("MQTT 参数不完整，无法连接。\r\n");
                return;
            }

            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();
            // ===== 连接成功 =====
            mqttClient.ConnectedAsync += async e =>
            {
                this.BeginInvoke(new Action(() =>
                {
                    txtResult.AppendText("MQTT 已连接\r\n");
                }));

                await Task.CompletedTask;
            };

            // ===== 断开连接 =====
            mqttClient.DisconnectedAsync += async e =>
            {
                this.BeginInvoke(new Action(() =>
                {
                    txtResult.AppendText(
                        $"MQTT 已断开: {e.Exception?.Message ?? "服务器关闭"}\r\n"
                    );
                }));

                // 手动关闭时不重连
                if (isManualClose)
                    return;

                // 防止重复重连
                if (isReconnecting)
                    return;

                isReconnecting = true;

                while (!mqttClient.IsConnected)
                {
                    try
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            txtResult.AppendText("正在尝试重连 MQTT...\r\n");
                        }));

                        await Task.Delay(3000);

                        await mqttClient.ConnectAsync(mqttOptions);

                        await mqttClient.SubscribeAsync(
                            "quote/+",
                            MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
                        );

                        this.BeginInvoke(new Action(() =>
                        {
                            txtResult.AppendText("MQTT 重连成功\r\n");
                        }));

                        break;
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            txtResult.AppendText(
                                $"MQTT 重连失败: {ex.Message}\r\n"
                            );
                        }));
                    }
                }

                isReconnecting = false;
            };
            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                try
                {
                    string topic = e.ApplicationMessage.Topic;
                    string payload = e.ApplicationMessage.ConvertPayloadToString();

                   // Console.WriteLine(payload);
                    // ★ 处理 tick（必须要）
                    HandleTick(payload);
                   // Console.WriteLine(topic);
                    // ★ UI 安全输出
                    this.BeginInvoke(new Action(() =>
                    {
                        
                        if (!payload.Contains("match_quote"))
                        {
                            Console.WriteLine(
                        "\r\n====================\r\n" +
                        "[MQTT 收到消息]\r\n" +
                        payload +
                        "\r\n====================\r\n"
                    );
                        }
                    
                    }));
                }
                catch (Exception ex)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        txtResult.AppendText("[MQTT ERROR] " + ex.Message + "\r\n");
                    }));
                }

                return Task.CompletedTask;
            };

            mqttOptions = new MqttClientOptionsBuilder()
                .WithClientId(mqttUsername)
                .WithCredentials(mqttUsername, token)
                .WithProtocolVersion(MqttProtocolVersion.V311)
                .WithWebSocketServer(ws =>
                {
                    ws.Uri = $"wss://{mqttHost}:{mqttPort}/mqtt";
                })
                .Build();

          //  txtResult.AppendText("正在连接行情 MQTT...\r\n");

            try
            {
                await mqttClient.ConnectAsync(mqttOptions);
              //  txtResult.AppendText("MQTT 连接成功！订阅 quote/+...\r\n");
                //   await mqttClient.SubscribeAsync("#");

                await mqttClient.SubscribeAsync("quote/+",  MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
                //  await mqttClient.SubscribeAsync("#", MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
              //  txtResult.AppendText("已订阅#\r\n已订阅所有 topic (#)\r\n");
            }
            catch (Exception ex)
            {
               // txtResult.AppendText("MQTT 连接失败: " + ex.Message + "\r\n");
            }
        }
        public class StrategyResult
        {
            public StrategyContext Context { get; set; }

            public bool TriggerOrder;   // 是否触发下单
     
            public bool Triggered { get; set; }
            public string Action { get; set; }     // BUY / SELL / CLOSE / NONE
            public string Reason { get; set; }     // 触发原因
            public decimal? Price { get; set; }
        }
        int duokong_sig = -1;
        bool new2_qie = false;
        private int GetKlineCountdown(long unixTime)
        {
            long sec = unixTime % 60;
            return (int)(60 - sec);
        }
        // 上一次 UI 刷新时间（全局变量放 class 外）
        DateTime lastUIRefresh = DateTime.MinValue;
        DateTime lastCountdownRefresh = DateTime.MinValue;
        int sheng_shi = -1;
        int _Closeprice = 0;
        int _xiantype = 0;
        int _cs1 = 0;
        int jingguo = 0;
        bool _qingkong = false;
        public enum JinMayiLineType
        {
            None = 0,
            SupportRed,   // 支撑线（红，G 不变）
            PressureBlue  // 压力线（蓝，G 变化）
        }
    

        private JinMayiLineType GetLastJinMayiLineType(
    IList indicators,
    int currentIndex
)
        {
            // 从当前往前找最近一个 G ≠ 0 的点
            for (int i = currentIndex; i >= 0; i--)
            {
                dynamic p = indicators[i];

                if (p.G != 0)
                {
                    if (p.K2 == 1) return JinMayiLineType.PressureBlue;
                    if (p.K2 == -3) return JinMayiLineType.SupportRed;
                    break;
                }
            }

            return JinMayiLineType.None;
        }
        int t_wanzheng = 0;
        int t_yinyang = 0;
        bool t_jiance = true;
        bool new_jrcan = false;
        bool qie = false;
        List<int> new1_fw = new List<int>();
        int new1_dian = 0;
        int new1_fx = 1;
        int n4_shang = 0;
        int n4_cuo = 0;
        bool new8_cuo = false;
        int new8_num = 0;
        bool new8_f = false;
        // ================= Tick → 1 分钟 K =================
        private void HandleTick(string json)
        {
            try
            {
              //  Console.WriteLine(json);
                var t = JsonConvert.DeserializeObject<TickQuote>(json);
                if (t == null || t.instruct != "match_quote")
                    return;

                // === 交易时间过滤 09:00 - 21:00 ===
                DateTime dt = DateTimeOffset.FromUnixTimeSeconds(t.time).LocalDateTime;
                if (dt.Hour < 9 || dt.Hour >= 21)
                    return;

                long tickTime = t.time;
                long candleTime = tickTime - (tickTime % klinePeriodSeconds);

                // === 防止倒退 Tick ===
                if (klineList.Count > 0 && candleTime < klineList.Last().ktime)
                    return;

                // === 倒计时（最多每 200ms 更新一次 UI）===
                if ((DateTime.Now - lastCountdownRefresh).TotalMilliseconds > 200)
                {
                    int leftSec = 60 - (int)(t.time % 60);
                 
                    this.BeginInvoke(new Action(() =>
                    {
                        sheng_shi = leftSec;
                        lblCountdown.Text = $"倒计时：{leftSec}s";
                    }));
                    lastCountdownRefresh = DateTime.Now;
                }

                // ===========================================================
                //                    构建 / 推送 K 线
                // ===========================================================
                if (buildingKline == null || buildingKline.ktime != candleTime)
                {
                    // ------------ K 线收线（上一根 K 完成） ------------
                    if (buildingKline != null)
                    {
                        klineList.Add(buildingKline);
                        if (klineList.Count > 5000)
                            klineList.RemoveAt(0);

                        // === ⚠ 核心优化：指标只在收线时计算一次 ===
                        var indicators = CalcJinMayi(klineList);
                        // === 2️⃣ 判断上一根 K 对应的是红线还是蓝线 ===
                        int idx = indicators.Count - 1;
                        var lineType = GetLastJinMayiLineType(indicators, idx);


                        //AppendLog(
                        //    $"K线收线 {buildingKline.stime} | 金蚂蚁线={lineType}"
                        //);

                       
                        var ind = indicators.Last();

                        string sig = "-";
                         qie = false;
                        if (ind.BuySignal) sig = "做多";
                        if (ind.SellSignal) sig = "做空";
                        if (lineType.ToString() == "PressureBlue")
                        {
                            if (_huaxian == -3)
                            {
                                AppendLog("蓝红 切换");

                                qie = true;
                                if (!_xincelue)
                                    jg_zu.Clear();
                                new_jrcan = false;
                                new_jrcan2 = false;
                                new_jrcan3 = false;
                                t_wanzheng = 0;
                                if (new_jr == 2)
                                {
                                    new3_buzhou = 1;
                                    new3_cishu = _setcuoci - 1;
                                    _newjr_cuoci = 1;

                                }
                                new8_f = false;
                                //shouxu = 0;
                                new8_cuoci = 0;
                            }
                            _huaxian = 1;
                        }
                      

                        if (lineType.ToString() == "SupportRed")
                        {

                            if (_huaxian == 1)
                            {
                                AppendLog("红蓝 切换");
                                if (!_xincelue)
                                    jg_zu.Clear();
                                qie = true;
                                new_jrcan = false;
                                new_jrcan2 = false;
                                new_jrcan3 = false;
                                t_wanzheng = 0;
                                new3_buzhou = 1;
                                // shouxu = 0;
                                if (new_jr == 2)
                                {
                                    new3_buzhou = 1;
                                    new3_cishu = _setcuoci - 1;
                                    _newjr_cuoci = 1;

                                }
                                new8_f = false;
                                new8_cuoci = 0;
                            }
                            _huaxian = -3;
                        }
                      

                        // 更新表格
                        this.BeginInvoke(new Action(() =>
                        {
                            historyTable.Insert(0, new HistoryRow
                            {
                                Time = buildingKline.stime.Substring(11, 5),
                                Price = buildingKline.close,
                                Signal = sig,
                                Volume = buildingKline.vol,
                                close = buildingKline.close,
                                open = buildingKline.open,
                                high = buildingKline.high,
                                low = buildingKline.low
                            });

                            // 控制最大显示数量（比如 200 条）
                            if (historyTable.Count > 200)
                                historyTable.RemoveAt(historyTable.Count - 1);
                        }));
                        var closedKline = buildingKline.Clone();
                        _Closeprice = closedKline.close;
                        _m_mai.Insert(0, 0);
                        if (Math.Abs(closedKline.open - closedKline.close) > 1)
                        {
                            if (closedKline.close > closedKline.open)
                            {
                                _Closeprice = closedKline.open + 1;
                            }
                            if (closedKline.close < closedKline.open)
                            {
                                _Closeprice = closedKline.open - 1;
                            }

                            AppendLog($"上K：开盘：{closedKline.open} 收盘：{closedKline.close}");
                        }
                        jingguo++;
                        _chedanzu.Clear();
                        new_jrcan = false;
                        if ((int)ind.G == closedKline.high || (int)ind.G == closedKline.low)
                        {
                            Console.WriteLine($"ind  { ind.G}-- { closedKline.high }-- { closedKline.low}");
                            if (new_jr == 0 )
                            {

                                // jg_zu.Clear();
                                if (qie)
                                    new_jrcan = false;
                                else
                                {
                                    //  if (!new_jrcan)
                                    {
                                    //    jr_dian.Clear();
                                        AppendLog("线相交！开始介入！");
                                        //   if (xiangjiaoqingkong_f)
                                      //  if (!_xincelue)
                                          //  jg_zu.Clear();
                                        new_jrcan = true;
                                    }

                                }

                            }
                            if(new_jr == 7)
                            {
                                new8_f = true;
                              
                            }
                          
                        }
                        if ((int)ind.G == closedKline.open || (int)ind.G == closedKline.close)
                        {
                            Console.WriteLine($"ind  { ind.G}-- { closedKline.high }-- { closedKline.low}");
                            if (new_jr == 0)
                            {

                                // jg_zu.Clear();
                                if (qie)
                                    new_jrcan2 = false;
                                else
                                {
                                    AppendLog("撞了！");
                                    // if (xiangjiaoqingkong_f)
                                   // jg_zu.Clear();
                                    new_jrcan2 = true;
                                    new_jrcan = true;
                                }


                            }

                        }
                        _quanmai = false;   
                         
                        //_huaxian = _huaxian2;
                        if (liveStrategyRunning)
                        {
                            // 1️⃣ 固定一根“已收线K线”
                           
                            Console.WriteLine("close......", closedKline.stime);

                            if (((closedKline.high - closedKline.low) >= 3) && (closedKline.open != closedKline.close))
                            {
                                t_wanzheng += 1;
                            }

                            _quanmai = false;
                            // bool shifou_wanzheng = false;
                            if (((closedKline.high - closedKline.low) >= 3) && (closedKline.open != closedKline.close))
                            {
                                shifou_wanzheng = true;
                                t_wanzheng += 1;
                            }
                            else
                            {
                                shifou_wanzheng = false;
                            }
                            if (shifou_wanzheng)
                            {
                                //if (shifou_wanzheng)
                                //{
                                //    _dengyifenzhong++;
                                //}
                                _Closeprice = closedKline.close;
                            }
                           

                                _cs1++;
                          
                            // xiance yiqiande

                            bool jr_f = true;
                        



                            foreach (StrategyContext n_sc in _strategyList)
                            {
                                if( n_sc.buzhou<6 && n_sc.maijia == closedKline.close)
                                {
                                    jr_f = false;
                                  
                                }

                            }
                            int yinyang = 0;
                            if ((closedKline.close - closedKline.open) >= 1)
                            {
                            
                                    yinyang = 1;
                            }
                            if (qie)
                            {
                                if (!new2_qie)
                                {
                                    new2_qie = true;
                                   // AppendLog("红蓝");
                                }
                              
                            }
                            if ((closedKline.open - closedKline.close) >= 1)
                            {
                                yinyang = -1;
                            }
                            if (yinyang == 0 || !shifou_wanzheng)
                            {
                                t_yinyang = 0;
                            }
                            else
                            {
                                if (yinyang == 1)
                                {
                                    if (t_yinyang <= 0)
                                    {
                                        t_yinyang = 1;
                                    }
                                    else
                                    {
                                        
                                        t_yinyang += 1;
                                    }
                                }
                                else
                                {
                                    if (t_yinyang >= 0)
                                    {
                                        t_yinyang = -1;
                                    }
                                    else
                                    {

                                        t_yinyang -= 1;
                                    }
                                }
                              
                            }
                            if (t_yinyang != 0)
                            {
                                if (n4_shang == 0)
                                {
                                    n4_shang = t_yinyang;
                                }
                                else
                                {
                                    if (n4_shang > 0)
                                    {
                                        if (t_yinyang > 0)
                                        {
                                            n4_cuo = 0;
                                        }
                                        else
                                        {
                                            n4_cuo++;
                                        }
                                    }
                                    if (n4_shang < 0)
                                    {
                                        if (t_yinyang < 0)
                                        {
                                            n4_cuo = 0;
                                        }
                                        else
                                        {
                                            n4_cuo++;
                                        }
                                    }
                                    n4_shang = t_yinyang;
                                }
                            }
                         
                            if (qie)
                            {
                                t_yinyang = 0;
                                jr_f = false;
                                t_wanzheng = 0;
                                //  AppendLog("多空 切换！");
                            }
                            //if (new_jr != 3)
                            //{
                            //    if (tongxiang_f)
                            //    {
                            //        if (jg_zu.Contains(_Closeprice))
                            //        {
                            //            AppendLog("同方向价格重复，放弃进场");
                            //        }
                            //    }
                            //}
                            AppendLog("更新了");

                            if (((closedKline.high - closedKline.low) >= 3) && (closedKline.open != closedKline.close))
                            {
                                _xiantype = 3;
                            }
                            else
                            {
                                _xiantype = 0;
                                AppendLog("上个K平头,这个K 不挂单！");
                            }

                            t_jiance = true;
                                // 2️⃣ 推进【所有】活跃策略
                                foreach (StrategyContext n_sc in _strategyList)
                            {
                                continue;
                                if (n_sc.buzhou>1)
                                {
                                    continue;
                                }
                                if(qie)
                                if (n_sc.buzhou == 1)
                                {
                                        AppendLog("编号[" + n_sc.StrategyId.ToString() + "] 红蓝切换，重新开始计划");
                                        n_sc.buzhou = 12;

                                    continue;
                                }

                                int passedMinutes =(int)(DateTime.Parse(closedKline.stime) - n_sc.TriggerTime.Value).TotalMinutes;

                                
                                if ((closedKline.close - closedKline.open) >=1)
                                {
                                    yinyang = 1;
                                }
                                if ((closedKline.open - closedKline.close) >= 1)
                                {
                                    yinyang = -1;
                                }

                                if (_xiantype == 0)
                                {
                                    jr_f = false;
                                    AppendLog("平头不介入");
                                    n_sc.buzhou = 11;
                                    continue;
                                }


                                if (true)
                                {

                                    //  AppendLog("编号：" + n_sc.StrategyId.ToString() + " " + "已过 " + passedMinutes+" "+ sheng_shi.ToString());

                                   


                                    if (_Scan.jieru == 0)
                                    {
                                        if (passedMinutes >= _Scan.jr_can)
                                        {
                                            if (jr_f)
                                            {
                                                if (_huaxian == -3)
                                                    n_sc.duokong = 1;// duokong_sig;
                                                else
                                                    n_sc.duokong = 2;
                                                n_sc.closeprice = closedKline.close;
                                                n_sc.buzhou = 2;
                                                AppendLog("编号【" + n_sc.StrategyId.ToString() + "】 " + "已过 " + passedMinutes + "分钟，" + " 达到介入条件，当前价格：" + closedKline.close.ToString());

                                                n_sc.shitou = true;
                                            }
                                            else
                                            {
                                                n_sc.buzhou = 11;
                                                AppendLog("编号【" + n_sc.StrategyId.ToString() + "】" + " 有同价格未完成/未完整k，放弃介入，关闭该计划");

                                            }
                                           
                                          //  _Scan.jieru = 1;
                                        }
                                    }
                                    if (_Scan.jieru == 1)
                                    {
                                        int wz = 0;
                                      if(((closedKline.high- closedKline.low) >= 3)&&(closedKline.open!=closedKline.close))
                                        {
                                            wz = 1;
                                        }
                                        n_sc.wanzheng_k += wz;
                                        int lian = 0;
                                        if (closedKline.close == closedKline.open)
                                        {
                                            n_sc.lianyin = 0;
                                            n_sc.lianyang = 0;
                                        }
                                        if ((closedKline.close - closedKline.open) > 0)
                                        {
                                            n_sc.lianyin = 0;
                                            n_sc.lianyang += 1;
                                        }
                                        if ((closedKline.open- closedKline.close) > 0)
                                        {
                                            n_sc.lianyang= 0;
                                            n_sc.lianyin += 1;
                                        }

                                        if (n_sc.wanzheng_k >= _Scan.jr_can)
                                        {
                                        

                                            if (jr_f)
                                            {
                                                if (_huaxian == -3)
                                                    n_sc.duokong = 1;// duokong_sig;
                                                else
                                                    n_sc.duokong = 2;
                                                n_sc.closeprice = closedKline.close;
                                                n_sc.buzhou = 2;
                                                AppendLog("编号【" + n_sc.StrategyId.ToString() + "】 " + "已过 " + n_sc.wanzheng_k + "完整K，" + " 达到介入条件，当前价格：" + closedKline.close.ToString());

                                                n_sc.shitou = true;
                                            }
                                            else
                                            {
                                                n_sc.buzhou = 11;
                                                AppendLog("编号【" + n_sc.StrategyId.ToString() + "】" + " 有同价格未完成/未完整k，放弃介入，关闭该计划");

                                            }
                                            //  _Scan.jieru = 1;

                                        }

                                    }
                                    if (_Scan.jieru == 2)
                                    {
                                        int bd = 0;
                                        if (closedKline.open!= closedKline.close)
                                        {
                                            bd = 1;
                                        }
                                        n_sc.bodong_k += bd;
                                        if (n_sc.bodong_k >= _Scan.jr_can)
                                        {
                                      

                                            if (jr_f)
                                            {
                                                if (_huaxian == -3)
                                                    n_sc.duokong = 1;// duokong_sig;
                                                else
                                                    n_sc.duokong = 2;
                                                n_sc.buzhou = 2;
                                                AppendLog("编号【" + n_sc.StrategyId.ToString() + "】 " + "已过 " + n_sc.wanzheng_k + "波动K，" + " 达到介入条件，当前价格：" + closedKline.close.ToString());
                                                n_sc.closeprice = closedKline.close;
                                                n_sc.shitou = true;
                                            }
                                            else
                                            {
                                                n_sc.buzhou = 11;
                                                AppendLog("编号【" + n_sc.StrategyId.ToString() + "】" + " 有同价格未完成/未完整k，放弃介入，关闭该计划");

                                            }
                                            //  _Scan.jieru = 1;

                                        }
                                    }
                                    //
                                    if (_Scan.jieru == 3)
                                    {
                                        //
                                        if (yinyang == 1)
                                        {
                                            n_sc.lianyang += 1;
                                            n_sc.lianyin = 0;
                                        }
                                        if (yinyang == -1)
                                        {
                                            n_sc.lianyang = 0;
                                            n_sc.lianyin += 1;
                                        }
                                        if (yinyang == 0)
                                        {
                                            n_sc.lianyang =0;
                                            n_sc.lianyin = 0;
                                        }
                                        if (n_sc.duokong == 1)
                                        {
                                            if (n_sc.lianyin >= _Scan.jr_can)
                                            {
                                            

                                                if (jr_f)
                                                {
                                                    if (_huaxian == -3)
                                                        n_sc.duokong = 1;// duokong_sig;
                                                    else
                                                        n_sc.duokong = 2;
                                                    n_sc.buzhou = 2;
                                                    AppendLog("编号【" + n_sc.StrategyId.ToString() + "】 " + "连阴 " + n_sc.lianyin.ToString() + " 个K，" + " 达到介入条件，当前价格：" + closedKline.close.ToString());
                                                    n_sc.closeprice = closedKline.close;
                                                    n_sc.shitou = true;
                                                }
                                                else
                                                {
                                                    n_sc.buzhou = 11;
                                                    AppendLog("编号【" + n_sc.StrategyId.ToString() + "】" + " 有同价格未完成/未完整k，放弃介入，关闭该计划");

                                                }
                                            }


                                        }
                                        if (n_sc.duokong == 2)
                                        {
                                            if (n_sc.lianyang >= _Scan.jr_can)
                                            {
                                          
                                                if (jr_f)
                                                {
                                                    if (_huaxian == -3)
                                                        n_sc.duokong = 1;// duokong_sig;
                                                    else
                                                        n_sc.duokong = 2;
                                                    n_sc.buzhou = 2;
                                                    AppendLog("编号【" + n_sc.StrategyId.ToString() + "】 " + "连阳 " + n_sc.lianyin.ToString() + " 个K，" + " 达到介入条件，当前价格：" + closedKline.close.ToString());
                                                    n_sc.closeprice = closedKline.close;
                                                    n_sc.shitou = true;
                                                }
                                                else
                                                {
                                                    n_sc.buzhou = 11;
                                                    AppendLog("编号【" + n_sc.StrategyId.ToString() + "】" + " 有同价格未完成/未完整k，放弃介入，关闭该计划");

                                                }
                                            }


                                        }


                                    }


                                }
                                
                                continue;
                                
                                //var result = RunLiveStrategy(ctx, closedKline);
                               

                                //// 3️⃣ UI 展示（只读 result）
                                //this.BeginInvoke(new Action(() =>
                                //{
                                //    txtStrategyResult.AppendText(
                                //        $"[{DateTime.Now:HH:mm:ss}] K线收线 {closedKline.stime}\r\n");

                                //    txtStrategyResult.AppendText(
                                //        $"  ➜ Strategy#{ctx.StrategyId} | Stage={ctx.Stage} | {result.Reason}\r\n\n");
                                //}));

                                //// 4️⃣ 触发下单
                                //if (result.TriggerOrder)
                                //{
                                //    AppendLog($"▶ Strategy#{ctx.StrategyId} 下单：{result.Action} @ {result.Price}");
                                //    // TODO: 接 OrderService
                                //}

                                //// 5️⃣ 已结束的策略 → 移除
                                //if (ctx.Stage == StrategyStage.Finished)
                                //{
                                //    _strategyList.Remove(ctx);
                                //    AppendLog($"⛔ Strategy#{ctx.StrategyId} 已结束，移除");
                                //}
                            }
                            if (ind.BuySignal)
                            {
                                duokong_sig = 1;
                            }
                            if (ind.SellSignal)
                            {
                                duokong_sig = 2;
                            }
                            ///
                                     // jianchaxinde
                             if(false)//(duokong_sig>0)
                           // if (_Scan.chufa <3)
                            {
                                StrategyContext n_sc = new StrategyContext();
                                n_sc.StrategyId = _strategySeq++;
                           
                                n_sc.beishu = _Scan.beishu;
                               

                                AppendLog("启动：[" + n_sc.StrategyId.ToString() + "] " + closedKline.stime);
                                n_sc.buzhou = 1;
                                n_sc.TriggerTime = DateTime.Parse(closedKline.stime);

                                _strategyList.Add(n_sc);


                            }
                            else
                            {


                            }
                        }

                    }

                    // ------------ 创建新 K 线 ------------
                    buildingKline = new KlineItem
                    {
                        ktime = candleTime,
                        stime = UnixToTime(candleTime),
                        open = t.price,
                        high = t.price,
                        low = t.price,
                        close = t.price,
                        vol = t.vol
                    };
                }
                else
                {
                    // ------------ 更新当前 K 线 ------------
                    buildingKline.close = t.price;
                    if (t.price > buildingKline.high) buildingKline.high = t.price;
                    if (t.price < buildingKline.low) buildingKline.low = t.price;
                    buildingKline.vol += t.vol;
                }

                // ===========================================================
                //                    限制 UI 刷新频率
                // ===========================================================
                if ((DateTime.Now - lastUIRefresh).TotalMilliseconds > 200)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        UpdateStatusLabel();
                        panelChart.Invalidate();
                    }));
                    lastUIRefresh = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Tick ERROR: " + ex.Message);
            }
        }
        int new8_cuoci = 0;
        private void AppendLog(string msg)
        {
            if (this.IsDisposed) return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => AppendLog(msg)));
                return;
            }

            //for (int i = 0; i < 30; i++)
            //{
            //    AppendColorText($"下单成功 {i}", Color.Red);
            //    AppendColorText("oid=123456\n", Color.Blue);
            //}

            string line = $"[{DateTime.Now:HH:mm:ss}]";
            AppendColorText(line, Color.Black);
            if (msg.Contains("结果sd"))
            {
                dataGridView7.Rows[0].Cells[3].Value = msg.Replace("结果sd", "");
                return;
            }
            if (msg == "更新了")
            {
                dataGridView7.Rows.Insert(0, new DataGridViewRow());
                dataGridView7.Rows[0].Cells[0].Value = DateTime.Now.ToString("HH:mm");
                string k1 = "";
                if (_huaxian == -3)
                {
                    k1 = "多";
                }
                else
                {
                    k1 = "空";
                }
                dataGridView7.Rows[0].Cells[1].Value = _Closeprice.ToString()+"["+k1+"]";
                if (!shifou_wanzheng)
                {
                    dataGridView7.Rows[0].Cells[2].Value = "不整";
                }
                else
                {
                    if (t_yinyang > 0)
                    {
                        dataGridView7.Rows[0].Cells[2].Value = "阳";
                    }
                    if (t_yinyang < 0)
                    {
                        dataGridView7.Rows[0].Cells[2].Value = "阴";
                    }
                }

                return;
            }
            if (msg.Contains("做多"))
            {
                AppendColorText( $"{ msg}\r\n", Color.Red);
               // AppendColorText($"{ msg}\r\n", Color.Red);
            }
            else
            {
                if (msg.Contains("做空"))
                {
                    AppendColorText($"{ msg}\r\n", Color.Blue);

                }
                else
                {
                    if (msg.Contains("完成全部流程"))
                    {
                        AppendColorText($"{ msg}\r\n", Color.Green);
                    }
                    else
                    AppendColorText($"{ msg}\r\n", Color.Black);
                }
            }
           
           

            //  richTextBox1.AppendText(line);

            // 自动滚动到底部（可选）
            TrimRichLogIfNeeded();
            richTextBox1.SelectionStart = richTextBox1.TextLength;
            richTextBox1.ScrollToCaret();
        }
        int new8_fx = 0;
        private void TrimRichLogIfNeeded()
        {
            if (richTextBox1.TextLength <= MaxRichLogChars)
                return;

            int removeChars = Math.Min(RichLogTrimChars, richTextBox1.TextLength);
            int newlineIndex = richTextBox1.Text.IndexOf('\n', removeChars);
            if (newlineIndex > 0)
                removeChars = newlineIndex + 1;

            richTextBox1.Select(0, removeChars);
            richTextBox1.SelectedText = string.Empty;
        }

        public class LiveStrategyContext
        {
            // ===== 当前状态 =====
            public StrategyStage Stage = StrategyStage.Idle;

            // ===== 启动信息 =====
            public string Direction;           // "buy" / "sell"
            public DateTime TriggerTime;        // 触发时间

            // ===== 固定 N 分钟介入 =====
            public int EntryDelayMinutes = 5;  // N（可由 UI 设置）

            // ===== 调试 / 日志 =====
            public DateTime LastKlineTime;
        }
        private void StartStrategy(string direction)
        {
            var ctx = new StrategyContext
            {
                StrategyId = ++_strategySeq,   // 🔑 新编号
                Stage = StrategyStage.Triggered,
                Direction = direction,
                EntryDelayMinutes = 1,         // 先写死，后面接 UI
                TakeProfit = 1,                // 极小值，方便测试
                StopLoss = 1,
                TriggerTime = DateTime.Now
            };


            _strategyList.Add(ctx);

            AppendLog(
                $"🆕 启动新策略 #{ctx.StrategyId} | 方向={direction} | 等待 {ctx.EntryDelayMinutes} 分钟介入"
            );
        }


         



     




        // ================= 用户信息 =================
        private async Task LoadUserInfoAsync()
        {
            if (string.IsNullOrEmpty(token))
            {
                txtResult.AppendText("未找到 token，无法获取用户信息。\r\n");
                return;
            }

            var url = "https://47.57.4.140:8003/member/getUserInfo";

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent("", Encoding.UTF8, "application/json")
            };

            request.Headers.TryAddWithoutValidation("token", token);

            var response = await httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            txtResult.AppendText("\r\n--- 用户信息 ---\r\n");
            txtResult.AppendText(json + "\r\n");
        }

        // ================= 结构体定义 =================
        public class KlineItem
        {
            public string b_id { get; set; }
            public int open { get; set; }
            public int high { get; set; }
            public int low { get; set; }
            public int close { get; set; }
            public long ktime { get; set; }
            public string stime { get; set; }
            public long vol { get; set; }

            /// <summary>
            /// 冻结一份快照（策略用），避免 buildingKline 后续被行情更新改掉
            /// </summary>
            public KlineItem Clone()
            {
                return new KlineItem
                {
                    b_id = this.b_id,
                    open = this.open,
                    high = this.high,
                    low = this.low,
                    close = this.close,
                    ktime = this.ktime,
                    stime = this.stime,
                    vol = this.vol
                };
            }
        }

        private class BacktestMarker
        {
            public int Index { get; set; }
            public string Text { get; set; }
            public int Price { get; set; }
            public double Profit { get; set; }
            public Color Color { get; set; }
        }


        public class JinMayiItem
        {
            public double G;
            public int K2;           // 1 = 多，-3 = 空，0 = 无
            public double HH;        // N 区间最高
            public double LH;        // N 区间最低
            public bool BuySignal;   // 做多
            public bool SellSignal;  // 做空
        }

        // ================= EMA 工具 =================
        private static double[] EMA(double[] src, int period)
        {
            int n = src.Length;
            double[] ema = new double[n];
            if (n == 0) return ema;

            double k = 2.0 / (period + 1);
            ema[0] = src[0];

            for (int i = 1; i < n; i++)
                ema[i] = k * src[i] + (1 - k) * ema[i - 1];

            return ema;
        }

        /// <summary>
        /// 金蚂蚁指标计算（完全与目标软件一致）
        /// 固定参数 N=1, N1=1, Q=0, Q1=0
        /// </summary>
        public static List<JinMayiItem> CalcJinMayi(List<KlineItem> kl)
        {
            int n = kl.Count;
            var r = new List<JinMayiItem>();
            for (int i = 0; i < n; i++)
                r.Add(new JinMayiItem());

            // ======== 1. HHV(HIGH,N) 和 LLV(LOW,N) ========
            int N = 1;
            int N1 = 1;
            int Q = 0;
            int Q1 = 0;

            double[] HH = new double[n];
            double[] LH = new double[n];

            for (int i = 0; i < n; i++)
            {
                double hv = kl[i].high;
                double lv = kl[i].low;

                for (int j = Math.Max(0, i - (N - 1)); j <= i; j++)
                {
                    hv = Math.Max(hv, kl[j].high);
                    lv = Math.Min(lv, kl[j].low);
                }
                HH[i] = hv;
                LH[i] = lv;
            }

            // ======== 2. H1 / L1 ========
            double[] H1 = new double[n];
            double[] L1 = new double[n];

            for (int i = 1; i < n; i++)
            {
                // --- H1 条件 ---
                bool condH1 =
                    HH[i] < HH[i - 1] &&
                    LH[i] < LH[i - 1] &&
                    kl[i - 1].open > kl[i].close &&
                    kl[i].open > kl[i].close &&
                    ((kl[i].open) - kl[i].close) > Q1;

                if (condH1)
                {
                    int idx = Math.Max(0, i - N1);
                    H1[i] = HH[idx];
                }
                else H1[i] = 0;

                // --- L1 条件 ---
                bool condL1 =
                    LH[i] > LH[i - 1] &&
                    HH[i] > HH[i - 1] &&
                    kl[i - 1].open < kl[i].close &&
                    kl[i].open < kl[i].close &&
                    (kl[i].close - kl[i].open) > Q1;

                if (condL1)
                {
                    int idx = Math.Max(0, i - N1);
                    L1[i] = LH[idx];
                }
                else L1[i] = 0;
            }

            // ======== 3. barslast + ref 计算 bab/cab ========
            double[] bab = new double[n];
            double[] cab = new double[n];

            int lastH = -1, lastL = -1;

            for (int i = 0; i < n; i++)
            {
                if (H1[i] != 0) lastH = i;
                if (L1[i] != 0) lastL = i;

                if (lastH < 0) bab[i] = 0;
                else bab[i] = H1[lastH];

                if (lastL < 0) cab[i] = 0;
                else cab[i] = L1[lastL];
            }

            // ======== 4. K1 ========
            int[] K1 = new int[n];
            for (int i = 0; i < n; i++)
            {
                double close = kl[i].close;
                if (close > bab[i]) K1[i] = -3;   // 压力（空）
                else if (close < cab[i]) K1[i] = 1; // 支撑（多）
                else K1[i] = 0;
            }

            // ======== 5. K2（延续） ========
            int[] K2 = new int[n];
            int lastSig = 0;

            for (int i = 0; i < n; i++)
            {
                if (K1[i] != 0)
                {
                    K2[i] = K1[i];
                    lastSig = K1[i];
                }
                else
                {
                    K2[i] = lastSig;
                }
            }

            // ======== 6. G（绘制阶梯线用） ========
            double[] G = new double[n];
            for (int i = 0; i < n; i++)
            {
                if (K2[i] == 1) G[i] = bab[i];
                else if (K2[i] == -3) G[i] = cab[i];
                else G[i] = 0;
            }
           // Console.WriteLine($"g{ G[G.Count() - 1] }---{G.Count()}");

            // ======== 7. CROSS 信号 ========
            double[] TMP = K2.Select(x => (double)x).ToArray();

            bool[] Buy = new bool[n];
            bool[] Sell = new bool[n];

            // EMA55（用于过滤 ↑↓）
            double[] ema55 = EMA(kl.Select(k => (double)k.close).ToArray(), 55);

            for (int i = 1; i < n; i++)
            {
                double now = TMP[i];
                double prev = TMP[i - 1];

                // CROSS(TMP,0) 卖
                bool crossSell = (prev < 0 && now > 0);

                // CROSS(0,TMP) 买
                bool crossBuy = (prev > 0 && now < 0);

                Sell[i] = crossSell;
                Buy[i] = crossBuy;

                // 是否满足 MA55 条件
                bool buy55 = crossBuy && kl[i].close >= ema55[i];
                bool sell55 = crossSell && kl[i].close <= ema55[i];

                r[i].BuySignal = buy55;
                r[i].SellSignal = sell55;
            }

            // ======== 8. 回填所有结果 ========
            for (int i = 0; i < n; i++)
            {
                r[i].G = G[i];
                r[i].K2 = K2[i];
                r[i].HH = HH[i];
                r[i].LH = LH[i];
            }

            return r;
        }

        // ================= 拉一次历史 K 线 =================
        private async Task<List<KlineItem>> GetKlineAsync(string instrumentId, string klineType = "1")
        {
            var url = "https://47.76.96.34:8004/kline";

            var body = new
            {
                instrument_id = instrumentId,
                kline_type = klineType,
                kline_total = 200,
                query_type = "1"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(body),
                    Encoding.UTF8,
                    "application/json")
            };

            request.Headers.TryAddWithoutValidation("token", token);

            var resp = await httpClient.SendAsync(request);
            var json = await resp.Content.ReadAsStringAsync();

            var list = JsonConvert.DeserializeObject<List<KlineItem>>(json);

            // 过滤掉非 9:00-21:00 的 K 线
            list = list.Where(k =>
            {
                var dt = DateTime.Parse(k.stime);
                return dt.Hour >= 9 && dt.Hour <= 21;
            }).ToList();

            return list;

        }

        private void DebugPrintLastKlines(string tag)
        {
            try
            {
                int count = klineList.Count;

                txtResult.AppendText($"\r\n----- {tag} (最后 5 根历史K线) -----\r\n");

                for (int i = Math.Max(0, count - 5); i < count; i++)
                {
                    var k = klineList[i];
                    txtResult.AppendText($"{i}: {k.stime} (ktime={k.ktime})\r\n");
                }

                if (buildingKline != null)
                {
                    txtResult.AppendText($"[buildingKline]: {buildingKline.stime} (ktime={buildingKline.ktime})\r\n");
                }

                txtResult.AppendText("------------------------------------\r\n\r\n");
            }
            catch { }
        }

        private async void btnLoadKline_Click(object sender, EventArgs e)
        {
            klineList = await GetKlineAsync("1", "1");
            txtResult.Text = $"共加载K线 {klineList.Count} 根。";
            DebugPrintLastKlines("按钮加载");
            // 接管最后一根K线为 buildingKline
            TakeLastKlineAsBuilding();
            // === 把历史 K 线全部写入表格 ===
            //historyTable.Clear();
            //foreach (var k in klineList)
            //{
            //    string signal = "-";
            //    if (k.close > k.open) signal = "多";
            //    else if (k.close < k.open) signal = "空";

            //    historyTable.Add(new HistoryRow
            //    {
            //        Time = k.stime.Substring(11, 5),
            //        Price = k.close,
            //        Signal = signal,
            //        Volume = k.vol
            //    });
            //}

            //RefreshHistoryTable();


            if (klineList.Count > 0)
            {
                //klineList.RemoveAt(klineList.Count - 1);
                visibleCount = Math.Min(18, klineList.Count);
                startIndex = Math.Max(0, klineList.Count - visibleCount);
            }

            panelChart.Invalidate();
        }

        private async void BtnRunHistoryBacktest_Click(object sender, EventArgs e)
        {
            _historyBacktestButton.Enabled = false;
            try
            {
                if (klineList == null || klineList.Count < 2)
                {
                    klineList = await GetKlineAsync("1", "1");
                }

                RunCloseGreaterThanOpenBacktest();
                ResetHistoryBacktestView();
                UpdateHistoryBacktestSummary();
                tabControl1.SelectedTab = _historyBacktestTabPage;
                _historyBacktestChartPanel.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("历史验证失败：" + ex.Message);
            }
            finally
            {
                _historyBacktestButton.Enabled = true;
            }
        }

        private void RunCloseGreaterThanOpenBacktest()
        {
            _backtestMarkers.Clear();
            _backtestTotalProfit = 0;
            _backtestClosedCount = 0;
            _backtestHasOpenPosition = false;

            if (klineList == null || klineList.Count < 2)
                return;

            bool inPosition = false;
            int entryIndex = -1;
            int entryPrice = 0;

            for (int i = 1; i < klineList.Count; i++)
            {
                KlineItem k = klineList[i];

                if (inPosition)
                {
                    bool takeProfit = k.high >= entryPrice + 1;
                    bool stopLoss = k.low <= entryPrice - 1;

                    if (takeProfit || stopLoss)
                    {
                        int exitPrice = takeProfit ? entryPrice + 1 : entryPrice - 1;
                        double profit = exitPrice - entryPrice;
                        _backtestTotalProfit += profit;
                        _backtestClosedCount++;

                        _backtestMarkers.Add(new BacktestMarker
                        {
                            Index = i,
                            Text = "卖",
                            Price = exitPrice,
                            Profit = profit,
                            Color = profit >= 0 ? Color.Lime : Color.Orange
                        });

                        inPosition = false;
                        entryIndex = -1;
                        continue;
                    }
                }

                if (!inPosition && k.close > k.open)
                {
                    inPosition = true;
                    entryIndex = i;
                    entryPrice = k.close;

                    _backtestMarkers.Add(new BacktestMarker
                    {
                        Index = i,
                        Text = "入",
                        Price = k.close,
                        Profit = 0,
                        Color = Color.Yellow
                    });
                }
            }

            _backtestHasOpenPosition = inPosition;
            if (inPosition && entryIndex >= 0)
            {
                KlineItem last = klineList[klineList.Count - 1];
                _backtestMarkers.Add(new BacktestMarker
                {
                    Index = klineList.Count - 1,
                    Text = "持",
                    Price = last.close,
                    Profit = last.close - entryPrice,
                    Color = Color.White
                });
            }

            txtResult.AppendText(
                $"\r\n[回测] close>open 入，盈利1或亏损1卖；平仓 {_backtestClosedCount} 次，总盈亏 {_backtestTotalProfit:0.##}。\r\n");
        }

        private void UpdateHistoryBacktestSummary()
        {
            if (_historyBacktestSummaryLabel == null)
                return;

            _historyBacktestSummaryLabel.Text =
                $"历史K:{(klineList == null ? 0 : klineList.Count)}  显示:{_historyBacktestStartIndex + 1}-{Math.Min(_historyBacktestStartIndex + _historyBacktestVisibleCount, klineList == null ? 0 : klineList.Count)}  平仓:{_backtestClosedCount}  盈亏:{_backtestTotalProfit:0.##}  拖动图表/滚轮缩放" +
                (_backtestHasOpenPosition ? "  持仓中" : "");
            _historyBacktestSummaryLabel.ForeColor = _backtestTotalProfit >= 0 ? Color.Lime : Color.Orange;
        }

        private void ResetHistoryBacktestView()
        {
            int count = klineList == null ? 0 : klineList.Count;
            if (count <= 0)
            {
                _historyBacktestStartIndex = 0;
                UpdateHistoryBacktestScrollBar();
                return;
            }

            _historyBacktestVisibleCount = Math.Min(60, count);
            _historyBacktestStartIndex = Math.Max(0, count - _historyBacktestVisibleCount);
            UpdateHistoryBacktestScrollBar();
        }

        private void UpdateHistoryBacktestScrollBar()
        {
            if (_historyBacktestScrollBar == null)
                return;

            int count = klineList == null ? 0 : klineList.Count;
            int maxStart = Math.Max(0, count - _historyBacktestVisibleCount);
            _historyBacktestStartIndex = Math.Max(0, Math.Min(_historyBacktestStartIndex, maxStart));

            _historyBacktestScrollBar.ValueChanged -= HistoryBacktestScrollBar_ValueChanged;
            _historyBacktestScrollBar.Enabled = maxStart > 0;
            _historyBacktestScrollBar.Minimum = 0;
            _historyBacktestScrollBar.LargeChange = Math.Max(1, Math.Min(_historyBacktestVisibleCount, Math.Max(1, count)));
            _historyBacktestScrollBar.SmallChange = 1;
            _historyBacktestScrollBar.Maximum = maxStart + _historyBacktestScrollBar.LargeChange - 1;
            _historyBacktestScrollBar.Value = _historyBacktestStartIndex;
            _historyBacktestScrollBar.ValueChanged += HistoryBacktestScrollBar_ValueChanged;
        }

        // ================= 价格 → Y =================
        private int PriceToY(double price, Rectangle rect)
        {
            if (_viewMaxPrice <= _viewMinPrice)
                return rect.Bottom;

            double ratio = (price - _viewMinPrice) / (_viewMaxPrice - _viewMinPrice);
            double y = rect.Bottom - ratio * rect.Height;
            return (int)Math.Round(y);
        }

        // ================= Panel Paint =================
        private void panelChart_Paint(object sender, PaintEventArgs e)
        {
            var all = new List<KlineItem>();
            if (klineList != null && klineList.Count > 0) all.AddRange(klineList);
            if (buildingKline != null) all.Add(buildingKline);

            if (all.Count == 0) return;

            int maxVisible = Math.Min(visibleCount, all.Count);
            //   int safeStart = Math.Max(0, Math.Min(startIndex, all.Count - maxVisible));
            int safeStart = Math.Max(0, all.Count - maxVisible);

            var window = all.Skip(safeStart).Take(maxVisible).ToList();

            DrawKline(e.Graphics, panelChart.ClientRectangle, all, window, safeStart);
        }

        private void HistoryBacktestChartPanel_Paint(object sender, PaintEventArgs e)
        {
            if (klineList == null || klineList.Count == 0)
            {
                e.Graphics.Clear(Color.Black);
                return;
            }

            int count = klineList.Count;
            int visible = Math.Max(5, Math.Min(_historyBacktestVisibleCount, count));
            int maxStart = Math.Max(0, count - visible);
            int safeStart = Math.Max(0, Math.Min(_historyBacktestStartIndex, maxStart));
            var window = klineList.Skip(safeStart).Take(visible).ToList();

            DrawKline(
                e.Graphics,
                _historyBacktestChartPanel.ClientRectangle,
                klineList,
                window,
                safeStart,
                drawBacktestMarkers: true,
                drawCross: false,
                drawIndicator: true,
                drawIndicatorText: false);
        }

        private void HistoryBacktestScrollBar_ValueChanged(object sender, EventArgs e)
        {
            _historyBacktestStartIndex = _historyBacktestScrollBar.Value;
            UpdateHistoryBacktestSummary();
            _historyBacktestChartPanel.Invalidate();
        }

        private void HistoryBacktestChartPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _historyBacktestDragging = true;
            _historyBacktestDragStartX = e.X;
            _historyBacktestDragStartIndex = _historyBacktestStartIndex;
            _historyBacktestChartPanel.Capture = true;
        }

        private void HistoryBacktestChartPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_historyBacktestDragging || klineList == null || klineList.Count == 0)
                return;

            int width = Math.Max(1, _historyBacktestChartPanel.ClientSize.Width - 100);
            float step = width / (float)Math.Max(1, _historyBacktestVisibleCount);
            int movedBars = (int)Math.Round((_historyBacktestDragStartX - e.X) / step);
            int maxStart = Math.Max(0, klineList.Count - _historyBacktestVisibleCount);
            int nextStart = Math.Max(0, Math.Min(_historyBacktestDragStartIndex + movedBars, maxStart));

            if (nextStart == _historyBacktestStartIndex)
                return;

            _historyBacktestStartIndex = nextStart;
            UpdateHistoryBacktestScrollBar();
            UpdateHistoryBacktestSummary();
            _historyBacktestChartPanel.Invalidate();
        }

        private void HistoryBacktestChartPanel_MouseUp(object sender, MouseEventArgs e)
        {
            _historyBacktestDragging = false;
            _historyBacktestChartPanel.Capture = false;
        }

        private void HistoryBacktestChartPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            if (klineList == null || klineList.Count == 0)
                return;

            int oldVisible = _historyBacktestVisibleCount;
            int delta = e.Delta > 0 ? -10 : 10;
            int nextVisible = Math.Max(20, Math.Min(klineList.Count, oldVisible + delta));
            if (nextVisible == oldVisible)
                return;

            int centerIndex = _historyBacktestStartIndex + oldVisible / 2;
            _historyBacktestVisibleCount = nextVisible;
            _historyBacktestStartIndex = centerIndex - nextVisible / 2;
            UpdateHistoryBacktestScrollBar();
            UpdateHistoryBacktestSummary();
            _historyBacktestChartPanel.Invalidate();
        }

        BindingList<HistoryRow> historyTable = new BindingList<HistoryRow>();

        private void InitHistoryGrid()
        {
            // 自动根据数据列生成列
            dgHistory.AutoGenerateColumns = true;

            // 绑定数据源
            dgHistory.DataSource = historyTable;

            dgHistory.ReadOnly = true;
            dgHistory.AllowUserToAddRows = false;
            dgHistory.AllowUserToDeleteRows = false;
        }


        // ================= 画 K 线 + 成交量 + 十字线 + 金蚂蚁 =================
        private void DrawKline(Graphics g, Rectangle area,
                               List<KlineItem> allData,
                               List<KlineItem> window,
                               int globalStartIndex,
                               bool drawBacktestMarkers = false,
                               bool drawCross = true,
                               bool drawIndicator = true,
                               bool drawIndicatorText = true)
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.Clear(Color.Black);

            DrawGrid(g, area);


            if (window == null || window.Count == 0) return;

            int padL = 50, padR = 50, padT = 20, padB = 20;
            float totalWidth = area.Width - padL - padR;
            float klineHeight = area.Height - padT - padB - volumeZoneHeight - 10;
            float volumeHeight = volumeZoneHeight;

            int cnt = window.Count;
            if (cnt <= 0 || totalWidth <= 0 || klineHeight <= 0) return;

            float step = totalWidth / cnt;

            // 统一计算窗口价格范围
            var prices = new List<double>();
            prices.AddRange(window.Select(k => (double)k.high));
            prices.AddRange(window.Select(k => (double)k.low));
            prices.AddRange(window.Select(k => (double)k.open));
            prices.AddRange(window.Select(k => (double)k.close));

            prices = prices.Where(v => v > 0).ToList();
            double maxP = prices.Count > 0 ? prices.Max() : 1;
            double minP = prices.Count > 0 ? prices.Min() : 0;

            if (maxP == minP)
            {
                maxP += 1;
                minP -= 1;
            }

            _viewMaxPrice = maxP;
            _viewMinPrice = minP;

            float maxVol = window.Max(k => (float)k.vol);
            if (maxVol <= 0) maxVol = 1;

            Color bullColor = Color.Cyan; // 涨
            Color bearColor = Color.Red;  // 跌

            Rectangle kRect = new Rectangle(
                padL,
                padT,
                (int)totalWidth,
                (int)klineHeight
            );

            // === K线 ===
            // === 正确金蚂蚁风格：青色实心（涨），红色空心（跌） ===
            for (int i = 0; i < cnt; i++)
            {
                var k = window[i];
                float x = padL + step * (i + 0.5f);

                int yH = PriceToY(k.high, kRect);
                int yL = PriceToY(k.low, kRect);
                int yO = PriceToY(k.open, kRect);
                int yC = PriceToY(k.close, kRect);

                bool up = k.close > k.open;      // 涨
                bool down = k.close < k.open;    // 跌

                Color colorUp = Color.Red;      // 涨：青色实心
                Color colorDown = Color.Cyan;    // 跌：红色空心

                //Color colorUp = Color.Cyan;   // 涨：青色
                //Color colorDown = Color.Red;  // 跌：红色


                // --- 影线 ---
                using (Pen wickPen = new Pen(up ? colorUp : colorDown, 1))
                {
                    g.DrawLine(wickPen, x, yH, x, yL);
                }

                // --- 实体宽度（目标软件接近 45% step） ---
                // float w = Math.Max(1, step * 0.45f);
                float w;
                if (cnt <= 60)
                    w = step * 0.95f;   // 几乎贴满
                else
                    w = step * 0.7f;    // 正常

                int bodyTop = Math.Min(yO, yC);
                int bodyBottom = Math.Max(yO, yC);
                int bodyHeight = Math.Max(1, bodyBottom - bodyTop);

                RectangleF body = new RectangleF(
                    x - w / 2,
                    bodyTop,
                    w,
                    bodyHeight
                );

                if (up)
                {
                    // === 涨：青色实心 ===
                    //using (Brush b = new SolidBrush(colorUp))
                    //    g.FillRectangle(b, body);

                    //using (Pen p = new Pen(colorUp, 1))
                    //    g.DrawRectangle(p, body.X, body.Y, body.Width, body.Height);


                    using (Pen p = new Pen(colorUp, 1))
                        g.DrawRectangle(p, body.X, body.Y, body.Width, body.Height);
                }
                else if (down)
                {
                    using (Brush b = new SolidBrush(colorDown))
                        g.FillRectangle(b, body);
                    // === 跌：红色空心 ===
                    using (Pen p = new Pen(colorDown, 1))
                        g.DrawRectangle(p, body.X, body.Y, body.Width, body.Height);

                    //using (Pen p = new Pen(colorDown, 1))
                    //    g.DrawRectangle(p, body.X, body.Y, body.Width, body.Height);
                }
                else
                {
                    // 十字线（青色）
                    using (Pen p = new Pen(colorUp, 1))
                    {
                        float mid = (yO + yC) / 2f;
                        g.DrawLine(p, x - w / 2, mid, x + w / 2, mid);
                    }
                }
            }

            // === 分界线 ===
            using (Pen sepPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1))
            {
                float ySep = padT + klineHeight + 5;
                g.DrawLine(sepPen, padL, ySep, padL + totalWidth, ySep);
            }

            // === 成交量 ===
            float volTop = padT + klineHeight + 10;
            for (int i = 0; i < cnt; i++)
            {
                var k = window[i];
                float x = padL + step * (i + 0.5f);

                bool up = k.close >= k.open;
                Color c = !up ? bullColor : bearColor;

                float h = (k.vol / maxVol) * volumeHeight;
                float y = volTop + (volumeHeight - h);

                using (Brush b = new SolidBrush(c))
                    g.FillRectangle(b, x - step / 3, y, step / 1.5f, h);
            }

            // === 价格刻度 ===
            using (Brush tb = new SolidBrush(Color.Gray))
            {
                g.DrawString(_viewMaxPrice.ToString(), panelChart.Font, tb, 0, padT - 2);
                g.DrawString(_viewMinPrice.ToString(), panelChart.Font, tb, 0, padT + klineHeight - 12);
            }

            // === 十字线 ===
            if (drawCross && showCross)
            {
                int idx = (int)((mouseX - padL) / step);
                idx = Math.Max(0, Math.Min(cnt - 1, idx));
                var k = window[idx];

                float cx = padL + step * (idx + 0.5f);
                int cy = PriceToY(k.close, kRect);

                using (Pen p = new Pen(Color.White, 1))
                {
                    p.DashStyle = DashStyle.Dot;
                    g.DrawLine(p, cx, padT, cx, padT + klineHeight + volumeHeight + 10);
                    g.DrawLine(p, padL, mouseY, padL + totalWidth, mouseY);
                }

                string info =
                    $"时间：{k.stime}\n" +
                    $"开盘：{k.open}\n" +
                    $"最高：{k.high}\n" +
                    $"最低：{k.low}\n" +
                    $"收盘：{k.close}\n" +
                    $"成交量：{k.vol}";

                SizeF size = g.MeasureString(info, panelChart.Font);
                float boxW = size.Width + 10, boxH = size.Height + 10;

                float boxX = mouseX + 15, boxY = mouseY + 15;

                if (boxX + boxW > area.Width) boxX = mouseX - boxW - 15;
                if (boxY + boxH > area.Height) boxY = mouseY - boxH - 15;
                if (boxX < 0) boxX = 0;
                if (boxY < 0) boxY = 0;

                RectangleF box = new RectangleF(boxX, boxY, boxW, boxH);

                using (Brush bg = new SolidBrush(Color.FromArgb(150, 30, 30, 30)))
                    g.FillRectangle(bg, box);

                using (Pen pen = new Pen(Color.White))
                    g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);

                g.DrawString(info, panelChart.Font, Brushes.White, box.X + 5, box.Y + 5);
            }

            // === 金蚂蚁指标 ===
            if (drawIndicator)
                DrawJinMayiIndicator(g, kRect, allData, window, globalStartIndex, drawIndicatorText);
            if (drawBacktestMarkers)
                DrawBacktestMarkers(g, kRect, window, globalStartIndex, step, padL);



        }


        private void DrawBacktestMarkers(
            Graphics g,
            Rectangle kRect,
            List<KlineItem> window,
            int globalStartIndex,
            float step,
            int padL)
        {
            if (_backtestMarkers == null || _backtestMarkers.Count == 0)
                return;

            bool compactMarkers = step < 30;

            using (Font markerFont = new Font("微软雅黑", 8, FontStyle.Bold))
            using (Font summaryFont = new Font("微软雅黑", 10, FontStyle.Bold))
            using (Brush summaryBg = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
            using (Brush summaryBrush = new SolidBrush(_backtestTotalProfit >= 0 ? Color.Lime : Color.Orange))
            {
                string summary = $"回测 平仓:{_backtestClosedCount} 盈亏:{_backtestTotalProfit:0.##}" +
                                 (_backtestHasOpenPosition ? " 持仓中" : "");
                SizeF summarySize = g.MeasureString(summary, summaryFont);
                RectangleF summaryRect = new RectangleF(kRect.Left + 4, kRect.Top + 4, summarySize.Width + 10, summarySize.Height + 6);
                g.FillRectangle(summaryBg, summaryRect);
                g.DrawString(summary, summaryFont, summaryBrush, summaryRect.X + 5, summaryRect.Y + 3);

                foreach (var marker in _backtestMarkers)
                {
                    int localIndex = marker.Index - globalStartIndex;
                    if (localIndex < 0 || localIndex >= window.Count)
                        continue;

                    KlineItem k = window[localIndex];
                    float x = padL + step * (localIndex + 0.5f);
                    int markerY = PriceToY(marker.Price, kRect);

                    if (compactMarkers)
                    {
                        using (Pen p = new Pen(marker.Color, 2))
                        using (Brush b = new SolidBrush(marker.Color))
                        {
                            if (marker.Text == "入")
                            {
                                g.DrawLine(p, x, markerY + 4, x, markerY + 12);
                                PointF[] tri =
                                {
                                    new PointF(x, markerY + 1),
                                    new PointF(x - 4, markerY + 7),
                                    new PointF(x + 4, markerY + 7)
                                };
                                g.FillPolygon(b, tri);
                            }
                            else if (marker.Text == "卖")
                            {
                                g.DrawLine(p, x, markerY - 4, x, markerY - 12);
                                PointF[] tri =
                                {
                                    new PointF(x, markerY - 1),
                                    new PointF(x - 4, markerY - 7),
                                    new PointF(x + 4, markerY - 7)
                                };
                                g.FillPolygon(b, tri);
                            }
                            else
                            {
                                g.FillRectangle(b, x - 3, markerY - 3, 6, 6);
                            }
                        }

                        continue;
                    }

                    int yO = PriceToY(k.open, kRect);
                    int yC = PriceToY(k.close, kRect);
                    int bodyTop = Math.Min(yO, yC);
                    int bodyBottom = Math.Max(yO, yC);
                    int bodyHeight = Math.Max(14, bodyBottom - bodyTop);

                    string text = marker.Text;
                    if (marker.Text == "卖")
                        text = marker.Profit >= 0 ? $"卖\n+{marker.Profit:0.##}" : $"卖\n{marker.Profit:0.##}";

                    SizeF textSize = g.MeasureString(text, markerFont);
                    float textX = x - textSize.Width / 2;
                    float textY = bodyTop + (bodyHeight - textSize.Height) / 2;

                    if (textY < kRect.Top)
                        textY = kRect.Top;
                    if (textY + textSize.Height > kRect.Bottom)
                        textY = kRect.Bottom - textSize.Height;

                    RectangleF textBg = new RectangleF(
                        textX - 3,
                        textY - 2,
                        textSize.Width + 6,
                        textSize.Height + 4);

                    using (Brush bg = new SolidBrush(Color.FromArgb(145, 0, 0, 0)))
                    using (Brush fg = new SolidBrush(marker.Color))
                    using (Pen border = new Pen(marker.Color, 1))
                    {
                        g.FillRectangle(bg, textBg);
                        g.DrawRectangle(border, textBg.X, textBg.Y, textBg.Width, textBg.Height);
                        g.DrawString(text, markerFont, fg, textX, textY);
                    }
                }
            }
        }

        private void Log(string msg)
        {
            try
            {
                string line = $"{DateTime.Now:HH:mm:ss}  {msg}";
                txtResult.AppendText(line + "\r\n");

                File.AppendAllText("kline_log.txt", line + "\r\n");
            }
            catch { }
        }

        /// <summary>
        /// 还原目标软件的网格刻度（红色虚线 + 自动整数刻度）
        /// </summary>
        private void DrawGrid(Graphics g, Rectangle rect)
        {
            int padL = 50, padR = 70, padT = 20, padB = 20;

            int gridLeft = padL;
            int gridRight = rect.Width - padR;
            int gridTop = padT;
            int gridBottom = rect.Height - padB - volumeZoneHeight - 10;

            int gridHeight = gridBottom - gridTop;
            if (gridHeight <= 0) return;

            // === 目标软件：固定 10 条水平网格 ===
            int gridCount = 10;

            // === 自动整数刻度 ===
            double range = _viewMaxPrice - _viewMinPrice;
            if (range <= 0) return;

            // 目标软件使用“整 2/5/10”刻度
            double rawStep = range / gridCount;
            double[] nice = { 1, 2, 5, 10, 20, 50, 100 };

            double step = nice[0];
            foreach (var v in nice)
            {
                if (v >= rawStep)
                {
                    step = v;
                    break;
                }
            }

            // 找到靠近 _viewMinPrice 的第一个整数刻度
            double firstTick = Math.Ceiling(_viewMinPrice / step) * step;

            // 网格线颜色：红色透明（与目标软件一致）
            Pen gridPen = new Pen(Color.FromArgb(80, 255, 60, 60), 1);
            gridPen.DashStyle = DashStyle.Dot;

            // 数字颜色：深红
            Brush textBrush = new SolidBrush(Color.FromArgb(180, 255, 60, 60));
            Font font = new Font("微软雅黑", 10);

            // === 画水平网格 ===
            for (double price = firstTick; price <= _viewMaxPrice; price += step)
            {
                int y = PriceToY(price, new Rectangle(gridLeft, gridTop, gridRight - gridLeft, gridHeight));

                g.DrawLine(gridPen, gridLeft, y, gridRight, y);

                // 右侧价格刻度
                string text = price.ToString("0");
                g.DrawString(text, font, textBrush, gridRight + 25, y - 8);
            }
        }
       


        int _huaxian = 0;
        int _huaxian2 = 0;
        // ================= 画金蚂蚁阶梯线 =================
        private void DrawJinMayiIndicator(
            Graphics g, Rectangle kRect,
            List<KlineItem> allData,
            List<KlineItem> window,
            int globalStartIndex,
            bool drawSignalText = true)
        {
            if (allData == null || allData.Count == 0) return;
            if (window == null || window.Count == 0) return;

            var indAll = CalcJinMayi(allData);


            if (indAll == null || indAll.Count != allData.Count) return;



            int nWin = window.Count;
            int startIdx = globalStartIndex;

            if (startIdx < 0) startIdx = 0;
            if (startIdx + nWin > indAll.Count)
                nWin = indAll.Count - startIdx;

            if (nWin <= 1) return;

            float step = (float)kRect.Width / nWin;
            float left = kRect.Left;

            using (Pen penUp = new Pen(Color.Cyan, 1.5f))   // K2=1
            using (Pen penDown = new Pen(Color.Red, 1.5f))  // K2=-3
            using (Font font = new Font("微软雅黑", 10))
            {
                for (int i = 1; i < nWin; i++)
                {
                    int giPrev = startIdx + i - 1;
                    int giNow = startIdx + i;

                    var p0 = indAll[giPrev];
                    var p1 = indAll[giNow];

                    if (p0.G == 0 && p1.G == 0) continue;

                    float x0 = left + (i - 0.5f) * step;
                    float x1 = left + (i + 0.5f) * step;

                    int y0 = PriceToY(p0.G == 0 ? p1.G : p0.G, kRect);
                    int y1 = PriceToY(p1.G == 0 ? p0.G : p1.G, kRect);

                    Pen pen = (p1.K2 == 1) ? penUp :
                              (p1.K2 == -3) ? penDown : penDown;
                    
                    if (drawSignalText)
                    {
                        if (_cs1== 0)
                        {
                            if(p1.K2==-3||p1.K2==1)
                            _huaxian = p1.K2;
                        }
                        else
                        {
                            if (p1.K2 == -3 || p1.K2 == 1)
                                _huaxian2 = p1.K2;
                        }
                        if (p1.K2 != -3 && p1.K2 != 1)
                          AppendLog("huaxian "+ p1.K2.ToString());

                    }
                  
                      
                  
                    if (Math.Abs(p1.G - p0.G) < 0.0001)
                    {
                      //  Console.WriteLine("红.....");
                        g.DrawLine(pen, x0, y0, x1, y0);
                    }
                    else
                    {
                      //  Console.WriteLine("蓝.....");
                        g.DrawLine(pen, x0, y0, x1, y1);
                    }

                    // 做多/做空文字
                    int localIdx = i;
                    if (drawSignalText && localIdx < window.Count)
                    {
                        var k = window[localIdx];

                        if (p1.BuySignal)
                        {
                            int y = PriceToY(k.low, kRect);
                            g.DrawString("做多", font, Brushes.Yellow, x1 - 12, y + 8);
                        }
                        if (p1.SellSignal)
                        {
                            int y = PriceToY(k.high, kRect);
                            g.DrawString("做空", font, Brushes.Yellow, x1 - 12, y - 20);
                        }
                    }
                }
            }
        }

        // ================= 鼠标事件(只控制十字线) =================
        private void panelChart_MouseMove(object sender, MouseEventArgs e)
        {
            mouseX = e.X;
            mouseY = e.Y;
            showCross = true;
            panelChart.Invalidate();
        }

        private void panelChart_MouseLeave(object sender, EventArgs e)
        {
            showCross = false;
            panelChart.Invalidate();
        }

        // ================= JSON 结构 =================
        public class CaptchaResponse
        {
            public int code { get; set; }
            public string msg { get; set; }
            public CaptchaData data { get; set; }
        }

        public class CaptchaData
        {
            public string key { get; set; }
            public string base64 { get; set; }
        }

        public class LoginResponse
        {
            public int code { get; set; }
            public string msg { get; set; }
            public LoginData data { get; set; }
        }

        public class LoginData
        {
            public int id { get; set; }
            public string token { get; set; }
            public string username { get; set; }
            public QuoteMQTT quotemqtt { get; set; }
            public QuoteMQTT push { get; set; }
            public Quotebalance balance { get; set; }
        }
        public class Quotebalance
        {
            public double balance { get; set; }
            public double freeze { get; set; }
           
        }
        public class QuoteMQTT
        {
            public string host { get; set; }
            public int port { get; set; }
            public int is_ssl { get; set; }
            public string username { get; set; }
        }

        public class TickQuote
        {
            public string instruct { get; set; }
            public int b_id { get; set; }
            public int price { get; set; }
            public int vol { get; set; }
            public long time { get; set; }
            public int high_price { get; set; }
            public int low_price { get; set; }
            public int open_price { get; set; }
        }

        // 实时更新标签（当前行情 + 当前 K 线状态）
        int _zx_price = -1;
        int _gm_price = -1;
        private void UpdateStatusLabel()
        {
            if (klineList.Count == 0 && buildingKline == null)
            {
                labelInfo.Text = "无数据";
                return;
            }

            var last = (buildingKline != null) ? buildingKline : klineList.Last();
            _zx_price = last.close;



            //if ( sheng_shi>=_Scan.miao1 && sheng_shi<=_Scan.miao2)

            //{
            //    //_Xdzt.StrategyId > 0 && _Xdzt.oid > 0 && _Xdzt.listing_no > 0 &&
            //    foreach (StrategyContext n_sc in _strategyList)
            //    {

            //      //  int passedMinutes = (int)(DateTime.Parse(closedKline.stime) - n_sc.TriggerTime.Value).TotalMinutes;

            //        if (n_sc.shitou)
            //        {
            //            byte[] packet = BuildPlaceOrderPacket(360245, 1, closedKline.close, _clientId, 1, 1, 1, 1, 1);

            //            string clientId2 = _orderService.PlaceOrder(packet, 360245, _clientId);
            //            packet = BuildPlaceOrderPacket(360245, 1, closedKline.close + 1, _clientId, 1, 1, 1, 1, 1);

            //            clientId2 = _orderService.PlaceOrder(packet, 360245, _clientId);
            //        }
            //        else
            //        {
            //            AppendLog("编号：" + n_sc.StrategyId.ToString() + " " + "已过 " + passedMinutes + " " + sheng_shi.ToString());

            //            if (_Scan.jieru == 0)
            //            {
            //                if (passedMinutes >= _Scan.jr_can && sheng_shi >= _Scan.miao1 && sheng_shi <= _Scan.miao2)
            //                {
            //                    AppendLog("编号：" + n_sc.StrategyId.ToString() + " " + n_sc.kongduo + " 实投，当前价格：" + closedKline.close.ToString());
            //                    byte[] packet = BuildPlaceOrderPacket(360245, 1, closedKline.close, _clientId, 1, 1, 1, 1, 1);

            //                    string clientId2 = _orderService.PlaceOrder(packet, 360245, _clientId);
            //                    packet = BuildPlaceOrderPacket(360245, 1, closedKline.close + 1, _clientId, 1, 1, 1, 1, 1);

            //                    clientId2 = _orderService.PlaceOrder(packet, 360245, _clientId);
            //                    _Xdzt.StrategyId = n_sc.StrategyId;
            //                    n_sc.shitou = true;
            //                    //_Scan.jieru = 1;
            //                }
            //            }
            //        }
            //    }
            //                if (Math.Abs(_zx_price - _Xdzt.gm_price) >= 1)
            //    {
            //        byte[] packet = BuildzhuanOrderPacket(
            //       360245,
            //       1,
            //       1,
            //       2,//oflag
            //       2,
            //     (_zx_price),
            //      1,
            //      _Xdzt.listing_no,
            //      1,

            //       _clientId
            //  );
            //        Console.WriteLine(_Xdzt.oid.ToString() + " " + _zx_price.ToString() + "zhuan\r\n");

            //        _Xdzt.StrategyId = -1;
            //        _Xdzt.listing_no = -1;
            //        _Xdzt.oid = -1;
            //        string clientId2 = _orderService.PlaceOrder(packet, 360245, _clientId);
            //       // txtResult.AppendText(_Xdzt.oid.ToString() + " " + _zx_price.ToString() + "zhuan\r\n");
            //    }
         

            //}

               
            string text =
                $"时间：{last.stime}\n" +
                $"\n" +
                $"最新价：{last.close}\n" +
                $"\n" +
                $"开：{last.open}   高：{last.high}   低：{last.low}   收：{last.close}";

            labelInfo.Text = text;
        }
        private readonly object historyLock = new object();

        private string UnixToTime(long ts)
        {
            DateTime dt = DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime().DateTime;
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        }
        private void InitHistoryTable()
        {
            dgHistory.ColumnCount = 4;

            dgHistory.Columns[0].Name = "时间";
            dgHistory.Columns[1].Name = "价格";
            dgHistory.Columns[2].Name = "信号";
            dgHistory.Columns[3].Name = "成交量";

            dgHistory.Columns[0].Width = 70;
            dgHistory.Columns[1].Width = 60;
            dgHistory.Columns[2].Width = 50;
            dgHistory.Columns[3].Width = 70;

            dgHistory.ColumnHeadersDefaultCellStyle.BackColor = Color.Black;
            dgHistory.ColumnHeadersDefaultCellStyle.ForeColor = Color.OrangeRed;

            dgHistory.DefaultCellStyle.BackColor = Color.Black;
            dgHistory.DefaultCellStyle.ForeColor = Color.White;
            dgHistory.DefaultCellStyle.SelectionBackColor = Color.DarkRed;
        }
        private void RefreshHistoryTable()
        {
            dgHistory.Rows.Clear();

            foreach (var row in historyTable)
            {
                dgHistory.Rows.Add(row.Time,
                                   row.Price.ToString("0.00"),
                                   row.Signal,
                                   row.Volume);
            }
        }
        private void ShowTradeResults(List<Trade> closedTrades)
        {
            dgTrades.Rows.Clear();

            // 最新交易排最上面
            foreach (var t in closedTrades.OrderByDescending(x => x.TradeID))
            {
                dgTrades.Rows.Add(
                    t.TradeID,
                    t.Type == "buy" ? "买多" : "卖空",
                    t.EntryTime,
                    t.EntryPrice,
                    t.ExitTime,
                    t.ExitPrice,
                    t.Profit
                );
            }
        }
        private OrderQueryService _orderQueryService;
        private void ShowTradeStats(List<Trade> closedTrades, int totalLots)
        {
            int takeProfitCount = closedTrades.Count(t => t.Profit > 0);
            int stopLossCount = closedTrades.Count(t => t.Profit < 0);
            double totalProfit = closedTrades.Sum(t => t.Profit);

            double winRate = closedTrades.Count > 0
                ? (takeProfitCount * 100.0 / closedTrades.Count)
                : 0;

            textBox1.AppendText("\r\n===== 统计 =====\r\n");
            textBox1.AppendText($"总开仓手数：{totalLots}\r\n");
            textBox1.AppendText($"止盈手数：{takeProfitCount}\r\n");
            textBox1.AppendText($"止损手数：{stopLossCount}\r\n");
            textBox1.AppendText($"胜率：{winRate:F2}%\r\n");
            textBox1.AppendText($"总利润：{totalProfit}\r\n");
        }


        public class OrderRowVM
        {
            public int type { get; set; }
            public int freeze_num { get; set; }
            public long listing_no { get; set; }
            public long oid { get; set; }
            public string breed_no { get; set; }
            public string price { get; set; }
            public int num { get; set; }
            public int deal_num { get; set; }
            public string ocata_name { get; set; }

            public string TimeStr { get; set; }
            public string TypeText { get; set; }
            public string StatusText { get; set; }
            public long weituo_time { get; set; }
            public string weituo_price { get; set; }
        }
        private async Task LoadOrdersAsync()
        {
           // var service = new OrderQueryService(token);
            var resp = await _orderQueryService.GetOrderListAsync();
           // Console.WriteLine(resp.code);
            if (resp.code != 0)
            {
                Console.WriteLine(resp.msg);
             //   MessageBox.Show(resp.msg);
                return;
            }

            var list = new List<OrderRowVM>();
            foreach (var o in resp.data.data)
            {
                list.Add(new OrderRowVM
                {
                    listing_no = o.listing_no,
                    breed_no = o.breed_no,
                    price = o.price,
                    type = o.type,
                    num = o.num,
                    deal_num = o.deal_num,
                    ocata_name = o.ocata_name,
                    TypeText = o.type == 1 ? "买" : "卖",
                    TimeStr = DateTimeOffset
                    .FromUnixTimeSeconds(o.time)
                    .LocalDateTime
                    .ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            t_list = list;
            foreach (StrategyContext n_sc in _strategyList)
            {
//                if (n_sc.Oid == -1)
//                {
//                    foreach (var o in resp.data.data)
//                    {
//                        DateTime httpOrderTime =
//DateTimeOffset.FromUnixTimeSeconds(o.time).LocalDateTime;
//                        TimeSpan diff = (httpOrderTime - n_sc.OrderSendTime).Duration();
//                        if (diff.TotalSeconds < 12)
//                        {
//                            n_sc.Oid = o.listing_no;
//                            n_sc.buzhou = 2;
//                            AppendLog("[" + n_sc.StrategyId.ToString() + "]"+n_sc.maijia.ToString()+" 订货下单成功" + o.listing_no);
//                            break;
//                        }

//                    }

//                 }
               

            }
           
        
       
           // dataGridView1.DataSource = list;
        }
        List<OrderRowVM> t_list2 = new List<OrderRowVM>();
        List<OrderRowVM> t_list = new List<OrderRowVM>();
        private async Task LoadOrdersAsync2()
        {
          //  var service = new OrderQueryService(token);
            var resp = await _orderQueryService.GetOrderListAsync2();
            Console.WriteLine(resp);
            Console.WriteLine(resp.msg);
            if (resp.code != 0)
            {
                Console.WriteLine(resp.msg);
               // MessageBox.Show(resp.msg);
                return;
            }

            var list = new List<OrderRowVM>();
            foreach (StrategyContext n_sc in _strategyList)
            {
              //  if (n_sc.Oid == -1)
                {
                 //   Console.WriteLine("shaushuashau2");
                    foreach (var o in resp.data.data)
                    {
                        DateTime httpOrderTime =
DateTimeOffset.FromUnixTimeSeconds(o.weituo_time).LocalDateTime;
                        TimeSpan diff = (httpOrderTime - n_sc.OrderSendTime).Duration();
                     Console.WriteLine("chazhi:" + diff.TotalSeconds.ToString() + " " + o.listing_no.ToString());

                        if (diff.TotalSeconds < 12 && n_sc.Oid<10&& n_sc.buzhou<3)
                        {
                            n_sc.buzhou = 3;
                            
                            n_sc.Oid = o.listing_no;
                            n_sc.listing_no = o.oid;
                            AppendLog("[" + n_sc.StrategyId.ToString() + "]"+n_sc.maijia.ToString()+" 订货成交" + o.listing_no);
                            break;
                        }

                    }

                }
            }
            foreach (var o in resp.data.data)
            {
             
                foreach(StrategyContext n_sc in _strategyList)
                {
                    if (n_sc.Oid == o.listing_no)
                    {
                        if (o.freeze_num == 1)
                        {
                         //   AppendLog("编号[" + n_sc.StrategyId + "]未成交,撤单重挂" );
                             n_sc.buzhou = 5;
                        }
                        if (o.freeze_num == 0)
                        {
                            n_sc.listing_no = o.oid;
                            n_sc.buzhou = 3;
                        }
                      
                     //   AppendLog("获取到编号：" + n_sc.StrategyId.ToString() + " " + o.oid);
                        break;
                    }
                }


                list.Add(new OrderRowVM
                {  listing_no=o.listing_no,
                    oid = o.oid,
                    breed_no = o.breed_no,
                    price = o.price,
                    num = o.num,
                    type= o.type,
                    deal_num = o.deal_num,
                    ocata_name = o.ocata_name,
                    TypeText = o.type == 1 ? "买" : "卖",
                    TimeStr = DateTimeOffset
                        .FromUnixTimeSeconds(o.time)
                        .LocalDateTime
                        .ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
           
                t_list2 = list;
            _shua2 = false;
          //  dataGridView2.DataSource = list;
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dataGridView1.Columns[e.ColumnIndex].Name == "Action")
            {
                var row = (OrderRowVM)dataGridView1.Rows[e.RowIndex].DataBoundItem;
                CancelOrder(row);
            }
            if (dataGridView1.Columns[e.ColumnIndex].Name == "Action2")
            {
                var row = (OrderRowVM)dataGridView1.Rows[e.RowIndex].DataBoundItem;
                ZhuanOrder(row);
            }
        }
        private void ZhuanOrder(OrderRowVM row)
        {
            var confirm = MessageBox.Show(
                $"确认转货？\n挂货编号：{row.oid}{row.price}",
                "确认",
                MessageBoxButtons.YesNo);
            //return;
            if (confirm != DialogResult.Yes)
                return;

            // 你之前写好的 WS 撤单包
            byte[] packet = BuildzhuanOrderPacket(
                 user_id,
                 1,
                 1,
                 2,//oflag
                 2,
                (int)float.Parse(row.price),
                1,
                row.oid,
                1,
        
                 _clientId
            );



            string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);

        }
        private void CancelOrder(OrderRowVM row)
        {
            var confirm = MessageBox.Show(
                $"确认撤单？\n挂货编号：{row.listing_no}",
                "确认",
                MessageBoxButtons.YesNo);

            if (confirm != DialogResult.Yes)
                return;

            // 你之前写好的 WS 撤单包
            byte[] packet = BuildcancelOrderPacket(
                 user_id,
                 row.listing_no,
                 _clientId
            );
          


            string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
          
        }
        private void InitHackerTextBox(TextBox tb)
        {
            tb.Multiline = true;
            // tb.BackColor = Color.FromArgb(12, 12, 12);
           // tb.ForeColor = Color.BurlyWood; // Color.FromArgb(0, 220, 120);
            tb.Font = new Font("Consolas", 9.5f);
            tb.BorderStyle = BorderStyle.None;
            tb.ScrollBars = ScrollBars.Vertical;
            tb.ReadOnly = true;
            tb.WordWrap = true;
        }
        //private void OnLoginSuccess(string account)
        //{
        //    if (!_accountStore.Accounts.Contains(account))
        //    {
        //        _accountStore.Accounts.Add(account);
        //    }

        //    _accountStore.LastAccount = account;
        //    AccountStoreHelper.Save(_accountStore);
        //}
        public class LoginHistoryStore
        {
            public List<string> Usernames { get; set; } = new List<string>();
        }
        public static class LoginHistoryHelper
        {
            private static readonly string FilePath =
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WinOK",
                    "login_history.json"
                );

            public static LoginHistoryStore Load()
            {
                try
                {
                    if (!File.Exists(FilePath))
                        return new LoginHistoryStore();

                    return JsonConvert.DeserializeObject<LoginHistoryStore>(
                        File.ReadAllText(FilePath)
                    ) ?? new LoginHistoryStore();
                }
                catch
                {
                    return new LoginHistoryStore();
                }
            }

            public static void Save(LoginHistoryStore store)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(store));
            }
        }


        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _lblStatus;
        private ToolStripStatusLabel _lblAccount;
        private ToolStripStatusLabel _lblNet;

        private void InitStatusStrip()
        {
            _statusStrip = new StatusStrip();
            _statusStrip.Dock = DockStyle.Bottom;

            _lblStatus = new ToolStripStatusLabel("未登录");
            _lblAccount = new ToolStripStatusLabel("账号：-");
           // _lblNet = new ToolStripStatusLabel("好运！");

            _statusStrip.Items.Add(_lblStatus);
            _statusStrip.Items.Add(new ToolStripSeparator());
            _statusStrip.Items.Add(_lblAccount);
           // _statusStrip.Items.Add(new ToolStripSeparator());
           // _statusStrip.Items.Add(_lblNet);

            this.Controls.Add(_statusStrip);
        }

        private void ReloadMainAccounts()
        {
            var store = MainAccountStoreHelper.Load();

            cboAccount.DataSource = store.Accounts.Select(a => a.Username).ToList();

            // 自动选中上次账号
            if (!string.IsNullOrEmpty(store.LastAccount))
            {
                cboAccount.SelectedItem = store.LastAccount;
               
                // txtMainUser.Text = store.LastAccount;

                var pwd = MainAccountStoreHelper.GetPassword(store.LastAccount);
                if (pwd != null) txtPass.Text = pwd;
            }
        }
        void AppendColorText(string text, Color color)
        {
            richTextBox1.SelectionStart = richTextBox1.TextLength;
            richTextBox1.SelectionLength = 0;
            richTextBox1.SelectionColor = color;
            richTextBox1.AppendText(text);
            richTextBox1.SelectionColor = richTextBox1.ForeColor;
        }
        private List<int> _values2 = new List<int> { 1, 0, 0, 0, 0, 0, 0, 0 };
        private void InitNumericUpDowns()
        {
           

            ///
            groupBox13.Controls.Clear();

            int count = 8;
            int startX = 10;
            int startY = 25;
            int gap = 10;
            int width = 60;

            for (int i = 0; i < count; i++)
            {
                //int kb = 0;
                //if (i == 0)
                //{
                //    kb = 1;
                //}


                var nud = new NumericUpDown
                {
                    Name = $"nud{i}",
                    Minimum = 0,
                    Maximum = 130,
                    Value = _values2[i],
                    Width = width,
                    Location = new Point(
                        startX + i * (width + gap),
                        startY
                    ),
                    Tag = i   // ⭐ 关键：存索引
                };

                nud.ValueChanged += Nud_ValueChanged2;

                groupBox13.Controls.Add(nud);
            }
        }
        private void Nud_ValueChanged2(object sender, EventArgs e)
        {
            if (sender is NumericUpDown nud && nud.Tag is int index)
            {
                _values2[index] = (int)nud.Value;
                AppendLog("手数数量调节为:" + _values2[index].ToString());
                // 调试用
                // Console.WriteLine(string.Join(",", _values));
            }
        }
        List<int> jg_zu = new List<int>();
        int new_bei_xu = 0;
        bool new_shangci = true;

        int cangshu = 1;
        int m2_price = 0;

        private void radioButton_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton rb = sender as RadioButton;

            if (rb.Checked)
            {
                if (rb.Text == "1仓")
                {
                    cangshu = 1;
                }
                if (rb.Text == "2仓")
                {
                    cangshu = 2;
                }
                if (rb.Text == "不限制")
                {
                    cangshu = 3;
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            foreach (RadioButton rb in groupBox18.Controls.OfType<RadioButton>())
            {
                rb.CheckedChanged += radioButton_CheckedChanged;
            }
            InitNumericUpDowns();



            InitStatusStrip();
          //  ReloadMainAccounts();
            comboBox1.SelectedIndex = 0;
            //var store = LoginHistoryHelper.Load();

            //if (!store.Usernames.Contains(_login.Username))
            //{
            //    store.Usernames.Add(_login.Username);
            //    LoginHistoryHelper.Save(store);
            //}
            comboBox2.SelectedIndex = 0;
            comboBox7.SelectedIndex = 0;
            comboBox3.SelectedIndex = 0;
            // 可选：绑定到 ComboBox
            //    cboAccount.DataSource = store.Usernames.ToList();
            //_accountStore = AccountStoreHelper.Load();

            //cboAccount.Items.Clear();
            //foreach (var acc in _accountStore.Accounts)
            //{
            //    cboAccount.Items.Add(acc);
            //}

            //// 自动选中上次登录账号
            //if (!string.IsNullOrEmpty(_accountStore.LastAccount))
            //{
            //    cboAccount.Text = _accountStore.LastAccount;
            //}
            splitContainer1.SplitterDistance = 490;
          //  AppendLog("1.5更新说明：1 修复不同账号登录问题");
            AppendLog("===============");
            AppendLog("请先登录！");
            this.Width = 1300;
            this.Height = 550;
        
       tabControl1.TabPages.Remove(tabPage4);
            tabControl1.TabPages.Remove(tabPage5);
            tabControl1.TabPages.Remove(tabPage6);
            // tabControl1.TabPages.Remove(tabPage7);
            tabControl1.TabPages.Remove(tabPage8);
            tabControl1.TabPages.Remove(tabPage9);
          tabControl1.TabPages.Remove(tabPage1);
            // tabControl1.TabPages.Remove(tabPage2);
            tabControl1.TabPages.Remove(tabPage3);
            tabControl1.TabPages.Remove(tabPage7);
            panel2.Width = 800;
            splitContainer2.SplitterDistance = 265;
          //  InitHackerTextBox(txtStrategyResult);
            InitOrderGrid(dataGridView1);
            InitOrderGrid(dataGridView2);
            cbStartMode.SelectedIndex = 2;
            cbEntryMode.SelectedIndex = 0;
            cbBuyMode.SelectedIndex = 0; 

            for (int i = 0; i < jieru_s.Length; i++)
            {
                comboBox4.Items.Add(jieru_s[i]);
            }
            comboBox4.SelectedIndex = 0;

            for (int i = 0; i < tiaojian2_s.Length; i++)
            {
                comboBox5.Items.Add(tiaojian2_s[i]);
            }
            comboBox5.SelectedIndex = 0;

            for (int i = 0; i < tiaojian3_s.Length; i++)
            {
                comboBox6.Items.Add(tiaojian3_s[i]);
            }
          

            InitHistoryGrid();
            comboBox6.SelectedIndex = 0;
            //tabPage3.Hide();
            InitTradeGrid();
             LoadCaptchaAsync();
            //  InitHistoryTable();

        }
        private void UpdateOrderRow(long oid, int status)
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                var vm = row.DataBoundItem as OrderRowVM;
                if (vm == null) continue;

                if (vm.oid == oid)
                {
                    vm.StatusText = "已撤单";
                    row.DefaultCellStyle.ForeColor = Color.Gray;
                    row.Cells["Action"].Value = "—";
                    row.Cells["Action"].ReadOnly = true;
                    break;
                }
            }
        }

        public class StrategyState
        {
            public bool InPosition { get; set; } = false;
            public bool IsLong { get; set; } = false;
            public double EntryPrice { get; set; }
            public string EntryTime { get; set; }

            public double TakeProfit { get; set; }
            public double StopLoss { get; set; }

            public double Profit { get; set; }
        }

        private void btnRunStrategy_Click(object sender, EventArgs e)
        {
            var cfg = GetStrategyConfig();
          //  txtStrategyResult.Clear();
            RunFullStrategy(cfg);

            // RunStrategyTest();
        }

        string[] jieru_s = { "置做多", "置做空", "金蚂蚁指标" };
        string[] tiaojian2_s = { "固定K", "固定完整K", "N个盈亏" };
        string[] tiaojian3_s = { "正买", "反买" };
        //几倍
        //止盈  //止损
        //string[] 条_s = { "置做多", "置做空", "金蚂蚁指标" };A


        // ★★★ 多仓用的持仓结构 ★★★
        private class Position
        {
            public string Type;       // "buy" 或 "sell"
            public double EntryPrice;
            public string EntryTime;
            public int EntryIndex;
        }

        private void RunStrategyTest()
        {
            if (klineList == null || klineList.Count < 10)
            {
                MessageBox.Show("K线数据不足，先加载历史K线。");
                return;
            }

            // === 读取止盈/止损点数 ===
            if (!double.TryParse("1", out double tpPoints))
            {
                MessageBox.Show("止盈输入无效");
                return;
            }

            if (!double.TryParse("-1", out double slPoints))
            {
                MessageBox.Show("止损输入无效");
                return;
            }

            // 止损强制为负数（比如 -3）
            if (slPoints > 0) slPoints = -slPoints;

            txtResult.Clear();

            // === 计算全量金蚂蚁指标 ===
            var indicators = CalcJinMayi(klineList);

            // === 多仓容器 ===
            List<Position> positions = new List<Position>();
            double totalProfit = 0;

            int buySignalCount = 0;
            int sellSignalCount = 0;
            int openedBuyCount = 0;
            int openedSellCount = 0;

            // 从第 1 根开始，i-1 才有意义
            for (int i = 1; i < klineList.Count; i++)
            {
                var k = klineList[i];
                var ind = indicators[i];

                bool buySig = ind.BuySignal;
                bool sellSig = ind.SellSignal;

                if (buySig) buySignalCount++;
                if (sellSig) sellSignalCount++;

                // ===============================
                // 1）先处理已有仓位的 TP / SL
                // ===============================
                if (positions.Count > 0)
                {
                    List<Position> toRemove = new List<Position>();

                    foreach (var pos in positions)
                    {
                        if (pos.Type == "buy")
                        {
                            double tpPrice = pos.EntryPrice + tpPoints;
                            double slPrice = pos.EntryPrice + slPoints;

                            // 多单止盈
                            if (k.high >= tpPrice)
                            {
                                totalProfit += tpPoints;
                                PrintLine($"[多止盈] {k.stime} +{tpPoints}  (入场: {pos.EntryTime} 价={pos.EntryPrice})");
                                toRemove.Add(pos);
                                continue;
                            }

                            // 多单止损
                            if (k.low <= slPrice)
                            {
                                totalProfit += slPoints;
                                PrintLine($"[多止损] {k.stime} {slPoints}  (入场: {pos.EntryTime} 价={pos.EntryPrice})");
                                toRemove.Add(pos);
                                continue;
                            }
                        }
                        else // 空单
                        {
                            double tpPrice = pos.EntryPrice - tpPoints;   // 空头止盈：跌 tpPoints
                            double slPrice = pos.EntryPrice - slPoints;   // slPoints 为负，减掉变加

                            // 空单止盈
                            if (k.low <= tpPrice)
                            {
                                totalProfit += tpPoints;
                                PrintLine($"[空止盈] {k.stime} +{tpPoints}  (入场: {pos.EntryTime} 价={pos.EntryPrice})");
                                toRemove.Add(pos);
                                continue;
                            }

                            // 空单止损
                            if (k.high >= slPrice)
                            {
                                totalProfit += slPoints;
                                PrintLine($"[空止损] {k.stime} {slPoints}  (入场: {pos.EntryTime} 价={pos.EntryPrice})");
                                toRemove.Add(pos);
                                continue;
                            }
                        }
                    }

                    // 移除已平仓
                    foreach (var p in toRemove)
                        positions.Remove(p);
                }

                // ===============================
                // 2）再根据本根信号开新仓（多仓并行）
                // ===============================
                if (buySig)
                {
                    positions.Add(new Position
                    {
                        Type = "buy",
                        EntryPrice = k.close,
                        EntryTime = k.stime,
                        EntryIndex = i
                    });
                    openedBuyCount++;
                    PrintLine($"[开多] {k.stime} 价={k.close}");
                }

                if (sellSig)
                {
                    positions.Add(new Position
                    {
                        Type = "sell",
                        EntryPrice = k.close,
                        EntryTime = k.stime,
                        EntryIndex = i
                    });
                    openedSellCount++;
                    PrintLine($"[开空] {k.stime} 价={k.close}");
                }
            }

            PrintLine("================================");
            PrintLine($"做多信号次数：{buySignalCount}，实际开多仓：{openedBuyCount}");
            PrintLine($"做空信号次数：{sellSignalCount}，实际开空仓：{openedSellCount}");
            PrintLine($"最终总收益：{totalProfit}");
            PrintLine($"当前剩余未平仓数：{positions.Count}");
        }

        private void PrintLine(string msg)
        {
            txtResult.AppendText(msg + Environment.NewLine);
        }
        public class Scanclass
        {
            public int jr_can2 = 0;
            public int z_zhiying = 0;
            public int z_zhisun = 0;
            public DateTime dingshi;


            public int shoutime = 0;
            public int miao1 = 0;
            public int miao2 = 0;
            public int miao3 = 0;
            public int miao4 = 0;
            /// <summary>未启动</summary>
           public int Idle = 0;

            /// <summary>已触发（方向已确定）</summary>
            public int chufa = 1;

            /// <summary>等待介入（固定N分钟 / N根K）</summary>
            public int jieru = 2;
            public int jr_can = 2;
            public int zhengfan = 0;
            public int beishu = 1;
            public int zhiying = 1;
            public int zhisun = -1;
            /// <summary>已介入（已下单 / 已持仓）</summary>
            public int InPosition = 3;

            /// <summary>已结束（止盈 / 止损 / 手动停止）</summary>
            public int Finished = 4;
            public int WaitingSignal = 1;  // 等待金蚂蚁信号（新加的）
        }
        Scanclass _Scan = new Scanclass();
        
        private void button2_Click(object sender, EventArgs e)
        {
            if (!liveStrategyRunning)
            {
                _shua4 = true;
                   _Xdzt = new xiadan_ztc();
                // —— 从关闭 → 开启 ——
                liveStrategyRunning = true;
                _Scan.chufa = cbStartMode.SelectedIndex;
                _Scan.jieru = cbEntryMode.SelectedIndex;
                _Scan.jr_can = (int)numericUpDown27.Value;//  numEntryN.Value;
                _Scan.zhengfan = cbBuyMode.SelectedIndex;
               // _Scan.beishu = (int)numLots.Value;
                _Scan.zhiying = (int)numTP.Value;
                _Scan.zhisun = (int)numSL.Value;
                _Scan.miao1 = (int)numericUpDown3.Value;
                _Scan.miao2 = (int)numericUpDown4.Value;
                _Scan.miao3 = (int)numericUpDown5.Value;
                _Scan.miao4 = (int)numericUpDown6.Value;
                _Scan.shoutime = (int)numericUpDown10.Value;
                _Scan.jr_can2=(int)numericUpDown11.Value;
                _Scan.z_zhiying = int.Parse(textBox5.Text);
                _Scan.z_zhisun = int.Parse(textBox6.Text);
                int hour = (int)numericUpDown8.Value;
                int minute = (int)numericUpDown9.Value;

                // 今天的日期
                DateTime today = DateTime.Today;

                // 组合成今天的时间
                DateTime targetTime = new DateTime(
                    today.Year,
                    today.Month,
                    today.Day,
                    hour,
                    minute,
                    0   // 秒
                );

                _Scan.dingshi = targetTime;


                duokong_sig = 0;
                if (_Scan.chufa == 0)
                {
                  
                    duokong_sig = 1;
                }
                if (_Scan.chufa == 1)
                {
                 
                    duokong_sig = 2;
                }
                _strategyList.Clear();
                _strategySeq = 1;
                liveOrderId = 0;
               // StartStrategy("buy");
                liveOpenTrades.Clear();
                liveClosedTrades.Clear();
                // groupBox1.Enabled = false;
                groupBox6.Enabled = false;
                AppendLog("=== 实盘策略已启动 ===\r\n");
                button2.Text = "停止";
                button2.BackColor = Color.DarkRed;
                cbEntryMode.Enabled = false;
                groupBox15.Enabled = false;
                //button8_Click(null, null);
               
            }
            else
            {
                groupBox15.Enabled = true;
                _quanmai = true;
                cbEntryMode.Enabled = true;
                // groupBox1.Enabled = true;
                groupBox6.Enabled = true;
                // —— 从开启 → 关闭 ——
                liveStrategyRunning = false;
                AppendLog("=== 实盘策略已停止 ===\r\n");
                button2.Text = "开启";
                button2.BackColor = Color.SteelBlue;
                //button8_Click(null, null);

            }
        }
     

        public class Trade
        {
            public int TradeID;
            public string Type;  // buy / sell
            public double EntryPrice;
            public string EntryTime;
            public double ExitPrice;
            public string ExitTime;
            public double Profit;
        }
        private void RunFullStrategy(StrategyConfig cfg)
        {
            int tradeIdCounter = 1;

            txtResult.Clear();

            var kl = klineList;
            var ind = CalcJinMayi(kl);

            List<Trade> openTrades = new List<Trade>();
            List<Trade> closedTrades = new List<Trade>();

            bool started = false;
            int startDirection = 0; // 1=多启动  -1=空启动
            DateTime startTime = DateTime.MinValue;
            int startIndex = -1;

            int counter = 0; // 用于 EntryN

            for (int i = 1; i < kl.Count; i++)
            {
                var k = kl[i];
                var signal = ind[i];

                // ===========================
                // Step1: 确定启动
                // ===========================
                if (!started)
                {
                    if (cfg.StartMode == 1)
                    {
                        // 用户立即启动
                        started = true;
                        startDirection = cfg.InitialDirection;
                        startTime = DateTime.Parse(k.stime);
                        startIndex = i;
                        Print($"[启动] 立即启动 方向={startDirection} 时间={k.stime}");
                    }
                    else if (cfg.StartMode == 2)
                    {
                        // 等待金蚂蚁信号
                        if (signal.BuySignal)
                        {
                            started = true;
                            startDirection = 1;
                            startTime = DateTime.Parse(k.stime);
                            startIndex = i;
                            Print($"[启动] 做多信号启动 {k.stime}");
                        }
                        else if (signal.SellSignal)
                        {
                            started = true;
                            startDirection = -1;
                            startTime = DateTime.Parse(k.stime);
                            startIndex = i;
                            Print($"[启动] 做空信号启动 {k.stime}");
                        }
                    }

                    // 未启动继续循环
                    continue;
                }

                // ===========================
                // Step2: 进入“等待 N 条件”
                // ===========================

                bool entryNow = false;

                if (cfg.EntryMode == 1)
                {
                    // N 分钟后介入
                    if ((DateTime.Parse(k.stime) - startTime).TotalMinutes >= cfg.EntryN)
                        entryNow = true;
                }
                else if (cfg.EntryMode == 2)
                {
                    // 完整 K（有上下影线）
                    bool fullK = (k.high != k.low);
                    if (fullK)
                    {
                        counter++;
                        if (counter >= cfg.EntryN)
                            entryNow = true;
                    }
                }
                else if (cfg.EntryMode == 3)
                {
                    // 波动 K（相邻 close 不一样）
                    double diff = Math.Abs(kl[i].close - kl[i - 1].close);
                    if (diff > 0)
                    {
                        counter++;
                        if (counter >= cfg.EntryN)
                            entryNow = true;
                    }
                }

                if (!entryNow)
                    continue;

                // ===========================
                // Step3: 开仓
                // ===========================
                int finalDirection;

                if (cfg.BuyMode == 1)
                    finalDirection = startDirection;   // 正买
                else
                    finalDirection = -startDirection;  // 反买

                for (int j = 0; j < cfg.Lots; j++)
                {
                    openTrades.Add(new Trade
                    {
                        TradeID = tradeIdCounter++,   // ★编号自动递增
                        Type = finalDirection == 1 ? "buy" : "sell",
                        EntryPrice = k.close,
                        EntryTime = k.stime
                    });

                    Print($"[开仓 #{tradeIdCounter - 1}] {k.stime} {(finalDirection == 1 ? "买多" : "卖空")} 价={k.close}");

                }

                // =========================
                // Step4: 管理持仓（止盈 / 止损）
                // =========================
                List<Trade> toClose = new List<Trade>();

                foreach (var pos in openTrades)
                {
                    // 多单
                    if (pos.Type == "buy")
                    {
                        // 止盈（high >= entry + TP）
                        if (k.high >= pos.EntryPrice + cfg.TakeProfit)
                        {
                            pos.ExitPrice = pos.EntryPrice + cfg.TakeProfit;
                            pos.ExitTime = k.stime;
                            pos.Profit = cfg.TakeProfit;
                            Print($"[多止盈 #{pos.TradeID}] {pos.ExitTime} +{pos.Profit}");

                            toClose.Add(pos);
                        }
                        // 止损（low <= entry + SL）
                        else if (k.low <= pos.EntryPrice + cfg.StopLoss)
                        {
                            pos.ExitPrice = pos.EntryPrice + cfg.StopLoss;
                            pos.ExitTime = k.stime;
                            pos.Profit = cfg.StopLoss;
                            Print($"[多止损 #{pos.TradeID}] {pos.ExitTime} {pos.Profit}");

                            toClose.Add(pos);
                        }
                    }
                    // 空单
                    else if (pos.Type == "sell")
                    {
                        // 止盈（low <= entry - TP）
                        if (k.low <= pos.EntryPrice - cfg.TakeProfit)
                        {
                            pos.ExitPrice = pos.EntryPrice - cfg.TakeProfit;
                            pos.ExitTime = k.stime;
                            pos.Profit = cfg.TakeProfit;
                            Print($"[空止盈 #{pos.TradeID}] {pos.ExitTime} +{pos.Profit}");

                            toClose.Add(pos);
                        }
                        // 止损（high >= entry - SL）
                        else if (k.high >= pos.EntryPrice - cfg.StopLoss)
                        {
                            pos.ExitPrice = pos.EntryPrice - cfg.StopLoss;
                            pos.ExitTime = k.stime;
                            pos.Profit = cfg.StopLoss;
                            Print($"[空止损 #{pos.TradeID}] {pos.ExitTime} {pos.Profit}");

                            toClose.Add(pos);
                        }
                    }
                }

                // 批量移除已平仓
                foreach (var c in toClose)
                {
                    openTrades.Remove(c);
                    closedTrades.Add(c);
                }


                // 开仓后不再重复触发 Entry
                started = false;
                counter = 0;
                // 统计总手数 = 每次开仓的 lots * 次数
                int totalLots = closedTrades.Count;

                Print2("----------------------");
                Print2($"最终收益：{closedTrades.Sum(t => t.Profit)}");

                ShowTradeResults(closedTrades);
                ShowTradeStats(closedTrades, totalLots);


            }

            Print("完成策略执行");
        }

        private void Print(string msg)
        {
          //  txtStrategyResult.AppendText(msg + "\r\n");
        }
        private void Print2(string msg)
        {
            textBox1.AppendText(msg + "\r\n");
        }


        private StrategyConfig GetStrategyConfig()
        {
            StrategyConfig cfg = new StrategyConfig();

            // ===== 启动方式 =====
            if (cbStartMode.SelectedIndex == 0)
            {
                cfg.StartMode = 1;        // 立即启动
                cfg.InitialDirection = 1; // 开多
            }
            else if (cbStartMode.SelectedIndex == 1)
            {
                cfg.StartMode = 1;         // 立即启动
                cfg.InitialDirection = -1; // 开空
            }
            else
            {
                cfg.StartMode = 2; // 等待金蚂蚁信号
            }

            // ===== 介入方式 =====
            cfg.EntryMode = cbEntryMode.SelectedIndex + 1;
            cfg.EntryN = (int)numEntryN.Value;

            // ===== 购买方式 =====
            cfg.BuyMode = cbBuyMode.SelectedIndex + 1;

            // ===== 手数 =====
            cfg.Lots = (int)numLots.Value;

            // ===== 止盈止损 =====
            cfg.TakeProfit = (double)numTP.Value;
            cfg.StopLoss = -(double)numSL.Value;  // 转成负数

            return cfg;
        }

        private void InitTradeGrid()
        {
            dgTrades.ColumnCount = 7;

            dgTrades.Columns[0].Name = "编号";
            dgTrades.Columns[1].Name = "类型";
            dgTrades.Columns[2].Name = "开仓时间";
            dgTrades.Columns[3].Name = "开仓价";
            dgTrades.Columns[4].Name = "平仓时间";
            dgTrades.Columns[5].Name = "平仓价";
            dgTrades.Columns[6].Name = "盈亏";

            dgTrades.Columns[0].Width = 50;
            dgTrades.Columns[1].Width = 60;
            dgTrades.Columns[2].Width = 120;
            dgTrades.Columns[3].Width = 70;
            dgTrades.Columns[4].Width = 120;
            dgTrades.Columns[5].Width = 70;
            dgTrades.Columns[6].Width = 70;

            dgTrades.ColumnHeadersDefaultCellStyle.BackColor = Color.Black;
            dgTrades.ColumnHeadersDefaultCellStyle.ForeColor = Color.Yellow;

            dgTrades.DefaultCellStyle.BackColor = Color.Black;
            dgTrades.DefaultCellStyle.ForeColor = Color.White;
            dgTrades.DefaultCellStyle.SelectionBackColor = Color.DarkRed;
        }
        private async Task SendPlaceOrderAsync()
        {
            long oid = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var order = new
            {
                instruct = "place_order",
                m_id = user_id,
                b_id = 1,
                num = 1,
                direction = 1,
                oflag = 1,
                price = 1370,
                bond = 1,
                oid = oid,
                record_type = 1,
                clientId = "f746defd-6ebf-4111-9cd2-868f9bf1c364"
            };

            //string json = Newtonsoft.Json.JsonConvert.SerializeObject(order);
            string json =
    "{\"instruct\":\"place_order\"," +
    "\"m_id\":"+user_id.ToString()+"," +
    "\"b_id\":1," +
    "\"num\":1," +
    "\"direction\":1," +
    "\"oflag\":1," +
    "\"price\":1510," +
    "\"bond\":1," +
    "\"oid\":" + oid + "," +
    "\"record_type\":1," +
    "\"clientId\":\"4b4cb3d4-7c56-451f-88bb-9c3a0a3877d7\"}";

            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            var list = new List<byte>();

            // 固定头（你抓到的）
            list.Add(0xBA);
            list.Add(0x01);
            list.Add(0x00);
            list.Add(0x05);

            // "trade"
            list.AddRange(Encoding.ASCII.GetBytes("trade"));

            // 固定字段（照抄）
            list.Add(0x95);
            list.Add(0xCA);

            // JSON payload
            list.AddRange(jsonBytes);

            byte[] frame = list.ToArray();

            ClientWebSocket ws = new ClientWebSocket();

            // 登录 token（非常重要）
            ws.Options.SetRequestHeader("Authorization", "Bearer " + token);

            await ws.ConnectAsync(
                new Uri("wss://47.57.4.140:8006"),
                CancellationToken.None
            );

            // 发送 Binary 下单帧
            await ws.SendAsync(
                new ArraySegment<byte>(frame),
                WebSocketMessageType.Binary,
                true,
                CancellationToken.None
            );

           
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            await SendPlaceOrderAsync();

        }


        public static byte[] BuildTradeOrderPacket(string json)
        {
            List<byte> buf = new List<byte>();

            // 1. 固定头
            buf.Add(0xBA);
            buf.Add(0x01);

            // 2. 固定控制字段
            buf.Add(0x00);
            buf.Add(0x05);

            // 3. topic: "trade"
            buf.AddRange(Encoding.ASCII.GetBytes("trade"));

            // 4. 协议字段（先固定，后续可研究是否变化）
            buf.Add(0xB5);
            buf.Add(0x6D);

            // 5. JSON payload
            buf.AddRange(Encoding.UTF8.GetBytes(json));

            return buf.ToArray();
        }

        private async Task PublishBinaryAsync(byte[] packet)
        {
            if (mqttClient == null || !mqttClient.IsConnected)
            {
                MessageBox.Show("MQTT 未连接");
                return;
            }

            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("trade") // ⚠️ topic 通常是 trade
                .WithPayload(packet)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await mqttClient.PublishAsync(msg);

            BeginInvoke(new Action(() =>
            {
                txtResult.AppendText(
                    $"[发送下单包]\r\n" +
                    $"HEX: {BitConverter.ToString(packet)}\r\n" +
                    $"-----------------------\r\n"
                );
            }));
        }
        byte[] BuildPlaceOrderFrame(string json)
        {
            var buffer = new List<byte>();

            // ===== 固定头（来自官方抓包）=====
            buffer.Add(0xBA);
            buffer.Add(0x01);
            buffer.Add(0x00);
            buffer.Add(0x05);

            // "trade"
            buffer.AddRange(Encoding.ASCII.GetBytes("trade"));

            // ===== 会话标记 / 序号（照抄官方）=====
            buffer.Add(0x2C);
            buffer.Add(0x36);

            // ===== JSON Payload =====
            buffer.AddRange(Encoding.UTF8.GetBytes(json));

            return buffer.ToArray();
        }
        //string json = BuildPlaceOrderJson(
        //    mId: 360245,
        //    bId: 1,
        //    num: 1,
        //    direction: 1,
        //    oflag: 1,
        //    price: 1511,
        //    bond: 1,
        //    oid: oid,
        //    clientId: "f5730aee-249f-4047-b0d2-53bae9ab42f1"
        //);
        string BuildPlaceOrderJson(
    int mId,
    int bId,
    int num,
    int direction,
    int oflag,
    int price,
    int bond,
    long oid,
    string clientId)
        {
            return
                "{" +
                $"\"instruct\":\"place_order\"," +
                $"\"m_id\":{mId}," +
                $"\"b_id\":{bId}," +
                $"\"num\":{num}," +
                $"\"direction\":{direction}," +
                $"\"oflag\":{oflag}," +
                $"\"price\":{price}," +
                $"\"bond\":{bond}," +
                $"\"oid\":{oid}," +
                $"\"record_type\":1," +
                $"\"clientId\":\"{clientId}\"" +
                "}";
        }
        private ClientWebSocket tradeWs;
    
        private TradeEngine engine;
        private byte[] HexStringToBytes(string hex)
        {
            hex = hex
                .Replace("0x", "")
                .Replace(",", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ");

            var parts = hex
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            byte[] bytes = new byte[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                bytes[i] = Convert.ToByte(parts[i], 16);
            }

            return bytes;
        }
        private RawWsMqttTradeClient _tradeClient;
        private OrderService _orderService;

        public static byte[] BuildPlaceOrderPayload_QoS0(
    int mId, int num, int price, string clientId,
    int direction = 1, int oflag = 1, int bId = 1, int bond = 1, int recordType = 1)
        {
            string json =
                "{"
                + "\"instruct\":\"place_order\","
                + $"\"m_id\":{mId},"
                + $"\"b_id\":{bId},"
                + $"\"num\":{num},"
                + $"\"direction\":{direction},"
                + $"\"oflag\":{oflag},"
                + $"\"price\":{price},"
                + $"\"bond\":{bond},"
                + "\"oid\":0,"
                + $"\"record_type\":{recordType},"
                + $"\"clientId\":\"{clientId}\""
                + "}";

            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] topic = Encoding.ASCII.GetBytes("trade");

            var body = new List<byte>();
            // Topic
            body.Add((byte)(topic.Length >> 8));
            body.Add((byte)(topic.Length & 0xFF));
            body.AddRange(topic);
            // Payload
            body.AddRange(payload);

            var pkt = new List<byte>();
            pkt.Add(0x30); // PUBLISH QoS0
            WriteRemainingLength2(pkt, body.Count);
            pkt.AddRange(body);
            return pkt.ToArray();
        }

        private static void WriteRemainingLength2(List<byte> frame, int len)
        {
            do
            {
                byte digit = (byte)(len % 128);
                len /= 128;
                if (len > 0) digit |= 0x80;
                frame.Add(digit);
            } while (len > 0);
        }

        private async void button4_Click(object sender, EventArgs e)
        {

            byte[] payload = BuildPlaceOrderPayload(
      user_id,
      1,
      1411,
      _clientId
  );
           
           // client.SendRaw(payload);

 //           byte[] packet = BuildPlaceOrderPacket(user_id, 1, 1411, _clientId, 1, 1, 1, 1, 1);
 //           Console.WriteLine(
 //    string.Join(" ", packet.Select(b => b.ToString("X2")))
 //);
          //  client.SendRaw(packet);
            client.SendRaw(payload);
            ////_Xdzt.StrategyId = 1;
            ////_Xdzt.oid = -1;
            ////_Xdzt.listing_no = -1;
            ////_Xdzt.gm_price = int.Parse(textBox4.Text);
            //// string clientId2 = _orderService.PlaceOrder(packet, 360245,_clientId);
            //client.SendRaw(packet);
            tiao_xu = 0;
            txtResult.AppendText(
                $"📤 已发送下单 clientId=\r\n");
            //try
            //{
            //    string hex = txtHex.Text.Trim();
            //    if (string.IsNullOrEmpty(hex))
            //    {
            //        MessageBox.Show("请输入 HEX 数据");
            //        return;
            //    }

            //    byte[] data = HexStringToBytes(hex);

            //    client.SendRaw(data);

            //    Console.WriteLine($"[MANUAL SEND] len={data.Length}\r\n{BitConverter.ToString(data)}");
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show("发送失败: " + ex.Message);
            //}
            return;

            //if (tradeWs != null && tradeWs.State == WebSocketState.Open)
            //        return;

            //    tradeWs = new ClientWebSocket();

            //    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            //    WebRequest.DefaultWebProxy = null;

            //    tradeWs.Options.SetRequestHeader(
            //        "Authorization",
            //        "Bearer " + token
            //    );
            //    //tradeWs.Options.SetRequestHeader("User-Agent", "Mozilla/5.0");
            //    tradeWs.Options.SetRequestHeader("Origin", "https://47.57.4.140");

            //    tradeWs.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

            //    await tradeWs.ConnectAsync(
            //        new Uri("wss://47.57.4.140:8006/"),
            //        CancellationToken.None
            //    );

            //    Console.WriteLine("✅ Trade WS 已连接");
           




        }

        private async void button5_Click(object sender, EventArgs e)
        {
            await LoadOrdersAsync();
            return;
            var queryService = new OrderQueryService(token);

            var result = await queryService.GetOrderListAsync();

            if (result.code == 0)
            {
                foreach (var order in result.data.data)
                {
                    Console.WriteLine(
                        $"订单号:{order.oid} 价格:{order.price} 数量:{order.num} 状态:{order.status}"
                    );
                }
            }
            else
            {
                Console.WriteLine("获取失败：" + result.msg);
            }

        }

        private void button6_Click(object sender, EventArgs e)
        {

            byte[] packet = BuildPlaceOrderPacket(user_id, 1, int.Parse(textBox4.Text), _clientId, 2, 2, 1, 1, 1);



            string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);

            txtResult.AppendText(
                $"📤 已发送下单 clientId={clientId2}\r\n");
        }

        private async void button7_Click(object sender, EventArgs e)
        {
            await LoadOrdersAsync2();
        }
        List<long> _chedanzu = new List<long>();
        private void dataGridView2_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dataGridView2.Columns[e.ColumnIndex].Name == "Action")
            {
                var row = (OrderRowVM)dataGridView2.Rows[e.RowIndex].DataBoundItem;
                CancelOrder(row);
            }
            if (dataGridView2.Columns[e.ColumnIndex].Name == "Action2")
            {
                var row = (OrderRowVM)dataGridView2.Rows[e.RowIndex].DataBoundItem;
                ZhuanOrder(row);
            }
        }
        int xd_bianhao = -1;
        bool _shua2 = true;
        int weicheng_miao = 0;
        bool _shua3 = true;
        bool _shua4 = true;
        bool _shua5 = true;
        int shua_jishi = 0;
        bool _shua6 = true;
     
              
        //        foreach(StrategyContext n_sc in _strategyList)
        //        {
        //            if(n_sc.buzhou==3  )
        //            {
        //                if (n_sc.Oid == -1)
        //                {
        //                    f2 = true;
        //                    break;
        //                }
        //            }
        //        }
        //        if (f2)
        //        {
        //            AppendLog("有编号，去刷新");
        //            await LoadOrdersAsync();
        //            await LoadOrdersAsync2();
        //            timer1.Interval =2000;
        //            return;
        //        }//||n_sc.buzhou==4
        //        if(sheng_shi>20 && sheng_shi < 24)
        //        {
        //            await LoadOrdersAsync2();
        //            timer1.Interval = 2000;
        //            return;

        //        }
        //        if (sheng_shi > 16 && sheng_shi < 20)
        //        {
        //            await LoadOrdersAsync();
        //            timer1.Interval = 2000;
        //            return;

        //        }

        //        //if (xd_bianhao != -1)
        //        //{
        //        //    timer1.Interval = 1000;
        //        //    weicheng_miao += 1;
        //        //    if (weicheng_miao > 1)
        //        //    {
        //        //        AppendLog("有问题！！！！！！！！！！！！！！！！");
        //        //        await LoadOrdersAsync();
        //        //         xd_bianhao = -1;

        //        //    }
        //        //    return;



        //        //}
        //        weicheng_miao = 0;
        //        if (sheng_shi < 25 && sheng_shi >15 && _shua2)
        //        {
        //            await LoadOrdersAsync2();
        //            timer1.Interval = 1000;
        //            _shua2 = false;
        //            return;

        //        }
        //        if (sheng_shi < 10)
        //        {
        //            _shua2 = true;
        //        }
        //        if (sheng_shi < 15)
        //        {
        //            if (_shua3)
        //            {
        //                await LoadOrdersAsync();
        //                _shua3 = false;
        //            }


        //        }
        //        else
        //        {
        //            _shua3 = true;
        //        }



        //        label4.Text = _strategyList.Count.ToString();
        //        int wancheng = 0;
             
        //        int chedan = 0;
        //        //_zx_price
        //        int yingkui = 0;
              
          
        //        //   if (sheng_shi >= _Scan.miao1 && sheng_shi <= _Scan.miao2)
        //        {
        //            //_Xdzt.StrategyId > 0 && _Xdzt.oid > 0 && _Xdzt.listing_no > 0 &&
        //            foreach (StrategyContext n_sc in _strategyList)
        //            {
        //                int yingkui2 = 0;
        //                if (n_sc.buzhou ==6 )
        //                {
                           
        //                    if (n_sc.duokong == 1)
        //                    {
        //                        n_sc.yingkui = (n_sc.shoujia - n_sc.maijia);
        //                    }
        //                    if (n_sc.duokong ==2)
        //                    {
        //                        n_sc.yingkui = (n_sc.maijia - n_sc.shoujia);
        //                    }

        //                }
        //                if(n_sc.buzhou>1&& n_sc.buzhou < 6)
        //                {
        //                    wancheng++;
        //                }
        //                if (n_sc.buzhou == 10)
        //                {
        //                    chedan++;
        //                }
        //                if(sheng_shi>=5 && sheng_shi<8 && n_sc.buzhou == 7)
        //                {
                           
        //                    byte[] packet = BuildcancelOrderPacket(
        //     user_id,
        //     n_sc.oid2,
        //     _clientId
        //);

        //                    n_sc.buzhou = 7;
        //                    xd_bianhao = n_sc.StrategyId;
        //                    client.SendRaw(packet);
        //                    //string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
        //                    AppendLog("编号：" + n_sc.StrategyId.ToString() + " 未完全卖出撤单重新挂");

        //                }


        //                if (n_sc.maimai==2 && sheng_shi <5 && n_sc.listing_no == 0) // (int)numericUpDown7.Value
        //                {
        //                    byte[] packet = BuildcancelOrderPacket(
        //        user_id,
        //        n_sc.Oid,
        //        _clientId
        //   );


        //                  //  xd_bianhao = n_sc.StrategyId;
        //                    n_sc.buzhou = 9;
        //                    client.SendRaw(packet);
        //                   //string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
        //                    AppendLog("编号：" + n_sc.StrategyId.ToString() + " 订货未成单撤单");
        //                    n_sc.maimai = 0;
                          
        //                    //
        //                }

        //                //  int passedMinutes = (int)(DateTime.Parse(closedKline.stime) - n_sc.TriggerTime.Value).TotalMinutes;
        //                if (!n_sc.shitou)
        //                {
        //                    continue;
        //                }

        //                if (n_sc.buzhou==2 && sheng_shi>50 && sheng_shi<=56) //sheng_shi >= _Scan.miao1 && sheng_shi <= _Scan.miao2)
        //                {
                           
        //                    int oflag = 1;
                            


        //                    int chuan = n_sc.closeprice;
        //                 byte[] packet = BuildPlaceOrderPacket(user_id, n_sc.beishu, chuan, _clientId, n_sc.duokong, oflag, 1, 1, 1);
        //                    //    byte[] packet = BuildPlaceOrderPacket(user_id, 1, n_sc.beishu, n_sc.maimai, n_sc.duokong, _zx_price, 1, n_sc.listing_no, 1, _clientId);

        //                    //_Xdzt.StrategyId = 1;
        //                    //_Xdzt.oid = -1;
        //                    //_Xdzt.listing_no = -1;
        //                    //_Xdzt.gm_price = int.Parse(textBox4.Text);
        //                 //   xd_bianhao = n_sc.StrategyId;
        //                    n_sc.maijia = chuan;
        //                    // string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
        //                    client.SendRaw(packet);
        //                    //client.SendRaw(packet, "PLACE_ORDER");
        //                    AppendLog("编号："+n_sc.StrategyId.ToString()+ " 订货成功");
        //                    n_sc.OrderSendTime = DateTime.Now;
        //                    n_sc.buzhou = 3;
        //                    n_sc.maimai = 2;
        //                    n_sc.shitou = false;

        //                }
        //                if (n_sc.buzhou == 4 && sheng_shi>56 && sheng_shi<=59 )//sheng_shi >= _Scan.miao3 && sheng_shi <= _Scan.miao4)
        //                {
        //                    int chuan = _zx_price;
        //                    int oflag = 2;
        //                    if (_xiantype == 0)
        //                    {
        //                        AppendLog("上个K线不完整，放弃转货");
        //                    }
        //                    else
        //                    {
        //                        if (n_sc.duokong == 1)
        //                        {
        //                            if ((chuan - n_sc.maijia) >= _Scan.zhiying)
        //                            {
        //                                byte[] packet = BuildzhuanOrderPacket(user_id, 1, n_sc.beishu, oflag, n_sc.maimai, chuan, 1, n_sc.listing_no, 1, _clientId);
        //                                xd_bianhao = n_sc.StrategyId;
        //                                //_Xdzt.StrategyId = 1;
        //                                //_Xdzt.oid = -1;
        //                                //_Xdzt.listing_no = -1;
        //                                //_Xdzt.gm_price = int.Parse(textBox4.Text);
        //                                // Console.WriteLine(user_id, n_sc.beishu, _zx_price, _clientId, n_sc.duokong, n_sc.maimai);
        //                                client.SendRaw(packet);
        //                                //  string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
        //                                n_sc.shoujia = chuan;
        //                                AppendLog("编号：" + n_sc.StrategyId.ToString() + " 止盈销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
        //                                xd_bianhao = n_sc.StrategyId;
        //                                n_sc.buzhou = 5;
        //                                n_sc.shitou = false;
        //                            }
        //                            if ((DateTime.Now - n_sc.xiacheng_time).TotalSeconds > _Scan.shoutime)
        //                                if ((chuan - n_sc.maijia) <= _Scan.zhisun)
        //                                {

        //                                    byte[] packet = BuildzhuanOrderPacket(user_id, 1, n_sc.beishu, oflag, 2, chuan, 1, n_sc.listing_no, 1, _clientId);
        //                                    xd_bianhao = n_sc.StrategyId;
        //                                    n_sc.shoujia = chuan;
        //                                    client.SendRaw(packet);
        //                                    // string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
        //                                    xd_bianhao = n_sc.StrategyId;
        //                                    AppendLog("编号：" + n_sc.StrategyId.ToString() + " 止损销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
        //                                    n_sc.buzhou = 5;
        //                                    n_sc.shitou = false;
        //                                }
        //                        }
        //                        if (n_sc.duokong == 2)
        //                        {
        //                            oflag = 2;
        //                            if ((n_sc.maijia - chuan) >= _Scan.zhiying)
        //                            {
        //                                byte[] packet = BuildzhuanOrderPacket(user_id, 1, n_sc.beishu, oflag, 1, chuan, 1, n_sc.listing_no, 1, _clientId);
        //                                xd_bianhao = n_sc.StrategyId;
        //                                //_Xdzt.StrategyId = 1;
        //                                //_Xdzt.oid = -1;
        //                                //_Xdzt.listing_no = -1;
        //                                //_Xdzt.gm_price = int.Parse(textBox4.Text);
        //                                // Console.WriteLine(user_id, n_sc.beishu, _zx_price, _clientId, n_sc.duokong, n_sc.maimai);
        //                                client.SendRaw(packet);
        //                                //string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
        //                                n_sc.shoujia = chuan;
        //                                AppendLog("编号：" + n_sc.StrategyId.ToString() + " 止盈销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
        //                                xd_bianhao = n_sc.StrategyId;
        //                                n_sc.buzhou = 5;
        //                                n_sc.shitou = false;
        //                            }
        //                            if ((DateTime.Now - n_sc.xiacheng_time).TotalSeconds > _Scan.shoutime)
        //                                if ((n_sc.maijia - chuan) <= _Scan.zhisun)
        //                                {

        //                                    byte[] packet = BuildzhuanOrderPacket(user_id, 1, n_sc.beishu, oflag, 1, chuan, 1, n_sc.listing_no, 1, _clientId);
        //                                    xd_bianhao = n_sc.StrategyId;
        //                                    n_sc.shoujia = chuan;
        //                                    client.SendRaw(packet);
        //                                    // string clientId2 = _orderService.PlaceOrder(packet, user_id, _clientId);
        //                                    xd_bianhao = n_sc.StrategyId;
        //                                    AppendLog("编号：" + n_sc.StrategyId.ToString() + " 止损销货(" + n_sc.maijia.ToString() + "/" + chuan.ToString() + ")");
        //                                    n_sc.buzhou = 5;
        //                                    n_sc.shitou = false;
        //                                }
        //                        }
        //                    }

                           

        //                }
                      
        //               // break;
        //            }

        //            dataGridView3.RowCount = _strategyList.Count + 1;
        //            for(int i=0;i<_strategyList.Count;i++)
        //            {
        //                StrategyContext n_sc = _strategyList[_strategyList.Count - 1 - i];
        //                dataGridView3.Rows[i].Cells[0].Value = n_sc.StrategyId;
        //                dataGridView3.Rows[i].Cells[1].Value = n_sc.TriggerTime.ToString();
        //                string dk_s = "";
        //                if (n_sc.buzhou > 1&& n_sc.buzhou<7)
        //                {
        //                    if (n_sc.duokong == 1)
        //                    {
        //                        dk_s = "多";
        //                    }
        //                    if (n_sc.duokong == 2)
        //                    {
        //                        dk_s = "空";
        //                    }

        //                }
                      
        //                dataGridView3.Rows[i].Cells[2].Value = dk_s;

        //                dataGridView3.Rows[i].Cells[3+2].Value = n_sc.beishu;

        //                if(n_sc.maijia>0)
        //                dataGridView3.Rows[i].Cells[4 + 2].Value = n_sc.maijia;
        //                if (n_sc.shoujia > 0)
        //                    dataGridView3.Rows[i].Cells[5 + 2].Value = n_sc.shoujia;
        //                string wcs = "";
        //                if (n_sc.buzhou == 1)
        //                {
        //                    wcs = "启动";
        //                }
        //                if (n_sc.buzhou == 2)
        //                {
        //                    wcs = "介入";
        //                }
        //                if (n_sc.buzhou == 3)
        //                {
        //                    wcs = "订货下单";
        //                }
        //                if (n_sc.buzhou == 4)
        //                {
        //                    wcs = "订货成功";
        //                }
        //                if (n_sc.buzhou == 5)
        //                {
        //                    wcs = "转货下单";
        //                }
        //                if (n_sc.buzhou == 6)
        //                {
        //                    wcs = "完成";
        //                }
        //                if (n_sc.buzhou == 10)
        //                {
        //                    wcs = "撤单";
        //                }


        //                dataGridView3.Rows[i].Cells[6 + 2].Value = n_sc.buzhou;
        //                if(n_sc.buzhou==6)
        //                dataGridView3.Rows[i].Cells[7 + 2].Value = n_sc.yingkui;
        //                else
        //                    dataGridView3.Rows[i].Cells[7 + 2].Value = "";
        //                if (n_sc.Oid>0)
        //                dataGridView3.Rows[i].Cells[8 + 2].Value = n_sc.Oid;
        //                else
        //                    dataGridView3.Rows[i].Cells[8 + 2].Value = "";
        //                dataGridView3.Rows[i].Cells[3].Value = n_sc.wanzheng_k;
        //                string lianxu = "";
        //                if (n_sc.lianyang > 0)
        //                {
        //                    lianxu = "阳[" + n_sc.lianyang.ToString() + "]";
        //                }
        //                if (n_sc.lianyin > 0)
        //                {
        //                    lianxu = "阴[" + n_sc.lianyin.ToString() + "]";
        //                }
        //                dataGridView3.Rows[i].Cells[4].Value = lianxu;
        //                yingkui += n_sc.yingkui;
        //            }


        //        }

        //        label5.Text = wancheng.ToString();
        //        label7.Text = yingkui.ToString();
        //        if (yingkui >= _Scan.z_zhiying)
        //        {
        //            AppendLog("止盈，停止挂机");
        //            button2_Click(null, null);
                   

        //        }
        //        if (yingkui <= _Scan.z_zhisun)
        //        {
        //            AppendLog("止损，停止挂机");
        //            button2_Click(null, null);
        //        }
        //        if (DateTime.Now >= _Scan.dingshi)
        //        {
        //            AppendLog("到指定时间，停止挂机");
        //            button2_Click(null, null);
//              }
//            }
//}}
//timer1.Interval = 1000;
    //    }

        private async void picCaptcha_DoubleClick(object sender, EventArgs e)
        {
            await LoadCaptchaAsync();
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (splitContainer1.SplitterDistance == 500)
            {
                button8.Text = "》";

                splitContainer1.SplitterDistance = 100;
            }
            else
            {
                button8.Text = "《";
                splitContainer1.SplitterDistance = 500;
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            client.Dispose();
            ConnectTradeWS();
        }

        private void button10_Click(object sender, EventArgs e)
        {
            panelChart.Visible = false;
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab == tabPage2)
            {
                panelChart.Invalidate(); // 🔥 关键
            }
            if (_historyBacktestTabPage != null && tabControl1.SelectedTab == _historyBacktestTabPage)
            {
                _historyBacktestChartPanel.Invalidate();
            }
        }
        public static async Task<bool> CheckLocalTimeAsync(
    int allowDiffSeconds,
    Action<string> onError)
        {
            try
            {
                DateTime beijingTime = await TimeCheckHelper.GetBeijingTimeAsync();
                DateTime localTime = DateTime.Now;

                double diff = Math.Abs((beijingTime - localTime).TotalSeconds);

                if (diff > allowDiffSeconds)
                {
                    onError?.Invoke(
                        $"检测到系统时间异常！\n\n" +
                        $"北京时间：{beijingTime:yyyy-MM-dd HH:mm:ss}\n" +
                        $"本地时间：{localTime:yyyy-MM-dd HH:mm:ss}\n\n" +
                        $"相差 {diff:F1} 秒\n\n" +
                        $"请同步系统时间后再启动软件。"
                    );
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                onError?.Invoke("时间校验失败，请检查网络。\n" + ex.Message);
                return false;
            }
        }

        private void cboAccount_SelectedIndexChanged(object sender, EventArgs e)
        {
            var user = cboAccount.SelectedItem as string;
            if (string.IsNullOrEmpty(user)) return;

            cboAccount.Text = user;

            var pwd = MainAccountStoreHelper.GetPassword(user);
            txtPass.Text = pwd ?? "";
        }
        int jr_fujia = 0;
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            jr_fujia = comboBox1.SelectedIndex;
        }
        bool shua_xin = true;
        private async void timer2_Tick(object sender, EventArgs e)
        {
            if (_timer2Busy)
                return;

            _timer2Busy = true;
            try
            {
            if (sheng_shi > 10)
            {
                if (t_list.Count > 0 || t_list2.Count > 0)
                {
                    Console.WriteLine("tt: "+t_list.Count.ToString()+" / "+t_list2.Count.ToString() );
                }

                if (shua_xin)
                {
                    try
                    {
                        await LoadOrdersAsync();
                    }
                    catch
                    {
                        Console.WriteLine("shuacuo ");
                        AppendLog("list 1 error");
                    }
                   
                }
                else
                {
                    try
                    {
                        await LoadOrdersAsync2();
                    }
                    catch
                    {
                        AppendLog("list 2 error");
                        Console.WriteLine("shuacuo2 ");
                    }
                   
                }
            }

            shua_xin = !shua_xin;
           //timer1.Interval=
            }
            catch (Exception ex)
            {
                AppendLog("timer2 error: " + ex.Message);
            }
            finally
            {
                _timer2Busy = false;
            }
        }
        bool _zhengfan = true;
        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            _zhengfan = false;
            AppendLog("切换为 反买");
            //radioButton4.BackColor = Color.DarkGray;


        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            AppendLog("切换为 正买");
            _zhengfan = true;
        }

        int t_yunxing = 0;
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            t_yunxing = comboBox2.SelectedIndex;
        }

        private void numEntryN_ValueChanged(object sender, EventArgs e)
        {
            _Scan.jr_can = (int)numEntryN.Value;
        }

        private void numericUpDown11_ValueChanged(object sender, EventArgs e)
        {
            _Scan.jr_can2 = (int)numericUpDown11.Value;
            AppendLog("阴阳连调节为：" + _Scan.jr_can2.ToString());
        }

        private void numLots_ValueChanged(object sender, EventArgs e)
        {
            _Scan.beishu = (int)numLots.Value;
            AppendLog("购买数量调节为：" + _Scan.beishu.ToString());
        }

        private void numTP_ValueChanged(object sender, EventArgs e)
        {
            _Scan.zhiying = (int)numTP.Value;
            AppendLog("止盈调节为：" + _Scan.zhiying.ToString());

        }

        private void numSL_ValueChanged(object sender, EventArgs e)
        {
            _Scan.zhisun = (int)numSL.Value;
            AppendLog("止损调节为：" + _Scan.zhisun.ToString());
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox5.Checked)
            {
                groupBox1.Enabled = false;
            }
            else
            {
                groupBox1.Enabled = true;
            }
        }
        bool tongxiang_f = true;
        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            tongxiang_f = checkBox6.Checked;

        }
        int new_jr = 0;

        private void cbEntryMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            new_jr = cbEntryMode.SelectedIndex;

            groupBox13.Visible = false;
            if (cbEntryMode.SelectedIndex == 0)
            {
                // numEntryN.Visible = false;
                numEntryN.Value = 1;
                groupBox13.Visible = true;
            }

            if (cbEntryMode.SelectedIndex == 1)
            {
                numEntryN.Visible = true;
                numEntryN.Value = 1;
            }
            if (cbEntryMode.SelectedIndex == 2)
            {
                numEntryN.Visible = true;
                numEntryN.Value = 2;
            }
        }
        bool quanmai_f = true;
        bool _quanmai = false;
        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            quanmai_f = checkBox7.Checked;
                        
        }
        bool xiangjiaoqingkong_f = true;
        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            xiangjiaoqingkong_f = checkBox8.Checked;
        }

        private void button10_Click_1(object sender, EventArgs e)
        {
            //SendWsBinary(new byte[] { 0xC0, 0x00 });
            client.SendRaw(new byte[] { 0xC0, 0x00 });
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            tiao_xu++;
           
            //if (tiao_xu > 112)
            //{
            //    Console.WriteLine(tiao_xu);
            //    AppendLog("掉线,重连");
            //    //client.Dispose();
            //    //ConnectTradeWS();
            //  //  tiao_xu = 24;
            //}
        }

        private void button11_Click(object sender, EventArgs e)
        {
            btnLogin_Click(null, null);
        }

        private void picCaptcha_Click(object sender, EventArgs e)
        {

        }
        int new4_can = 2;
        private void numericUpDown27_ValueChanged(object sender, EventArgs e)
        {
           _Scan.jr_can = (int)numericUpDown27.Value;
        }
        int new3_buzhou = 1;
        private void comboBox7_SelectedValueChanged(object sender, EventArgs e)
        {


            new_jr = comboBox7.SelectedIndex;
            //if (new_jr == 2 || new_jr == 3)
            //{
            //    new_jr = 4;
            //}
            if (new_jr == 6 || new_jr == 4)
            {
                numericUpDown13.Visible = true;
                label50.Visible = true;
            }
            else
            {
                numericUpDown13.Visible = false;
                label50.Visible = false;
            }

            if (new_jr == 0)
            {
                checkBox16.Visible = true;
            }
            else
            {
                checkBox16.Visible = false;
            }
            if (new_jr != 3 && new_jr != 1)
            {
                //  groupBox16.Visible = false;
                numericUpDown27.Visible = false;
            }
            else
            {
                //  groupBox16.Visible = true;
                numericUpDown27.Visible = true;
            }

            if (new_jr == 3 || new_jr == 4)//new_jr == 2 || 
            {
                groupBox19.Visible = true;
            }
            else
            {
                groupBox19.Visible = false;
            }
            if (new_jr == 3)
            {
                comboBox3.Visible = true;
            }
            else
            {
                comboBox3.Visible = false;
            }



        }
        int _m4_can = 0;
        bool _m4_up = true;
        bool _m4_down = true;
        List<int> _m_mai = new List<int>();
        private void numericUpDown12_ValueChanged(object sender, EventArgs e)
        {
            _Scan.beishu = (int)numericUpDown12.Value;
            AppendLog("购买数量调节为：" + _Scan.beishu.ToString());
        }

        private void checkBox14_CheckedChanged(object sender, EventArgs e)
        {
            quanmai_f = checkBox14.Checked;
            _quanmai = false;
        }
        bool _quanmai_zhisun_f = false;
        int n4_zhiying = 5;
        int n4_zhisun = -5;
        bool n4_chushou = false;
        int n4_price = -1;
        int n4_nowprice = -1;
        int n4_beishu = 0;
        int n4_nowyingkui = 0;
        int n_bei_xu = 0;
        int _newjr_cuoci = 0;
        bool _xincelue = false;
        bool new_jrcan2 = false;
        bool new_jrcan3 = false;
        bool new_jrcan4 = false;
        int jr_price = -1;
        bool yinyang_qie = true;
        int jr3_zt = 0;
        int shouxu = 0;
        bool jixu = true;
        List<int> _xince = new List<int>();
        int _maifangxiang = 0;
        int _jindian = 0;
        int n5_fangxiang = 0;
        List<int> _new_tj = new List<int>();
        bool honglan_jiaoyi = true;
        bool shifou_wanzheng = false;
        int _setcuoci = 0;

        string _dayin = "";
        bool tou_f = false;
        bool tou_duokong = false;
        int tou_duicuo = 0;
        bool _tou_duokong = true;
        int _tou_duicuo = 0;
        int _dengyifenzhong = 0;

        bool n4_duokong = true;
        int n4_meige = 2;

        private void checkBox10_CheckedChanged(object sender, EventArgs e)
        {
            _quanmai_zhisun_f = checkBox10.Checked;
            _quanmai = false;
        }
        int jin_zhengfan = 0;
        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            jin_zhengfan = comboBox3.SelectedIndex;
          
        }

        private void checkBox9_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox9.Checked)
            {
                _shizhan_f = true;
            }
            else
            {
                _shizhan_f = false;
            }
        }
        int new3_cangshu = 0;
        List<int> _suoding = new List<int>();
        int new3_cishu = 0;
        bool _yizhimai = false;
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                textBox5.Enabled = false;
                _Scan.z_zhiying = int.Parse(textBox5.Text);
            }
            else
            {
                textBox5.Enabled = true;
            }
        }

        private void checkBox16_CheckedChanged(object sender, EventArgs e)
        {
            _yizhimai = checkBox16.Checked;
        }

        int _m_fenzhong = 20;
        private void numericUpDown13_ValueChanged(object sender, EventArgs e)
        {
            _m_fenzhong = (int)numericUpDown13.Value;
        }
    }
}
