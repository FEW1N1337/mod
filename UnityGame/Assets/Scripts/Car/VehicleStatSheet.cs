using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.Car
{
    // Modifiye edilebilir araç istatistikleri.
    public enum VehicleStat
    {
        MotorTorque = 0,
        TopSpeed = 1,
        BrakeTorque = 2,
        SteeringAngle = 3,
        Downforce = 4,
        Mass = 5,
        Grip = 6,
        FuelDrain = 7,
    }

    // Araç istatistiklerini DEĞİŞTİRMENİN tek meşru yolu.
    //
    // NEDEN VAR: bugün nitro, aracın topSpeedKmh alanına doğrudan yazıyor ve
    // bırakınca kendi sakladığı eski değeri geri koyuyor:
    //
    //     _originalTopSpeed = _car.topSpeedKmh;      // Awake
    //     _car.topSpeedKmh  = _originalTopSpeed + 60 // basılınca
    //     _car.topSpeedKmh  = _originalTopSpeed;     // bırakınca
    //
    // Bu, tek değiştirici varken çalışıyor. İkinci bir değiştirici çıktığı anda
    // bozuluyor: turbo yükseltmesi üst hızı 220'ye çıkarsa, o sırada nitroya basıp
    // bırakan oyuncunun aracı Awake'te okunan 180'e DÜŞÜYOR ve satın alınan turbo
    // sessizce kayboluyor. Şartnamedeki motor/turbo/egzoz yükseltmeleri, lastik ve
    // süspansiyon ayarları — hepsi aynı alanlara yazacak. Yani bu çakışma
    // kaçınılmaz, sadece henüz olmadı.
    //
    // Çözüm: kimse temel değeri değiştirmez. Herkes KENDİ ADIYLA bir değiştirici
    // bırakır, sonuç her karede yeniden hesaplanır:
    //
    //     sonuç = (temel + Σ toplamalar) × Π çarpanlar
    //
    // Aynı kaynak ikinci kez Set çağırırsa öncekinin YERİNE geçer — üst üste
    // binmez. Kaynak Clear ederse yalnızca kendi katkısı kalkar.
    public class VehicleStatSheet : MonoBehaviour
    {
        const int StatCount = 8;

        struct Modifier
        {
            public string source;
            public VehicleStat stat;
            public float add;
            public float mul;
        }

        readonly List<Modifier> _modifiers = new List<Modifier>();
        readonly float[] _add = new float[StatCount];
        readonly float[] _mul = new float[StatCount];
        bool _dirty = true;

        // İstatistik değişince haber alması gerekenler için (HUD, ses, telemetri).
        public event System.Action Changed;

        // add: mutlak ekleme (Nm, km/s, derece...). mul: çarpan (1 = etkisiz).
        public void Set(string source, VehicleStat stat, float add, float mul = 1f)
        {
            if (string.IsNullOrEmpty(source))
            {
                Debug.LogError("[VehicleStatSheet] Kaynak adı boş olamaz — " +
                               "adsız değiştirici Clear ile geri alınamaz.");
                return;
            }

            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].source != source || _modifiers[i].stat != stat) continue;

                // Aynı kaynak + aynı istatistik: değiştir, ekleme.
                if (Mathf.Approximately(_modifiers[i].add, add) &&
                    Mathf.Approximately(_modifiers[i].mul, mul)) return;

                _modifiers[i] = new Modifier { source = source, stat = stat, add = add, mul = mul };
                Invalidate();
                return;
            }

            _modifiers.Add(new Modifier { source = source, stat = stat, add = add, mul = mul });
            Invalidate();
        }

        // Bir kaynağın TÜM katkılarını kaldırır. Nitro bırakılınca, parça sökülünce.
        public void Clear(string source)
        {
            bool removed = false;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].source != source) continue;
                _modifiers.RemoveAt(i);
                removed = true;
            }
            if (removed) Invalidate();
        }

        public void Clear(string source, VehicleStat stat)
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].source != source || _modifiers[i].stat != stat) continue;
                _modifiers.RemoveAt(i);
                Invalidate();
                return;
            }
        }

        // FixedUpdate'ten çağrılıyor: sonuç önbellekten okunuyor, liste taranmıyor.
        public float Evaluate(VehicleStat stat, float baseValue)
        {
            if (_dirty) Rebuild();
            int i = (int)stat;
            return (baseValue + _add[i]) * _mul[i];
        }

        public bool HasModifier(string source)
        {
            for (int i = 0; i < _modifiers.Count; i++)
                if (_modifiers[i].source == source) return true;
            return false;
        }

        void Invalidate()
        {
            _dirty = true;
            Changed?.Invoke();
        }

        void Rebuild()
        {
            for (int i = 0; i < StatCount; i++) { _add[i] = 0f; _mul[i] = 1f; }

            for (int i = 0; i < _modifiers.Count; i++)
            {
                int s = (int)_modifiers[i].stat;
                if (s < 0 || s >= StatCount) continue;
                _add[s] += _modifiers[i].add;
                _mul[s] *= _modifiers[i].mul;
            }

            _dirty = false;
        }
    }
}
