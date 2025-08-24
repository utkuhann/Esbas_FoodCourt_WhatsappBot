namespace DotNetWhatsApp;

using System.Net.Http.Headers;
using System.Net.Http.Json;

public class WhatsAppApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppApiClient> _logger;
    private readonly string _accessToken;
    private readonly string _phoneNumberId;
    private readonly string _apiVersion = "v19.0";

    public WhatsAppApiClient(HttpClient httpClient, ILogger<WhatsAppApiClient> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _accessToken = configuration["WhatsAppConfiguration:AccessToken"]!;
        _phoneNumberId = configuration["WhatsAppConfiguration:PhoneNumberId"]!;
        _httpClient.BaseAddress = new Uri("https://graph.facebook.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public async Task<bool> SendTextMessageAsync(string recipientPhoneNumber, string message)
    {
        var requestUrl = $"/{_apiVersion}/{_phoneNumberId}/messages";
        var payload = new
        {
            messaging_product = "whatsapp",
            to = recipientPhoneNumber,
            type = "text",
            text = new { body = message }
        };

        try
        {
            _logger.LogInformation("WhatsApp mesajı gönderiliyor: {PhoneNumber}", recipientPhoneNumber);
            var response = await _httpClient.PostAsJsonAsync(requestUrl, payload);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("WhatsApp mesajı başarıyla gönderildi.");
                return true;
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("WhatsApp mesajı gönderilemedi. Durum Kodu: {StatusCode}, Cevap: {Response}", response.StatusCode, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp mesajı gönderilirken kritik bir hata oluştu.");
            return false;
        }
    }
}