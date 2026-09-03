namespace DreamCar.Customization.Modules
{
    // Turbo. Torku çarpanla artırıyor ama yakıt tüketimine de ceza yazıyor (ModItem.statB genellikle FuelDrain). İki etkinin de aynı kaynak adıyla tabloya gitmesi önemli: turbo çıkarıldığında ikisi birden kalkıyor.
    //
    // Sınıf gövdesi boş: etkinin tamamı katalogdaki ModItem'ın istatistik
    // alanlarından geliyor ve ModModuleBase onu VehicleStatSheet'e yazıyor.
    // Değerleri koda gömmek, dengeyi değiştirmek için kod değişikliği
    // gerektirirdi; katalog prosedürel üretiliyor.
    public class TurboModule : ModModuleBase
    {
        public override string Slot => "turbo";
    }
}
