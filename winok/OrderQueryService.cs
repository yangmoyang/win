using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class OrderQueryService
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)   // ★ 必须有
    };

    private readonly string _baseUrl = "https://47.76.96.34:8003";
    private readonly string _token;

    // 防止重入
    private int _loadingFlag = 0;

    public OrderQueryService(string token)
    {
        _token = token;
    }

    public async Task<OrderListResponse> GetOrderListAsync(
        int page = 1,
        int pageSize = 20,
        string type = "",
        string direction = "",
        string ocata = "0")
    {
        // ★ 防止并发
        if (Interlocked.Exchange(ref _loadingFlag, 1) == 1)
            return null;

        try
        {
            var url = $"{_baseUrl}/nodeallist";

            var body = new
            {
                page = page,
                pagesize = pageSize,
                type = type,
                direction = direction,
                ocata = ocata
            };

            string json = JsonConvert.SerializeObject(body);

             var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("X-Version", "ASC1.0.17");
            req.Headers.Add("X-Client", "PC");
            req.Headers.Add("token", _token);
            req.Headers.Add("Accept", "application/json");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

             var resp = await _httpClient.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            string respJson = await resp.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<OrderListResponse>(respJson);
        }
        catch (TaskCanceledException)
        {
            // 超时
            return null;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            Interlocked.Exchange(ref _loadingFlag, 0);
        }
    }
    public async Task<OrderListResponse> GetOrderListAsync2(
        int page = 1,
        int pageSize = 20,
        string type = "",
        string direction = "",
        string ocata = "0")
    {
        // ★ 防止并发
        if (Interlocked.Exchange(ref _loadingFlag, 1) == 1)
            return null;

        try
        {
            var url = $"{_baseUrl}/orderlist";

            var body = new
            {
                page = page,
                pagesize = pageSize,
                type = type,
                direction = direction,
                ocata = ocata
            };

            string json = JsonConvert.SerializeObject(body);

            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("X-Version", "ASC1.0.17");
            req.Headers.Add("X-Client", "PC");
            req.Headers.Add("token", _token);
            req.Headers.Add("Accept", "application/json");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            string respJson = await resp.Content.ReadAsStringAsync();
            OrderListResponse t2 = JsonConvert.DeserializeObject<OrderListResponse>(respJson);
            return t2;
        }
        catch (TaskCanceledException)
        {
            // 超时
            return null;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            Interlocked.Exchange(ref _loadingFlag, 0);
        }
    }
}
