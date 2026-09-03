namespace DreamCar.Customization.Modules
{
    // Lastik seti. Tutuşu değiştiriyor — CarController tutuşu WheelCollider sürtünme eğrisinin stiffness'ı üzerinden uyguluyor (VehicleStat.Grip).
    //
    // Sınıf gövdesi boş: etkinin tamamı katalogdaki ModItem'ın istatistik
    // alanlarından geliyor ve ModModuleBase onu VehicleStatSheet'e yazıyor.
    // Değerleri koda gömmek, dengeyi değiştirmek için kod değişikliği
    // gerektirirdi; katalog prosedürel üretiliyor.
    public class TireModule : ModModuleBase
    {
        public override string Slot => "tire";
    }
}
