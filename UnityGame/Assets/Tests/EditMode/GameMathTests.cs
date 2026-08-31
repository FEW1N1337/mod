using NUnit.Framework;
using DreamCar.Util;

namespace DreamCar.Tests
{
    // GameMath saf mantık olduğu için Unity Editor'ü açmadan CI'da da koşar.
    // CI: .github/workflows/unity-ios-build.yml → compile-check job'u bunları çalıştırır.
    public class GameMathTests
    {
        // ---------------------------------------------------------- Biçimleme

        [Test]
        public void FormatLapTime_UnderOneMinute_ShowsSecondsOnly()
        {
            Assert.AreEqual("45.20s", GameMath.FormatLapTime(45200));
        }

        [Test]
        public void FormatLapTime_OverOneMinute_ShowsMinutesAndSeconds()
        {
            Assert.AreEqual("1:23.45", GameMath.FormatLapTime(83450));
        }

        [Test]
        public void FormatLapTime_NegativeValue_TreatedAsUnset()
        {
            // Leaderboard'da süreler negatif saklanıyor (küçük süre = yüksek skor),
            // ama biçimleyici mutlak değeri kullanmalı.
            Assert.AreEqual("-", GameMath.FormatLapTime(0));
            Assert.AreEqual("1:23.45", GameMath.FormatLapTime(-83450));
        }

        [Test]
        public void FormatDistance_SwitchesToKilometresAtThreshold()
        {
            Assert.AreEqual("999 m", GameMath.FormatDistance(999f));
            StringAssert.Contains("km", GameMath.FormatDistance(1500f));
        }

        [Test]
        public void FormatDuration_HandlesHoursMinutesSeconds()
        {
            Assert.AreEqual("45sn", GameMath.FormatDuration(45f));
            Assert.AreEqual("2dk 30sn", GameMath.FormatDuration(150f));
            Assert.AreEqual("1s 1dk", GameMath.FormatDuration(3660f));
        }

        // ---------------------------------------------------------- Ekonomi

        [Test]
        public void StreakMultiplier_TiersAtThreeAndSeven()
        {
            Assert.AreEqual(1f, GameMath.StreakMultiplier(1));
            Assert.AreEqual(1f, GameMath.StreakMultiplier(2));
            Assert.AreEqual(2f, GameMath.StreakMultiplier(3));
            Assert.AreEqual(2f, GameMath.StreakMultiplier(6));
            Assert.AreEqual(3f, GameMath.StreakMultiplier(7));
            Assert.AreEqual(3f, GameMath.StreakMultiplier(30));
        }

        [Test]
        public void NextStreak_ContinuesOnConsecutiveDay()
        {
            Assert.AreEqual(4, GameMath.NextStreak(3, 1));
        }

        [Test]
        public void NextStreak_UnchangedWhenSameDay()
        {
            Assert.AreEqual(3, GameMath.NextStreak(3, 0));
        }

        [Test]
        public void NextStreak_ResetsAfterGap()
        {
            Assert.AreEqual(1, GameMath.NextStreak(9, 2));
            Assert.AreEqual(1, GameMath.NextStreak(9, 30));
        }

        [Test]
        public void RepairPrice_ZeroWhenUndamaged()
        {
            Assert.AreEqual(0, GameMath.RepairPrice(100f, 100f, 10f));
        }

        [Test]
        public void RepairPrice_ScalesWithMissingHealth()
        {
            // %50 hasar × 100 birim × 10₺ = 500₺
            Assert.AreEqual(500, GameMath.RepairPrice(50f, 100f, 10f));
        }

        [Test]
        public void RefuelPrice_ZeroWhenTankFull()
        {
            Assert.AreEqual(0, GameMath.RefuelPrice(60f, 60f, 25f));
        }

        [Test]
        public void RefuelPrice_ChargesForMissingLitres()
        {
            // 20 litre eksik × 25₺ = 500₺
            Assert.AreEqual(500, GameMath.RefuelPrice(40f, 60f, 25f));
        }

