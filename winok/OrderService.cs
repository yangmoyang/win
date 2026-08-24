using System;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;


namespace winok
{
    /// <summary>
    /// 订单状态管理服务
    /// </summary>
    public class OrderService
    {

        // ===== 对外事件（UI 订阅）=====
        public event Action<string> OnUserNotify;                 // 提示消息
      
        public event Action<long, int, string> OnOrderStatus;     // oid, status, msg
        public event Action<FundInfo> OnFundUpdate;               // 资金变动

        private readonly RawWsMqttTradeClient _client;

        /// <summary>
        /// clientId -> OrderContext
        /// </summary>
        private readonly ConcurrentDictionary<string, OrderContext> _orders
            = new ConcurrentDictionary<string, OrderContext>();

        public OrderService(RawWsMqttTradeClient client)
        {
            _client = client;

            // ★ 关键：订阅 WS 推送
            _client.OnPublish += HandlePublish;
        }

        // =====================================================
        // 下单（外部调用）
        // =====================================================
        public string PlaceOrder(
            byte[] placeOrderPacket,
            int marketId,string clientId)
        {
           // string clientId = Guid.NewGuid().ToString();

            var ctx = new OrderContext
            {
                ClientId = clientId,
                MarketId = marketId,
                Status = OrderStatus.Sent,
                CreateTime = DateTime.Now
            };

            _orders[clientId] = ctx;

            Console.WriteLine($"📤 PLACE ORDER clientId={clientId}");

            _client.SendRaw(placeOrderPacket);

            return clientId;
        }
        public string SendPlaceOrder(byte[] placeOrderPacket, string clientId, int marketId)
        {
            var ctx = new OrderContext
            {
                ClientId = clientId,
                MarketId = marketId,
                Status = OrderStatus.Sent,
                CreateTime = DateTime.Now
            };

            _orders[clientId] = ctx;

            Console.WriteLine($"📤 下单发送 clientId={clientId}");

            _client.SendRaw(placeOrderPacket);

            return clientId;
        }

        // =====================================================
        // WS → MQTT → PUBLISH 回调
        // =====================================================
        private void HandlePublish(string topic, string payload)
        {
            if (!topic.StartsWith("push_"))
                return;
            if(!payload.Contains("1004"))
           Console.WriteLine($"📩 PUSH {topic}\n{payload}");

            JObject obj;
            try
            {
                obj = JObject.Parse(payload);
            }
            catch
            {
                return;
            }

            int code = obj["code"]?.Value<int>() ?? -1;
            string msg = obj["msg"]?.ToString();

            // =================================================
            // 1️⃣ 通用提示（下单成功 / 撤单成功 / 指令发送成功）
            // =================================================
            if (!string.IsNullOrEmpty(msg) && code > 0)
            {
                OnUserNotify?.Invoke(payload);
                
            }

            // =================================================
            // 2️⃣ 下单 ACK（通过 clientId）
            // =================================================
            if (obj.ContainsKey("clientId"))
            {
                string clientId = obj["clientId"]?.ToString();
                if (!string.IsNullOrEmpty(clientId) &&
                    _orders.TryGetValue(clientId, out var ctx))
                {
                    ctx.LastUpdateTime = DateTime.Now;

                    if (code == 1000) // 下单成功
                    {
                        ctx.Oid = obj["oid"]?.Value<long>();
                        ctx.Status = OrderStatus.Accepted;

                        Console.WriteLine($"✅ ORDER ACCEPTED clientId={clientId} oid={ctx.Oid}");
                    }
                    else
                    {
                        ctx.Status = OrderStatus.Rejected;
                        Console.WriteLine($"❌ ORDER REJECTED clientId={clientId} code={code}");
                    }
                }
            }

            // =================================================
            // 3️⃣ 订单状态推送（order_status，用 oid）
            // =================================================
            if (obj["instruct"]?.ToString() == "order_status")
            {
                long oid = obj["oid"]?.Value<long>() ?? 0;
                int status = obj["status"]?.Value<int>() ?? -1;

                if (oid > 0)
                {
                    var ctx = GetOrderByOid(oid);
                    if (ctx != null)
                    {
                        ctx.Status = (OrderStatus)status;
                        ctx.LastUpdateTime = DateTime.Now;
                    }

                    OnOrderStatus?.Invoke(oid, status, msg);
                }

                return;
            }

            // =================================================
            // 4️⃣ 资金信息
            // =================================================
            if (code == 1004) // 资金推送
            {
                var fund = new FundInfo
                {
                    Balance = obj["balance"]?.Value<decimal>() ?? 0,
                    Freeze = obj["freeze"]?.Value<decimal>() ?? 0,
                    Bond = obj["bond"]?.Value<decimal>() ?? 0
                };

                OnFundUpdate?.Invoke(fund);
            }
        }
        public class FundInfo
        {
            public decimal Balance { get; set; }
            public decimal Freeze { get; set; }
            public decimal Bond { get; set; }
        }
        // =====================================================
        // 查询 / 工具方法
        // =====================================================

        public OrderContext GetOrderByClientId(string clientId)
        {
            _orders.TryGetValue(clientId, out var ctx);
            return ctx;
        }

        public OrderContext GetOrderByOid(long oid)
        {
            foreach (var kv in _orders)
            {
                if (kv.Value.Oid == oid)
                    return kv.Value;
            }
            return null;
        }

        public void DumpOrders()
        {
            Console.WriteLine("===== ORDERS =====");
            foreach (var kv in _orders)
            {
                var o = kv.Value;
                Console.WriteLine(
                    $"clientId={o.ClientId} oid={o.Oid} status={o.Status}");
            }
        }
    }
}
