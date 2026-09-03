namespace DreamCar.Customization.Modules
{
    // Fren yükseltmesi. Fren torkunu artırıyor. El freni de aynı limiti kullandığı için drift hissi de değişiyor.
    //
    // Sınıf gövdesi boş: etkinin tamamı katalogdaki ModItem'ın istatistik
    // alanlarından geliyor ve ModModuleBase onu VehicleStatSheet'e yazıyor.
    // Değerleri koda gömmek, dengeyi değiştirmek için kod değişikliği
    // gerektirirdi; katalog prosedürel üretiliyor.
    public class BrakeModule : ModModuleBase
    {
        public override string Slot => "brake";
    }
}
