using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DreamCar.RCCPBridge
{
    // RCCP'nin API'si bu projede hiç görülmeden köprü yazıldı: namespace, sınıf ve
    // alan adları tahminden ibaretti. Tahmin yanlışsa, kullanıcı paketi import edip
    // define'ı eklediği anda proje DERLENMEZ — ve derleme hatası yalnızca "şu ad yok"
    // der, doğrusunun ne olduğunu söylemez.
    //
    // Bu yüzden tipe doğrudan bağlanmıyoruz. Çalışma anında adla arıyoruz:
    //   • yanlış tahmin derleme hatası değil, yakalanabilir bir durum olur
    //   • bulunamazsa gerçek üye adlarını Console'a döküp teşhis veririz
    //
    // Aynı kalıp projede iki yerde daha kullanılıyor (bildirim ikonları, SSAO).
    public static class RCCPReflection
    {
        const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        // RCCP tipini adıyla bulur. Namespace'ten bağımsız: RCCP, BCG.RCCP ya da
        // global — hepsi çalışır.
        public static System.Type FindType(string simpleName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }   // bağımlılığı eksik assembly — atla

                foreach (var t in types)
                    if (t != null && t.Name == simpleName) return t;
            }
            return null;
        }

        // Bir üyeye yazma/okuma erişimi. Alan da olabilir property de; hangisi varsa.
        public class Member
        {
            readonly FieldInfo _field;
            readonly PropertyInfo _property;

            public string Name => _field != null ? _field.Name : _property?.Name;
            public bool Found => _field != null || _property != null;

            Member(FieldInfo f, PropertyInfo p) { _field = f; _property = p; }

            // Aday adları sırayla dener, ilk bulduğunu kullanır. RCCP sürümleri
            // arasında ad değişse bile listede varsa tutar.
            public static Member Resolve(System.Type type, params string[] candidates)
            {
                if (type == null) return new Member(null, null);

                foreach (var name in candidates)
                {
                    var f = type.GetField(name, Flags);
                    if (f != null) return new Member(f, null);

                    var p = type.GetProperty(name, Flags);
                    if (p != null) return new Member(null, p);
                }
                return new Member(null, null);
            }

            public void SetFloat(object target, float value)
            {
                if (target == null) return;
                if (_field != null) { _field.SetValue(target, value); return; }
                if (_property != null && _property.CanWrite) _property.SetValue(target, value);
            }

            public void SetBool(object target, bool value)
            {
                if (target == null) return;
                if (_field != null) { _field.SetValue(target, value); return; }
                if (_property != null && _property.CanWrite) _property.SetValue(target, value);
            }

            public float GetFloat(object target, float fallback = 0f)
            {
                if (target == null) return fallback;
                object raw = _field != null ? _field.GetValue(target)
                           : _property != null && _property.CanRead ? _property.GetValue(target)
                           : null;
                return raw is float f ? f : fallback;
            }
        }

        // Teşhis: aradığımız üye yoksa, tipin GERÇEKTEN sahip olduğu adları bas.
        // Kullanıcının göndereceği tek ekran görüntüsü doğru adları öğrenmeye yeter.
        public static void LogAvailableMembers(System.Type type, string context)
        {
            if (type == null)
            {
                Debug.LogWarning($"[RCCP] {context}: tip bulunamadı. RCCP import edildi mi?");
                return;
            }

            var names = new List<string>();
            foreach (var f in type.GetFields(Flags)) names.Add(f.Name);
            foreach (var p in type.GetProperties(Flags)) names.Add(p.Name + " (property)");
            names.Sort();

            Debug.LogWarning(
                $"[RCCP] {context}\n" +
                $"Tip bulundu: {type.FullName}\n" +
                $"Ama beklenen üye adları eşleşmedi. Bu tipin gerçek üyeleri:\n  " +
                string.Join("\n  ", names) +
                "\n\nBu listeyi geliştiriciye gönder — köprü adları buna göre düzeltilir.");
        }
    }
}
