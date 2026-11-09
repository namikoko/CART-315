using System.Collections;
using UnityEngine;

namespace PinkBackrooms
{
    /// Lying down. The screen goes dark, the affirmations play close like they do every night,
    /// and she wakes up where the day started, to a room a little messier than before. The mess always comes back.
    public class BedSleep : MonoBehaviour
    {
        public SpeakerToggle speaker;
        public AudioClip rustle;
        [Tooltip("Turned on one per night, in order.")]
        public GameObject[] messBatches = new GameObject[0];
        public float sleepSeconds = 10f;

        [Tooltip("Shown after waking. The last one repeats.")]
        [TextArea(1, 2)] public string[] wakeLines = new string[0];

        public int Nights { get; private set; }
        private bool _busy;

        /// Hooked to the bed's Interactable.
        public void LieDown()
        {
            var d = BedroomDirector.Instance;
            if (_busy || d == null || d.IsEnding) return;
            StartCoroutine(Sleep(d));
        }

        private IEnumerator Sleep(BedroomDirector d)
        {
            _busy = true;
            d.FreezePlayer(true);
            if (rustle) AudioSource.PlayClipAtPoint(rustle, transform.position + Vector3.up * 0.6f, 0.7f);

            // sink down onto the pillow
            Vector2 look0 = d.GetLook();
            Vector3 p0 = d.player.position;
            var black = Color.black; var clear = new Color(0, 0, 0, 0);
            StartCoroutine(d.hud.Fade(clear, black, 2.6f));
            for (float t = 0; t < 2.6f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 2.6f);
                d.SetLook(look0.x, Mathf.Lerp(look0.y, 25f, k));
                yield return null;
            }

            // dark: everything else cut, the affirmations right in her ear, the heart slowing
            var cut = d.Hush();
            var aff = speaker ? speaker.affirmations : null;
            float vol = aff ? aff.volume : 0, blend = aff ? aff.spatialBlend : 0;
            if (speaker && !speaker.IsOn) speaker.SetOn(true);
            if (aff)
            {
                aff.spatialBlend = 0f;
                if (!aff.isPlaying) aff.Play();
                aff.volume = vol * 1.6f;
            }
            d.SetHeartbeatBase(0.05f);
            yield return new WaitForSeconds(sleepSeconds);

            // morning, or whatever time it is: the room has gotten worse
            if (Nights < messBatches.Length && messBatches[Nights]) messBatches[Nights].SetActive(true);
            Nights++;
            if (aff) { aff.volume = vol; aff.spatialBlend = blend; }
            d.SetHeartbeatBase(speaker && !speaker.IsOn ? speaker.heartbeatWhenOff : 0.16f);
            d.Unhush(cut);
            d.Teleport(d.StartPosition, d.StartRotation);
            yield return new WaitForSeconds(0.8f);

            yield return d.hud.Fade(black, clear, 3f);
            if (wakeLines.Length > 0) d.hud.ShowLine(wakeLines[Mathf.Min(Nights - 1, wakeLines.Length - 1)]);
            d.FreezePlayer(false);
            _busy = false;
        }
    }
}
