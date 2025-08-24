namespace DotNetWhatsApp.Controllers;

using DotNetWhatsApp;
using Microsoft.AspNetCore.Mvc;
using System.Text;

[ApiController]
[Route("api/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly ILogger<WhatsAppWebhookController> _logger;
    private readonly EsbasApiClient _esbasClient;
    private readonly WhatsAppApiClient _whatsAppClient;
    private readonly TrackingStateService _trackingState;
    private readonly string _verifyToken;
    private static readonly HashSet<string> HazirDurumlar = new() { "Teslime Hazır", "Teslime hazır", "Teslim Edildi", "Teslim edildi" };

    public WhatsAppWebhookController(
        ILogger<WhatsAppWebhookController> logger,
        EsbasApiClient esbasClient,
        WhatsAppApiClient whatsAppClient,
        IConfiguration configuration,
        TrackingStateService trackingState)
    {
        _logger = logger;
        _esbasClient = esbasClient;
        _whatsAppClient = whatsAppClient;
        _verifyToken = configuration["WhatsAppConfiguration:WebhookVerifyToken"]!;
        _trackingState = trackingState;
    }

    [HttpGet("webhook")]
    public IActionResult VerifyWebhook([FromQuery(Name = "hub.mode")] string mode,
                                       [FromQuery(Name = "hub.challenge")] string challenge,
                                       [FromQuery(Name = "hub.verify_token")] string token)
    {
        if (mode == "subscribe" && token == _verifyToken) { return Ok(challenge); }
        return Forbid();
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveMessage([FromBody] WhatsAppWebhookPayload payload)
    {
        var message = payload?.Entry?.FirstOrDefault()?.Changes?.FirstOrDefault()?.Value?.Messages?.FirstOrDefault();
        if (message?.Text?.Body == null || message.From == null) return Ok();

        await ProcessCommand(message.From, message.Text.Body);
        return Ok();
    }

    private async Task ProcessCommand(string userPhoneNumber, string messageText)
    {
        var command = messageText.Trim().ToLower();
        var parts = command.Split(' ');

        switch (command)
        {
            case "start": case "merhaba": case "başlat": 
                await SendStartMessageAsync(userPhoneNumber); 
                return;
            case "stop": case "bitir": case "hoşçakal": 
                await StopAllTrackingForUserAsync(userPhoneNumber); 
                return;
            case "listele": case "liste": case "sırala": 
                await ListTrackedOrdersAsync(userPhoneNumber); 
                return;
            case "durum": case "bilgi": 
                await ShowOrderStatusForAllTrackedAsync(userPhoneNumber); 
                return;
        }

        if (parts.Length == 2 && long.TryParse(parts[0], out _) && parts[1] == "iptal") { await CancelSpecificOrderTrackingAsync(userPhoneNumber, parts[0]); return; }
        if (parts.Length == 2 && long.TryParse(parts[0], out _) && parts[1] == "durum") { await ShowOrderStatusForSpecificOrderAsync(userPhoneNumber, parts[0]); return; }
        if (parts.Length == 1 && long.TryParse(parts[0], out _)) { await StartNewOrderTrackingAsync(userPhoneNumber, parts[0]); return; }

        await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, "Üzgünüm, bu komutu anlayamadım. 🧐\nKullanabileceğim komutları görmek için 'merhaba' yazabilirsiniz.");
    }
    
    // --- Tüm Yardımcı Metotlar (Emojili ve Profesyonel Mesajlarla) ---

    private Task SendStartMessageAsync(string userPhoneNumber)
    {
        var message = @"🤖 Sipariş Takip Asistanı'na hoş geldiniz.
Takip etmek için sipariş numaranızı gönderebilirsiniz.

📝 *Kullanılabilir Komutlar:*
➡️ *start, merhaba, başlat*: Bu yardım mesajını gösterir.
➡️ *listele, liste, sırala*: Aktif olarak takip edilen tüm siparişleri listeler.
➡️ *durum, bilgi*: Takipteki tüm siparişlerin beklemedeki ürünlerini gösterir.
➡️ *[sipariş no] durum*: Belirli bir siparişin beklemedeki ürünlerini gösterir.
➡️ *[sipariş no] iptal*: Belirli bir siparişin takibini sonlandırır.
➡️ *stop, hoşçakal, bitir*: Tüm sipariş takiplerini sonlandırır.";
        return _whatsAppClient.SendTextMessageAsync(userPhoneNumber, message);
    }

    private async Task StopAllTrackingForUserAsync(string userPhoneNumber)
    {
        if (_trackingState.TryRemoveAllOrders(userPhoneNumber))
        {
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, "Tüm aktif sipariş takipleri sonlandırıldı. 🛑");
        }
        else
        {
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, "Takip edilen aktif bir sipariş bulunmuyor. 🗒️");
        }
    }

    private async Task ListTrackedOrdersAsync(string userPhoneNumber)
    {
        var trackedOrders = _trackingState.GetTrackedOrders(userPhoneNumber);
        if (trackedOrders.Any())
        {
            var message = new StringBuilder("📋 Aktif Takipteki Siparişleriniz:\n");
            foreach (var orderNumber in trackedOrders)
            {
                message.AppendLine($"- {orderNumber}");
            }
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, message.ToString());
        }
        else
        {
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, "Takip edilen aktif bir sipariş bulunmuyor. 🗒️");
        }
    }

    private async Task ShowOrderStatusForAllTrackedAsync(string userPhoneNumber)
    {
        var trackedOrders = _trackingState.GetTrackedOrders(userPhoneNumber);
        if (!trackedOrders.Any())
        {
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, "Durumu kontrol edilecek aktif bir sipariş takibi bulunmamaktadır.");
            return;
        }

        var messageBuilder = new StringBuilder("⏳ Beklemedeki Ürünleriniz:\n");
        bool foundAnyPendingItem = false;

        foreach (var orderNumber in trackedOrders)
        {
            var orderData = await _esbasClient.QueryOrderAsync(orderNumber);
            if (orderData == null) continue;

            var pendingItems = orderData.Where(urun => !HazirDurumlar.Contains(urun.StatusText)).ToList();
            if (pendingItems.Any())
            {
                foundAnyPendingItem = true;
                messageBuilder.AppendLine($"\n*Sipariş: {orderNumber}*");
                foreach (var item in pendingItems) { messageBuilder.AppendLine($"- {item.ProductName}: _{item.StatusText}_"); }
            }
        }

        if (foundAnyPendingItem)
        {
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, messageBuilder.ToString());
        }
        else
        {
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, "Harika haber! Takip ettiğiniz siparişlerde beklemede olan ürün bulunmamaktadır. ✅");
        }
    }
    
    private async Task CancelSpecificOrderTrackingAsync(string userPhoneNumber, string orderNumber)
    {
        if (_trackingState.TryRemoveOrder(userPhoneNumber, orderNumber))
        {
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, $"Sipariş takibi iptal edildi: {orderNumber} ❌");
        }
        else
        {
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, $"{orderNumber} numaralı sipariş zaten takip edilmiyor. 🤔");
        }
    }
    
    private async Task ShowOrderStatusForSpecificOrderAsync(string userPhoneNumber, string orderNumber)
    {
        var orderData = await _esbasClient.QueryOrderAsync(orderNumber);
        if (orderData == null)
        {
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, $"{orderNumber} numaralı sipariş için durum bilgisi alınamadı.");
            return;
        }
        
        var pendingItems = orderData.Where(urun => !HazirDurumlar.Contains(urun.StatusText)).ToList();
        if (pendingItems.Any())
        {
            var messageBuilder = new StringBuilder($"*Sipariş {orderNumber} | Beklemedeki Ürünler:*\n");
            foreach (var item in pendingItems) { messageBuilder.AppendLine($"- {item.ProductName}: _{item.StatusText}_"); }
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, messageBuilder.ToString());
        }
        else
        {
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, $"Siparişinizde bekleyen ürün bulunmamaktadır: {orderNumber} ✅");
        }
    }
    
    private async Task StartNewOrderTrackingAsync(string userPhoneNumber, string orderNumber)
    {
    
        var cooldown = _trackingState.CheckCooldown(userPhoneNumber, 8);
        if (cooldown.IsActive)
        {

            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, $"Çok hızlı komut gönderdiniz. Lütfen {cooldown.RemainingSeconds:F1} saniye sonra tekrar deneyin.");
            return;
        }

        var (added, token) = _trackingState.TryAddOrder(userPhoneNumber, orderNumber);
        if (added)
        {
            _trackingState.UpdateCooldownTimestamp(userPhoneNumber);
            
            _ = Task.Run(() => TrackOrderAsync(userPhoneNumber, orderNumber, token));
        }
        else
        {
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, $"Bu sipariş zaten takip listemde: {orderNumber} 👍");
        }
    }
    
    private async Task TrackOrderAsync(string userPhoneNumber, string orderNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation("'{OrderNumber}' için takip başladı.", orderNumber);
        try
        {
            var initialData = await _esbasClient.QueryOrderAsync(orderNumber);
            if (cancellationToken.IsCancellationRequested) return;

            if (initialData == null || !initialData.Any())
            {
                await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, $"Sipariş bulunamadı: {orderNumber} 💬\nLütfen numarayı kontrol edip tekrar deneyin.");
                _trackingState.TryRemoveOrder(userPhoneNumber, orderNumber);
                return;
            }

            if (initialData.All(urun => HazirDurumlar.Contains(urun.StatusText)))
            {
                await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, $"Bu sipariş zaten tamamlanmış görünüyor: {orderNumber} ✅");
                _trackingState.TryRemoveOrder(userPhoneNumber, orderNumber);
                return;
            }

            var lastKnownStatus = initialData.ToDictionary(o => o.ProductName, o => o.StatusText);
            
            var initialMessage = new StringBuilder($"Siparişiniz takibe alındı: {orderNumber} 🎯\n\n*Mevcut Durum:*\n");
            foreach (var item in lastKnownStatus.OrderBy(kvp => kvp.Key))
            {
                initialMessage.AppendLine($"- {item.Key}: _{item.Value}_");
            }
            await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, initialMessage.ToString());

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);

                var currentData = await _esbasClient.QueryOrderAsync(orderNumber);
                if (currentData == null) continue;
                
                var currentStatus = currentData.ToDictionary(o => o.ProductName, o => o.StatusText);

                foreach (var (productName, previousStatus) in lastKnownStatus)
                {
                    var newStatus = currentStatus.GetValueOrDefault(productName, "Teslim Edildi"); 
                    if (previousStatus != newStatus && HazirDurumlar.Contains(newStatus))
                    {
                        var readyMessage = $"✅ ÜRÜN HAZIR ✅| Sipariş: {orderNumber}\n\n- *{productName}* adlı ürününüzü teslim alabilirsiniz.";
                        await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, readyMessage);
                    }
                }
                
                lastKnownStatus = currentStatus;

                if (!currentStatus.Any() || currentStatus.Values.All(s => HazirDurumlar.Contains(s)))
                {
                    var finalMessage = $"🎉 SİPARİŞİNİZ TAMAMLANDI 🎉| No: {orderNumber}\n\nTüm ürünleriniz teslim alınmaya hazır. Afiyet olsun!";
                    await _whatsAppClient.SendTextMessageAsync(userPhoneNumber, finalMessage);
                    break;
                }
            }
        }
        catch (TaskCanceledException) { _logger.LogInformation("'{OrderNumber}' takibi iptal edildi.", orderNumber); }
        catch (Exception ex) { _logger.LogError(ex, "'{OrderNumber}' takibi sırasında hata.", orderNumber); }
        finally
        {
            _trackingState.TryRemoveOrder(userPhoneNumber, orderNumber);
        }
    }
}