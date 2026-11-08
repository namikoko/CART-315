using System.Collections;
using UnityEngine;

namespace PinkBackrooms
{
    /// Drives one or more lights plus their glowing bulbs (and an optional buzz sound) together,
    /// so the room never feels fully stable.
    public class FlickerLight : MonoBehaviour
    {
        public enum Mode { Flicker, Dying, Pulse, Off, On }

        public Mode mode = Mode.Flicker;
        public Light[] lights = new Light[0];
        public Renderer[] bulbs = new Renderer[0];
        [ColorUsage(false, true)] public Color bulbColor = new Color(1f, 0.7f, 0.8f) * 2f;
        public AudioSource buzz;

        [Tooltip("Flicker: chance per second of a stutter. Dying: chance per second of coming briefly alive.")]
        public float eventRate = 0.35f;
        public float pulseSpeed = 0.25f;

        private float[] _base;
        private float _buzzBase;
        private MaterialPropertyBlock _mpb;
        private float _level = 1;
        private bool _busy;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _base = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) _base[i] = lights[i] ? lights[i].intensity : 0;
            _buzzBase = buzz ? buzz.volume : 0;
            _mpb = new MaterialPropertyBlock();
        }

        public void SetMode(Mode m) { mode = m; }

        public void SetBaseIntensity(float multiplier)
        {
            for (int i = 0; i < lights.Length; i++) _base[i] *= multiplier;
        }

        private void Update()
        {
            if (_busy) return;
            switch (mode)
            {
                case Mode.On: Apply(1); break;
                case Mode.Off: Apply(0); break;
                case Mode.Pulse:
                    Apply(0.75f + 0.25f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + Random.Range(-0.03f, 0.03f));
                    break;
                case Mode.Flicker:
                    Apply(Mathf.Lerp(_level, 0.92f + Random.Range(-0.05f, 0.05f), 0.3f));
                    if (Random.value < eventRate * Time.deltaTime) StartCoroutine(Stutter(false));
                    break;
                case Mode.Dying:
                    Apply(0.02f);
                    if (Random.value < eventRate * Time.deltaTime) StartCoroutine(Stutter(true));
                    break;
            }
        }

        private IEnumerator Stutter(bool dying)
        {
            _busy = true;
            int n = Random.Range(2, 7);
            for (int i = 0; i < n; i++)
            {
                Apply(dying ? Random.Range(0.4f, 1f) : Random.Range(0f, 0.25f));
                yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
                Apply(dying ? 0.02f : 1f);
                yield return new WaitForSeconds(Random.Range(0.02f, 0.15f));
            }
            if (!dying && Random.value < 0.25f)
            {
                Apply(0);
                yield return new WaitForSeconds(Random.Range(0.4f, 1.6f)); // a proper blackout now and then
            }
            _busy = false;
        }

        private void Apply(float k)
        {
            _level = k;
            for (int i = 0; i < lights.Length; i++)
                if (lights[i]) lights[i].intensity = _base[i] * k;
            if (buzz) buzz.volume = _buzzBase * Mathf.Clamp01(k);
            foreach (var r in bulbs)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColor, bulbColor * k);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
