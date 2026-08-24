using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.IO;
using System.Threading;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using HtmlAgilityPack;

namespace WIN
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            // 开启自动缩放（根据字体或 DPI）
            this.AutoScaleMode = AutoScaleMode.Font;

            // 允许在小屏幕显示时出现滚动条
            this.AutoScroll = true;

            // 加载时处理分辨率
           // this.Load += AdaptiveForm_Load;
        }
        public class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; }
            public int ProcessId { get; set; }
            public string ProcessName { get; set; }
        }
        // Windows API functions
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        // POINT structure
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        private Point originalLocation;
        private Point mouseDownLocation;
        private bool isDragging = false;
        private bool isDragging2 = false;
        private bool isDragging3 = false;
        private Cursor customCursor;

       
        private void button1_Click(object sender, EventArgs e)
        {
            // 获取所有带有主窗口的进程
            var processes = Process.GetProcesses()
                .Where(p => p.MainWindowHandle != IntPtr.Zero);

            foreach (var process in processes)
            {
                // 获取进程的可执行文件路径
                string processPath = process.MainModule?.FileName;
                string processDirectory = System.IO.Path.GetDirectoryName(processPath);
                Console.WriteLine($"进程名: {process.ProcessName}, 窗口标题: {process.MainWindowTitle}");
            }
        }


        public static string GetWindowTitle(IntPtr hWnd)
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            if (GetWindowText(hWnd, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        // 导入必要的 Windows API 函数
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);



        private void button1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                mouseDownLocation = e.Location;
                originalLocation = button1.Location;
                // Create a custom cursor using the crosshair image
                customCursor = CreateCrosshairCursor(button1.Size);
                Cursor.Current = customCursor;

                

                //// Hide the button
                // button1.Visible = false;
            }
            //if (e.Button == MouseButtons.Left)
            //{
            //    isDragging = true;
            //    mouseDownLocation = e.Location;
            //    originalLocation = button1.Location;//= originalLocation;
            //    // Create a custom cursor using the button's image
            //    Bitmap bitmap = new Bitmap(button1.Width, button1.Height);
            //    button1.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            //    customCursor = CreateCursor(bitmap, 0, 0);
            //    Cursor.Current = customCursor;

            //    // Hide the button
            //    button1.Visible = false;
            //}
        }
        private Cursor CreateCrosshairCursor(Size size)
        {
            Bitmap bmp = CreateCrosshairImage(size);
            IntPtr ptr = bmp.GetHicon();
            IconInfo tmp = new IconInfo();
            GetIconInfo(ptr, ref tmp);
            tmp.xHotspot = size.Width / 2;
            tmp.yHotspot = size.Height / 2;
            tmp.fIcon = false;
            ptr = CreateIconIndirect(ref tmp);
            return new Cursor(ptr);
        }
        private Bitmap CreateCrosshairImage(Size size)
        {
            Bitmap bmp = new Bitmap(size.Width, size.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);

                Pen pen = new Pen(Color.Red, 6);
                int midX = size.Width / 2;
                int midY = size.Height / 2;
                g.DrawLine(pen, midX, 0, midX, size.Height); // Vertical line
                g.DrawLine(pen, 0, midY, size.Width, midY); // Horizontal line
            }
            return bmp;
        }
        private Cursor CreateCursor(Bitmap bmp, int xHotSpot, int yHotSpot)
        {
            IntPtr ptr = bmp.GetHicon();
            IconInfo tmp = new IconInfo();
            GetIconInfo(ptr, ref tmp);
            tmp.xHotspot = xHotSpot;
            tmp.yHotspot = yHotSpot;
            tmp.fIcon = false;
            ptr = CreateIconIndirect(ref tmp);
            return new Cursor(ptr);
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct IconInfo
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }
        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateIconIndirect(ref IconInfo icon);
        private void button1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                // Update the mouse cursor position
                Point screenPosition = Control.MousePosition;
                Cursor.Position = screenPosition;
            }
        }
        List<IntPtr> H_z = new List<IntPtr>();
        private void button1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;

                // Restore the cursor
                Cursor.Current = Cursors.Default;

                // Move the button back to the original location and make it visible again
                button1.Location = originalLocation;
                button1.Visible = true;
                GetWindowInfoUnderCursor();
            }
        }
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, System.Text.StringBuilder lParam);
        static string wansha = "";
        static string qiansha = "";
        static string[] zhongying = { "-1", "-1", "-1", "-1", "-1"};
        public static void _jiexi(string s1)
        {
            string[] parts = s1.Split('\n');
            
            for(int i = 0; i < 5; i++)
            {
                zhongying[i] = "-1";
            }

            for (int i = 0; i < parts.Count(); i++)
            {
                string sz2 = parts[i];
                if (sz2.Length < 3)
                {
                    continue;
                }
                if (sz2.Contains(touruqi+"期") && sz2.Contains("等开") && sz2.Contains("万位杀码"))
                {
                    string[] kk = parts[i + 2].Replace("\r", "").Split(' ');
                    Console.WriteLine(kk);
                    zhongying[0] = kk[0];

                }
                if (sz2.Contains(touruqi + "期") && sz2.Contains("等开") && sz2.Contains("千位杀码"))
                {
                    string[] kk = parts[i + 2].Replace("\r", "").Split(' ');
                    Console.WriteLine(kk);
                    zhongying[1] = kk[0];
                   
                }
                if (sz2.Contains(touruqi + "期") && sz2.Contains("等开") && sz2.Contains("百位杀码"))
                {
                    string[] kk = parts[i + 2].Replace("\r", "").Split(' ');
                    Console.WriteLine(kk);
                    zhongying[2] = kk[0];

                }
                if (sz2.Contains(touruqi + "期") && sz2.Contains("等开") && sz2.Contains("十位杀码"))
                {
                    string[] kk = parts[i + 2].Replace("\r", "").Split(' ');
                    Console.WriteLine(kk);
                    zhongying[3] = kk[0];

                }
                if (sz2.Contains(touruqi + "期") && sz2.Contains("等开") && sz2.Contains("个位杀码"))
                {
                    string[] kk = parts[i + 2].Replace("\r", "").Split(' ');
                    Console.WriteLine(kk);
                    zhongying[4] = kk[0];

                }

            }
            //label14.Text = "万位杀： " + wansha + "千位杀： " + qiansha;
        }
        static int quleng = 13;

        const uint WM_GETTEXT = 0x000D;
        const uint WM_GETTEXTLENGTH = 0x000E;
        public IntPtr hWnd3= IntPtr.Zero;
        private void GetWindowInfoUnderCursor()
        {

            //https://api.chuanqiking.com/CQShiCai/getBaseCQShiCaiList?date=2025-09-08&lotCode=11
            // Get the cursor position
            if (GetCursorPos(out POINT cursorPos))
            {
                // Get the window handle from the cursor position
                IntPtr hWnd = WindowFromPoint(cursorPos);
                if (hWnd != IntPtr.Zero)
                {
                    // Get the window title
                    int length = (int)SendMessage(hWnd, WM_GETTEXTLENGTH, IntPtr.Zero, null);

                    // 定义一个缓冲区来接收文本内容
                    System.Text.StringBuilder windowText = new System.Text.StringBuilder(length + 1);

                    // 发送WM_GETTEXT消息获取窗口文本
                    SendMessage(hWnd, WM_GETTEXT, (IntPtr)windowText.Capacity, windowText);



                        // Display the information
                        //label1.Text=($"Cursor Position: X = {cursorPos.X}, Y = {cursorPos.Y}\n" +
                        //                $"Window Handle: {hWnd}\n" +
                        //                $"Window Title: {windowText}\n" +
                        //                $"Window Class: {className}");
                        //_fh_class fh= _jiexi(windowText.ToString(), cb_wf[0].SelectedIndex);
                        //label4.Text = fh.qi + " " + fh.kj;
                        //label5.Text = fh.fh.Count.ToString() + " 注";
                        _jiexi(windowText.ToString());
                    label14.Text = string.Join(",", zhongying);
                    // richTextBox2.Text = windowText.ToString();
                    hWnd3 = hWnd;
                    //label8.Text = fh.zu_san.Count.ToString() + " 注";

                    //richTextBox1.Text = string.Join("\r\n", fh.zu_san);
                    //label9.Text = fh.zu_liu.Count.ToString() + " 注";

                    //richTextBox2.Text = string.Join("\r\n", fh.zu_liu);
                    //F_z[xu] = fh;

                    //   H_z[xu] = hWnd;


                }
                else
                {
                    // MessageBox.Show("No window found at the cursor position.");
                }
            }
            else
            {
                // MessageBox.Show("Failed to get cursor position.");
            }
        }


        /// <summary>
        /// 获取所有可见窗口的信息
        /// </summary>
        public static List<WindowInfo> GetAllWindows()
        {
            var windows = new List<WindowInfo>();
            EnumWindowsProc callback = (hWnd, lParam) =>
            {
                // 只处理可见窗口
                if (IsWindowVisible(hWnd))
                {
                    // 获取窗口标题
                    int length = GetWindowTextLength(hWnd);
                    if (length > 0)
                    {
                        StringBuilder titleBuilder = new StringBuilder(length + 1);
                        GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                        string title = titleBuilder.ToString();

                        // 获取进程ID
                        uint processId;
                        GetWindowThreadProcessId(hWnd, out processId);

                        // 获取进程名（不访问模块，避免32/64位问题）
                        string processName = GetProcessNameWithoutModules((int)processId);

                        windows.Add(new WindowInfo
                        {
                            Handle = hWnd,
                            Title = title,
                            ProcessId = (int)processId,
                            ProcessName = processName
                        });
                    }
                }
                return true;
            };

            EnumWindows(callback, IntPtr.Zero);
            return windows;
        }

        /// <summary>
        /// 不通过模块获取进程名的方法（避免32/64位访问问题）
        /// </summary>
        private static string GetProcessNameWithoutModules(int processId)
        {
            try
            {
                // 使用Process类但不访问MainModule
                var process = Process.GetProcessById(processId);
                return process.ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// 获取当前活动窗口的信息
        /// </summary>
        public static WindowInfo GetActiveWindow()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
                return null;

            // 获取窗口标题
            int length = GetWindowTextLength(hWnd);
            StringBuilder titleBuilder = new StringBuilder(length + 1);
            GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            string title = titleBuilder.ToString();

            // 获取进程ID
            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);

            // 获取进程名
            string processName = GetProcessNameWithoutModules((int)processId);

            return new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ProcessId = (int)processId,
                ProcessName = processName
            };
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        // 获取特定进程的目录（优先尝试安全方法）
        // 获取所有可见窗口及其运行目录

        [DllImport("kernel32.dll")]
        static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags,
    StringBuilder lpExeName, ref int size);
        public static string GetProcessPath(int processId)
        {
            var process = Process.GetProcessById(processId);
            StringBuilder sb = new StringBuilder(1024);
            int size = sb.Capacity;

            if (QueryFullProcessImageName(process.Handle, 0, sb, ref size))
            {
                return sb.ToString();
            }
            return null;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("psapi.dll")]
        static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule,
            [Out] StringBuilder lpBaseName, [In][MarshalAs(UnmanagedType.U4)] int nSize);

        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint PROCESS_VM_READ = 0x0010;

        public static string GetProcessPathSafe(int processId)
        {
            IntPtr processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
            if (processHandle == IntPtr.Zero)
            {
                // 获取错误代码
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"打开进程失败，错误代码: {error}");
                return null;
            }

            try
            {
                StringBuilder sb = new StringBuilder(1024);
                if (GetModuleFileNameEx(processHandle, IntPtr.Zero, sb, sb.Capacity) > 0)
                {
                    return sb.ToString();
                }
            }
            finally
            {
                Marshal.Release(processHandle);
            }
            return null;
        }
        public static string GetProcessDirectoryWMI(int processId)
        {
            try
            {
                // 使用更安全的查询方式
                var query = $"SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {processId}";

                using (var searcher = new ManagementObjectSearcher(query))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        string exePath = obj["ExecutablePath"]?.ToString();
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            try
                            {
                                return Path.GetDirectoryName(exePath);
                            }
                            catch (ArgumentException ex)
                            {
                                Console.WriteLine($"路径格式无效: {ex.Message}");
                                continue;
                            }
                        }
                    }
                }
            }
            catch (ManagementException ex)
            {
                Console.WriteLine($"WMI 查询失败 (管理异常): {ex.Message}");
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Console.WriteLine($"WMI 查询失败 (COM 异常): {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WMI 查询失败 (一般异常): {ex.Message}");
            }

            return null;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);


        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        /// <summary>
        /// 获取进程路径（兼容32/64位）
        /// </summary>
        public static string GetProcessPath2(int processId)
        {
            IntPtr processHandle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (processHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"OpenProcess 失败，错误代码: {error}");
                return null;
            }

            try
            {
                int bufferSize = 1024;
                StringBuilder sb = new StringBuilder(bufferSize);
                if (QueryFullProcessImageName(processHandle, 0, sb, ref bufferSize))
                {
                    return sb.ToString();
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"QueryFullProcessImageName 失败，错误代码: {error}");
                    return null;
                }
            }
            finally
            {
                CloseHandle(processHandle);
            }
        }

        public class FirstLineMonitor
        {
            private DataGridView _dataGridView;
            private RichTextBox _richtext;
            private int _kaix;
            private int _toux;
            private int _kaix2;
            private int _toux2;
            private string touqi="";
            private int touwei;
            private int touhao;

            private int touwei2;
            private int touhao2;

            private Label _label;
            private DateTime _lastEventTime = DateTime.MinValue;
            private FileSystemWatcher watcher;
            private bool _isMonitoring;
            private Label lb_ok;
            private Label lb_no;

            private int ok_shu = 0;
            private int no_shu = 0;

            private int ok_shu2 = 0;
            private int no_shu2 = 0;

            private int _zf1 = 0;
            private int _zf2 = 0;

            private RichTextBox _richtext2;
            private Label _label2;
            private Label _lbshu;
            private NumericUpDown _nm1;
            private IntPtr _h3;
            private Label _lbsha;
            public FirstLineMonitor(DataGridView dataGridView,RichTextBox richText,int kx,int tx,Label label2,Label label3,Label label4,int kx2,int tx2,int zf1,int zf2,RichTextBox rb2,Label lb7,Label lb8,NumericUpDown nm1,IntPtr h3,Label lbsha)
            {
                _dataGridView = dataGridView;
                _richtext = richText;
                _kaix = kx;
                _toux = tx;
                _kaix2 = kx2;
                _toux2 = tx2;
                _label = label2;
                lb_ok = label3;
                lb_no = label4;
                _zf1 = zf1;
                _zf2 = zf2;
                _richtext2 = rb2;
                _label2 = lb7;
                touwei = 0;
                touwei2 = 1;
                _lbshu = lb8;

                _nm1 = nm1;
                _h3 = h3;
                _lbsha = lbsha;



               // InitializeGridView();
            }

            private void InitializeGridView()
            {
                _dataGridView.Columns.Clear();
                _dataGridView.Columns.Add("LineContent", "文件首行");
                _dataGridView.Columns.Add("UpdateTime", "更新时间");
            }

            public void StartMonitoring(string filePath)
            {
                watcher = new FileSystemWatcher
                {
                    Path = Path.GetDirectoryName(filePath),
                    Filter = Path.GetFileName(filePath),
                    NotifyFilter = NotifyFilters.LastWrite
                };

                watcher.Changed += (sender, e) =>
                {
                    if (DateTime.Now - _lastEventTime < TimeSpan.FromMilliseconds(500)) return;
                    _lastEventTime = DateTime.Now;

                    Thread.Sleep(100); // 等待文件释放
                    UpdateFirstLine(e.FullPath);
                };
                _isMonitoring = true;
                watcher.EnableRaisingEvents = true;
            }
            public void StopMonitoring()
            {
                if (watcher != null)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                    _isMonitoring = false;
                }
            }
            public void Dispose()
            {
                StopMonitoring();
            }

            private void UpdateFirstLine(string path)
            {
                // 如果不需要处理 FileStream，可以直接使用这个方法

                try
                {
                    string[] allLines = File.ReadAllLines(path, Encoding.UTF8);
                    //foreach (string line in allLines)
                    //{
                    //    Console.WriteLine(line);

                    //}
                    UpdateGridView(allLines);
                }
                catch
                {

                }
               
                //try
                //{
                //    string firstLine;
                //    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                //    using (var reader = new StreamReader(fs))
                //    {
                //        firstLine = reader.ReadLine() ?? "[空行]";
                //    }
                   

                //    UpdateGridView(firstLine);
                //}
                //catch (Exception ex)
                //{
                //    UpdateGridView($"错误: {ex.Message}");
                //}
            }
            // 使用System.Windows.Forms.Timer替代while循环
           
            static List<string> _Tz = new List<string>();
            static List<string> _Tz2 = new List<string>();
            static string t_qi = "";
            private void UpdateGridView(string[] content)
            {
                string[] cz = content[0].Split('\t');
                //if (cz[0] == touqi)
                //{
                //    return;
                //}
                if (_dataGridView.InvokeRequired)
                {
                    _dataGridView.Invoke(new Action(() =>
                    {
                     
                        


                        _lbshu.Text = content.Count().ToString();
                        if (t_qi == _lbshu.Text)
                        {
                            return;
                        }

                        t_qi = _lbshu.Text;


                     //   if (touruqi !=t_qi )
                        {
                            if (touwei == 0)
                            {
                                string kshu = cz[1].Substring(0, 3);
                                if (_zf1==1)
                                {
                                    if (tz1.Contains(kshu)) 
                                   // if (kshu.Contains(touhao.ToString()))
                                    {
                                        ok_shu += 1;
                                        lb_ok.Text = "前三 中：" + ok_shu.ToString() + "  错:" + no_shu.ToString();
                                        _dataGridView.Rows[0].Cells[3].Value = "中";
                                    }
                                    else
                                    {
                                        no_shu += 1;
                                        lb_ok.Text = "前三 中：" + ok_shu.ToString() + "  错:" + no_shu.ToString();
                                        _dataGridView.Rows[0].Cells[3].Value = "挂";
                                    }
                                }
                                else
                                {
                                    if (tz1.Contains(kshu))
                                    //    if (!kshu.Contains(touhao.ToString()))
                                    {
                                        ok_shu += 1;
                                        lb_ok.Text = "前三 中：" + ok_shu.ToString() + "  错:" + no_shu.ToString();
                                        _dataGridView.Rows[0].Cells[3].Value = "中";
                                    }
                                    else
                                    {
                                        no_shu += 1;
                                        lb_ok.Text = "前三 中：" + ok_shu.ToString() + "  错:" + no_shu.ToString();
                                        _dataGridView.Rows[0].Cells[3].Value = "挂";
                                    }
                                }
                               
                            }
                            else
                            {
                                //string kshu = cz[1].Substring(1, 4);
                                //if (kshu.Contains(touhao.ToString()))
                                //{
                                //    ok_shu += 1;
                                //    lb_ok.Text = "正确个数： " + ok_shu.ToString();
                                //    _dataGridView.Rows[0].Cells[3].Value = "中";
                                //}
                                //else
                                //{
                                //    no_shu += 1;
                                //    lb_no.Text = "错误个数： " + no_shu.ToString();
                                //    _dataGridView.Rows[0].Cells[3].Value = "挂";
                                //}

                            }

                            if (touwei2 == 1)
                            {
                                string kshu = cz[1].Substring(2, 3);
                                if (_zf2 == 1)
                                {
                                    if (tz2.Contains(kshu))
                                      //  if (kshu.Contains(touhao2.ToString()))
                                    {
                                        ok_shu2 += 1;
                                        lb_no.Text = "后三 中：" + ok_shu2.ToString() + "  错:" + no_shu2.ToString();
                                        _dataGridView.Rows[0].Cells[3].Value = _dataGridView.Rows[0].Cells[3].Value + "/中";
                                    }
                                    else
                                    {
                                        no_shu2 += 1;
                                        lb_no.Text = "后三 中：" + ok_shu2.ToString() + "  错:" + no_shu2.ToString();
                                        _dataGridView.Rows[0].Cells[3].Value = _dataGridView.Rows[0].Cells[3].Value + "/挂";
                                    }
                                }
                                else
                                {
                                    if (tz2.Contains(kshu))
                                        //if (!kshu.Contains(touhao2.ToString()))
                                    {
                                        ok_shu2 += 1;
                                        lb_no.Text = "后三 中：" + ok_shu2.ToString() + "  错:" + no_shu2.ToString();
                                        _dataGridView.Rows[0].Cells[3].Value = _dataGridView.Rows[0].Cells[3].Value + "/中";
                                    }
                                    else
                                    {
                                        no_shu2 += 1;
                                        lb_no.Text = "后三 中：" + ok_shu2.ToString() + "  错:" + no_shu2.ToString();
                                        _dataGridView.Rows[0].Cells[3].Value = _dataGridView.Rows[0].Cells[3].Value + "/挂";
                                    }
                                }
                               
                            }
                            else
                            {
                                //string kshu = cz[1].Substring(1, 4);
                                //if (kshu.Contains(touhao.ToString()))
                                //{
                                //    ok_shu += 1;
                                //    lb_ok.Text = "正确个数： " + ok_shu.ToString();
                                //    _dataGridView.Rows[0].Cells[3].Value = "中";
                                //}
                                //else
                                //{
                                //    no_shu += 1;
                                //    lb_no.Text = "错误个数： " + no_shu.ToString();
                                //    _dataGridView.Rows[0].Cells[3].Value = "挂";
                                //}

                            }

                        }
                        _dataGridView.Rows[0].Cells[0].Value = cz[0];
                        _dataGridView.Rows[0].Cells[1].Value = cz[1];
                        touqi = cz[0];
                        touruqi = (int.Parse(cz[0].Substring(cz[0].Length-4,4))+1).ToString();
                        if (touruqi == "1025")
                        {
                            touruqi = "001";
                        }
                       // t_qi = touruqi;
                        _dataGridView.Rows.Insert(0, new DataGridViewRow());
                        int kt= int.Parse(cz[1].Substring(_kaix, 1));
                        int kt2= int.Parse(cz[1].Substring(_kaix2, 1));
                       
                        touhao = kt;
                        touhao2 = kt2;
                        while (true)
                        {
                            int length = (int)SendMessage(_h3, WM_GETTEXTLENGTH, IntPtr.Zero, null);

                            // 定义一个缓冲区来接收文本内容
                            System.Text.StringBuilder windowText = new System.Text.StringBuilder(length + 1);

                            // 发送WM_GETTEXT消息获取窗口文本
                            SendMessage(_h3, WM_GETTEXT, (IntPtr)windowText.Capacity, windowText);
                            _jiexi(windowText.ToString());
                            if (zhongying[xuan_y1] == "-1" || zhongying[xuan_y2] == "-1")
                            {
                               
                                Thread.Sleep(2000);
                            }
                            else
                            {
                                break;
                            }
                           
                        }
                       
                        _lbsha.Text = string.Join(",", zhongying);
                        List<string> numbers = new List<string>();
                        List<string> numbers2 = new List<string>();
                        for (var i = 0; i < _nm1.Value; i++)
                        {
                            string[] sz2 = content[i].Split('\t');

                            numbers.Add(sz2[1].Substring(xuan_y1,1));
                            numbers2.Add(sz2[1].Substring(xuan_y2, 1));

                        }
                        var allDigits = "0123456789".ToCharArray();

                        var result = allDigits
                            .Select(digit => new
                            {
                                DigitChar = digit,
                                DigitNumber = digit - '0',
                                Count = numbers.Count(s => !string.IsNullOrEmpty(s) && s[0] == digit)
                            })
                            .OrderBy(x => x.Count)
                            .ThenBy(x => x.DigitNumber);

                        Console.WriteLine("数字出现次数统计（排序后）:");
                        int w_sha = 0;
                
                        foreach (var item in result)
                        {
                         
                            w_sha = item.DigitNumber;
                            Console.WriteLine($"数字 {item.DigitChar}: 出现 {item.Count} 次");
                            break;
                        }
                        var result2 = allDigits
                            .Select(digit => new
                            {
                                DigitChar = digit,
                                DigitNumber = digit - '0',
                                Count = numbers2.Count(s => !string.IsNullOrEmpty(s) && s[0] == digit)
                            })
                            .OrderBy(x => x.Count)
                            .ThenBy(x => x.DigitNumber);

                        Console.WriteLine("数字出现次数统计（排序后）:");
                        int q_sha = 0;
                        foreach (var item in result2)
                        {

                            q_sha = item.DigitNumber;
                            Console.WriteLine($"数2字 {item.DigitChar}: 出现 {item.Count} 次");
                            break;
                        }
                        tongsha1 = kt;
                        tongsha2 = kt2;
                        lengsha1 = w_sha;
                        lengsha2 = q_sha;
                    
                    }));
                }
                else
                {
                    _dataGridView.Rows.Insert(0, cz[0], cz[1]);
                    _richtext.Text = cz[1];
                    //  _dataGridView.Rows.Insert(0, content, DateTime.Now.ToString("HH:mm:ss"));
                }
            }
        }
        public int no_shu = 0;
        string touqi = "";
        List<int> sha2_z = new List<int>();
        string new_kj = "";
        public void _chuliluoji(string[] content)
        {
           
            //foreach (string line in allLines)
            //{
            //    Console.WriteLine(line);

            //}
           
            string[] cz = content[0].Split('\t');
            //if (cz[0] == touqi)
            //{
            //    return;
            //}
            //touqi = cz[0];
            if (dataGridView1.InvokeRequired)
            {
                dataGridView1.Invoke(new Action(() =>
                {




                    //  _lbshu.Text = content.Count().ToString();
                    //if (t_qi == _lbshu.Text)
                    //{
                    //    return;
                    //}

                    //t_qi = _lbshu.Text;

                    new_kj = cz[1];
                      if (tz1.Count()>0 )
                    {
                        if(true)
                       // if (touwei == 0)
                        {
                            string kshu = cz[1].Substring(1, 4);
                         //   if (comboBox5.SelectedIndex == 1)
                            {
                                if (tz1.Contains(kshu))
                                // if (kshu.Contains(touhao.ToString()))
                                {
                                    ok_shu += 1;
                                    label20.Text = "中：" + ok_shu.ToString() + "  错:" + no_shu.ToString();
                                    dataGridView1.Rows[0].Cells[3].Value = "中";
                                }
                                else
                                {
                                    no_shu += 1;
                                    label20.Text = "中：" + ok_shu.ToString() + "  错:" + no_shu.ToString();
                                    dataGridView1.Rows[0].Cells[3].Value = "挂";
                                }
                            }
                            //else
                            //{
                            //    if (tz1.Contains(kshu))
                            //    //    if (!kshu.Contains(touhao.ToString()))
                            //    {
                            //        ok_shu += 1;
                            //        label3.Text = "前四 中：" + ok_shu.ToString() + "  错:" + no_shu.ToString();
                            //        dataGridView1.Rows[0].Cells[3].Value = "中";
                            //    }
                            //    else
                            //    {
                            //        no_shu += 1;
                            //        label3.Text = "前四 中：" + ok_shu.ToString() + "  错:" + no_shu.ToString();
                            //        dataGridView1.Rows[0].Cells[3].Value = "挂";
                            //    }
                            //}

                        }
                        else
                        {
                            //string kshu = cz[1].Substring(1, 4);
                            //if (kshu.Contains(touhao.ToString()))
                            //{
                            //    ok_shu += 1;
                            //    lb_ok.Text = "正确个数： " + ok_shu.ToString();
                            //    _dataGridView.Rows[0].Cells[3].Value = "中";
                            //}
                            //else
                            //{
                            //    no_shu += 1;
                            //    lb_no.Text = "错误个数： " + no_shu.ToString();
                            //    _dataGridView.Rows[0].Cells[3].Value = "挂";
                            //}

                        }

                        if (true)//(touwei2 == 1)
                        {
                            string kshu = cz[1].Substring(1, 4);
                           // if (comboBox6.SelectedIndex == 1)
                            {
                                if (tz2.Contains(kshu))
                                //  if (kshu.Contains(touhao2.ToString()))
                                {
                                    ok_shu2 += 1;
                                    label21.Text = "中：" + ok_shu2.ToString() + "  错:" + no_shu2.ToString();
                                    dataGridView1.Rows[0].Cells[3].Value = dataGridView1.Rows[0].Cells[3].Value + "/中";
                                }
                                else
                                {
                                    no_shu2 += 1;
                                    label21.Text = "中：" + ok_shu2.ToString() + "  错:" + no_shu2.ToString();
                                    dataGridView1.Rows[0].Cells[3].Value = dataGridView1.Rows[0].Cells[3].Value + "/挂";
                                }
                            }
                            //else
                            //{
                            //    if (tz2.Contains(kshu))
                            //    //if (!kshu.Contains(touhao2.ToString()))
                            //    {
                            //        ok_shu2 += 1;
                            //        label4.Text = "后四 中：" + ok_shu2.ToString() + "  错:" + no_shu2.ToString();
                            //        dataGridView1.Rows[0].Cells[3].Value = dataGridView1.Rows[0].Cells[3].Value + "/中";
                            //    }
                            //    else
                            //    {
                            //        no_shu2 += 1;
                            //        label4.Text = "后四 中：" + ok_shu2.ToString() + "  错:" + no_shu2.ToString();
                            //        dataGridView1.Rows[0].Cells[3].Value = dataGridView1.Rows[0].Cells[3].Value + "/挂";
                            //    }
                            //}

                        }
                        else
                        {
                            //string kshu = cz[1].Substring(1, 4);
                            //if (kshu.Contains(touhao.ToString()))
                            //{
                            //    ok_shu += 1;
                            //    lb_ok.Text = "正确个数： " + ok_shu.ToString();
                            //    _dataGridView.Rows[0].Cells[3].Value = "中";
                            //}
                            //else
                            //{
                            //    no_shu += 1;
                            //    lb_no.Text = "错误个数： " + no_shu.ToString();
                            //    _dataGridView.Rows[0].Cells[3].Value = "挂";
                            //}

                        }

                    }
                    dataGridView1.Rows[0].Cells[0].Value = cz[0];
                    dataGridView1.Rows[0].Cells[1].Value = cz[1];
                   // touqi = cz[0];
                    touruqi = (int.Parse(cz[0].Substring(cz[0].Length - 3, 3)) + 1).ToString();
                   
                    // t_qi = touruqi;
                    dataGridView1.Rows.Insert(0, new DataGridViewRow());


                    int kt = 0;// int.Parse(cz[1].Substring(comboBox1.SelectedIndex, 1));
                  
                    int kt2 = 0;// int.Parse(cz[1].Substring(comboBox3.SelectedIndex, 1));
                  
                  
                    List<string> numbers = new List<string>();
                    List<string> numbers2 = new List<string>();

                    List<string> numbers5 = new List<string>();
                    List<string> numbers6= new List<string>();

                    List<string> numbers3 = new List<string>();
                    List<string> numbers4 = new List<string>();
                    List<List<string>> numbers_z = new List<List<string>>();
                    for (int j = 0; j < 5; j++) {

                        numbers_z.Add(new List<string>());
                    }
                    for (var i = 0; i < numericUpDown1.Value; i++)
                    {
                        string[] sz2 = content[i].Split('\t');
                        for(int j = 0; j < 5; j++)
                        {
                            numbers_z[j].Add(sz2[1].Substring(j, 1));
                        }
                        numbers.Add(sz2[1].Substring(xuan_y1, 1));
                        numbers2.Add(sz2[1].Substring(xuan_y2, 1));

                        numbers5.Add(sz2[1].Substring(xuan_x1, 1));
                        numbers6.Add(sz2[1].Substring(xuan_x2, 1));

                        for (var j = 0; j < 4; j++)
                        {
                            numbers3.Add(sz2[1].Substring(j, 1));
                        }
                        for (var j = 1; j < 5; j++)
                        {
                            numbers4.Add(sz2[1].Substring(j, 1));
                        }
                       

                    }
                    List<int> sha_z = new List<int>();
                    for (int j = 0; j < 5; j++){
                        
                        var allDigitsa = "0123456789".ToCharArray();
                        var result5a = allDigitsa
                         .Select(digit => new
                         {
                             DigitChar = digit,
                             DigitNumber = digit - '0',
                             Count = numbers_z[j].Count(s => !string.IsNullOrEmpty(s) && s[0] == digit)
                         })
                         .OrderBy(x => x.Count)
                         .ThenBy(x => x.DigitNumber);
                        int shaa = 0;

                        foreach (var item in result5a)
                        {

                            shaa = item.DigitNumber;
                            Console.WriteLine($"数字 {item.DigitChar}: 出现 {item.Count} 次");
                            break;
                        }
                        sha_z.Add(shaa);
                    }
                    sha2_z = sha_z;
                    var allDigits = "0123456789".ToCharArray();
                    var result5 = allDigits
                     .Select(digit => new
                     {
                         DigitChar = digit,
                         DigitNumber = digit - '0',
                         Count = numbers5.Count(s => !string.IsNullOrEmpty(s) && s[0] == digit)
                     })
                     .OrderBy(x => x.Count)
                     .ThenBy(x => x.DigitNumber);

                    Console.WriteLine("数字出现次数统计（排序后）:");
                    int w_sha5 = 0;

                    foreach (var item in result5)
                    {

                        w_sha5 = item.DigitNumber;
                        Console.WriteLine($"数字 {item.DigitChar}: 出现 {item.Count} 次");
                        break;
                    }

                    var result6 = allDigits
                    .Select(digit => new
                    {
                        DigitChar = digit,
                        DigitNumber = digit - '0',
                        Count = numbers6.Count(s => !string.IsNullOrEmpty(s) && s[0] == digit)
                    })
                    .OrderBy(x => x.Count)
                    .ThenBy(x => x.DigitNumber);

                    Console.WriteLine("数字出现次数统计（排序后）:");
                    int w_sha6 = 0;

                    foreach (var item in result6)
                    {

                        w_sha6 = item.DigitNumber;
                        Console.WriteLine($"数字 {item.DigitChar}: 出现 {item.Count} 次");
                        break;
                    }


                    var result3 = allDigits
                      .Select(digit => new
                      {
                          DigitChar = digit,
                          DigitNumber = digit - '0',
                          Count = numbers3.Count(s => !string.IsNullOrEmpty(s) && s[0] == digit)
                      })
                      .OrderBy(x => x.Count)
                      .ThenBy(x => x.DigitNumber);

                    Console.WriteLine("数字出现次数统计（排序后）:");
                    int w_sha1 = 0;

                    foreach (var item in result3)
                    {

                        w_sha1 = item.DigitNumber;
                        Console.WriteLine($"数字 {item.DigitChar}: 出现 {item.Count} 次");
                        break;
                    }
                    var result4 = allDigits
                     .Select(digit => new
                     {
                         DigitChar = digit,
                         DigitNumber = digit - '0',
                         Count = numbers4.Count(s => !string.IsNullOrEmpty(s) && s[0] == digit)
                     })
                     .OrderBy(x => x.Count)
                     .ThenBy(x => x.DigitNumber);

                    Console.WriteLine("数字出现次数统计（排序后）:");
                    int w_sha2 = 0;

                    foreach (var item in result4)
                    {

                        w_sha2 = item.DigitNumber;
                        Console.WriteLine($"数字 {item.DigitChar}: 出现 {item.Count} 次");
                        break;
                    }

                    var result = allDigits
                        .Select(digit => new
                        {
                            DigitChar = digit,
                            DigitNumber = digit - '0',
                            Count = numbers.Count(s => !string.IsNullOrEmpty(s) && s[0] == digit)
                        })
                        .OrderBy(x => x.Count)
                        .ThenBy(x => x.DigitNumber);

                    Console.WriteLine("数字出现次数统计（排序后）:");
                    int w_sha = 0;

                    foreach (var item in result)
                    {

                        w_sha = item.DigitNumber;
                        Console.WriteLine($"数字 {item.DigitChar}: 出现 {item.Count} 次");
                        break;
                    }
                    var result2 = allDigits
                        .Select(digit => new
                        {
                            DigitChar = digit,
                            DigitNumber = digit - '0',
                            Count = numbers2.Count(s => !string.IsNullOrEmpty(s) && s[0] == digit)
                        })
                        .OrderBy(x => x.Count)
                        .ThenBy(x => x.DigitNumber);

                    Console.WriteLine("数字出现次数统计（排序后）:");
                    int q_sha = 0;
                    foreach (var item in result2)
                    {

                        q_sha = item.DigitNumber;
                        Console.WriteLine($"数2字 {item.DigitChar}: 出现 {item.Count} 次");
                        break;
                    }

                    if (w1_xu == 0)
                    {
                        kt = kai_xu;
                    }
                    if (w1_xu == 1)
                    {
                        if (kai_xu <5)
                        {
                            kt = sha_z[kai_xu];
                        }
                        else
                        {
                            kt = w_sha1;
                        }
                      
                    }
                    if (w1_xu == 2)
                    {
                        kt = int.Parse(cz[1].Substring(kai_xu, 1));/// [kai_xu]);

                    }



                    if (w2_xu == 0)
                    {
                        kt2 = kai_xu2 ;
                    }
                    if (w2_xu == 1)
                    {
                       // kt2 = w_sha2;// int.Parse(cz[1].Substring(comboBox3.SelectedIndex, 1));
                        if (kai_xu < 5)
                        {
                            kt2 = sha_z[kai_xu2];
                        }
                        else
                        {
                            kt2 = w_sha2;
                        }
                    }
                    if (w2_xu == 2)
                    {
                        kt2 = int.Parse(cz[1].Substring(kai_xu2, 1));

                    }
                    tongsha1 = kt;
                    tongsha2 = kt2;
                    lengsha1 = w_sha;
                    lengsha2 = q_sha;

                    lengsha3 = w_sha5;
                    lengsha4 =w_sha6;
                    kaiguan = false;

                }));
            }
            else
            {
                dataGridView1.Rows.Insert(0, cz[0], cz[1]);
               // _richtext.Text = cz[1];
                //  _dataGridView.Rows.Insert(0, content, DateTime.Now.ToString("HH:mm:ss"));
            }

        }
        private FirstLineMonitor _monitor;
        public int ok_shu = 0;
        public int ok_shu2 = 0;
        public int no_shu2 = 0;
        bool kai = false;
        private void button2_Click(object sender, EventArgs e)
        {
            if (button2.Text == "停止")
            {
                groupBox1.Enabled = true;

              //  _monitor.StopMonitoring();
                button2.Text = "开启";
                return;

            }
            //if (hWnd3 == IntPtr.Zero)
            //{
            //    MessageBox.Show("未匹配众赢");
            //    return;
            //}
            bool f1 = true;
            if (!kai)
            {
                var windows = GetAllWindows();
              

                foreach (var window in windows)
                {
                    Console.WriteLine(window.ProcessName);

                    if (window.Title.Contains("娱乐"))//(window.ProcessName == "NDMain")&& window.Title.Contains("辉达娱乐")) //window.Title.Contains("娱乐") )//
                    {
                        string win2 = GetProcessPath2(window.ProcessId);
                        Console.WriteLine($"窗口标题: {window.Title}");
                        Console.WriteLine($"进程ID: {window.ProcessId}");
                        Console.WriteLine($"进程名: {window.ProcessName}");
                        Console.WriteLine("-----------------------------" + win2);
                        // 组合相对路径
                        string baseDir = Path.GetDirectoryName(win2); // 正确获取父目录
                        // string targetFile = Path.Combine(baseDir, "OpenCode", "TXFFC.txt");
                        string targetFile =  Path.Combine(baseDir, "OpenCode", "TXFFC.txt");
                        Console.WriteLine($"监控文件路径: {targetFile}");
                        string content = File.ReadAllText(targetFile, Encoding.UTF8);
                        Console.WriteLine($"文件内容:\n{content}");

                        // 初始化监控
                        var monitor = new AdvancedFileLinesMonitor(targetFile, 2000);
                        
                        // 订阅事件
                        monitor.FileLinesRead += (content3, filePath) =>
                        {
                            Console.WriteLine($"文件更新: {filePath}");
                            Console.WriteLine($"内容: {content3}");

                            // 调用你的处理逻辑
                            _chuliluoji(content3);
                            // _jiexi(content);
                        };

                        monitor.FileDeleted += filePath => Console.WriteLine($"文件删除: {filePath}");
                        monitor.FileError += ex => Console.WriteLine($"错误: {ex.Message}");

                        // 开始监控
                        monitor.Start();


                        //  _monitor = new FirstLineMonitor(dataGridView1,richTextBox1,kai_xu,tou_xu,label2,label3,label4,kai_xu2,tou_xu2,zf1,zf2,richTextBox2,label7,label8,numericUpDown1, hWnd3,label14);
                     
                        //  _monitor.StartMonitoring(targetFile);

                       
                        f1 = false;
                        break;
                    }




                }
              

            }
            else
            {
                f1 = false;
            }
            if (f1)
            {
                MessageBox.Show("未启动挂机！");
            }
            else
            {
                if (dataGridView1.RowCount != 1)
                {
                    dataGridView1.RowCount = 1;
                }
                ok_shu = 0;
                no_shu = 0;
                ok_shu2 = 0;
                no_shu2 = 0;
                dataGridView1.Rows[0].Cells[0].Value = "";
                dataGridView1.Rows[0].Cells[1].Value = "";
                dataGridView1.Rows[0].Cells[2].Value = "";
                dataGridView1.Rows[0].Cells[3].Value = "";
                //dataGridView1.Rows.Insert(0, new DataGridView());
                label3.Text = "";
                label4.Text = "";
                label2.Text = "";
                groupBox1.Enabled = false;
                kai = true;
                button2.Text = "停止";
            }



        }
        static bool cksha1 = true;
        static bool cksha2 = true;
        static bool cksha3 = true;
        static bool cksha4 = true;


        string[] kai_str = { "万", "千", "百", "十", "个" };

        string[] new1_str = { "通杀固定", "冷号通杀","出某杀某"};

        string[] tou_str = { "前三", "后三" };// { "前四", "后四" };
        string[] zhengfan = { "正投" , "反投" };
        string[] shaxuan = {"定位", "冷号" };
        List<int> cmb_indexz = new List<int>();
        private void cmbz_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            int index = (int)cb.Tag;
            cmb_indexz[index] = cb.SelectedIndex;
            Console.WriteLine(index);
        }
        List<ComboBox> lbs_z = new List<ComboBox>();
        private void Form1_Load(object sender, EventArgs e)

            
        {
            lbs_z.Add(cmb1);
            lbs_z.Add(cmb2);
            lbs_z.Add(cmb3);
            lbs_z.Add(cmb4);
            lbs_z.Add(cmb5);
            lbs_z.Add(cmb6);
            lbs_z.Add(cmb7);
            lbs_z.Add(cmb8);

            for(int i = 0; i < 8; i++)
            {
                lbs_z[i].Items.Add("冷");
                lbs_z[i].Items.Add("万");
                lbs_z[i].Items.Add("千");
                lbs_z[i].Items.Add("百");
                lbs_z[i].Items.Add("十");
                lbs_z[i].Items.Add("个");
                lbs_z[i].SelectedIndex = 0;
                lbs_z[i].SelectedIndexChanged += cmbz_SelectedIndexChanged;
                lbs_z[i].Tag = i;
                cmb_indexz.Add(0);
            }
            lbs_z[0].SelectedIndex = 1;
            lbs_z[1].SelectedIndex = 3;
            lbs_z[2].SelectedIndex = 4;
            lbs_z[3].SelectedIndex = 5;
            lbs_z[4].SelectedIndex = 0;
            lbs_z[5].SelectedIndex = 3;
            lbs_z[6].SelectedIndex = 4;
            lbs_z[7].SelectedIndex = 5;
            // 获取主屏幕分辨率
            var screenBounds = Screen.PrimaryScreen.Bounds;

            // 给窗体一个安全的最小尺寸（比如 800x600）
            this.MinimumSize = new Size(860, 446);

            for (var i = 0; i < new1_str.Length; i++)
            {
                comboBox15.Items.Add(new1_str[i]);
                comboBox16.Items.Add(new1_str[i]);
            }
            comboBox15.SelectedIndex = 0;
            comboBox16.SelectedIndex = 2;
            this.Width = 860;
            this.Height = 456;

            // 如果窗体大于屏幕，自动缩小
            //if (this.Width > screenBounds.Width || this.Height > screenBounds.Height)
            //{
            //    float scaleX = (float)screenBounds.Width / this.Width;
            //    float scaleY = (float)screenBounds.Height / this.Height;
            //    float scale = Math.Min(scaleX, scaleY);

            //    // 计算缩放后的尺寸
            //    int newWidth = (int)(this.Width * scale);
            //    int newHeight = (int)(this.Height * scale);

            //    this.Size = new Size(newWidth, newHeight);
            //}

            // 居中显示
            this.StartPosition = FormStartPosition.CenterScreen;

            for (int i = 0; i < 2; i++)
            {
                comboBox7.Items.Add(shaxuan[i]);
                comboBox8.Items.Add(shaxuan[i]);
                comboBox9.Items.Add(shaxuan[i]);
                comboBox10.Items.Add(shaxuan[i]);

            }
            for(int i = 0; i < 5; i++)
            {
                comboBox11.Items.Add(kai_str[i]);
                comboBox12.Items.Add(kai_str[i]);
                comboBox13.Items.Add(kai_str[i]);
                comboBox14.Items.Add(kai_str[i]);
            }
            comboBox11.SelectedIndex = 0;
            comboBox12.SelectedIndex = 1;
            comboBox13.SelectedIndex = 3;
            comboBox14.SelectedIndex = 4;


            comboBox7.SelectedIndex = 0;
            comboBox8.SelectedIndex = 0;
            comboBox9.SelectedIndex = 1;
            comboBox10.SelectedIndex = 1;

            foreach (var ep in zhengfan)
            {
                comboBox5.Items.Add(ep);
                comboBox6.Items.Add(ep);
            }
            comboBox5.SelectedIndex = 0;
            comboBox6.SelectedIndex = 0;

            //foreach (var ep in kai_str)
            //{
            //    comboBox1.Items.Add(ep);
            //    comboBox3.Items.Add(ep);
            //}
            //comboBox1.SelectedIndex = 0;
            //comboBox3.SelectedIndex = 1;

            foreach (var ep in tou_str)
            {
                comboBox2.Items.Add(ep);
                comboBox4.Items.Add(ep);
            }
            comboBox2.SelectedIndex = 0;
            comboBox4.SelectedIndex = 1;
           // backgroundWorker1.RunWorkerAsync();



        }
        int kai_xu = 0;
        int tou_xu = 0;

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            kai_xu = comboBox1.SelectedIndex;
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            tou_xu = comboBox2.SelectedIndex;
        }
        int zf1 = 0;
        int zf2 = 0;
        private void comboBox5_SelectedIndexChanged(object sender, EventArgs e)
        {
            zf1 = comboBox5.SelectedIndex;
        }

        private void comboBox6_SelectedIndexChanged(object sender, EventArgs e)
        {
            zf2 = comboBox6.SelectedIndex;
        }

        int kai_xu2 = 0;
        int tou_xu2 = 0;

        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            kai_xu2 = comboBox3.SelectedIndex;
        }

        private void comboBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            tou_xu2 = comboBox4.SelectedIndex;
        }

        private async void button3_Click(object sender, EventArgs e)
        {

            try
            {
                string url ="https://api.chuanqiking.com/CQShiCai/getBaseCQShiCaiList?date=2025-09-08&lotCode=11";

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    client.DefaultRequestHeaders.Add("cookie", "PHPSESSID=7caefa92a5df5bf779d732a61c25d535");

                    HttpResponseMessage response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(content);

                        var jsonObject = JObject.Parse(content);
                        Console.WriteLine(jsonObject["message"]);
                        foreach(var ep in jsonObject["result"]["data"]){

                            Console.WriteLine(ep["preDrawIssue"] + " " + ep["preDrawCode"]);
                        }


                        // return new ApiResult { Success = true, Data = content };
                    }
                    else
                    {
                        //return new ApiResult
                        //{
                        //    Success = false,
                        //    ErrorMessage = $"HTTP错误: {(int)response.StatusCode} {response.ReasonPhrase}"
                        //};
                    }
                }
            }
            catch (Exception ex)
            {
                //return new ApiResult
                //{
                //    Success = false,
                //    ErrorMessage = $"请求异常: {ex.Message}"
                //};
            }

        }

        private async void button4_Click(object sender, EventArgs e)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var content = new FormUrlEncodedContent(new[]
                    {
                    new KeyValuePair<string, string>("id", "0")
                });

                    var response = await client.PostAsync("http://www.ttcffc.com/jh-kj/old.php", content);
                    var html = await response.Content.ReadAsStringAsync();

                    var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                    htmlDoc.LoadHtml(html);

                    // 提取所有开奖信息
                    var items = htmlDoc.DocumentNode.SelectNodes("//div[@class='kj-box']");

                    foreach (var item in items)
                    {
                        // 日期时间
                        var dateTime = item.SelectSingleNode(".//div[@class='kj-a']")?.InnerText
                            ?.Replace("&nbsp;", " ").Trim();

                        // 期号
                        var period = item.SelectSingleNode(".//div[@class='kj-b']")?.InnerText?.Trim();

                        // 号码
                        var numbers = item.SelectNodes(".//div[@class='kj-c']/span");
                        var numberStr = "";
                        if (numbers != null)
                        {
                            foreach (var num in numbers)
                            {
                                numberStr += num.InnerText.Trim();
                            }
                        }

                        Console.WriteLine($"{dateTime} - {period}: {numberStr}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }

        private async void button5_Click(object sender, EventArgs e)
        {
            try
            {
               // lblStatus.Text = "正在获取数据...";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    var response = await client.GetAsync("http://www.ttcffc.com/go/new.php");
                    var html = await response.Content.ReadAsStringAsync();

                    var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                    htmlDoc.LoadHtml(html);

                    // 直接提取并显示数据
                    var date = htmlDoc.DocumentNode.SelectSingleNode("//span[@class='kja-f0']")?.InnerText.Trim() ?? "";
                    var time = htmlDoc.DocumentNode.SelectSingleNode("//span[@class='kja-f1']")?.InnerText.Trim() ?? "";
                    var period = htmlDoc.DocumentNode.SelectSingleNode("//span[@class='kja-f2']")?.InnerText.Trim() ?? "";

                    var numberNodes = htmlDoc.DocumentNode.SelectNodes("//div[@class='kj-b']/span[contains(@class, 'new-n')]");
                    var numbers = "";
                    if (numberNodes != null)
                    {
                        foreach (var node in numberNodes)
                        {
                            numbers += node.InnerText.Trim();
                        }
                    }

                    // 在界面显示
                    //lblDate.Text = $"日期: {date}";
                    //lblTime.Text = $"时间: {time}";
                    //lblPeriod.Text = $"期号: {period}";
                    //lblNumbers.Text = $"开奖号码: {numbers}";

                    //lblStatus.Text = "数据获取成功";
                }
            }
            catch (Exception ex)
            {
                //lblStatus.Text = $"错误: {ex.Message}";
                MessageBox.Show($"发生错误: {ex.Message}");
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            //quleng = (int)(numericUpDown1.Value);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            cksha1 = checkBox1.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            cksha2 = checkBox2.Checked;
        }

        private void comboBox7_VisibleChanged(object sender, EventArgs e)
        {

        }
        static int _shax1 = 0;
        static int _shay1 = 1;
        static int _shax2 = 0;
        static int _shay2 = 1;

        private void comboBox7_SelectedIndexChanged(object sender, EventArgs e)
        {
            _shax1 = comboBox7.SelectedIndex;

        }

        private void comboBox9_SelectedIndexChanged(object sender, EventArgs e)
        {
            _shay1 = comboBox9.SelectedIndex;
        }

        private void comboBox8_SelectedIndexChanged(object sender, EventArgs e)
        {
            _shax2 = comboBox8.SelectedIndex;
        }

        private void comboBox10_SelectedIndexChanged(object sender, EventArgs e)
        {
            _shay2 = comboBox10.SelectedIndex;
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            cksha3 = checkBox3.Checked;
        
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            cksha4 = checkBox4.Checked;
        }
        static int xuan_x1=1;
        static int xuan_y1 = 1;
        static int xuan_x2 = 4;
        static int xuan_y2 = 4;
        private void comboBox11_SelectedIndexChanged(object sender, EventArgs e)
        {
            xuan_x1 = comboBox11.SelectedIndex;
        }

        private void comboBox12_SelectedIndexChanged(object sender, EventArgs e)
        {
            xuan_y1 = comboBox12.SelectedIndex;
        }

        private void comboBox13_SelectedIndexChanged(object sender, EventArgs e)
        {
            xuan_x2 = comboBox13.SelectedIndex;
        }

        private void comboBox14_SelectedIndexChanged(object sender, EventArgs e)
        {
            xuan_y2 = comboBox14.SelectedIndex;
        }
        static string touruqi = "";
        static int tongsha1 = 0;
        static int tongsha2 = 0;
        static int lengsha1 = 0;
        static int lengsha2 = 0;

        static int lengsha3 = 0;
        static int lengsha4 = 0;
        static List<string> tz1 = new List<string>();
        static List<string> tz2 = new List<string>();
 

        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private const uint SMTO_NORMAL = 0x0000;
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam,
    StringBuilder lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        private string SafeGetWindowText(IntPtr hWnd)
        {
            try
            {
                int length = (int)SendMessage(hWnd, WM_GETTEXTLENGTH, IntPtr.Zero, null);
                if (length <= 0) return string.Empty;

                StringBuilder windowText = new StringBuilder(length + 1);
                IntPtr result;

                // 设置超时时间（例如2秒）
                SendMessageTimeout(hWnd, WM_GETTEXT, (IntPtr)windowText.Capacity, windowText,
                    SMTO_ABORTIFHUNG, 2000, out result);

                return windowText.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
        private async Task<string> GetWindowTextAsync(IntPtr hWnd)
        {
            return await Task.Run(() =>
            {
                try
                {
                    int length = (int)SendMessage(hWnd, WM_GETTEXTLENGTH, IntPtr.Zero, null);
                    if (length <= 0) return null;

                    StringBuilder windowText = new StringBuilder(length + 1);
                    IntPtr result;

                    // 带超时的消息发送
                    var timeoutResult = SendMessageTimeout(hWnd, WM_GETTEXT,
                        (IntPtr)windowText.Capacity, windowText,
                        SMTO_ABORTIFHUNG, 2000, out result);

                    if (timeoutResult == IntPtr.Zero)
                        return null; // 超时或失败

                    return windowText.ToString();
                }
                catch
                {
                    return null;
                }
            });
        }
        private async void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();

            try
            {
                if ( kaiguan)
                {
                    timer1.Interval = 3000;
                    timer1.Start();
                    return;
                }
                string text = "";
                //try
                //{
                //    if (hWnd3 != IntPtr.Zero)
                //    {
                //        // Get the window title
                //        int length = (int)SendMessage(hWnd3, WM_GETTEXTLENGTH, IntPtr.Zero, null);

                //        // 定义一个缓冲区来接收文本内容
                //        System.Text.StringBuilder windowText = new System.Text.StringBuilder(length + 1);

                //        // 发送WM_GETTEXT消息获取窗口文本
                //        SendMessage(hWnd3, WM_GETTEXT, (IntPtr)windowText.Capacity, windowText);
                //        text = windowText.ToString();
                //    }
                //}
                //catch
                //{

                //}
                //if (text == "")
                //{
                //    timer1.Interval = 3000;
                //    timer1.Start();
                //    return;
                //}

                // 在后台线程执行耗时操作
               // string text = await GetWindowTextAsync(hWnd3);

   

               // if ((int)hWnd3 > 0)
                {
                    if (touruqi != "")
                    {
                        richTextBox3.Text = DateTime.Now.ToShortTimeString();

                       // string text2 = await GetWindowTextAsync(hWnd3);

                      //  if (!string.IsNullOrEmpty(text))
                        {

                        //    _jiexi(text);
                            richTextBox3.Text = richTextBox3.Text+" test1"+" "+ string.Join(",", zhongying)+","+ zhongying[xuan_y1]+","+ zhongying[xuan_y2]+"----"+text;
                            // 回到 UI 线程更新
                            this.Invoke((MethodInvoker)delegate {
                                label14.Text = string.Join(",", zhongying);

                                if (zhongying != null &&
                                    xuan_y1 < zhongying.Length &&
                                    xuan_y2 < zhongying.Length )
                                {
                                    richTextBox3.Text = richTextBox3.Text + " test3";
                                    List<int> n1 = new List<int>();
                                    for(int i = 0; i < 4; i++)
                                    {
                                        if (cmb_indexz[i] == 0)
                                        {
                                            n1.Add(sha2_z[i + 1]);
                                        }
                                        else
                                        {
                                            n1.Add(int.Parse(new_kj.Substring(cmb_indexz[i]-1, 1)));
                                        }

                                    }
                                    List<int> n2 = new List<int>();
                                    for (int i = 0; i < 4; i++)
                                    {
                                        if (cmb_indexz[i+4] == 0)
                                        {
                                            n2.Add(sha2_z[i + 1]);
                                        }
                                        else
                                        {
                                            n2.Add(int.Parse(new_kj.Substring(cmb_indexz[i + 4] -1, 1)));
                                        }

                                    }
                                    dataGridView1.Rows[0].Cells[2].Value = string.Join(" ", n1) + " / " + string.Join(" ", n2); ;

                                    List<string> Tz = new List<string>();
                                    for (int m1 = 0; m1 < 10; m1++)
                                        for (int m2 = 0; m2 < 10; m2++)
                                            for (int m3 = 0; m3 < 10; m3++)
                                                for (int m4 = 0; m4 < 10; m4++)
                                                {
                                                  
                                                      if(m1!=n1[0] && m2!=n1[1] && m3 != n1[2] && m4 != n1[3])
                                                        {
                                                            bool f1 = true;
                                                            
                                                          

                                                            if (f1)
                                                            {

                                                                Tz.Add(m1.ToString() + m2.ToString() + m3.ToString() + m4.ToString());
                                                            }
                                                     
                                                   
                                                      }
                                                   
                                             

                                                }


                                    richTextBox1.Text = string.Join("\n", Tz);
                                   groupBox2.Text ="后四【方案一】 " +"共 " + Tz.Count.ToString() + " 注";
                                    List<string> Tz2 = new List<string>();
                                    for (int m1 = 0; m1 < 10; m1++)
                                        for (int m2 = 0; m2 < 10; m2++)
                                            for (int m3 = 0; m3 < 10; m3++)
                                                for (int m4 = 0; m4 < 10; m4++)
                                                {

                                                    if (m1 != n2[0] && m2 != n2[1] && m3 != n2[2] && m4 != n2[3])
                                                    {
                                                        bool f1 = true;



                                                        if (f1)
                                                        {

                                                            Tz2.Add(m1.ToString() + m2.ToString() + m3.ToString() + m4.ToString());
                                                        }


                                                    }



                                                }
                                    tz1 = Tz;
                                    tz2 = Tz2;

                                    richTextBox2.Text = string.Join("\n", Tz2);
                                    groupBox3.Text = "后四【方案一】 " + "共 " + Tz2.Count.ToString() + " 注";
                                    touruqi = "";
                                    // 你的业务逻辑
                                }
                            });


                            kaiguan = true;
                    }


                }

            }
            }
            catch (Exception ex)
            {
                // 记录异常
                Debug.WriteLine($"Timer tick error: {ex.Message}");
            }
            finally
            {
                //Console.WriteLine("jieshu");
                timer1.Interval = 3000;
                timer1.Start();
            }


        }

        private void button1_Click_1(object sender, EventArgs e)
        {

        }

        private void button6_Click(object sender, EventArgs e)
        {

        }
        public bool kaiguan=true;
        private async void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                Thread.Sleep(2000);
                try
                {
                    if ( kaiguan)
                    {
                        continue;
                    }
                    string text = "";
                    //try
                    //{
                    //    if (hWnd3 != IntPtr.Zero)
                    //    {
                    //        // Get the window title
                    //        int length = (int)SendMessage(hWnd3, WM_GETTEXTLENGTH, IntPtr.Zero, null);

                    //        // 定义一个缓冲区来接收文本内容
                    //        System.Text.StringBuilder windowText = new System.Text.StringBuilder(length + 1);

                    //        // 发送WM_GETTEXT消息获取窗口文本
                    //        SendMessage(hWnd3, WM_GETTEXT, (IntPtr)windowText.Capacity, windowText);
                    //        text = windowText.ToString();
                    //    }
                    //}
                    //catch
                    //{

                    //}
                    //if (text == "")
                    //{
                    //    continue;
                    //}

                    // 在后台线程执行耗时操作
                    // string text = await GetWindowTextAsync(hWnd3);

                 

                    if(true)// ((int)hWnd3 > 0)
                    {
                        if (touruqi != "")
                        {

                          //  string text2 = await GetWindowTextAsync(hWnd3);

                            if (!string.IsNullOrEmpty(text))
                            {

                                _jiexi(text);

                                // 回到 UI 线程更新
                                this.Invoke((MethodInvoker)delegate {
                                    label14.Text = string.Join("_", zhongying);

                                    if (zhongying != null &&
                                        xuan_y1 < zhongying.Length &&
                                        xuan_y2 < zhongying.Length )
                                    {

                                        dataGridView1.Rows[0].Cells[2].Value = tongsha1.ToString() + "[" + zhongying[xuan_x1] + "," + lengsha1.ToString() + "]" + "/" + tongsha2.ToString() + "[" + zhongying[xuan_x2] + "," + lengsha2.ToString() + "]";

                                        List<string> Tz = new List<string>();
                                        for (int m1 = 0; m1 < 10; m1++)
                                            for (int m2 = 0; m2 < 10; m2++)
                                                for (int m3 = 0; m3 < 10; m3++)
                                                    for (int m4 = 0; m4 < 10; m4++)
                                                    {
                                                        if (comboBox5.SelectedIndex == 1)
                                                        {
                                                            if (m1 == tongsha1 || m2 == tongsha1 || m3 == tongsha1 || m4 == tongsha1)

                                                            {
                                                                bool f1 = true;

                                                                if (cksha1)
                                                                {
                                                                    if (m1 == int.Parse(zhongying[xuan_x1]))
                                                                    {
                                                                        f1 = false;
                                                                    }



                                                                }

                                                                if (cksha3)
                                                                {
                                                                    if (m4 == lengsha1)
                                                                    {
                                                                        f1 = false;
                                                                    }

                                                                }


                                                                if (f1)
                                                                {

                                                                    Tz.Add(m1.ToString() + m2.ToString() + m3.ToString() + m4.ToString());
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {



                                                            if (m1 == tongsha1 || m2 == tongsha1 || m3 == tongsha1 || m4 == tongsha1)
                                                            {

                                                            }
                                                            else
                                                            {
                                                                bool f1 = true;

                                                                if (cksha1)
                                                                {
                                                                    if (m1 == int.Parse(zhongying[xuan_x1]))
                                                                    {
                                                                        f1 = false;
                                                                    }



                                                                }

                                                                if (cksha3)
                                                                {
                                                                    if (m4 == lengsha1)
                                                                    {
                                                                        f1 = false;
                                                                    }

                                                                }


                                                                if (f1)
                                                                {

                                                                    Tz.Add(m1.ToString() + m2.ToString() + m3.ToString() + m4.ToString());
                                                                }


                                                            }
                                                        }


                                                    }


                                        richTextBox1.Text = string.Join("\n", Tz);
                                        label2.Text = "共 " + Tz.Count.ToString() + " 注";
                                        List<string> Tz2 = new List<string>();
                                        for (int m1 = 0; m1 < 10; m1++)
                                            for (int m2 = 0; m2 < 10; m2++)
                                                for (int m3 = 0; m3 < 10; m3++)
                                                    for (int m4 = 0; m4 < 10; m4++)
                                                    {
                                                        if (comboBox6.SelectedIndex == 1)
                                                        {
                                                            if (m1 == tongsha2 || m2 == tongsha2 || m3 == tongsha2 || m4 == tongsha2)
                                                            {
                                                                bool f1 = true;

                                                                if (cksha2)
                                                                {
                                                                    if (m1 == int.Parse(zhongying[xuan_x2]))
                                                                    {
                                                                        f1 = false;
                                                                    }

                                                                }

                                                                if (cksha4)
                                                                {
                                                                    if (m4 == lengsha2)
                                                                    {
                                                                        f1 = false;
                                                                    }

                                                                }


                                                                if (f1)
                                                                {

                                                                    Tz2.Add(m1.ToString() + m2.ToString() + m3.ToString() + m4.ToString());
                                                                }

                                                            }
                                                            else
                                                            {

                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (m1 == tongsha2 || m2 == tongsha2 || m3 == tongsha2 || m4 == tongsha2)
                                                            {

                                                            }
                                                            else
                                                            {


                                                                bool f1 = true;

                                                                if (cksha2)
                                                                {
                                                                    if (m1 == int.Parse(zhongying[xuan_x2]))
                                                                    {
                                                                        f1 = false;
                                                                    }

                                                                }

                                                                if (cksha4)
                                                                {
                                                                    if (m4 == lengsha2)
                                                                    {
                                                                        f1 = false;
                                                                    }

                                                                }


                                                                if (f1)
                                                                {

                                                                    Tz2.Add(m1.ToString() + m2.ToString() + m3.ToString() + m4.ToString());
                                                                }



                                                            }
                                                        }


                                                    }
                                        tz1 = Tz;
                                        tz2 = Tz2;

                                        richTextBox2.Text = string.Join("\n", Tz2);
                                        label6.Text = "共 " + Tz2.Count.ToString() + " 注";
                                        touruqi = "";
                                        // 你的业务逻辑
                                    }
                                });



                            }

                            kaiguan = true;
                        }

                    }
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Debug.WriteLine($"Timer tick error: {ex.Message}");
                }
                finally
                {
                    //Console.WriteLine("jieshu");
                    //timer1.Interval = 3000;
                    //timer1.Start();
                }

            }


        }
        int w1_xu = 0;
        int w2_xu = 0;
        private void comboBox15_SelectedIndexChanged(object sender, EventArgs e)
        {
            w1_xu = comboBox15.SelectedIndex;
            if (comboBox15.SelectedIndex == 0)
            {
                comboBox1.Items.Clear();
                for(var i = 0; i < 10; i++)
                {
                    comboBox1.Items.Add(i.ToString());
                }
                comboBox1.SelectedIndex = 0;
              //  comboBox1.Visible = true;


            }
            if (comboBox15.SelectedIndex == 1)
            {
                //  comboBox1.Visible = false;//
                comboBox1.Items.Clear();
                for (var i = 0; i < kai_str.Length; i++)
                {
                    comboBox1.Items.Add(kai_str[i] + "冷");
                }
                comboBox1.Items.Add("全位");
                comboBox1.SelectedIndex = 0;
            }
            if (comboBox15.SelectedIndex == 2)
            {
                comboBox1.Items.Clear();
                for (var i = 0; i < kai_str.Length; i++)
                {
                    comboBox1.Items.Add(kai_str[i] );
                }
               
                comboBox1.SelectedIndex = 0;
            }
      
        }

        private void comboBox16_SelectedIndexChanged(object sender, EventArgs e)
        {
            w2_xu = comboBox16.SelectedIndex;
            if (comboBox16.SelectedIndex == 0)
            {
                comboBox3.Items.Clear();
                for (var i = 0; i < 10; i++)
                {
                    comboBox3.Items.Add(i.ToString());
                }
                comboBox3.SelectedIndex = 1;

              //  comboBox3.Visible = true;

            }
            if (comboBox16.SelectedIndex == 1)
            {
              //  comboBox3.Visible = false;
                comboBox3.Items.Clear();
                for (var i = 0; i < kai_str.Length; i++)
                {
                    comboBox3.Items.Add(kai_str[i] + "冷");
                }
                comboBox3.Items.Add( "全位");
                comboBox3.SelectedIndex = 2;
            }
            if (comboBox16.SelectedIndex == 2)
            {
                //  comboBox3.Visible = false;
                comboBox3.Items.Clear();
                for (var i = 0; i < kai_str.Length; i++)
                {
                    comboBox3.Items.Add(kai_str[i] );
                }
                
                comboBox3.SelectedIndex = 2;
            }
        }

        private void groupBox3_Enter(object sender, EventArgs e)
        {

        }
    }
}
