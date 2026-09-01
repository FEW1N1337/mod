using UnityEngine;
using DreamCar.Car;

namespace DreamCar.RCCPBridge
{
    // RCCP (Realistic Car Controller Pro) denetleyicisini IDriveInput arayüzü altında
    // sarar. MobileTouchInput, CarNetworkSync, HUD — hepsi RCCP'nin varlığından
    // habersiz çalışmaya devam eder.
    //
    // RCCP'ye DOĞRUDAN BAĞLANMIYORUZ. Sebebi: bu köprü RCCP'nin gerçek API'si hiç
    // görülmeden yazıldı, adlar tahmindi. Doğrudan bağlanmak, tahminlerden biri bile
    // yanlışsa projenin derlenmemesi demekti. Şimdi adlar çalışma anında aranıyor;
    // bulunamazsa tipin gerçek üyeleri Console'a dökülüyor ve tek bir ekran
    // görüntüsüyle düzeltilebiliyor.
    //
    // Kurulum: RCCP import → Player Settings → Scripting Define Symbols →
    // RCCP_INSTALLED → araca RCCP denetleyicisi + bu bileşen.
    [RequireComponent(typeof(Rigidbody))]
    public class RCCPCarAdapter : MonoBehaviour, IDriveInput
    {
        [Tooltip("HUD ve vites için üst hız. RCCP kendi eğrisini kullanır; bu yalnızca gösterim.")]
        public float overrideTopSpeedKmh = 220f;

        float _throttle, _brake, _steer;
        bool _hand;

        Component _rccp;
        Rigidbody _rb;

        RCCPReflection.Member _throttleMember;
        RCCPReflection.Member _brakeMember;
        RCCPReflection.Member _steerMember;
        RCCPReflection.Member _handbrakeMember;
        RCCPReflection.Member _overrideMember;
        RCCPReflection.Member _speedMember;

        bool _wired;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();

#if RCCP_INSTALLED
            Wire();
#else
            // Define yoksa bu bileşen kasıtlı olarak sessiz kalır: proje RCCP'siz de
            // çalışıyor, uyarı basmanın anlamı yok.
            enabled = false;
#endif
        }

        void Wire()
        {
            var type = RCCPReflection.FindType("RCCP_CarController");
            if (type == null)
            {
                Debug.LogWarning(
                    "[RCCP] RCCP_CarController tipi bulunamadı. RCCP_INSTALLED tanımlı " +
                    "ama paket import edilmemiş olabilir. Araç bizim CarController'ımıza düşecek.");
                enabled = false;
                return;
            }

            _rccp = GetComponent(type);
            if (_rccp == null)
            {
                Debug.LogWarning($"[RCCP] Bu araçta {type.Name} bileşeni yok. " +
                                 "RCCPCarAdapter, RCCP denetleyicisiyle aynı GameObject'te olmalı.");
                enabled = false;
                return;
            }

            // Aday adlar: RCCP sürümleri arasında değişebildiği için birkaç makul
            // varyant deneniyor, ilk bulunan kullanılıyor.
            _throttleMember  = RCCPReflection.Member.Resolve(type, "throttleInput_V", "throttleInput", "throttle");
            _brakeMember     = RCCPReflection.Member.Resolve(type, "brakeInput_V", "brakeInput", "brake");
            _steerMember     = RCCPReflection.Member.Resolve(type, "steerInput_V", "steerInput", "steering", "steer");
            _handbrakeMember = RCCPReflection.Member.Resolve(type, "handbrakeInput_V", "handbrakeInput", "handbrake");
            _overrideMember  = RCCPReflection.Member.Resolve(type, "overrideInputs", "externalController", "overrideInternalInputs");
            _speedMember     = RCCPReflection.Member.Resolve(type, "speed", "currentSpeed", "absoluteSpeed");

            // Gaz ve direksiyon olmadan araç sürülemez; ikisi de zorunlu.
            _wired = _throttleMember.Found && _steerMember.Found;

            if (!_wired)
            {
                RCCPReflection.LogAvailableMembers(type,
                    "Araç girdileri bağlanamadı (gaz ve/veya direksiyon üyesi bulunamadı).");
                enabled = false;
                return;
            }

            Debug.Log($"[RCCP] Bağlandı: {type.FullName} " +
                      $"(gaz={_throttleMember.Name}, direksiyon={_steerMember.Name})");
        }

        public void Move(float throttle, float brake, float steer, bool handbrake)
        {
            _throttle = Mathf.Clamp(throttle, -1f, 1f);
            _brake = Mathf.Clamp01(brake);
            _steer = Mathf.Clamp(steer, -1f, 1f);
            _hand = handbrake;

            if (!_wired || _rccp == null) return;

            // RCCP'ye kendi girdilerimizi veriyoruz. Negatif gaz frene eklenir —
            // mobil kontrolde "geri" ayrı bir tuş değil, gazın ters yönü.
            _throttleMember.SetFloat(_rccp, Mathf.Max(0f, _throttle));
            _steerMember.SetFloat(_rccp, _steer);

            if (_brakeMember.Found)
                _brakeMember.SetFloat(_rccp, _brake + (_throttle < 0f ? -_throttle : 0f));

            if (_handbrakeMember.Found)
                _handbrakeMember.SetFloat(_rccp, _hand ? 1f : 0f);

            // RCCP kendi girdi okumasını yapıyorsa bizimkini ezerdi; varsa kapatıyoruz.
            if (_overrideMember.Found)
                _overrideMember.SetBool(_rccp, true);
        }

        public float SpeedKmh
        {
            get
            {
                if (_wired && _rccp != null && _speedMember.Found)
                    return _speedMember.GetFloat(_rccp);

                // RCCP hız üyesi bulunamadıysa gövdeden hesapla — sürüş etkilenmez,
                // yalnızca gösterge biraz farklı olabilir.
                return _rb ? _rb.linearVelocity.magnitude * 3.6f : 0f;
            }
        }

        public float TopSpeedKmh => overrideTopSpeedKmh;
        public float ThrottleInput => _throttle;
        public float BrakeInput => _brake;
        public float SteerInput => _steer;
    }
}
