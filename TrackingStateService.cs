namespace DotNetWhatsApp;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

public class TrackingStateService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CancellationTokenSource>> _userTrackingData = new();
    
    // YENİ EKLENEN: Kullanıcıların son komut zamanını tutar.
    private readonly ConcurrentDictionary<string, DateTime> _userCooldowns = new();

    // YENİ METOT: Kullanıcının bekleme süresinde olup olmadığını kontrol eder.
    public (bool IsActive, double RemainingSeconds) CheckCooldown(string userPhoneNumber, int cooldownSeconds)
    {
        if (_userCooldowns.TryGetValue(userPhoneNumber, out var lastRequestTime))
        {
            var timePassed = DateTime.UtcNow - lastRequestTime;
            if (timePassed < TimeSpan.FromSeconds(cooldownSeconds))
            {
                var remainingTime = (TimeSpan.FromSeconds(cooldownSeconds) - timePassed).TotalSeconds;
                return (true, remainingTime); // Bekleme süresi aktif.
            }
        }
        return (false, 0); // Bekleme süresi bitti veya hiç yok.
    }

    // YENİ METOT: Kullanıcının son komut zamanını günceller.
    public void UpdateCooldownTimestamp(string userPhoneNumber)
    {
        _userCooldowns[userPhoneNumber] = DateTime.UtcNow;
    }

    public (bool, CancellationToken) TryAddOrder(string userPhoneNumber, string orderNumber)
    {
        var userOrders = _userTrackingData.GetOrAdd(userPhoneNumber, _ => new ConcurrentDictionary<string, CancellationTokenSource>());
        var cts = new CancellationTokenSource();
        if (userOrders.TryAdd(orderNumber, cts))
        {
            return (true, cts.Token);
        }
        cts.Dispose();
        return (false, CancellationToken.None);
    }

    public bool TryRemoveOrder(string userPhoneNumber, string orderNumber)
    {
        if (_userTrackingData.TryGetValue(userPhoneNumber, out var userOrders) && userOrders.TryRemove(orderNumber, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            return true;
        }
        return false;
    }

    public bool TryRemoveAllOrders(string userPhoneNumber)
    {
        if (_userTrackingData.TryRemove(userPhoneNumber, out var userOrders))
        {
            foreach (var cts in userOrders.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            return true;
        }
        return false;
    }

    public List<string> GetTrackedOrders(string userPhoneNumber)
    {
        if (_userTrackingData.TryGetValue(userPhoneNumber, out var userOrders))
        {
            return userOrders.Keys.OrderBy(k => k).ToList();
        }
        return new List<string>();
    }
}