using MQTTnet;
using MQTTnet.Client;

using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class TradeEngine
{
    // ====== 外部可订阅的事件 ======
    public event Action<string> OnLog;
    public event Action<string, JObject> OnPushJson;   // topic, json
    public event Action<int, string> OnError;          // code, msg
    public event Action<long, string> OnAck;           // oid, msg(可空)

    // ====== 配置 ======
    private readonly int _mId;
    private readonly string _clientId;
    private readonly string _token;

    // MQTT 推送通道
    private readonly string _mqttHost;
    private readonly int _mqttPort;
    private readonly string _mqttUsername;
    private readonly bool _mqttSsl;

    // WS 指令通道（8006）
    private readonly string _wsHost;
    private readonly int _wsPort;
    private readonly bool _wsSsl;

    // ====== 内部对象 ======
    private IMqttClient _pushClient;
    private ClientWebSocket _tradeWs;

    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    public TradeEngine(
        int mId,
        string clientId,
        string token,
        string mqttHost, int mqttPort, bool mqttSsl, string mqttUsername,
        string wsHost, int wsPort, bool wsSsl
    )
    {
        _mId = mId;
        _clientId = clientId;
        _token = token;

        _mqttHost = mqttHost;
        _mqttPort = mqttPort;
        _mqttSsl = mqttSsl;
        _mqttUsername = mqttUsername;

        _wsHost = wsHost;
        _wsPort = wsPort;
        _wsSsl = wsSsl;
    }

    // =======================
    // 启动：连 MQTT + 连 WS
    // =======================
    public async Task StartAsync()
    {
        await ConnectPushMqttAsync();
        await ConnectTradeWsAsync();

        // WS 接收循环（可选，但建议开着，方便看到服务端发的二进制提示/心跳）
        _ = Task.Run(() => TradeWsReceiveLoop(_cts.Token));
    }

    public async Task StopAsync()
    {
        try { _cts.Cancel(); } catch { }

        try
        {
            if (_pushClient != null && _pushClient.IsConnected)
                await _pushClient.DisconnectAsync();
        }
        catch { }

        try
        {
            if (_tradeWs != null && _tradeWs.State == WebSocketState.Open)
                await _tradeWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "close", CancellationToken.None);
        }
        catch { }
    }

    // =======================
    // 1) MQTT：只收 push
    // =======================
    private async Task ConnectPushMqttAsync()
    {
        var factory = new MqttFactory();
        _pushClient = factory.CreateMqttClient();

        _pushClient.ApplicationMessageReceivedAsync += e =>
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = e.ApplicationMessage.ConvertPayloadToString();

            try
            {
                // 只处理 push_360245 / msg_broadcast
                if (topic == $"push_{_mId}" || topic == "msg_broadcast")
                {
                    JObject jo = JObject.Parse(payload);
                    OnPushJson?.Invoke(topic, jo);

                    // 通用 code/msg 解析（你刚抓到的 {"code":1001,"msg":"不在交易时间"...}）
                    int code = jo["code"]?.Value<int>() ?? 0;
                    string msg = jo["msg"]?.ToString();

                    if (code != 0)
                    {
                        OnError?.Invoke(code, msg ?? "");
                    }
                    else
                    {
                        // 有些系统成功也会带 code=0
                        // 如果有 oid，尝试触发 OnAck
                        long oid = jo["oid"]?.Value<long>() ?? 0;
                        if (oid != 0)
                            OnAck?.Invoke(oid, msg ?? "");
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[PUSH RAW] {topic} {payload}");
                OnLog?.Invoke($"[PUSH PARSE ERROR] {ex.Message}");
            }

            return Task.CompletedTask;
        };

        var optBuilder = new MqttClientOptionsBuilder()
            .WithClientId(_mqttUsername) // 登录返回的 push.username
            .WithCredentials(_mqttUsername, _token) // 你之前就是这样连的
            .WithProtocolVersion(MqttProtocolVersion.V311);

        // 你现在是 wss / ws？登录返回里 push.is_ssl=1，通常走 wss
        if (_mqttSsl)
        {
            optBuilder.WithWebSocketServer(ws => ws.Uri = $"wss://{_mqttHost}:{_mqttPort}/mqtt");
        }
        else
        {
            optBuilder.WithWebSocketServer(ws => ws.Uri = $"ws://{_mqttHost}:{_mqttPort}/mqtt");
        }

        var options = optBuilder.Build();

        OnLog?.Invoke("[PUSH] connecting...");
        await _pushClient.ConnectAsync(options);
        OnLog?.Invoke("[PUSH] connected");

        // ✅ 订阅正确 topic（不是 trade/#）
        await _pushClient.SubscribeAsync($"push_{_mId}", MqttQualityOfServiceLevel.AtLeastOnce);
        await _pushClient.SubscribeAsync("msg_broadcast", MqttQualityOfServiceLevel.AtLeastOnce);

        OnLog?.Invoke($"[PUSH] subscribed push_{_mId} + msg_broadcast");
    }

    // =======================
    // 2) WS：只发交易指令帧
    // =======================
    private async Task ConnectTradeWsAsync()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        WebRequest.DefaultWebProxy = null;

        var uri = new Uri(
    $"wss://47.57.4.140:8006/?token={_token}"
);

        _tradeWs = new ClientWebSocket();

        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        WebRequest.DefaultWebProxy = null;

      //  _tradeWs.Options.SetRequestHeader("User-Agent", "Mozilla/5.0");
        _tradeWs.Options.SetRequestHeader("Origin", "https://47.57.4.140");

        await _tradeWs.ConnectAsync(uri, CancellationToken.None);

        OnLog?.Invoke("[TRADE-WS] connected");
    }

    // WS 接收循环（可选）
    private async Task TradeWsReceiveLoop(CancellationToken ct)
    {
        var buf = new byte[8192];

        while (!ct.IsCancellationRequested && _tradeWs != null && _tradeWs.State == WebSocketState.Open)
        {
            try
            {
                var res = await _tradeWs.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                if (res.MessageType == WebSocketMessageType.Close) break;

                // 这边不强依赖 WS 推送，因为你已经通过 MQTT push_360245 收到回执了
                // 但可以打印出来辅助排查
                var data = new byte[res.Count];
                Buffer.BlockCopy(buf, 0, data, 0, res.Count);

                int jsonStart = Array.IndexOf(data, (byte)'{');
                if (jsonStart >= 0)
                {
                    string json = Encoding.UTF8.GetString(data, jsonStart, data.Length - jsonStart);
                    OnLog?.Invoke("[TRADE-WS RX JSON] " + json);
                }
                else
                {
                    OnLog?.Invoke("[TRADE-WS RX BIN] len=" + data.Length);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke("[TRADE-WS RX ERROR] " + ex.Message);
                break;
            }
        }

        OnLog?.Invoke("[TRADE-WS] receive loop exit");
    }

    // =======================
    // 下单：构造 JSON + BA01帧 + Send
    // =======================
    public async Task<long> PlaceOrderAsync(int bId, int num, int direction, int oflag, int price, int bond)
    {
        if (_tradeWs == null || _tradeWs.State != WebSocketState.Open)
            throw new InvalidOperationException("Trade WS not connected");

        long oid = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        string json =
            "{" +
            "\"instruct\":\"place_order\"," +
            $"\"m_id\":{_mId}," +
            $"\"b_id\":{bId}," +
            $"\"num\":{num}," +
            $"\"direction\":{direction}," +
            $"\"oflag\":{oflag}," +
            $"\"price\":{price}," +
            $"\"bond\":{bond}," +
            $"\"oid\":{oid}," +
            "\"record_type\":1," +
            $"\"clientId\":\"{_clientId}\"" +
            "}";

        byte[] frame = BuildBa01TradeFrame(json);

        await _tradeWs.SendAsync(
            new ArraySegment<byte>(frame),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None
        );

        OnLog?.Invoke($"[SEND] place_order oid={oid} price={price} num={num}");
        return oid;
    }

    // 你目前抓到的固定结构：BA 01 00 05 "trade" 2C 36 + JSON
    // 注意：0x2C 0x36 先按抓包固定写死（后面如果发现它会变，再升级为自增序号）
    private static byte[] BuildBa01TradeFrame(string json)
    {
        var bytes = new List<byte>(128 + json.Length);

        bytes.Add(0xBA);
        bytes.Add(0x01);
        bytes.Add(0x00);
        bytes.Add(0x05);
        bytes.AddRange(Encoding.ASCII.GetBytes("trade"));

        // 你抓到的会话/序号字段
        bytes.Add(0x2C);
        bytes.Add(0x36);

        bytes.AddRange(Encoding.UTF8.GetBytes(json));
        return bytes.ToArray();
    }
}
