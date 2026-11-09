using System.Collections;
using UnityEngine;

namespace PinkBackrooms
{
    /// Hitting the pen or the J: inhale, hold, cough out a cloud, and the room starts to soften.
    public class SmokeHit : MonoBehaviour
    {
        public AudioClip inhale;
        public AudioClip cough;
        public ParticleSystem exhale;   // lives on the camera
        public Light glow;              // the cherry / the LED brightening while you pull
        public float glowBoost = 6f;

        private bool _busy;

        /// Hooked to the Interactable.
        public void Hit()
        {
            var d = BedroomDirector.Instance;
            if (_busy || d == null || d.IsEnding) return;
            StartCoroutine(Run(d));
        }

        private IEnumerator Run(BedroomDirector d)
        {
            _busy = true;
            var cam = d.interactor ? d.interactor.cam.transform : null;
            if (inhale) AudioSource.PlayClipAtPoint(inhale, cam ? cam.position : transform.position, 0.7f);
            float g0 = glow ? glow.intensity : 0;
            for (float t = 0; t < 1.6f; t += Time.deltaTime)
            {
                if (glow) glow.intensity = g0 * (1 + glowBoost * Mathf.Sin(Mathf.PI * t / 1.6f));
                yield return null;
            }
            if (glow) glow.intensity = g0;
            yield return new WaitForSeconds(0.6f);   // hold it
            if (cough) AudioSource.PlayClipAtPoint(cough, cam ? cam.position : transform.position, 0.8f);
            if (exhale) { exhale.Play(); exhale.Emit(60); }
            if (d.mood) d.mood.AddHigh(1f);
            d.HeartSpike(0.35f);
            yield return new WaitForSeconds(2f);
            _busy = false;
        }
    }
}
