using System.Collections;
using UnityEngine;

namespace PinkBackrooms
{
    /// The scale's display: no number, just a blink that won't stop.
    public class ScaleDisplay : MonoBehaviour
    {
        public Renderer display;
        public Light glow;
        [ColorUsage(false, true)] public Color color = new Color(1f, 0.25f, 0.4f) * 2.5f;
        public float blinkRate = 1.2f;

        private MaterialPropertyBlock _mpb;
        private bool _glitching;
        private float _glowBase;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (glow) _glowBase = glow.intensity;
        }

        private void Update()
        {
            if (_glitching) return;
            Set(Mathf.Repeat(Time.time * blinkRate, 1f) < 0.55f ? 1f : 0.1f);
        }

        /// Hooked to the scale's Interactable: the display panics for a second.
        public void Glitch()
        {
            if (!_glitching) StartCoroutine(GlitchRoutine());
        }

        private IEnumerator GlitchRoutine()
        {
            _glitching = true;
            for (float t = 0; t < 1.4f; t += 0.05f)
            {
                Set(Random.value > 0.5f ? 1.6f : 0f);
                yield return new WaitForSeconds(0.05f);
            }
            _glitching = false;
        }

        private void Set(float k)
        {
            if (display)
            {
                display.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", color * k);
                display.SetPropertyBlock(_mpb);
            }
            if (glow) glow.intensity = _glowBase * k;
        }
    }
}
