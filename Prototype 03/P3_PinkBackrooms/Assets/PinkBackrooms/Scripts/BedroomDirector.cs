using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PinkBackrooms
{
    /// Runs the bedroom: fade in, objectives, knocks at the door, waking the mirror, and the ending.
    public class BedroomDirector : MonoBehaviour
    {
        public static BedroomDirector Instance { get; private set; }

        [Header("Refs")]
        public HUD hud;
        public Transform player;
        public PlayerInteractor interactor;
        public Volume volume;

        [Header("Objectives")]
        public string objectiveStart = "get up";
        public string objectiveLook = "look around";
        public string objectiveMirror = "look in the mirror";
        [Tooltip("How many things the player has to look at before the mirror wakes up.")]
        public int examinedToWakeMirror = 6;

        [Header("Mirror")]
        public FlickerLight mirrorLights;
        public Renderer mirrorGlass;
        [ColorUsage(false, true)] public Color mirrorGlow = new Color(1f, 0.55f, 0.75f) * 1.5f;

        [Header("Sound")]
        public AudioSource sfx;
        public AudioClip mirrorWakeSound;
        public AudioClip glitchSound;
        public AudioSource knockSource;
        public AudioClip[] knocks = new AudioClip[0];
        public Vector2 knockInterval = new Vector2(30, 70);
        public AudioSource[] ambienceToSilence = new AudioSource[0];
        [Tooltip("Faint anxious heartbeat under everything; it races during the ending.")]
        public AudioSource heartbeat;
        [Tooltip("Resting heartbeat volume. Louder when the speaker is off, quieter while she sleeps.")]
        public float heartbeatBase = 0.16f;

        [Header("Ending")]
        [TextArea(1, 2)] public string[] endingLines = { "it lets you go.", "for now." };
        public string endTitle = "MY ANA\n\npart one: the room";

        public bool MirrorAwake { get; private set; }

        private readonly HashSet<Interactable> _seen = new HashSet<Interactable>();
        private Vector3 _start;
        private int _stage;
        private bool _ending;
        private float _spike;
        private readonly List<Behaviour> _frozen = new List<Behaviour>();

        public Vector3 StartPosition { get; private set; }
        public Quaternion StartRotation { get; private set; }

        private void Awake() { Instance = this; }

        private IEnumerator Start()
        {
            if (player) { _start = player.position; StartPosition = player.position; StartRotation = player.rotation; }
            hud.SetObjective("");
            if (hud.fader) hud.fader.color = Color.black;
            yield return new WaitForSeconds(1.0f);
            yield return hud.Fade(Color.black, new Color(0, 0, 0, 0), 3.5f);
            hud.SetObjective(objectiveStart);
            StartCoroutine(Knocks());
        }

        private void Update()
        {
            if (heartbeat && !_ending)
            {
                _spike = Mathf.MoveTowards(_spike, 0, Time.deltaTime * 0.22f);
                heartbeat.volume = Mathf.Lerp(heartbeat.volume, heartbeatBase + _spike * 0.55f, Time.deltaTime * 3f);
                heartbeat.pitch = Mathf.Lerp(heartbeat.pitch, 1f + _spike * 0.5f, Time.deltaTime * 2f);
            }
            if (_stage == 0 && player)
            {
                var d = player.position - _start; d.y = 0;
                if (d.magnitude > 1.2f) Advance();
            }
        }

        private void Advance()
        {
            if (_stage > 0) return;
            _stage = 1;
            if (!MirrorAwake) hud.SetObjective(objectiveLook);
        }

        public void OnExamined(Interactable it)
        {
            Advance();
            _seen.Add(it);
            if (!MirrorAwake && _seen.Count >= examinedToWakeMirror) StartCoroutine(WakeMirror());
        }

        private IEnumerator WakeMirror()
        {
            MirrorAwake = true;
            yield return new WaitForSeconds(2.5f);
            if (sfx && mirrorWakeSound) sfx.PlayOneShot(mirrorWakeSound, 0.8f);
            if (mirrorLights)
            {
                mirrorLights.SetBaseIntensity(3f);
                mirrorLights.SetMode(FlickerLight.Mode.Flicker);
            }
            if (mirrorGlass)
            {
                var m = mirrorGlass.material;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", mirrorGlow * 0.25f);
            }
            hud.SetObjective(objectiveMirror);
        }

        private IEnumerator Knocks()
        {
            yield return new WaitForSeconds(Random.Range(18f, 28f));
            while (!_ending)
            {
                if (knockSource && knocks.Length > 0)
                {
                    int n = Random.Range(2, 4);
                    for (int i = 0; i < n; i++)
                    {
                        knockSource.PlayOneShot(knocks[Random.Range(0, knocks.Length)], Random.Range(0.6f, 1f));
                        yield return new WaitForSeconds(Random.Range(0.35f, 0.6f));
                    }
                }
                yield return new WaitForSeconds(Random.Range(knockInterval.x, knockInterval.y));
            }
        }

        // ---------------------------------------------------------------- shared helpers for the room's rituals

        /// Pushes the heartbeat up (0..1). It eases back down on its own over a few seconds.
        public void HeartSpike(float amount = 1f) { _spike = Mathf.Max(_spike, amount); }

        public void SetHeartbeatBase(float v) { heartbeatBase = v; }

        /// Cuts every ambient sound that is playing right now, leaving only the heartbeat. Returns what was cut.
        public List<AudioSource> Hush()
        {
            var cut = new List<AudioSource>();
            foreach (var a in ambienceToSilence)
                if (a && a.isPlaying) { a.Pause(); cut.Add(a); }
            return cut;
        }

        public void Unhush(List<AudioSource> cut)
        {
            if (_ending || cut == null) return;
            foreach (var a in cut) if (a) a.UnPause();
        }

        /// Takes control away from the player (movement, looking, [E]) or gives it back.
        public void FreezePlayer(bool freeze)
        {
            if (interactor) interactor.inputEnabled = !freeze;
            if (freeze)
            {
                _frozen.Clear();
                foreach (var mb in player.GetComponents<MonoBehaviour>())
                    if (mb.enabled && mb.GetType().Namespace == "Cart315.FirstPerson") { mb.enabled = false; _frozen.Add(mb); }
                hud.SetPrompt("");
            }
            else if (!_ending)
            {
                foreach (var b in _frozen) if (b) b.enabled = true;
                _frozen.Clear();
            }
        }

        /// Moves the player without fighting the CharacterController.
        public void Teleport(Vector3 pos, Quaternion? rot = null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.position = pos;
            if (cc) cc.enabled = true;
            if (rot.HasValue) SetLook(rot.Value.eulerAngles.y, 0f);
        }

        // The template's controller keeps its own look angles, so turning the head from a script has to update them too.
        private Component _fp;
        private System.Reflection.FieldInfo _lookField, _holderField;

        private bool FindLook()
        {
            if (_fp && _lookField != null) return true;
            foreach (var mb in player.GetComponents<MonoBehaviour>())
                if (mb.GetType().Name == "FPController") _fp = mb;
            if (!_fp) return false;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            _lookField = _fp.GetType().GetField("_lookDirection", F);
            _holderField = _fp.GetType().GetField("_cameraHolderTransform", F);
            return _lookField != null;
        }

        /// yaw in degrees around Y; pitch in degrees, negative looks down.
        public Vector2 GetLook()
        {
            if (!FindLook()) return new Vector2(player.eulerAngles.y, 0);
            return (Vector2)_lookField.GetValue(_fp);
        }

        public void SetLook(float yaw, float pitch)
        {
            player.rotation = Quaternion.Euler(0, yaw, 0);
            if (!FindLook()) return;
            _lookField.SetValue(_fp, new Vector2(yaw, pitch));
            var holder = _holderField != null ? _holderField.GetValue(_fp) as Transform : null;
            if (holder) holder.localRotation = Quaternion.Euler(-pitch, 0, 0);
        }

        public bool IsEnding => _ending;

        public void BeginEnding()
        {
            if (_ending) return;
            _ending = true;
            StartCoroutine(Ending());
        }

        private IEnumerator Ending()
        {
            if (interactor) interactor.inputEnabled = false;
            hud.SetObjective("");
            hud.SetPrompt("");

            // Freeze the player where they stand.
            foreach (var mb in player.GetComponents<MonoBehaviour>())
                if (mb.GetType().Namespace == "Cart315.FirstPerson") mb.enabled = false;

            if (sfx && glitchSound) sfx.PlayOneShot(glitchSound, 1f);

            ChromaticAberration ca = null; LensDistortion ld = null; ColorAdjustments color = null;
            if (volume && volume.profile)
            {
                volume.profile.TryGet(out ca);
                volume.profile.TryGet(out ld);
                volume.profile.TryGet(out color);
            }
            Material glass = mirrorGlass ? mirrorGlass.material : null;

            for (float t = 0; t < 2.2f; t += Time.deltaTime)
            {
                float k = t / 2.2f;
                if (ca) ca.intensity.Override(Mathf.Lerp(0.45f, 1f, k));
                if (ld) ld.intensity.Override(Mathf.Lerp(-0.15f, -0.65f, k) + Random.Range(-0.05f, 0.05f));
                if (color) color.postExposure.Override(Mathf.Lerp(0f, 2.5f, k * k));
                if (glass) glass.SetColor("_EmissionColor", mirrorGlow * Mathf.Lerp(0.25f, 6f, k));
                foreach (var a in ambienceToSilence) if (a) a.volume *= 0.97f;
                if (heartbeat) { heartbeat.pitch = Mathf.Lerp(1f, 1.7f, k); heartbeat.volume = Mathf.Lerp(heartbeat.volume, 0.6f, 0.03f); }
                yield return null;
            }

            yield return hud.Fade(new Color(1f, 0.85f, 0.9f, 0f), new Color(1f, 0.85f, 0.9f, 1f), 0.6f);
            yield return hud.Fade(new Color(1f, 0.85f, 0.9f, 1f), Color.black, 1.5f);
            foreach (var a in ambienceToSilence) if (a) a.Stop();
            if (heartbeat) heartbeat.Stop();

            foreach (var l in endingLines)
            {
                hud.ShowLine(l);
                yield return new WaitForSeconds(2.2f + l.Length * 0.08f);
            }
            yield return hud.ShowTitle(endTitle, 6f);
        }
    }
}
