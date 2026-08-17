namespace DreamCar.GameModes
{
    // Kural yok — sadece sür. Ekonomi/ödül harici sistemler kendi ödüllerini verir
    // (örn. DriftScore bank'ından PlayerMoney'ye periyodik conversion).
    public class FreeRoamMode : GameModeBase
    {
        public override GameModeType Type => GameModeType.Free;
    }
}
