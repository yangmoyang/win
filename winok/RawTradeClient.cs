using System;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

public class RawTradeClient
{
    private TcpClient _tcp;
    private SslStream _ssl;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    private const string HOST = "47.57.4.140";
    private const int PORT = 8006;

    // ======== 对外入口 ========
    public async Task ConnectAsync(string token)
    {
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(HOST, PORT);

        _ssl = new SslStream(
            _tcp.GetStream(),
            false,
            (sender, cert, chain, errors) => true // 不校验证书（和官方 PC 客户端一致）
        );

        await _ssl.AuthenticateAsClientAsync(
            HOST,
            null,
            SslProtocols.Tls12,
            false
        );

        Log("TLS connected");

        // 启动接收循环
        _ = Task.Run(ReceiveLoop);

        // ★ 关键：一上来就发 MQTT CONNECT
        byte[] connect = BuildMqttConnect(token);
        await _ssl.WriteAsync(connect, 0, connect.Length);
        await _ssl.FlushAsync();

        Log($"SEND MQTT CONNECT ({connect.Length} bytes)");
    }

    // ======== 下单 ========
    public async Task PlaceOrderAsync()
    {
        byte[] frame = BuildPlaceOrder();
        await _ssl.WriteAsync(frame, 0, frame.Length);
        await _ssl.FlushAsync();

        Log($"SEND PLACE_ORDER ({frame.Length} bytes)");
    }

    // ======== 接收 ========
    private async Task ReceiveLoop()
    {
        byte[] buf = new byte[8192];

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                int n = await _ssl.ReadAsync(buf, 0, buf.Length);
                if (n <= 0) break;

                byte[] data = buf.Take(n).ToArray();
                Log("RX: " + BitConverter.ToString(data));

                // MQTT CONNACK = 20 02 00 00
                if (data.Length >= 4 &&
                    data[0] == 0x20 &&
                    data[1] == 0x02 &&
                    data[2] == 0x00 &&
                    data[3] == 0x00)
                {
                    Log("MQTT CONNACK OK");

                    // ★ 验证成功后，你可以立刻下单
                    await PlaceOrderAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Log("RX ERROR: " + ex.Message);
        }
    }

    // ======== 构造 MQTT CONNECT ========
    private byte[] BuildMqttConnect(string token)
    {
        // 可变头 + Payload（你抓包里看到的内容）
        var body = new List<byte>
        {
            0x00,0x04,0x4D,0x51,0x54,0x54, // "MQTT"
            0x04,                         // v3.1.1
            0xC0,                         // username + password
            0x00,0x5A,                    // keepalive = 90

            0x00,0x24                    // clientId length = 36
        };

        // clientId（你抓到的 UUID）
        body.AddRange(Encoding.ASCII.GetBytes(
            "f5730aee-249f-4047-b0d2-53bae9ab42f1"
        ));

        // username
        body.Add(0x00);
        body.Add(0x0D);
        body.AddRange(Encoding.ASCII.GetBytes("mem_360245_pc"));

        // password（token）
        byte[] pwd = Encoding.ASCII.GetBytes(token);

        // 固定头
        var frame = new List<byte>();
        frame.Add(0x10); // CONNECT

        int remainingLength = body.Count + pwd.Length;
        frame.Add((byte)remainingLength); // ★ 当前长度 < 127，1 字节即可

        frame.AddRange(body);
        frame.AddRange(pwd);

        return frame.ToArray();
    }

    // ======== 构造下单帧 ========
    private byte[] BuildPlaceOrder()
    {
        string json =
            "{\"instruct\":\"place_order\",\"m_id\":360245,\"b_id\":1,\"num\":1," +
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

    private void Log(string s)
    {
        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " | " + s);
    }
}
