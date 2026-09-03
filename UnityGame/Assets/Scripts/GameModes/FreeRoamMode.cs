using System.Collections;
using Photon.Pun;
using UnityEngine;
using DreamCar.Core;
using DreamCar.Economy;
using DreamCar.Race;
using DreamCar.UI;

namespace DreamCar.GameModes
{
    // Serbest sürüş — yarış yok, tur yok, sadece sür.
    //
    // BURASI OYUNUN VARSAYILAN MODU: hızlı oyun ve lobiden kurulan odalar
    // "mode" özelliğini hiç yazmıyor, GameModeManager de yazılmamış değeri 0
    // (= Free) okuyor. Yani yeni bir oyuncunun gördüğü ilk mod bu.
    //
    // Buna rağmen serbest sürüşün HİÇBİR gelir kaynağı yoktu:
    // PlayerMoney.Add çağıran her yer (RaceManager, DriftMode, DailyReward,
    // ReferralSystem, başarımlar, reklam, PlayFab senkronu) ya başka bir modda
    // ya da oyun dışındaydı. Oyuncu 5.000 ₺ ile başlıyor, ikinci araç 25.000 ₺:
    // yalnızca sürerek ARADAKİ FARKI ASLA KAPATAMIYORDU. Ekonomi döngüsü
    // varsayılan modda kapalıydı.
    //
    // Ödül iki kaynaktan geliyor:
    //   • Kilometre — StatsTracker'ın zaten PlayerStats'e akıttığı mesafeden.
    //     Kendi mesafemizi saymıyoruz: StatsTracker ışınlanma sıçramalarını
    //     (>50 m) eliyor ve yalnızca sahibi olunan araçta çalışıyor, iki kez
    //     yazmanın anlamı yok.
    //   • Drift — yerel aracın DriftScore bankasındaki artıştan, DriftMode ile
    //     aynı kurdan. Serbest sürüşte drift yapmak da kazandırmalı.
    public class FreeRoamMode : GameModeBase
    {
        public override GameModeType Type => GameModeType.Free;

        [Tooltip("Kilometre başına kazanç. Yarış galibiyeti 1.000 ₺, günlük ödül 500 ₺.")]
        public long moneyPerKilometre = 120;

        [Tooltip("1.000 drift puanı başına kazanç — DriftMode ile aynı kur.")]
        public long rewardPerThousandDriftPoints = 5;

        [Tooltip("Bu kadar birikince tek bir bildirim gösterilir; her ödemede değil.")]
        public long toastEvery = 250;

        [Tooltip("Ödeme aralığı. StatsTracker de 5 saniyede bir boşaltıyor.")]
        public float tickSeconds = 5f;

        float _distanceBaseline = -1f;
        DriftScore _drift;
        int _driftBaseline;
        double _pending;          // ödenmemiş kesirli kazanç
        long _sinceLastToast;
        long _sessionEarned;
        bool _running;

        public long SessionEarned => _sessionEarned;

        public override void OnModeStart()
        {
            _running = true;
            StartCoroutine(PayoutLoop());
        }

        public override void OnModeEnd()
        {
            _running = false;
            if (_sinceLastToast > 0)
            {
                ToastNotification.Show($"Sürüş kazancı: +{_sinceLastToast:N0} ₺");
                _sinceLastToast = 0;
            }
        }

        IEnumerator PayoutLoop()
        {
            var wait = new WaitForSeconds(Mathf.Max(1f, tickSeconds));
            while (_running)
            {
                yield return wait;
                Tick();
            }
        }

        void Tick()
        {
            if (PlayerMoney.Instance == null) return;

            long perKm = Backend.RemoteConfig.GetLong("freeroam.moneyPerKm", moneyPerKilometre);
            long perDrift = Backend.RemoteConfig.GetLong("freeroam.driftPerThousand", rewardPerThousandDriftPoints);

            AccrueDistance(perKm);
            AccrueDrift(perDrift);

            long payout = (long)_pending;
            if (payout <= 0) return;
            _pending -= payout;

            PlayerMoney.Instance.Add(payout);
            _sessionEarned += payout;
            _sinceLastToast += payout;

            if (_sinceLastToast < toastEvery) return;
            ToastNotification.Show($"Sürüş kazancı: +{_sinceLastToast:N0} ₺");
            _sinceLastToast = 0;
        }

        void AccrueDistance(long perKm)
        {
            if (perKm <= 0) return;
            var stats = PlayerStats.Instance;
            if (stats == null) return;

            float total = stats.TotalDistanceMeters;

            // İlk tur yalnızca referans alır: mod başlamadan ÖNCE birikmiş
            // ömür boyu mesafe için ödeme yapılmaz.
            if (_distanceBaseline < 0f) { _distanceBaseline = total; return; }

            float metres = total - _distanceBaseline;
            if (metres <= 0f) return;           // bulut senkronu geri yazarsa negatif olabilir

            _distanceBaseline = total;
            _pending += metres / 1000.0 * perKm;
        }

        void AccrueDrift(long perThousand)
        {
            if (perThousand <= 0) return;

            // Araç odaya girilince doğuyor ve ölünce yeniden doğabiliyor:
            // yeni bileşenin bankası 0'dan başlar, eski taban çizgisiyle
            // karşılaştırmak ödemeyi sessizce durdururdu.
            if (!_drift)
            {
                _drift = FindLocalDrift();
                if (!_drift) return;
                _driftBaseline = _drift.Bank;
                return;
            }

            int bank = _drift.Bank;
            if (bank <= _driftBaseline) return;

            _pending += (bank - _driftBaseline) / 1000.0 * perThousand;
            _driftBaseline = bank;
        }

        static DriftScore FindLocalDrift()
        {
            var car = Network.RoomManager.LocalCar;
            if (car) return car.GetComponent<DriftScore>();

            // RoomManager yoksa (tek oyunculu Editor testi) taramaya düş.
            foreach (var d in FindObjectsByType<DriftScore>(FindObjectsSortMode.None))
            {
                var pv = d.GetComponent<PhotonView>();
                if (pv == null || pv.IsMine) return d;
            }
            return null;
        }
    }
}