        // ---------------------------------------------------------- Sürüş

        [Test]
        public void GearForSpeed_PicksFirstLimitAbleToContainSpeed()
        {
            float[] limits = { 30f, 60f, 100f, 140f, 180f };
            Assert.AreEqual(1, GameMath.GearForSpeed(10f, limits));
            Assert.AreEqual(2, GameMath.GearForSpeed(45f, limits));
            Assert.AreEqual(5, GameMath.GearForSpeed(150f, limits));
        }

        [Test]
        public void GearForSpeed_ClampsToTopGearAboveAllLimits()
        {
            float[] limits = { 30f, 60f };
            Assert.AreEqual(2, GameMath.GearForSpeed(500f, limits));
        }

        [Test]
        public void GearForSpeed_HandlesEmptyLimits()
        {
            Assert.AreEqual(1, GameMath.GearForSpeed(80f, new float[0]));
            Assert.AreEqual(1, GameMath.GearForSpeed(80f, null));
        }

        [Test]
        public void SpeedometerAngle_InterpolatesBetweenEndpoints()
        {
            Assert.AreEqual(220f, GameMath.SpeedometerAngle(0f, 200f, 220f, -40f), 0.001f);
            Assert.AreEqual(-40f, GameMath.SpeedometerAngle(200f, 200f, 220f, -40f), 0.001f);
            Assert.AreEqual(90f, GameMath.SpeedometerAngle(100f, 200f, 220f, -40f), 0.001f);
        }

        [Test]
        public void SpeedometerAngle_ClampsAboveTopSpeed()
        {
            Assert.AreEqual(-40f, GameMath.SpeedometerAngle(999f, 200f, 220f, -40f), 0.001f);
        }

        // ---------------------------------------------------------- Hile tespiti

        [Test]
        public void SpeedBetweenSamples_ConvertsToKilometresPerHour()
        {
            // 10 m / 0.5 sn = 20 m/s = 72 km/h
            Assert.AreEqual(72f, GameMath.SpeedBetweenSamples(10f, 0.5f), 0.01f);
        }

        [Test]
        public void SpeedBetweenSamples_GuardsAgainstZeroDelta()
        {
            Assert.AreEqual(0f, GameMath.SpeedBetweenSamples(10f, 0f));
        }

        [Test]
        public void IsPlausibleMovement_RejectsTeleportJump()
        {
            Assert.IsFalse(GameMath.IsPlausibleMovement(200f, 0.5f, 400f, 120f));
        }

        [Test]
        public void IsPlausibleMovement_RejectsImpossibleSpeed()
        {
            // 100 m / 0.5 sn = 720 km/h → 400 sınırının üstünde
            Assert.IsFalse(GameMath.IsPlausibleMovement(100f, 0.5f, 400f, 120f));
        }

        [Test]
        public void IsPlausibleMovement_AcceptsNormalDriving()
        {
            // 25 m / 0.5 sn = 180 km/h — hızlı ama meşru
            Assert.IsTrue(GameMath.IsPlausibleMovement(25f, 0.5f, 400f, 120f));
        }

        [Test]
        public void InterestTier_BucketsByDistance()
        {
            Assert.AreEqual(0, GameMath.InterestTier(50f, 80f, 200f));
            Assert.AreEqual(1, GameMath.InterestTier(150f, 80f, 200f));
            Assert.AreEqual(2, GameMath.InterestTier(300f, 80f, 200f));
        }

        // ---------------------------------------------------------- Sohbet

        [Test]
        public void RefillTokens_NeverExceedsCapacity()
        {
            Assert.AreEqual(4f, GameMath.RefillTokens(3.9f, 4f, 0.5f, 10f));
        }

        [Test]
        public void RefillTokens_AccumulatesOverTime()
        {
            Assert.AreEqual(2f, GameMath.RefillTokens(1f, 4f, 0.5f, 2f), 0.001f);
        }

