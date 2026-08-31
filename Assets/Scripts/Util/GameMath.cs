using System;
using System.Text;

namespace DreamCar.Util
{
    // Unity paketlerine bağlı olmayan saf mantık. Ayrı assembly'de olduğu için
    // EditMode testlerinden doğrudan çağrılabilir — PUN/PlayFab kurulu olmasa da.
    public static class GameMath
    {
        // --- Süre / mesafe biçimleme ---

        // Milisaniyeyi tur süresi formatına çevirir: 83450 → "1:23.45"
        // Liderlik tablosu süreleri sunucuda NEGATİF saklanır (PlayFab istatistikleri
        // büyük değeri iyi sayar, süre ise küçükken iyidir). O yüzden burada işaret
        // yok sayılır; yalnızca sıfır "kayıt yok" demektir.
        public static string FormatLapTime(int milliseconds)
        {
            if (milliseconds == 0) return "-";
            float seconds = Math.Abs(milliseconds) / 1000f;
            int minutes = (int)(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return minutes > 0
                ? $"{minutes}:{rest:00.00}"
                : $"{rest:0.00}s";
        }

        public static string FormatDistance(float meters)
        {
            if (meters < 1000f) return $"{(int)Math.Round(meters)} m";
            return $"{meters / 1000f:N1} km";
        }

        public static string FormatDuration(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int hours = (int)(seconds / 3600f);
            int minutes = (int)((seconds - hours * 3600f) / 60f);
            if (hours > 0) return $"{hours}s {minutes}dk";
            int secs = (int)(seconds - minutes * 60f);
            return minutes > 0 ? $"{minutes}dk {secs}sn" : $"{secs}sn";
        }

        // --- Ekonomi ---

        // Ardışık giriş gününe göre ödül çarpanı.
        public static float StreakMultiplier(int streak)
        {
            if (streak >= 7) return 3f;
            if (streak >= 3) return 2f;
            return 1f;
        }

        // İki tarih arasındaki gün farkına göre yeni streak değeri.
        // 1 gün = devam, 0 gün = değişmez, diğer = sıfırlanır.
        public static int NextStreak(int currentStreak, int daysSinceLastLogin)
        {
            if (daysSinceLastLogin == 0) return currentStreak;
            if (daysSinceLastLogin == 1) return currentStreak + 1;
            return 1;
        }

        // Hasar oranına göre tamir ücreti.
        public static long RepairPrice(float health, float maxHealth, float unitPrice)
        {
            if (maxHealth <= 0f) return 0;
            float missing = 1f - Clamp01(health / maxHealth);
            return (long)Math.Ceiling(missing * 100f * unitPrice);
        }

        // Yakıt doldurma ücreti.
        public static long RefuelPrice(float current, float capacity, float pricePerLiter)
        {
            float missing = capacity - current;
            if (missing <= 0f) return 0;
            return (long)Math.Ceiling(missing * pricePerLiter);
        }

        // --- Sürüş ---

        // Otomatik şanzıman: hıza göre vites.
        public static int GearForSpeed(float speedKmh, float[] gearSpeedLimits)
        {
            if (gearSpeedLimits == null || gearSpeedLimits.Length == 0) return 1;
            for (int i = 0; i < gearSpeedLimits.Length; i++)
                if (speedKmh < gearSpeedLimits[i]) return i + 1;
            return gearSpeedLimits.Length;
        }

        // Kilometre saati iğnesinin açısı.
        public static float SpeedometerAngle(float speedKmh, float topSpeedKmh, float minAngle, float maxAngle)
        {
            if (topSpeedKmh <= 0f) return minAngle;
            float t = Clamp01(speedKmh / topSpeedKmh);
            return minAngle + (maxAngle - minAngle) * t;
        }

        // --- Ağ / hile tespiti ---

        // İki örnek arası hız (km/h). Teleport ve hız hilesi tespitinde kullanılır.
        public static float SpeedBetweenSamples(float distanceMeters, float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return 0f;
            return (distanceMeters / deltaSeconds) * 3.6f;
        }

        public static bool IsPlausibleMovement(float distanceMeters, float deltaSeconds,
                                               float maxSpeedKmh, float maxJumpMeters)
        {
            if (distanceMeters > maxJumpMeters) return false;
            return SpeedBetweenSamples(distanceMeters, deltaSeconds) <= maxSpeedKmh;
        }

        // Mesafeye göre ağ gönderim kademesi: 0 = yakın, 1 = orta, 2 = uzak.
        public static int InterestTier(float distanceMeters, float nearDistance, float midDistance)
        {
            if (distanceMeters <= nearDistance) return 0;
            if (distanceMeters <= midDistance) return 1;
            return 2;
        }

        // --- Sohbet ---

        // Token bucket: geçen süreye göre yenilenen token miktarı.
        public static float RefillTokens(float currentTokens, float capacity,
                                         float refillPerSecond, float deltaSeconds)
        {
            float refilled = currentTokens + refillPerSecond * Math.Max(0f, deltaSeconds);
            return refilled > capacity ? capacity : refilled;
        }

        // Kademeli susturma süresi: her ihlalde katlanır, tavanla sınırlı.
        public static float NextMuteDuration(float currentPenalty, float multiplier, float maxSeconds)
        {
            float next = currentPenalty * multiplier;
            return next > maxSeconds ? maxSeconds : next;
        }

        // Kelimeyi yıldızla maskele.
        public static string MaskWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            return new string('*', word.Length);
        }

