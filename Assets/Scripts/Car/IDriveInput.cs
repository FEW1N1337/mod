namespace DreamCar.Car
{
    // Fizik motor bağımsız araç input arayüzü. Hem WheelCollider tabanlı CarController
    // hem RCCP adapter aynı arayüzü sunar. MobileTouchInput ve CarNetworkSync buna bağlı.
    public interface IDriveInput
    {
        void Move(float throttle, float brake, float steer, bool handbrake);
        float SpeedKmh { get; }
        float TopSpeedKmh { get; }
        float ThrottleInput { get; }
        float BrakeInput { get; }
        float SteerInput { get; }

        // CruiseControl el freni çekilince kendini iptal ediyor.
        bool Handbrake { get; }

        // FuelSystem yakıt bitince motoru kesiyor. Doğrudan throttleInput'a yazmak
        // işe yaramıyordu: MobileTouchInput her karede o alanı eziyor ve iki bileşen
        // arasında Update sırası garanti değil. Bayrak fizik adımında okunuyor.
        bool EngineCutoff { get; set; }
    }
}