        [Test]
        public void RefillTokens_IgnoresNegativeDelta()
        {
            Assert.AreEqual(1f, GameMath.RefillTokens(1f, 4f, 0.5f, -5f), 0.001f);
        }

        [Test]
        public void NextMuteDuration_DoublesUntilCap()
        {
            Assert.AreEqual(20f, GameMath.NextMuteDuration(10f, 2f, 300f));
            Assert.AreEqual(300f, GameMath.NextMuteDuration(200f, 2f, 300f));
        }

        [Test]
        public void MaskWord_PreservesLength()
        {
            Assert.AreEqual("****", GameMath.MaskWord("test"));
            Assert.AreEqual("", GameMath.MaskWord(""));
            Assert.IsNull(GameMath.MaskWord(null));
        }

        // ---------------------------------------------------------- Cihaz kademesi

        [Test]
        public void QualityTier_LowEndDeviceGetsLowTier()
        {
            // 2 GB RAM, 512 MB GPU, 4 çekirdek
            Assert.AreEqual(0, GameMath.QualityTier(2048, 512, 4, 1_000_000));
        }

        [Test]
        public void QualityTier_HighEndDeviceGetsHighTier()
        {
            // 8 GB RAM, 4 GB GPU, 8 çekirdek
            Assert.AreEqual(2, GameMath.QualityTier(8192, 4096, 8, 2_000_000));
        }

        [Test]
        public void QualityTier_HighResolutionCanDropATier()
        {
            // Bu donanım tam sınırda 4 puan alır (RAM +1, GPU +2, çekirdek +1) → High.
            // Yüksek piksel cezası 3'e düşürür → Mid. Ceza kademe değiştirebilmeli.
            int normal = GameMath.QualityTier(4096, 2048, 6, 1_000_000);
            int highRes = GameMath.QualityTier(4096, 2048, 6, 3_000_000);

            Assert.AreEqual(2, normal, "sınırdaki cihaz High olmalı");
            Assert.AreEqual(1, highRes, "yüksek çözünürlük bir kademe düşürmeli");
        }

        // ---------------------------------------------------------- Geometri

        [Test]
        public void SuperellipsePoint_AtZeroAngleReachesHalfWidth()
        {
            GameMath.SuperellipsePoint(0f, 2f, 1f, 4f, out float x, out float y);
            Assert.AreEqual(2f, x, 0.001f);
            Assert.AreEqual(0f, y, 0.001f);
        }

        [Test]
        public void SuperellipsePoint_HighExponentApproachesRectangle()
        {
            // 45°'de: elips ~0.707×yarıçap, dikdörtgen ~1.0×yarıçap
            GameMath.SuperellipsePoint(0.785398f, 1f, 1f, 1f, out float ex, out _);
            GameMath.SuperellipsePoint(0.785398f, 1f, 1f, 20f, out float rx, out _);
            Assert.Less(ex, rx);
            Assert.Greater(rx, 0.9f);
        }

        // ---------------------------------------------------------- Rich text

        [Test]
        public void ClampRichTextSize_LeavesSafeTagsAlone()
        {
            const string input = "merhaba <size=120%>dünya</size>";
            Assert.AreEqual(input, GameMath.ClampRichTextSize(input));
        }

        [Test]
        public void ClampRichTextSize_ClampsHostileTag()
        {
            // Ekranı kaplayan spam saldırısı
            string result = GameMath.ClampRichTextSize("<size=9999%>SPAM");
            StringAssert.Contains("<size=400%>", result);
            StringAssert.DoesNotContain("9999", result);
        }

        [Test]
        public void ClampRichTextSize_HandlesUnclosedTag()
        {
            Assert.DoesNotThrow(() => GameMath.ClampRichTextSize("<size=9999"));
        }

        [Test]
        public void ClampRichTextSize_HandlesNullAndEmpty()
        {
            Assert.IsNull(GameMath.ClampRichTextSize(null));
            Assert.AreEqual("", GameMath.ClampRichTextSize(""));
        }
    }
}
