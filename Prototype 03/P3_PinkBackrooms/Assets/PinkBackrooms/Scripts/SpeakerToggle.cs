using UnityEngine;

namespace PinkBackrooms
{
    /// The bluetooth speaker. Turning the affirmations off leaves the room quiet enough to hear her heart.
    public class SpeakerToggle : MonoBehaviour
    {
        public AudioSource affirmations;
        public FlickerLight led;
        public Interactable interactable;
        public AudioClip click;

        public string offVerb = "turn off the speaker";
        public string onVerb = "turn on the speaker";
        [Tooltip("Heartbeat volume while the speaker is off. On, it goes back to its usual level.")]
        public float heartbeatWhenOff = 0.45f;
        public float heartbeatWhenOn = 0.16f;

        [TextArea(1, 2)] public string[] offLines = new string[0];
        [TextArea(1, 2)] public string[] onLines = new string[0];

        public bool IsOn { get; private set; } = true;
        private int _off, _on;

        private void Start()
        {
            if (interactable) interactable.verb = offVerb;
        }

        /// Hooked to the speaker's Interactable.
        public void Toggle()
        {
            SetOn(!IsOn);
            var d = BedroomDirector.Instance;
            var pool = IsOn ? onLines : offLines;
            int idx = IsOn ? _on++ : _off++;
            if (d && d.hud && pool.Length > 0) d.hud.ShowLine(pool[Mathf.Min(idx, pool.Length - 1)]);
        }

        public void SetOn(bool on)
        {
            IsOn = on;
            if (affirmations)
            {
                if (on) { if (!affirmations.isPlaying) affirmations.Play(); }
                else affirmations.Stop();
            }
            if (led) led.SetMode(on ? FlickerLight.Mode.Pulse : FlickerLight.Mode.Off);
            if (interactable) interactable.verb = on ? offVerb : onVerb;
            if (click) AudioSource.PlayClipAtPoint(click, transform.position, 0.6f);
            var d = BedroomDirector.Instance;
            if (d) d.SetHeartbeatBase(on ? heartbeatWhenOn : heartbeatWhenOff);
        }
    }
}
