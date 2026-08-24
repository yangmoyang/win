using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace winok
{
    public static class TradePacketBuilder
    {
        // ===== 协议常量 =====
        private const byte MAGIC = 0xBA;
        private const byte VERSION = 0x01;
        private const string SERVICE = "trade";
        private const string FLAG = "PP";

        /// <summary>
        /// 构建通用交易二进制包
        /// </summary>
        public static byte[] Build(object payload)
        {
            // 1️⃣ JSON 序列化
            string json = JsonConvert.SerializeObject(payload, Formatting.None);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            // 2️⃣ service
            byte[] serviceBytes = Encoding.ASCII.GetBytes(SERVICE);
            ushort serviceLen = (ushort)serviceBytes.Length;

            // 3️⃣ flag
            byte[] flagBytes = Encoding.ASCII.GetBytes(FLAG);

            // 4️⃣ 组包
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // 包头
                bw.Write(MAGIC);
                bw.Write(VERSION);

                // service 长度（大端）
                bw.Write((byte)(serviceLen >> 8));
                bw.Write((byte)(serviceLen & 0xFF));

                // service
                bw.Write(serviceBytes);

                // 固定标记
                bw.Write(flagBytes);

                // JSON
                bw.Write(jsonBytes);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 构建 place_order 下单包
        /// </summary>
        public static byte[] BuildPlaceOrder(
            int m_id,
            int b_id,
            int num,
            int direction,
            int oflag,
            int price,
            int bond,
            int record_type = 1,
            int oid = 0
       
        )
        {
            var payload = new
            {
                instruct = "place_order",
                m_id,
                b_id,
                num,
                direction,
                oflag,
                price,
                bond,
                oid,
                record_type,
                clientId = "f746defd-6ebf-4111-9cd2-868f9bf1c364"
            };

            return Build(payload);
        }
    }
}
