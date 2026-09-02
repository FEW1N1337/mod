using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace DreamCar.Audio
{
    // Oyunda hiç müzik sistemi yoktu. İki AudioSource ile crossfade yapan playlist:
    // menü listesi + oyun içi listesi. Seviye AudioBus.MusicScale üzerinden gelir
    // (mixer varsa mixer, yoksa çarpan — bkz. Audio/AudioBus.cs).
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        public enum Playlist { Menu, Gameplay }

        [Tooltip("Bu sahne adı menü listesini çalar; diğer her sahne oyun içi listeyi.")]
        public string menuSceneName = "MainMenu";

        string MenuSceneName => string.IsNullOrEmpty(menuSceneName) ? "MainMenu" : menuSceneName;

        [Header("Parçalar")]
        public AudioClip[] menuTracks;
        public AudioClip[] gameplayTracks;

        [Header("Ayarlar")]
        public AudioMixerGroup musicMixerGroup;
        public float crossfadeSeconds = 2f;
        [Range(0f, 1f)] public float baseVolume = 0.6f;
        public bool shuffle = true;
        public bool playOnStart = true;

        AudioSource _a, _b;
        AudioSource _active;
        Playlist _current = Playlist.Menu;
        readonly List<int> _order = new();
        int _orderIndex;
        Coroutine _fadeRoutine;

        // Mixer yoksa müzik seviyesi buradan gelir; mixer varsa 1 döner.
        float TargetVolume => baseVolume * AudioBus.MusicScale;

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _a = CreateSource("MusicA");
            _b = CreateSource("MusicB");
            _active = _a;
        }

        void OnEnable()
        {
            AudioBus.OnChanged += OnVolumeChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            AudioBus.OnChanged -= OnVolumeChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Play(Playlist)'in sınıf DIŞINDA sıfır çağıranı vardı: Start() bir kez
        // Play(Menu) çağırıyor ve orada bitiyordu. Yani parçalar eklense bile
        // gameplayTracks asla çalmaz, menü parçası sürüş boyunca çalmaya devam
        // ederdi.
        //
        // Değişimi bileşen kendi dinliyor. Dışarıdan çağırmak her sahne için ayrı
        // kablolama demek olurdu ve bu bileşen DontDestroyOnLoad — ana menünün
        // Start()'ı sahne geçişlerinde bir daha koşmuyor, yani "birinde unutulur"
        // hatasına açık kalırdı. Olayı buradan dinleyince Editor'de atanacak
        // hiçbir alan kalmıyor.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;   // ek sahne müziği değiştirmesin

            var wanted = scene.name == MenuSceneName ? Playlist.Menu : Playlist.Gameplay;
            if (wanted == _current && _active != null && _active.isPlaying) return;

            Play(wanted);
        }

        // Sürgü oynatıldığında çalan parça anında uysun — bir sonraki geçişi bekleme.
        // Crossfade sürerken dokunma, yoksa geçişin ara değerini ezeriz.
        void OnVolumeChanged()
        {
            if (_fadeRoutine != null) return;
            if (_active != null && _active.isPlaying) _active.volume = TargetVolume;
        }

        AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = false;
            src.playOnAwake = false;
            src.volume = 0f;
            src.spatialBlend = 0f;
            if (musicMixerGroup) src.outputAudioMixerGroup = musicMixerGroup;
            return src;
        }

        void Start()
        {
            if (playOnStart) Play(Playlist.Menu);
        }

        void Update()
        {
            // Parça bitince sıradakine geç.
            if (_active != null && !_active.isPlaying && _active.clip != null && _fadeRoutine == null)
                Next();
        }

        public void Play(Playlist playlist)
        {
            _current = playlist;
            BuildOrder(TracksFor(playlist).Length);
            _orderIndex = 0;
            PlayCurrent(immediate: true);
        }

        public void Next()
        {
            var tracks = TracksFor(_current);
            if (tracks.Length == 0) return;
            _orderIndex = (_orderIndex + 1) % _order.Count;
            PlayCurrent(immediate: false);
        }

        public void StopMusic()
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeOutAll());
        }

        AudioClip[] TracksFor(Playlist p) =>
            p == Playlist.Menu ? (menuTracks ?? new AudioClip[0]) : (gameplayTracks ?? new AudioClip[0]);

        void BuildOrder(int count)
        {
            _order.Clear();
            for (int i = 0; i < count; i++) _order.Add(i);
            if (!shuffle) return;
            for (int i = _order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
        }

        void PlayCurrent(bool immediate)
        {
            var tracks = TracksFor(_current);
            if (tracks.Length == 0 || _order.Count == 0) return;

            var clip = tracks[_order[_orderIndex % _order.Count] % tracks.Length];
            if (clip == null) return;

            var incoming = _active == _a ? _b : _a;
            incoming.clip = clip;
            incoming.time = 0f;
            incoming.Play();

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(Crossfade(_active, incoming, immediate ? 0.2f : crossfadeSeconds));
            _active = incoming;
        }

        IEnumerator Crossfade(AudioSource from, AudioSource to, float duration)
        {
            float t = 0f;
            float fromStart = from != null ? from.volume : 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
                // Hedef seviye her karede okunur — geçiş sırasında sürgü oynatılsa
                // bile doğru seviyede biter.
                if (to != null) to.volume = Mathf.Lerp(0f, TargetVolume, k);
                yield return null;
            }
            if (from != null) { from.Stop(); from.volume = 0f; }
            if (to != null) to.volume = TargetVolume;
            _fadeRoutine = null;
        }

        IEnumerator FadeOutAll()
        {
            float t = 0f;
            float aStart = _a.volume, bStart = _b.volume;
            while (t < crossfadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / crossfadeSeconds);
                _a.volume = Mathf.Lerp(aStart, 0f, k);
                _b.volume = Mathf.Lerp(bStart, 0f, k);
                yield return null;
            }
            _a.Stop(); _b.Stop();
            _fadeRoutine = null;
        }
    }
}
