// Synthesized audio, ported from game/game/audio.ts.
// Master gain 0.22, engine sawtooth lowpassed at 250 Hz, engine gain 0.018,
// engine pitch 42 + |speed| * 4. Tones and explosion noise are generated
// as AudioClips in code; no audio files are required.
using UnityEngine;
using System.Collections;

namespace SmashKarts.Runtime
{
    public sealed class SynthAudio : MonoBehaviour
    {
        private AudioSource _master, _engine;
        private bool _muted;
        private float _lastShot;

        public void StartAudio()
        {
            if (_master != null) return;
            _master = gameObject.AddComponent<AudioSource>();
            _master.volume = 0.22f;
            _engine = gameObject.AddComponent<AudioSource>();
            _engine.clip = SawClip(55f, 1f);
            _engine.loop = true;
            _engine.volume = 0.018f;
            var filter = gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 250f;
            _engine.Play();
        }

        public void SetMuted(bool muted) { _muted = muted; if (_master != null) _master.volume = muted ? 0 : 0.22f; }
        public void SetSpeed(double speed) { if (_engine != null) _engine.pitch = (42f + Mathf.Abs((float)speed) * 4f) / 55f; }

        private AudioClip SawClip(float freq, float duration)
        {
            int rate = 44100, n = (int)(rate * duration);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float phase = (i * freq / rate) % 1f;
                data[i] = phase * 2 - 1;
            }
            var clip = AudioClip.Create("saw", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip ToneClip(float startFreq, float endFreq, float duration, int wave = 0)
        {
            int rate = 44100, n = (int)(rate * duration);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(startFreq, endFreq, t);
                float phase = (i * f / rate) % 1f;
                float v = wave == 1 ? (phase * 2 - 1) : Mathf.Sin(phase * Mathf.PI * 2);
                data[i] = v * (1 - t);
            }
            var clip = AudioClip.Create("tone", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Tone(float freq, float end, float duration, float volume = 0.18f, int wave = 0)
        {
            if (_muted) return;
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = ToneClip(freq, end, duration, wave);
            src.volume = volume;
            src.Play();
            Destroy(src, duration + 0.1f);
        }

        public void Shot(bool heavy = false)
        {
            if (Time.time - _lastShot < 0.055f) return;
            _lastShot = Time.time;
            Tone(heavy ? 170 : 850, 70, heavy ? 0.2f : 0.065f, heavy ? 0.23f : 0.1f, 1);
        }

        public void Explosion(float volume = 1f)
        {
            if (_muted) return;
            int rate = 44100, n = (int)(rate * 0.48f);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = (Random.value * 2 - 1) * (1 - i / (float)n);
            var clip = AudioClip.Create("noise", n, 1, rate, false);
            clip.SetData(data, 0);
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip; src.volume = 0.5f * volume; src.Play();
            Destroy(src, 0.6f);
            Tone(110, 25, 0.3f, 0.3f * volume);
        }

        public void Pickup() => Tone(620, 1240, 0.18f, 0.15f);
    }
}
