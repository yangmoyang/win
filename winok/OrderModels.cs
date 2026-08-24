using System;

namespace winok   // ⚠️ 一定要和 RawWsMqttTradeClient 的 namespace 一致
{
    public enum OrderStatus
    {
        Sent = 0,        // 已发送下单
        Accepted = 1,    // 下单成功（拿到 oid）
        Rejected = 2,    // 下单失败
        Canceled = 3     // 已撤单
    }

    public class OrderContext
    {
        public string ClientId { get; set; }   // clientId（你下单时生成的）
        public int MarketId { get; set; }      // m_id
        public long? Oid { get; set; }         // 服务器返回的 oid
        public OrderStatus Status { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime? LastUpdateTime { get; set; }  // ← 就是这行
    }
}
