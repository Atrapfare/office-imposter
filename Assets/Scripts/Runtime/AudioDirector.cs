using System;
using UnityEngine;

namespace OfficeImposter
{
    // All audio is synthesised at runtime rather than shipped as files: it keeps the
    // repo free of third-party assets and needs no licence review.
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        public static float MasterVolume = 0.7f;

        AudioSource _ambience;
        AudioSource _oneShots;

        AudioClip _hum;
        AudioClip _keyClack;
        AudioClip _footstep;
        AudioClip _alert;
        AudioClip _chime;
        AudioClip _success;
        AudioClip _mistake;
        AudioClip _fired;

        void Awake()
        {
            Instance = this;

            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.loop = true;
            _ambience.playOnAwake = false;
            _ambience.spatialBlend = 0f;

            _oneShots = gameObject.AddComponent<AudioSource>();
            _oneShots.playOnAwake = false;
            _oneShots.spatialBlend = 0f;

            BuildClips();

            _ambience.clip = _hum;
            _ambience.volume = 0.18f * MasterVolume;
            _ambience.Play();
        }

        void Update()
        {
            if (_ambience != null) _ambience.volume = 0.18f * MasterVolume;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static AudioClip Render(string name, float duration, Func<float, float> generator, int rate = 44100)
        {
            int count = Mathf.Max(1, (int)(duration * rate));
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(generator(i / (float)rate), -1f, 1f);

            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void BuildClips()
        {
            var noise = new System.Random(12345);

            // Ventilation and distant chatter: layered low sines plus a slow noise bed.
            float lastNoise = 0f;
            _hum = Render("Hum", 4f, t =>
            {
                float drift = Mathf.Sin(t * 0.7f) * 0.15f;
                float tone = Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.35f
                             + Mathf.Sin(2f * Mathf.PI * 104f * t) * 0.18f
                             + Mathf.Sin(2f * Mathf.PI * 77f * t) * 0.10f;
                lastNoise = Mathf.Lerp(lastNoise, (float)(noise.NextDouble() * 2.0 - 1.0), 0.02f);
                return (tone * (0.8f + drift) + lastNoise * 0.5f) * 0.5f;
            });

            var clickNoise = new System.Random(777);
            _keyClack = Render("KeyClack", 0.06f, t =>
            {
                float env = Mathf.Exp(-t * 90f);
                return (float)(clickNoise.NextDouble() * 2.0 - 1.0) * env * 0.5f;
            });

            var stepNoise = new System.Random(99);
            _footstep = Render("Footstep", 0.14f, t =>
            {
                float env = Mathf.Exp(-t * 26f);
                float body = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.6f;
                return (body + (float)(stepNoise.NextDouble() * 2.0 - 1.0) * 0.35f) * env * 0.45f;
            });

            _alert = Render("Alert", 0.45f, t =>
            {
                float env = Mathf.Exp(-t * 5f);
                float freq = Mathf.Lerp(740f, 380f, Mathf.Clamp01(t / 0.45f));
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
            });

            _chime = Render("Chime", 0.7f, t =>
            {
                float env = Mathf.Exp(-t * 3.2f);
                return (Mathf.Sin(2f * Mathf.PI * 660f * t) + Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.6f) * env * 0.3f;
            });

            _success = Render("Success", 0.4f, t =>
            {
                float env = Mathf.Exp(-t * 6f);
                float step = t < 0.13f ? 523f : t < 0.26f ? 659f : 784f;
                return Mathf.Sin(2f * Mathf.PI * step * t) * env * 0.4f;
            });

            _mistake = Render("Mistake", 0.22f, t =>
            {
                float env = Mathf.Exp(-t * 14f);
                return Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 140f * t)) * env * 0.28f;
            });

            _fired = Render("Fired", 1.3f, t =>
            {
                float env = Mathf.Exp(-t * 2.2f);
                float freq = Mathf.Lerp(420f, 110f, Mathf.Clamp01(t / 1.3f));
                return (Mathf.Sin(2f * Mathf.PI * freq * t)
                        + Mathf.Sin(2f * Mathf.PI * freq * 0.5f * t) * 0.5f) * env * 0.42f;
            });
        }

        void Play(AudioClip clip, float volume, float pitchJitter)
        {
            if (clip == null || _oneShots == null || MasterVolume <= 0.001f) return;

            _oneShots.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            _oneShots.PlayOneShot(clip, volume * MasterVolume);
        }

        public static void KeyClack() => Instance?.Play(Instance._keyClack, 0.55f, 0.12f);
        public static void Footstep() => Instance?.Play(Instance._footstep, 0.4f, 0.1f);
        public static void Alert() => Instance?.Play(Instance._alert, 0.7f, 0.03f);
        public static void Chime() => Instance?.Play(Instance._chime, 0.6f, 0f);
        public static void Success() => Instance?.Play(Instance._success, 0.55f, 0.02f);
        public static void Mistake() => Instance?.Play(Instance._mistake, 0.5f, 0.05f);
        public static void Fired() => Instance?.Play(Instance._fired, 0.8f, 0f);
    }
}
