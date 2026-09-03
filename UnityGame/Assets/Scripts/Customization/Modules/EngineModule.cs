namespace DreamCar.Customization.Modules
{
    // Motor yükseltmesi. Tork ve üst hız artıyor; katalogdaki seviye arttıkça çarpan büyüyor. Görsel karşılığı yok — kaput altı.
    //
    // Sınıf gövdesi boş: etkinin tamamı katalogdaki ModItem'ın istatistik
    // alanlarından geliyor ve ModModuleBase onu VehicleStatSheet'e yazıyor.
    // Değerleri koda gömmek, dengeyi değiştirmek için kod değişikliği
    // gerektirirdi; katalog prosedürel üretiliyor.
    public class EngineModule : ModModuleBase
    {
        public override string Slot => "engine";
    }
}
