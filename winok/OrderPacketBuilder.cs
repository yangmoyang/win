using System;
using System.Collections.Generic;
using System.Text;

public static class OrderPacketBuilder
{
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
}
