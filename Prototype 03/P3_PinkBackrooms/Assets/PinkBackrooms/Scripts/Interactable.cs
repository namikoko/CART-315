using UnityEngine;
using UnityEngine.Events;

namespace PinkBackrooms
{
    /// Anything in the room the player can look at with [E].
    /// The lines are written in the Inspector, so the words can change without touching code.
    public class Interactable : MonoBehaviour
    {
        [Tooltip("Shown as: Press [E] to <verb>")]
        public string verb = "look";

        [Tooltip("Lines shown at the bottom of the screen. Each press shows the next one; the last one repeats.")]
        [TextArea(1, 3)] public string[] lines = new string[0];

        [Tooltip("Counts toward waking up the mirror.")]
        public bool countsTowardMirror = true;

        [Header("Mirror")]
        [Tooltip("Only works once the mirror is awake. Before that, Locked Line is shown instead.")]
        public bool requiresMirrorAwake;
        [TextArea(1, 2)] public string lockedLine = "";
        [Tooltip("Interacting starts the ending.")]
        public bool triggersEnding;

        [Header("Feedback")]
        public AudioClip sound;
        [Range(0, 1)] public float soundVolume = 0.6f;
        public UnityEvent onInteract = new UnityEvent();

        private int _index;
        public bool Seen { get; private set; }

        public void Interact(HUD hud)
        {
            var director = BedroomDirector.Instance;

            if (requiresMirrorAwake && (director == null || !director.MirrorAwake))
            {
                if (hud && !string.IsNullOrEmpty(lockedLine)) hud.ShowLine(lockedLine);
                return;
            }

            if (triggersEnding && director != null)
            {
                director.BeginEnding();
                return;
            }

            if (lines != null && lines.Length > 0 && hud)
            {
                hud.ShowLine(lines[Mathf.Min(_index, lines.Length - 1)]);
                _index++;
            }

            if (sound) AudioSource.PlayClipAtPoint(sound, transform.position, soundVolume);

            if (!Seen)
            {
                Seen = true;
                if (countsTowardMirror && director != null) director.OnExamined(this);
            }

            onInteract?.Invoke();
        }
    }
}
