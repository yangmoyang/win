using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace winok.utils   // ⚠ 命名空间按你项目改
{
    public static class TimeCheckHelper
    {
        /// <summary>
        /// 获取北京时间（阿里云）
        /// </summary>
        public static async Task<DateTime> GetBeijingTimeAsync()
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);

                string url = "https://acs.m.taobao.com/gw/mtop.common.getTimestamp";
                string json = await client.GetStringAsync(url);

                var obj = JObject.Parse(json);
                long ms = obj["data"]["t"].Value<long>();

                return DateTimeOffset
                    .FromUnixTimeMilliseconds(ms)
                    .ToOffset(TimeSpan.FromHours(8))
                    .DateTime;
            }
        }

        /// <summary>
        /// 校验本地时间是否与北京时间一致
        /// </summary>
        public static async Task<bool> CheckLocalTimeAsync(
            int allowDiffSeconds,
            Action<string> onError)
        {
            try
            {
                DateTime beijingTime = await GetBeijingTimeAsync();
                DateTime localTime = DateTime.Now;

                double diff = Math.Abs(
                    (beijingTime - localTime).TotalSeconds);

                if (diff > allowDiffSeconds)
                {
                    onError?.Invoke(
                        $"检测到系统时间异常！\n\n" +
                        $"北京时间：{beijingTime:yyyy-MM-dd HH:mm:ss}\n" +
                        $"本地时间：{localTime:yyyy-MM-dd HH:mm:ss}\n\n" +
                        $"相差 {diff:F1} 秒\n\n" +
                        $"请同步系统时间后再启动软件。"
                    );
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                onError?.Invoke(
                    "时间校验失败，请检查网络。\n" + ex.Message);
                return false;
            }
        }
    }
}
