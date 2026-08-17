# PlayFab CloudScript — sunucu tarafı doğrulama kodları

Bu dosya PlayFab dashboard'una yapıştırılacak JavaScript kodlarını içerir. Client hile yapamasın diye para verme + araç satın alma sunucuda doğrulanır.

**Nereye yapıştırılır**: [developer.playfab.com](https://developer.playfab.com) → seçili Title → **Automation → Revisions** → yeni revision → yapıştır → **Deploy Revision**.

## 1. Para verme (addMoney)

Sadece belirli event'lerde para verilir (yarış bitti, drift bonus, günlük ödül, reklam izlendi). Rate limit ile aşırı çağrı engellenir.

```javascript
handlers.addMoney = function (args, context) {
    const source = String(args.source || "");
    const rawAmount = Math.floor(Number(args.amount) || 0);

    // Kaynak beyaz listesi + tavan
    const limits = {
        "race_win":       { max: 2000,  cooldown: 30 },
        "drift_reward":   { max: 5000,  cooldown: 60 },
        "daily_reward":   { max: 10000, cooldown: 60 * 60 * 20 },
        "ad_reward":      { max: 500,   cooldown: 30 }
    };
    if (!limits[source]) return { ok: false, reason: "invalid_source" };

    const amount = Math.min(rawAmount, limits[source].max);
    if (amount <= 0) return { ok: false, reason: "invalid_amount" };

    // Cooldown kontrolü
    const readReq = { PlayFabId: currentPlayerId, Keys: ["lastAward_" + source, "money"] };
    const readRes = server.GetUserInternalData(readReq);
    const now = Math.floor(Date.now() / 1000);
    const last = Number((readRes.Data["lastAward_" + source] || {}).Value || 0);
    if (now - last < limits[source].cooldown) return { ok: false, reason: "cooldown" };

    const currentMoney = Number((readRes.Data["money"] || {}).Value || 0);
    const newMoney = currentMoney + amount;

    server.UpdateUserInternalData({
        PlayFabId: currentPlayerId,
        Data: { money: String(newMoney), ["lastAward_" + source]: String(now) }
    });
    server.UpdateUserData({
        PlayFabId: currentPlayerId,
        Data: { money: String(newMoney) }
    });
    return { ok: true, money: newMoney, added: amount };
};
```

## 2. Araç satın alma (buyCar)

Katalog fiyatı server'da tanımlı. Client sadece `carId` gönderir; fiyat client'ta değil server'da bilinir → hile yapılamaz.

```javascript
const CAR_CATALOG = {
    "car.default":     { price:      0 },
    "car.sport":       { price:  15000 },
    "car.hatchback":   { price:  25000 },
    "car.suv":         { price:  45000 },
    "car.tuner":       { price:  85000 },
    "car.supercar":    { price: 250000 }
};

handlers.buyCar = function (args, context) {
    const carId = String(args.carId || "");
    const def = CAR_CATALOG[carId];
    if (!def) return { ok: false, reason: "unknown_car" };

    const invRes = server.GetUserInventory({ PlayFabId: currentPlayerId });
    for (let i = 0; i < invRes.Inventory.length; i++)
        if (invRes.Inventory[i].ItemId === carId)
            return { ok: false, reason: "already_owned" };

    const dataRes = server.GetUserInternalData({ PlayFabId: currentPlayerId, Keys: ["money"] });
    const money = Number((dataRes.Data["money"] || {}).Value || 0);
    if (money < def.price) return { ok: false, reason: "insufficient_funds" };

    const newMoney = money - def.price;
    server.UpdateUserInternalData({
        PlayFabId: currentPlayerId,
        Data: { money: String(newMoney) }
    });
    server.UpdateUserData({
        PlayFabId: currentPlayerId,
        Data: { money: String(newMoney) }
    });
    server.GrantItemsToUser({
        PlayFabId: currentPlayerId,
        ItemIds: [carId]
    });
    return { ok: true, money: newMoney };
};
```

## 3. Yarış sonucu kaydetme (submitRaceResult)

Best lap istatistiği ile birlikte para ödülü tek çağrıda. Ödül CloudScript içinde hesaplanır.

```javascript
handlers.submitRaceResult = function (args, context) {
    const lapMs = Math.floor(Number(args.lapMs) || 0);
    const rank  = Math.floor(Number(args.rank)  || 0);
    if (lapMs <= 0 || lapMs > 15 * 60 * 1000) return { ok: false, reason: "invalid_lap" };
    if (rank < 1 || rank > 16) return { ok: false, reason: "invalid_rank" };

    // Rank tabanlı ödül: 1. 2000, 2. 1200, 3. 800, sonrası 300
    const reward = rank === 1 ? 2000 : rank === 2 ? 1200 : rank === 3 ? 800 : 300;

    server.UpdatePlayerStatistics({
        PlayFabId: currentPlayerId,
        Statistics: [{ StatisticName: "raceBestLap", Value: -lapMs }]
    });

    // addMoney handler'ını yeniden kullan (rate limit aynı çalışsın)
    return handlers.addMoney({ source: "race_win", amount: reward }, context);
};
```

## Kurulum kontrol listesi

1. PlayFab Title oluştur → **Title ID**'yi `PlayFabAuth.titleId` alanına gir.
2. Yukarıdaki üç handler'ı tek revision olarak yükle.
3. Test: **Automation → Revisions → Test** → `addMoney({source: "race_win", amount: 500})` çağır.
4. Client'ta `PlayFabInventoryBridge.useServerAuthoritativePurchase = true` bırak.
5. Statistics tanımla: **Game Manager → Leaderboards** → `raceBestLap` (aggregation: Maximum — negatif değer verdiğimiz için "en büyük = en küçük süre"), `driftScore` (Maximum).
6. Catalog: **Economy → Catalog** → araç ID'lerini (`car.default`, `car.sport`…) item olarak tanımla.

## Sonraki geliştirmeler

- CloudScript yerine **Azure Functions** (PlayFab Cloud Script legacy oluyor — 2025+ Azure Functions öneriliyor). Kod aynı, host farklı.
- Anti-cheat için `PlayerStatistics` üzerinde anomaly detection (StatValue çok hızlı yükseliyorsa flag).
- IAP receipt validation: Apple receipt'i CloudScript'te `ValidateIOSReceipt` ile doğrula, sonra `addMoney`.