        // --- Cihaz uyarlama ---

        // Donanım özelliklerinden kalite kademesi: 0 = Low, 1 = Mid, 2 = High.
        public static int QualityTier(int systemMemoryMb, int graphicsMemoryMb,
                                      int processorCount, int screenPixels)
        {
            int score = 0;

            if (systemMemoryMb >= 5500) score += 2;
            else if (systemMemoryMb >= 3500) score += 1;

            if (graphicsMemoryMb >= 2000) score += 2;
            else if (graphicsMemoryMb >= 1000) score += 1;

            if (processorCount >= 6) score += 1;
            if (screenPixels > 2_500_000) score -= 1;

            if (score >= 4) return 2;
            if (score >= 2) return 1;
            return 0;
        }

        // --- Geometri (prosedürel üretim) ---

        // Superellipse konturu üzerinde bir nokta. exponent büyüdükçe dikdörtgene yaklaşır.
        public static void SuperellipsePoint(float angleRadians, float halfWidth, float halfHeight,
                                             float exponent, out float x, out float y)
        {
            float e = 2f / Math.Max(0.5f, exponent);
            float c = (float)Math.Cos(angleRadians);
            float s = (float)Math.Sin(angleRadians);
            x = Math.Sign(c) * (float)Math.Pow(Math.Abs(c), e) * halfWidth;
            y = Math.Sign(s) * (float)Math.Pow(Math.Abs(s), e) * halfHeight;
        }

        // --- Metin ---

        // TMP rich text içindeki zararlı büyük <size> etiketlerini kırpar.
        public static string ClampRichTextSize(string input, int maxPercent = 400)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new StringBuilder(input.Length);
            int i = 0;
            while (i < input.Length)
            {
                int tagStart = input.IndexOf("<size=", i, StringComparison.OrdinalIgnoreCase);
                if (tagStart < 0) { sb.Append(input, i, input.Length - i); break; }

                sb.Append(input, i, tagStart - i);

                int tagEnd = input.IndexOf('>', tagStart);
                if (tagEnd < 0) { sb.Append(input, tagStart, input.Length - tagStart); break; }

                string body = input.Substring(tagStart + 6, tagEnd - tagStart - 6).Trim().TrimEnd('%');
                if (int.TryParse(body, out int percent) && percent > maxPercent)
                    sb.Append($"<size={maxPercent}%>");
                else
                    sb.Append(input, tagStart, tagEnd - tagStart + 1);

                i = tagEnd + 1;
            }
            return sb.ToString();
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
