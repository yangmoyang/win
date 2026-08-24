using WebSocketSharp;
using System;
using System.Text;
using System.Security.Authentication;
using System.Linq;
using System.Collections.Generic;

public class OnlyConnectTest
{
    private WebSocket ws;

    public void Start(string token)
    {
        ws = new WebSocket("wss://47.57.4.140:8006/");
        ws.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;

        ws.OnOpen += (s, e) =>
        {
            Console.WriteLine("WS OPEN");

            // ★ 关键：一上来就发 MQTT CONNECT
            ws.Send(BuildMqttConnect(token));
            Console.WriteLine("SEND MQTT CONNECT");
        };

        ws.OnMessage += (s, e) =>
        {
            if (e.IsBinary)
                Console.WriteLine("RX BIN: " + BitConverter.ToString(e.RawData));
            else
                Console.WriteLine("RX TXT: " + e.Data);
        };

        ws.OnClose += (s, e) =>
            Console.WriteLine($"WS CLOSE {e.Code} {e.Reason}");

        ws.OnError += (s, e) =>
            Console.WriteLine("WS ERROR " + e.Message);

        ws.Connect();
    }

    private byte[] BuildMqttConnect(string token)
    {
        byte[] body = new byte[]
        {
        0x00,0x04,0x4D,0x51,0x54,0x54, // "MQTT"
        0x04,                         // v3.1.1
        0xC0,                         // username + password
        0x00,0x5A,                    // keepalive

        0x00,0x24,                    // clientId length
        // f5730aee-249f-4047-b0d2-53bae9ab42f1
        0x66,0x35,0x37,0x33,0x30,0x61,0x65,0x65,0x2D,0x32,0x34,0x39,
        0x66,0x2D,0x34,0x30,0x34,0x37,0x2D,0x62,0x30,0x64,0x32,0x2D,
        0x35,0x33,0x62,0x61,0x65,0x39,0x61,0x62,0x34,0x32,0x66,0x31,

        0x00,0x0D,                    // username length
        // mem_360245_pc
        0x6D,0x65,0x6D,0x5F,0x33,0x36,0x30,0x32,0x34,0x35,0x5F,0x70,0x63,

        0x01,0x05                     // password length (JWT 长度前缀的一部分)
        };

        byte[] pwd = Encoding.ASCII.GetBytes(token);

        int remainingLength = body.Length + pwd.Length;

        var list = new List<byte>();
        list.Add(0x10);                   // ★ MQTT CONNECT
        list.Add((byte)remainingLength);  // ★ Remaining Length（先用 1 字节，够用）
        list.AddRange(body);
        list.AddRange(pwd);

        return list.ToArray();
    }

}
