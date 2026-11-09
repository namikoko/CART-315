using System.Collections;
using UnityEngine;

namespace PinkBackrooms
{
    /// Stepping on the scale. No number, ever: the room goes silent, the heartbeat climbs, the display panics,
    /// and one thought. The player can do it again and again, the way she does.
    public class ScaleRitual : MonoBehaviour
    {
        public ScaleDisplay display;
        public AudioClip beep;
        [Range(0, 1)] public float beepVolume = 0.5f;
        [Tooltip("Seconds of dead silence before the display reacts.")]
        public float silence = 1.8f;

        [Tooltip("Said in order, one per weigh-in.")]
        [TextArea(1, 2)] public string[] lines = new string[0];
        [Tooltip("Once the lines run out, one of these each time. It doesn't stop.")]
        [TextArea(1, 2)] public string[] again = new string[0];

        public int TimesWeighed { get; private set; }
        private bool _busy;
        private int _lastAgain = -1;

        /// Hooked to the scale's Interactable.
        public void StepOn()
        {
            var d = BedroomDirector.Instance;
            if (_busy || d == null || d.IsEnding) return;
            StartCoroutine(Run(d));
        }

        private IEnumerator Run(BedroomDirector d)
        {
            _busy = true;
            d.FreezePlayer(true);

            // step up onto it and look down at the display
            // the display is at the scale's front edge (its local -Z): stand just behind centre, face it, look down at your feet
            Vector3 front = -transform.forward; front.y = 0; front.Normalize();
            Vector3 from = d.player.position;
            Vector3 c = transform.position - front * 0.05f;
            Vector3 to = new Vector3(c.x, from.y, c.z);
            Vector2 look0 = d.GetLook();
            float yaw1 = look0.x + Mathf.DeltaAngle(look0.x, Quaternion.LookRotation(front).eulerAngles.y);
            float pitch1 = -80f;
            for (float t = 0; t < 0.8f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 0.8f);
                d.Teleport(Vector3.Lerp(from, to, k));
                d.SetLook(Mathf.Lerp(look0.x, yaw1, k), Mathf.Lerp(look0.y, pitch1, k));
                yield return null;
            }
            d.Teleport(to);

            // the beat of silence: just the heart
            var cut = d.Hush();
            for (float t = 0; t < silence; t += Time.deltaTime)
            {
                d.HeartSpike(Mathf.Clamp01(t / silence) * 0.6f + 0.4f);
                yield return null;
            }

            if (display) display.Glitch();
            if (beep) AudioSource.PlayClipAtPoint(beep, transform.position, beepVolume);
            yield return new WaitForSeconds(0.5f);

            if (d.hud) d.hud.ShowLine(NextLine());
            TimesWeighed++;
            yield return new WaitForSeconds(1.6f);

            d.Unhush(cut);
            // lift the head back up a little so the player isn't stuck staring at the floor
            Vector2 look1 = d.GetLook();
            for (float t = 0; t < 0.6f; t += Time.deltaTime)
            {
                d.SetLook(look1.x, Mathf.Lerp(look1.y, -30f, Mathf.SmoothStep(0, 1, t / 0.6f)));
                yield return null;
            }
            d.FreezePlayer(false);
            _busy = false;
        }

        private string NextLine()
        {
            if (TimesWeighed < lines.Length) return lines[TimesWeighed];
            if (again.Length == 0) return lines.Length > 0 ? lines[lines.Length - 1] : "";
            int i = Random.Range(0, again.Length);
            if (again.Length > 1 && i == _lastAgain) i = (i + 1) % again.Length;
            _lastAgain = i;
            return again[i];
        }
    }
}
