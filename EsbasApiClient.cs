namespace DotNetWhatsApp;

using System.Net.Http.Headers;
using System.Net.Http.Json;

public class EsbasApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EsbasApiClient> _logger;

    public EsbasApiClient(HttpClient httpClient, ILogger<EsbasApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.esbas.com.tr");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://mip.esbas.com.tr");
    }

    private async Task<string?> GetNewAuthTokenAsync()
    {
        var authUrl = "/mipapigateway/api/Login/Authenticate";
        var payload = new AuthPayload("esbasdev", "esbasESBAS13", 0, "dize", "dize", "dize");
        try
        {
            var response = await _httpClient.PostAsJsonAsync(authUrl, payload);
            response.EnsureSuccessStatusCode();
            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (!string.IsNullOrEmpty(authResponse?.Token))
            {
                _logger.LogInformation("Yeni ESBAŞ API token'ı başarıyla alındı.");
                return authResponse.Token;
            }
            _logger.LogError("ESBAŞ kimlik doğrulama başarılı ancak yanıtta token bulunamadı.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yeni ESBAŞ token'ı alınırken hata oluştu.");
            return null;
        }
    }

    public async Task<List<OrderStatus>?> QueryOrderAsync(string orderNumber)
    {
        var token = await GetNewAuthTokenAsync();
        if (string.IsNullOrEmpty(token)) return null;

        var apiUrl = $"/MIPAPIGATEWAY/api/Stock/GetProductFoodCourtDeliveryOrderStatus?orderNumber={orderNumber}";
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<List<OrderStatus>>();
            return data ?? new List<OrderStatus>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ESBAŞ API sorgusu sırasında hata (Sipariş No: {SiparisNo})", orderNumber);
            return null;
        }
    }
}