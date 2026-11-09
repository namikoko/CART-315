using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PinkBackrooms
{
    /// The look of the room right now, on top of the base post-processing.
    /// High: after a hit, everything gets prettier and softer: warmer pinks, glow, a slow wobble, muffled sound, heavier body.
    /// Dim: the room slowly darkening while she's stuck on the walking pad.
    public class RoomMood : MonoBehaviour
    {
        public Volume volume;
        public Transform cam;
        public AudioLowPassFilter earMuffle;
        [Tooltip("How long one hit lasts before it has fully worn off (seconds).")]
        public float wearOff = 75f;
        public float maxHigh = 2.2f;

        [Header("Live")]
        [Range(0, 3)] public float high;
        [Range(0, 1)] public float dim;
        public float dimTarget;

        private ColorAdjustments _col; private Bloom _bloom; private ChromaticAberration _ca;
        private LensDistortion _lens; private Vignette _vig; private FilmGrain _grain;
        private float _sat, _exp, _bloomI, _caI, _lensI, _vigI, _grainI; private Color _filter;
        private float _rise;     // a new hit comes on over a few seconds rather than all at once
        private Component _fp; private System.Reflection.FieldInfo _speedField; private float _speed;

        private void Start()
        {
            if (!volume) return;
            var p = volume.profile; // runtime copy, so the asset on disk is never touched
            p.TryGet(out _col); p.TryGet(out _bloom); p.TryGet(out _ca); p.TryGet(out _lens); p.TryGet(out _vig); p.TryGet(out _grain);
            if (_col) { _sat = _col.saturation.value; _exp = _col.postExposure.value; _filter = _col.colorFilter.value; }
            if (_bloom) _bloomI = _bloom.intensity.value;
            if (_ca) _caI = _ca.intensity.value;
            if (_lens) _lensI = _lens.intensity.value;
            if (_vig) _vigI = _vig.intensity.value;
            if (_grain) _grainI = _grain.intensity.value;

            var player = BedroomDirector.Instance ? BedroomDirector.Instance.player : null;
            if (player)
                foreach (var mb in player.GetComponents<MonoBehaviour>())
                    if (mb.GetType().Name == "FPController")
                    {
                        _fp = mb;
                        _speedField = mb.GetType().GetField("_movementSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (_speedField != null) _speed = (float)_speedField.GetValue(mb);
                    }
        }

        /// One hit of the pen or the J.
        public void AddHigh(float amount = 1f) { _rise += amount; }

        private void Update()
        {
            float dt = Time.deltaTime;
            float take = Mathf.Min(_rise, dt * 0.35f);       // comes on over ~3 s
            _rise -= take; high = Mathf.Min(maxHigh, high + take);
            high = Mathf.MoveTowards(high, 0, dt / wearOff);
            dim = Mathf.MoveTowards(dim, dimTarget, dt * (dimTarget > dim ? 0.012f : 0.08f));

            float h = Mathf.Clamp01(high), hh = high, t = Time.time;
            float breathe = Mathf.Sin(t * 0.55f) * 0.5f + Mathf.Sin(t * 0.23f + 1.3f) * 0.5f;

            if (_col)
            {
                _col.saturation.Override(_sat + 38f * h);
                _col.postExposure.Override(_exp + 0.35f * h - 1.4f * dim);
                _col.colorFilter.Override(Color.Lerp(_filter, new Color(1f, 0.82f, 0.9f), h * 0.6f));
                _col.hueShift.Override(Mathf.Sin(t * 0.3f) * 6f * Mathf.Clamp01(hh - 1f));
            }
            if (_bloom) _bloom.intensity.Override(_bloomI + 2.2f * h);
            if (_ca) _ca.intensity.Override(Mathf.Clamp01(_caI + 0.35f * h * (0.6f + 0.4f * breathe)));
            if (_lens) _lens.intensity.Override(Mathf.Clamp(_lensI + 0.28f * hh * breathe - 0.1f * h, -0.9f, 0.9f));
            if (_vig) _vig.intensity.Override(Mathf.Lerp(_vigI, 0.25f, h) + 0.25f * dim);
            if (_grain) _grain.intensity.Override(Mathf.Lerp(_grainI, 0.2f, h));

            if (cam) cam.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.37f) * 1.2f * hh, 0, Mathf.Sin(t * 0.5f) * 3.5f * hh);
            if (earMuffle) earMuffle.cutoffFrequency = Mathf.Lerp(22000f, 1400f, h);
            if (_fp && _speedField != null) _speedField.SetValue(_fp, _speed * Mathf.Lerp(1f, 0.6f, h));
        }
    }
}
