using System.Collections.Generic;
using UnityEngine;
using DreamCar.Economy;
using DreamCar.Customization.Modules;

namespace DreamCar.Customization
{
    // Bir araç örneğine takılı modüllerin tamamı.
    //
    // MonoBehaviour DEĞİL, düz sınıf: hem gerçek araçta (CarCustomization
    // bileşeni üzerinden) hem menü garajındaki ÖNİZLEME prefabında kullanılıyor.
    // Önizleme prefabı bütün MonoBehaviour'ları kaybediyor (SavePreviewPrefab),
    // yani bileşen tabanlı bir tasarımda garajda hiçbir modifikasyon
    // görünmezdi — oyuncu parça satın alır, aracında hiçbir şey değişmezdi.
    public class CustomizationRuntime
    {
        readonly Dictionary<string, ICustomizationModule> _modules = new();
        readonly VehicleContext _context;
        readonly ModCatalog _catalog;

        public IEnumerable<string> Slots => _modules.Keys;
        public ModCatalog Catalog => _catalog;

        // Modül listesi KODDA sabit ve TEK YERDE.
        //
        // Katalogdan üretilseydi, katalogda olmayan bir slot sessizce modülsüz
        // kalırdı. Denetçi tam tersini kontrol ediyor: katalogdaki her slotun
        // burada bir karşılığı var mı. Fabrika listesi static olduğu için
        // denetçi araç örneği kurmadan slot adlarını okuyabiliyor.
        static readonly System.Func<ModModuleBase>[] Factories =
        {
            () => new PaintModule(),
            () => new WindowTintModule(),
            () => new RimColorModule(),
            () => new SpoilerModule(),
            () => new NeonModule(),
            () => new EngineModule(),
            () => new TurboModule(),
            () => new TireModule(),
            () => new BrakeModule(),
            () => new SuspensionModule(),
            () => new ExhaustModule(),
        };

        // Kodda tanımlı bütün slot adları. Denetçi ve editör araçları için.
        public static List<string> KnownSlots()
        {
            var result = new List<string>(Factories.Length);
            foreach (var factory in Factories) result.Add(factory().Slot);
            return result;
        }

        public CustomizationRuntime(GameObject root, ModCatalog catalog)
        {
            _catalog = catalog;
            _context = VehicleContext.FromVehicle(root);

            foreach (var factory in Factories) Register(factory());
        }

        void Register(ModModuleBase module)
        {
            module.Catalog = _catalog;
            _modules[module.Slot] = module;
        }

        public ICustomizationModule Module(string slot) =>
            _modules.TryGetValue(slot, out var m) ? m : null;

        public ItemId Equipped(string slot) =>
            _modules.TryGetValue(slot, out var m) ? m.Current : ItemId.None;

        // visualOnly: uzak oyuncuların araçlarında yalnızca görünen modüller
        // uygulanıyor. Uzak araçta fizik simüle edilmiyor, tork değiştirmenin
        // karşılığı yok — ve o araç bizim ekonomimize de yazmıyor.
        public void Equip(string slot, ItemId id, bool visualOnly = false)
        {
            if (!_modules.TryGetValue(slot, out var module)) return;
            if (visualOnly && module is ModModuleBase b && !b.AffectsRemoteVisuals) return;
            module.Apply(_context, id);
        }

        public void ApplyAll(IReadOnlyDictionary<string, ItemId> config, bool visualOnly = false)
        {
            foreach (var slot in _modules.Keys)
            {
                var id = config != null && config.TryGetValue(slot, out var v) ? v : ItemId.None;
                Equip(slot, id, visualOnly);
            }
        }

        // Kayıtlı yapılandırmayı diskten okuyup uygular.
        public void ApplySaved(string carId, bool visualOnly = false)
        {
            foreach (var slot in _modules.Keys)
                Equip(slot, ModSave.Equipped(carId, slot), visualOnly);
        }

        public void RemoveAll()
        {
            foreach (var module in _modules.Values) module.Remove(_context);
        }
    }
}
