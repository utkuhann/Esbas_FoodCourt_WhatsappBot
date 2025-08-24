# ESBAŞ Sipariş Takip WhatsApp Botu

Bu proje, ESBAŞ yemek katı siparişlerinin durumunu takip etmek için geliştirilmiş bir .NET tabanlı WhatsApp botudur. Proje, Meta'nın WhatsApp Business API'si ile entegre olmak için bir ASP.NET Core Web API altyapısı kullanır ve webhook aracılığıyla gelen mesajları işler.

Bot, belirli aralıklarla ESBAŞ API'sini sorgulayarak siparişteki ürünlerin durumu değiştiğinde kullanıcıya anlık bildirim gönderir.

## ✨ Özellikler

- **Webhook Entegrasyonu:** Meta'dan gelen WhatsApp mesajlarını anlık olarak işler.
- **Sipariş Takibi:** Kullanıcılar sipariş numaralarını göndererek takibi başlatabilir.
- **Anlık Durum Bildirimleri:** Siparişteki bir ürün "Teslime Hazır" durumuna geçtiğinde WhatsApp üzerinden anlık bildirim gönderir.
- **Komut Desteği:** `start`, `listele`, `durum`, `stop` gibi komutlarla interaktif kullanım sunar.
- **Durum Yönetimi:** Her kullanıcı için aktif sipariş takibini ayrı ayrı yönetir.

## 🚀 Kurulum ve Çalıştırma

Projeyi yerel makinenizde çalıştırmak ve Meta Webhook'u ile bağlamak için aşağıdaki adımları izleyin.

### Gereksinimler
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (veya üzeri)
- [ngrok](https://ngrok.com/download) - Yerel sunucunuzu internete açmak için.
- [Meta for Developers](https://developers.facebook.com/) hesabı ve yapılandırılmış bir WhatsApp Business App.

### Adım Adım Kurulum

1.  **Projeyi Klonlayın:**
    ```bash
    git clone [https://github.com/KULLANICI_ADINIZ/PROJE_ADINIZ.git](https://github.com/KULLANICI_ADINIZ/PROJE_ADINIZ.git)
    cd PROJE_ADINIZ
    ```

2.  **Gizli Bilgileri Yapılandırın (User Secrets):**
    Proje, hassas bilgileri (API Token'ları, şifreler) güvende tutmak için .NET User Secrets kullanır.
    
    a. Terminalde proje ana dizinindeyken `user-secrets`'i başlatın:
    ```bash
    dotnet user-secrets init
    ```
    b. `secrets.json` dosyasını yapılandırın (Visual Studio'da projeye sağ tık -> "Manage User Secrets" ile açılır):
    ```json
    {
      "WhatsAppConfiguration": {
        "AccessToken": "SIZIN_META_WHATSAPP_ACCESS_TOKENINIZ",
        "PhoneNumberId": "UYGULAMANIZIN_TELEFON_NUMARASI_IDSI",
        "WebhookVerifyToken": "BURAYA_RASTGELE_GUCLU_BIR_VERIFY_TOKEN_YAZIN"
      },
      "EsbasApi": {
        "Username": "esbasdev",
        "Password": "esbasESBAS13"
      }
    }
    ```

3.  **Uygulamayı Çalıştırın:**
    Projeyi `dotnet run` komutuyla başlatın. [cite: 2] Uygulama genellikle `https://localhost:7XXX` gibi bir adres üzerinden çalışmaya başlayacaktır. Terminaldeki çıktıdan bu adresi not alın.
    ```bash
    dotnet run
    ```

4.  **ngrok ile Tünel Oluşturun:**
    Yeni bir terminal açın ve `dotnet run` çıktısındaki HTTPS adresini kullanarak ngrok'u başlatın.
    ```bash
    ngrok http https://localhost:7123 --host-header="localhost:7123"
    ```
    ngrok size `https://<rastgele_kod>.ngrok-free.app` formatında bir "Forwarding" adresi verecektir. Bu adresi kopyalayın.

5.  **Meta Webhook'u Yapılandırın:**
    a. Meta for Developers'da WhatsApp uygulamanızın ayarlarına gidin.
    b. "Webhook" yapılandırma bölümünü bulun.
    c. "Callback URL" alanına, ngrok'un verdiği HTTPS adresini ve projedeki webhook yolunu birleştirerek yapıştırın. [cite: 3] Örnek: `https://<rastgele_kod>.ngrok-free.app/api/whatsapp/webhook`
    d. "Verify Token" alanına, `secrets.json` dosyasına yazdığınız `WebhookVerifyToken` değerini girin ve "Verify and save" butonuna tıklayın.

Artık WhatsApp üzerinden botunuza mesaj göndererek test edebilirsiniz.

## 🤖 Proje Yapısı

- **`Program.cs`**: ASP.NET Core uygulamasını yapılandırır ve başlatır.
- **`Controllers/WhatsAppWebhookController.cs`**: Meta'dan gelen webhook isteklerini karşılayan ve komutları işleyen ana kontrolcüdür.
- **`WhatsAppApiClient.cs`**: WhatsApp'a geri mesaj göndermek için Meta Graph API ile iletişim kurar.
- **`EsbasApiClient.cs`**: ESBAŞ API'sinden sipariş durumlarını sorgular.
- **`TrackingStateService.cs`**: Kullanıcıların aktif sipariş takiplerini yönetir.
- **`Model.cs`**: Gelen webhook verileri ve API yanıtları için C# kayıtlarını (records) tanımlar.