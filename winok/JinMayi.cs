using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace winok
{
    public static class JinMayi
    {

        public static double[] EMA(double[] src, int period)
        {
            int n = src.Length;
            double[] ema = new double[n];
            if (n == 0) return ema;

            double k = 2.0 / (period + 1);
            ema[0] = src[0];

            for (int i = 1; i < n; i++)
                ema[i] = k * src[i] + (1 - k) * ema[i - 1];

            return ema;
        }

        public static List<JinMayiItem> CalcJinMayi(List<KlineItem> kl)
        {
            int n = kl.Count;
            var r = new List<JinMayiItem>();
            for (int i = 0; i < n; i++)
                r.Add(new JinMayiItem());

            // ======== 1. HHV(HIGH,N) 和 LLV(LOW,N) ========
            int N = 1;
            int N1 = 1;
            int Q = 0;
            int Q1 = 0;

            double[] HH = new double[n];
            double[] LH = new double[n];

            for (int i = 0; i < n; i++)
            {
                double hv = kl[i].high;
                double lv = kl[i].low;

                for (int j = Math.Max(0, i - (N - 1)); j <= i; j++)
                {
                    hv = Math.Max(hv, kl[j].high);
                    lv = Math.Min(lv, kl[j].low);
                }
                HH[i] = hv;
                LH[i] = lv;
            }

            // ======== 2. H1 / L1 ========
            double[] H1 = new double[n];
            double[] L1 = new double[n];

            for (int i = 1; i < n; i++)
            {
                // --- H1 条件 ---
                bool condH1 =
                    HH[i] < HH[i - 1] &&
                    LH[i] < LH[i - 1] &&
                    kl[i - 1].open > kl[i].close &&
                    kl[i].open > kl[i].close &&
                    ((kl[i].open) - kl[i].close) > Q1;

                if (condH1)
                {
                    int idx = Math.Max(0, i - N1);
                    H1[i] = HH[idx];
                }
                else H1[i] = 0;

                // --- L1 条件 ---
                bool condL1 =
                    LH[i] > LH[i - 1] &&
                    HH[i] > HH[i - 1] &&
                    kl[i - 1].open < kl[i].close &&
                    kl[i].open < kl[i].close &&
                    (kl[i].close - kl[i].open) > Q1;

                if (condL1)
                {
                    int idx = Math.Max(0, i - N1);
                    L1[i] = LH[idx];
                }
                else L1[i] = 0;
            }

            // ======== 3. barslast + ref 计算 bab/cab ========
            double[] bab = new double[n];
            double[] cab = new double[n];

            int lastH = -1, lastL = -1;

            for (int i = 0; i < n; i++)
            {
                if (H1[i] != 0) lastH = i;
                if (L1[i] != 0) lastL = i;

                if (lastH < 0) bab[i] = 0;
                else bab[i] = H1[lastH];

                if (lastL < 0) cab[i] = 0;
                else cab[i] = L1[lastL];
            }

            // ======== 4. K1 ========
            int[] K1 = new int[n];
            for (int i = 0; i < n; i++)
            {
                double close = kl[i].close;
                if (close > bab[i]) K1[i] = -3;   // 压力（空）
                else if (close < cab[i]) K1[i] = 1; // 支撑（多）
                else K1[i] = 0;
            }

            // ======== 5. K2（延续） ========
            int[] K2 = new int[n];
            int lastSig = 0;

            for (int i = 0; i < n; i++)
            {
                if (K1[i] != 0)
                {
                    K2[i] = K1[i];
                    lastSig = K1[i];
                }
                else
                {
                    K2[i] = lastSig;
                }
            }

            // ======== 6. G（绘制阶梯线用） ========
            double[] G = new double[n];
            for (int i = 0; i < n; i++)
            {
                if (K2[i] == 1) G[i] = bab[i];
                else if (K2[i] == -3) G[i] = cab[i];
                else G[i] = 0;
            }

            // ======== 7. CROSS 信号 ========
            double[] TMP = K2.Select(x => (double)x).ToArray();

            bool[] Buy = new bool[n];
            bool[] Sell = new bool[n];

            // EMA55（用于过滤 ↑↓）
            double[] ema55 = EMA(kl.Select(k => (double)k.close).ToArray(), 55);

            for (int i = 1; i < n; i++)
            {
                double now = TMP[i];
                double prev = TMP[i - 1];

                // CROSS(TMP,0) 卖
                bool crossSell = (prev < 0 && now > 0);

                // CROSS(0,TMP) 买
                bool crossBuy = (prev > 0 && now < 0);

                Sell[i] = crossSell;
                Buy[i] = crossBuy;

                // 是否满足 MA55 条件
                bool buy55 = crossBuy && kl[i].close >= ema55[i];
                bool sell55 = crossSell && kl[i].close <= ema55[i];

                r[i].BuySignal = buy55;
                r[i].SellSignal = sell55;
            }

            // ======== 8. 回填所有结果 ========
            for (int i = 0; i < n; i++)
            {
                r[i].G = G[i];
                r[i].K2 = K2[i];
                r[i].HH = HH[i];
                r[i].LH = LH[i];
            }

            return r;
        }

    }
}
