namespace DotNetWhatsApp;
using System.Text.Json.Serialization;

// --- Esbas API Modelleri ---
public record AuthResponse(string Token);
public record AuthPayload(string KullaniciAdi, string Sifre, int Id, string Reklam, string Soyad, string Jeton);
public record OrderStatus(
    [property: JsonPropertyName("productDefinitionName")] string ProductName,
    [property: JsonPropertyName("foodCourtProductionStateText")] string StatusText
);

public record WhatsAppWebhookPayload
{
    [JsonPropertyName("entry")]
    public List<Entry>? Entry { get; set; }
}

public record Entry
{
    [JsonPropertyName("changes")]
    public List<Change>? Changes { get; set; }
}

public record Change
{
    [JsonPropertyName("value")]
    public Value? Value { get; set; }
}

public record Value
{
    [JsonPropertyName("messages")]
    public List<Message>? Messages { get; set; }
}

public record Message
{
    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("text")]
    public TextBody? Text { get; set; }
}

public record TextBody
{
    [JsonPropertyName("body")]
    public string? Body { get; set; }
}