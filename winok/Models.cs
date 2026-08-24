using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace winok
{


    // 行情 & 指标 & 交易结构
    public class LoginResult
    {
        public string banben { get; set; }
        /// <summary>
        /// 用户ID（如 m_id / user_id）
        /// </summary>
        public int UserId { get; set; }
        public DateTime ExpireAt { get; set; }   // ⚠ 到期时间（本地时间）
        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 登录 token（HTTP / WS 用）
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// 登录时间
        /// </summary>
        public DateTime LoginTime { get; set; } = DateTime.Now;
    }
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
    }

    public class JinMayiItem
    {
        public double G;
        public int K2;
        public double HH;
        public double LH;
        public bool BuySignal;
        public bool SellSignal;
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

    public class HistoryRow
    {
        public string Time { get; set; }
        public double Price { get; set; }
        public string Signal { get; set; }
        public double Volume { get; set; }

        public int open { get; set; }
        public int close { get; set; }
        public int high { get; set; }
        public int low { get; set; }
    }

    public class StrategyConfig
    {
        public int StartMode;
        public int InitialDirection;
        public int EntryMode;
        public int EntryN;
        public int BuyMode;
        public int Lots;
        public double TakeProfit;
        public double StopLoss;
    }
}
