using UnityEngine;

namespace DreamCar.Environment
{
    // Yağmur/kar particle + shader ıslaklık interpolation.
    public class Weather : MonoBehaviour
    {
        public enum Type { Clear, Rain, Snow }
        public Type type = Type.Clear;
        public ParticleSystem rainFX;
        public ParticleSystem snowFX;
        public AudioSource rainLoop;
        public float wetnessLerpSpeed = 0.5f;

        static readonly int WetnessId = Shader.PropertyToID("_GlobalWetness");
        float _wetness;

        // Yağmur döngüsü bir kez ayarlanıp Play() ediliyor — SFX sürgüsüne AudioBus bağlar.
        void Awake() => DreamCar.Audio.AudioBus.RegisterSfx(rainLoop);
        void OnDestroy() => DreamCar.Audio.AudioBus.Unregister(rainLoop);

        void Update()
        {
            bool rain = type == Type.Rain;
            bool snow = type == Type.Snow;

            SetOn(rainFX, rain);
            SetOn(snowFX, snow);

            if (rainLoop)
            {
                if (rain && !rainLoop.isPlaying) rainLoop.Play();
                else if (!rain && rainLoop.isPlaying) rainLoop.Stop();
            }

            float target = rain ? 1f : (snow ? 0.6f : 0f);
            _wetness = Mathf.MoveTowards(_wetness, target, wetnessLerpSpeed * Time.deltaTime);
            Shader.SetGlobalFloat(WetnessId, _wetness);
        }

        static void SetOn(ParticleSystem ps, bool on)
        {
            if (!ps) return;
            var emission = ps.emission;
            emission.enabled = on;
            if (on && !ps.isPlaying) ps.Play();
        }

        public void SetType(Type t) => type = t;
        public void Cycle() => type = (Type)(((int)type + 1) % 3);
    }
}
