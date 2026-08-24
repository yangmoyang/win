using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace winok
{
    public class RawWsMqttTradeClient
    {
        // =========================
        // 基本配置
        // =========================
        private readonly string _host;
        private readonly int _port;
        private readonly string _path;
        private readonly int _userId;
        private readonly string _client;
        private readonly byte[] _mqttConnect;
        // =========================
        // 网络对象
        // =========================
        private TcpClient _tcp;
        private SslStream _ssl;

        private readonly object _sendLock = new object();

        // =========================
        // 状态机
        // =========================
        private enum ClientState
        {
            Disconnected,
            WsConnecting,
            WsConnected,
            MqttConnecting,
            MqttConnected
        }

        private volatile ClientState _state = ClientState.Disconnected;

        // =========================
        // 心跳 & 重连
        // =========================
        private Timer _pingTimer;
        private DateTime _lastPong = DateTime.MinValue;

        private readonly TimeSpan PingInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(10);

        private volatile bool _running = false;

        // =========================
        // MQTT CONNECT / SUB 包
        // =========================
        private byte[] _mqttConnectPacket;

        // =========================
        // 事件（给业务层）
        // =========================
        public event Action<string> OnUserNotify;
        public event Action<long, int> OnOrderStatus;
        public event Action<FundInfo> OnFundUpdate;

        public event Action<string, string> OnPublish;
        // =========================
        // ctor
        // =========================
        public RawWsMqttTradeClient(string host, int port, string path, int userId,string client, byte[] mqttConnect)
        {
            _host = host;
            _port = port;
            _path = path;
            _userId = userId;
            _client = client;
            _mqttConnect = mqttConnect;
        }

        // =========================
        // 外部入口
        // =========================
        public async Task ConnectAsync(byte[] mqttConnectPacket)
        {
            _mqttConnectPacket = mqttConnectPacket;
            _running = true;

            await Task.Run(RunStateMachine);
        }

        public void Stop()
        {
            _running = false;
            _pingTimer?.Dispose();
            SafeClose();
        }

        // =========================
        // 状态机主循环
        // =========================
        private async Task RunStateMachine()
        {
            while (_running)
            {
                try
                {
                    if (_state == ClientState.Disconnected)
                    {
                        _state = ClientState.WsConnecting;
                        await ConnectWsAsync();
                        continue;
                    }

                    if (_state == ClientState.WsConnected)
                    {
                        _state = ClientState.MqttConnecting;
                        SendWsBinary(_mqttConnectPacket);
                        continue;
                    }

                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ STATE ERROR: " + ex.Message);
                    ForceReconnect();
                }
            }
        }

        // =========================
        // WS 连接
        // =========================
        private async Task ConnectWsAsync()
        {
            Console.WriteLine("🔌 WS connecting...");

            SafeClose();

            _tcp = new TcpClient();
            await _tcp.ConnectAsync(_host, _port);

            _ssl = new SslStream(_tcp.GetStream(), false, (a, b, c, d) => true);
            await _ssl.AuthenticateAsClientAsync(_host);

            byte[] keyBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(keyBytes);

            string wsKey = Convert.ToBase64String(keyBytes);

            string req =
                $"GET {_path} HTTP/1.1\r\n" +
                $"Host: {_host}:{_port}\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Key: {wsKey}\r\n" +
                "Sec-WebSocket-Version: 13\r\n" +
                "Sec-WebSocket-Protocol: mqtt\r\n\r\n";

            byte[] reqBytes = Encoding.ASCII.GetBytes(req);
            await _ssl.WriteAsync(reqBytes, 0, reqBytes.Length);
            await _ssl.FlushAsync();

            ReadHttpHeader(_ssl);

            _state = ClientState.WsConnected;

            StartReceiveLoop();
            StartPing();
            Console.WriteLine("✔ WS handshake OK");
        }

        // =========================
        // 接收循环
        // =========================
        private void StartReceiveLoop()
        {
            Task.Run(async () =>
            {
                var buf = new byte[8192];

                while (_running)
                {
                    try
                    {
                        int n = await _ssl.ReadAsync(buf, 0, buf.Length);
                        if (n <= 0) throw new IOException("WS closed");

                        var frame = buf.Take(n).ToArray();
                        var payload = DecodeWsFrame(frame);

                        HandleMqtt(payload);
                    }
                    catch
                    {
                        ForceReconnect();
                        return;
                    }
                }
            });
        }

        // =========================
        // MQTT 处理
        // =========================
        private void HandleMqtt(byte[] data)
        {
            if (data == null || data.Length < 2) return;

            byte packetType = (byte)(data[0] & 0xF0);

            if (data.Length == 2 && data[0] == 0xD0 && data[1] == 0x00)
            {
                _lastPong = DateTime.UtcNow;
                _pingTimeoutCount = 0;
                return;
            }
            // PINGRESP
            if (packetType == 0xD0)
            {
                _lastPong = DateTime.Now;
                return;
            }

            // CONNACK
            if (packetType == 0x20)
            {
                _state = ClientState.MqttConnected;

                // SUB push_xxx
                SendWsBinary(BuildSubscribe($"push_{_userId}", 1));
                SendWsBinary(BuildSubscribe("msg_broadcast", 2));
                return;
            }

            // SUBACK
            if (packetType == 0x90)
                return;

            // PUBLISH
            if (packetType == 0x30)
            {
                //ParsePublish(data);
                try
                {
                    int idx = 1;

                    // ---- Remaining Length（只用于跳过）----
                    int multiplier = 1;
                    int remainingLength = 0;
                    byte digit;
                    int rlBytes = 0;

                    do
                    {
                        digit = data[idx++];
                        remainingLength += (digit & 0x7F) * multiplier;
                        multiplier *= 128;
                        rlBytes++;
                    }
                    while ((digit & 0x80) != 0);

                    // ---- Topic Length ----
                    int topicLen = (data[idx] << 8) | data[idx + 1];
                    idx += 2;

                    // ---- Topic ----
                    string topic = Encoding.ASCII.GetString(data, idx, topicLen);
                    idx += topicLen;

                    // ---- QoS > 0 才有 PacketId ----
                    int qos = (data[0] >> 1) & 0x03;
                    if (qos > 0)
                    {
                        idx += 2; // Packet Identifier
                    }

                    // ✅ Payload = 剩余所有字节
                    string payload = Encoding.UTF8.GetString(data, idx, data.Length - idx);
                    if (!payload.Contains("1004"))
                    {
                        Console.WriteLine($"[PUBLISH]\nTopic={topic}\nPayload={payload}");
                    }
                   

                    // 🔥 通知业务层
                    OnPublish?.Invoke(topic, payload);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ PUBLISH parse error: " + ex);
                }

                return;
            }
        }

        private void ParsePublish(byte[] data)
        {
            int idx = 1;
            int remaining = 0;
            int mul = 1;
            byte digit;

            do
            {
                digit = data[idx++];
                remaining += (digit & 0x7F) * mul;
                mul *= 128;
            } while ((digit & 0x80) != 0);

            int topicLen = (data[idx] << 8) | data[idx + 1];
            idx += 2;

            string topic = Encoding.ASCII.GetString(data, idx, topicLen);
            idx += topicLen;

            string payload = Encoding.UTF8.GetString(data, idx, data.Length - idx);

            DispatchBusiness(topic, payload);
        }

        // =========================
        // 业务事件分发
        // =========================
        private void DispatchBusiness(string topic, string payload)
        {
            try
            {
                var obj = JObject.Parse(payload);
                int code = obj["code"]?.Value<int>() ?? -1;

                if (obj["msg"] != null)
                    OnUserNotify?.Invoke(obj["msg"].ToString());

                if (obj["oid"] != null && obj["status"] != null)
                    OnOrderStatus?.Invoke(
                        obj["oid"].Value<long>(),
                        obj["status"].Value<int>());

                if (code == 1004)
                {
                    OnFundUpdate?.Invoke(new FundInfo
                    {
                        Balance = obj["balance"]?.Value<decimal>() ?? 0,
                        Freeze = obj["freeze"]?.Value<decimal>() ?? 0,
                        Bond = obj["bond"]?.Value<decimal>() ?? 0
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ BUSINESS PARSE ERROR: " + ex.Message);
            }
        }

        // =========================
        // 发送
        // =========================
        public void SendRaw(byte[] mqttPacket)
        {
            if (_state != ClientState.MqttConnected)
            {
                Console.WriteLine($"⚠ DROP SEND, state={_state}");
                return;
            }

            SendWsBinary(mqttPacket);
        }

        private void SendWsBinary(byte[] payload)
        {
            byte[] frame = BuildWsFrame(0x82, payload);

            lock (_sendLock)
            {
                _ssl.Write(frame, 0, frame.Length);
                _ssl.Flush();
            }
        }
        private int _pingTimeoutCount = 0;
        private const int MaxPingTimeoutCount = 2;
        // ===== 心跳时间点 =====
        private DateTime _lastPing = DateTime.MinValue;
        //private DateTime _lastPong = DateTime.MinValue;

        // =========================
        // 心跳
        // =========================
        private void StartPing()
        {
            _pingTimer?.Dispose();

            _pingTimer = new Timer(_ =>
            {
                if (!_running || _state != ClientState.MqttConnected)
                    return;

                var now = DateTime.UtcNow;

                // ===== 超时检测 =====
                if (_lastPong != DateTime.MinValue &&
                    now - _lastPong > PingTimeout)
                {
                    _pingTimeoutCount++;

                    if (_pingTimeoutCount >= MaxPingTimeoutCount)
                    {
                        OnUserNotify?.Invoke("⚠️ 心跳发送失败，正在重连");
                        //  NotifyUser("⚠️ 心跳超时，连接可能已断开，正在重连…");
                       // ForceReconnect();
                        return;
                    }
                }
                else
                {
                    // 只要成功一次就清零
                    _pingTimeoutCount = 0;
                }

                // ===== 发送 PINGREQ =====
                try
                {
                    SendWsBinary(new byte[] { 0xC0, 0x00 });
                    _lastPing = now;
                }
                catch (Exception ex)
                {
                    OnUserNotify?.Invoke("⚠️ 心跳发送失败，正在重连");
                    ForceReconnect();
                }

            }, null, PingInterval, PingInterval);
        }

        private readonly object _reconnectLock = new object();

        // =========================
        // 工具
        // =========================
        private void ForceReconnect()
        {
            return;
            if (!_running) return;

            Console.WriteLine("🔄 FORCE RECONNECT...");

            // 防止并发重连
            lock (_reconnectLock)
            {
                if (_state == ClientState.WsConnecting ||
                    _state == ClientState.MqttConnecting)
                {
                    Console.WriteLine("⚠ Reconnect already in progress");
                    return;
                }

                _state = ClientState.Disconnected;

                SafeClose();

                // ⬇️ 关键：异步完整重连
                Task.Run(async () =>
                {
                    try
                    {
                        await ConnectWsAsync();          // 1️⃣ TCP + SSL + WS
                        SendMqttConnect();               // 2️⃣ MQTT CONNECT
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("❌ Reconnect failed: " + ex.Message);
                        await Task.Delay(2000);
                        ForceReconnect();
                    }
                });
            }
        }
        private void SendMqttConnect()
        {
            _state = ClientState.MqttConnecting;

           // string clientId = Guid.NewGuid().ToString();
            //string username = $"mem_{user_id}_pc"; // 例如 mem_360245_pc

            //byte[] mqttConnect = BuildMqttConnectPacket(token, clientId, username, 90);

            // 连接 WS 后：
            SendWsBinary(_mqttConnect);

        }

      
        // MQTT Remaining Length（变长）
    

        private void SafeClose()
        {
            try { _ssl?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            _ssl = null;
            _tcp = null;
        }

        private static string ReadHttpHeader(Stream s)
        {
            var sb = new StringBuilder();
            int prev = 0, cur;
            while ((cur = s.ReadByte()) != -1)
            {
                sb.Append((char)cur);
                if (prev == '\r' && cur == '\n' &&
                    sb.ToString().EndsWith("\r\n\r\n"))
                    break;
                prev = cur;
            }
            return sb.ToString();
        }

        private static byte[] BuildWsFrame(byte opcode, byte[] payload)
        {
            if (payload == null)
            {
                payload = Array.Empty<byte>(); // 这一行在 .NET 4.6+ 也 OK
            }

            var frame = new List<byte> { opcode };

            int len = payload.Length;
            if (len <= 125)
                frame.Add((byte)(0x80 | len));
            else
            {
                frame.Add(0xFE);
                frame.Add((byte)(len >> 8));
                frame.Add((byte)(len & 0xFF));
            }

            byte[] mask = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(mask);

            frame.AddRange(mask);

            for (int i = 0; i < payload.Length; i++)
                payload[i] ^= mask[i % 4];

            frame.AddRange(payload);
            return frame.ToArray();
        }

        private static byte[] DecodeWsFrame(byte[] frame)
        {
            int idx = 2;
            int len = frame[1] & 0x7F;

            if (len == 126)
            {
                len = (frame[2] << 8) | frame[3];
                idx = 4;
            }

            return frame.Skip(idx).Take(len).ToArray();
        }

        private static byte[] BuildSubscribe(string topic, ushort packetId)
        {
            var body = new List<byte>
            {
                (byte)(packetId >> 8),
                (byte)(packetId & 0xFF)
            };

            var t = Encoding.ASCII.GetBytes(topic);
            body.Add((byte)(t.Length >> 8));
            body.Add((byte)(t.Length & 0xFF));
            body.AddRange(t);
            body.Add(0x01);

            var frame = new List<byte> { 0x82 };
            WriteRemainingLength(frame, body.Count);
            frame.AddRange(body);
            return frame.ToArray();
        }

        private static void WriteRemainingLength(List<byte> frame, int len)
        {
            do
            {
                byte digit = (byte)(len % 128);
                len /= 128;
                if (len > 0) digit |= 0x80;
                frame.Add(digit);
            } while (len > 0);
        }
    }

    public class FundInfo
    {
        public decimal Balance { get; set; }
        public decimal Freeze { get; set; }
        public decimal Bond { get; set; }
    }
}
