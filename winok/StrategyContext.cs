using System;
namespace winok {

    /// <summary>
    /// 策略状态机阶段
    /// </summary>
    public enum StrategyStage
    {
        /// <summary>未启动</summary>
        Idle = 0,

        /// <summary>已触发（方向已确定）</summary>
        Triggered = 1,

        /// <summary>等待介入（固定N分钟 / N根K）</summary>
        WaitingEntry = 2,

        /// <summary>已介入（已下单 / 已持仓）</summary>
        InPosition = 3,

        /// <summary>已结束（止盈 / 止损 / 手动停止）</summary>
        Finished = 4,
        WaitingSignal = 1,  // 等待金蚂蚁信号（新加的）
    }


    public enum StrategyState
    {
        Idle,           // 未启动
        Triggered,      // 已触发，等待介入
        InPosition,     // 已下单，持仓中
        Finished        // 已结束
    }

    public class StrategyContext
    {
        // =========================
        // 状态机
        // =========================
        public int StrategyId { get; set; }      // 🔑 唯一编号
        public StrategyStage Stage { get; set; } = StrategyStage.Idle;
        public string kongduo = "做多";
        public bool shitou = false;
        public bool shizhan = true;
        public int duokong = 1;
        public int beishu = 1;
        public int maimai = 1;
        public long listing_no = 0;
        public int maijia = -1;
        public int shoujia = -1;
        public int buzhou = 0;
        public int wanzheng_k = 0;
        public int bodong_k = 0;
        public int lianyang = 0;
        public int lianyin = 0;
        public int yingkui = 0;
        public DateTime OrderSendTime { get; set; }
        public long oid2 = 0;
        public int closeprice = 0;
        public DateTime xiacheng_time { get; set; }
        public DateTime weituo1 { get; set; }
        public DateTime weituo2 { get; set; }

        public DateTime chengjiao1 { get; set; }
        public DateTime chengjiao2{ get; set; }
        // =========================
        // 时间
        // =========================
        public DateTime? TriggerTime { get; set; }
        public DateTime? LastKlineTime { get; set; }

        // =========================
        // 参数（来自 UI）
        // =========================
        public int EntryDelayMinutes { get; set; } = 1;
        public string Direction { get; set; } = "buy";
        public bool ReverseBuy { get; set; } = false;
        public int Quantity { get; set; } = 1;

        // =========================
        // 下单 / 持仓
        // =========================
        public bool OrderSent { get; set; } = false;
        public string ClientId { get; set; }
        public long Oid = -1;

        /// <summary>入场价格（非常关键）</summary>
        public decimal EntryPrice { get; set; }

        // =========================
        // 止盈 / 止损（先用极小值）
        // =========================
        public decimal TakeProfit { get; set; } = 1; // +1 就止盈
        public decimal StopLoss { get; set; } = 1;   // -1 就止损

        public void Reset()
        {
            Stage = StrategyStage.Idle;
            TriggerTime = null;
            LastKlineTime = null;
            OrderSent = false;
            ClientId = null;
            Oid = 0;
            EntryPrice = 0;
        }
    }
}

