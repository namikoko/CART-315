using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PinkBackrooms
{
    /// Runs the bedroom. No words anywhere: you wake up in bed staring at the ceiling, get up when you're ready,
    /// and after you've done two things in the room the mirror wakes and takes you further in.
    public class BedroomDirector : MonoBehaviour
    {
        public static BedroomDirector Instance { get; private set; }

        [Header("Refs")]
        public HUD hud;
        public Transform player;
        public PlayerInteractor interactor;
        public Volume volume;
        public RoomMood mood;

        [Header("Waking up")]
        [Tooltip("Where her eyes are while she lies in bed at the start (on her pillow, looking up).")]
        public Vector3 lyingEye = new Vector3(-0.86f, 0.8f, 1.8f);
        public float lyingYaw = 180f;
        [Tooltip("Where she stands once she's out of bed (the player's feet).")]
        public Vector3 standPosition = new Vector3(-1.62f, 0.05f, 1.0f);
        public float standYaw = 150f;
        [Tooltip("If the player does nothing, she gets up on her own after this long.")]
        public float getUpAnyway = 40f;

        [Header("Mirror")]
        [Tooltip("How many different things the player has to do before the mirror wakes up.")]
        public int examinedToWakeMirror = 2;
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
        [Tooltip("Resting heartbeat volume.")]
        public float heartbeatBase = 0.16f;

        public bool MirrorAwake { get; private set; }
        public bool IsEnding => _ending;
        public bool OutOfBed { get; private set; }
        public Vector3 StandPosition => standPosition;
        public Quaternion StandRotation => Quaternion.Euler(0, standYaw, 0);

        private readonly HashSet<Interactable> _seen = new HashSet<Interactable>();
        private readonly List<Behaviour> _frozen = new List<Behaviour>();
        private bool _ending;
        private float _spike;
        private Vector3 _eyeOffset;   // camera position relative to the player's feet, standing, in the player's local space

        private void Awake() { Instance = this; }

        private IEnumerator Start()
        {
            var cam = interactor ? interactor.cam : Camera.main;
            if (cam) _eyeOffset = player.InverseTransformPoint(cam.transform.position);
            if (_eyeOffset.y < 0.5f) _eyeOffset = new Vector3(0, 1.6f, 0);

            // black. lying on her back on the pillow, looking up at the canopy.
            FreezePlayer(true);
            if (hud.fader) hud.fader.color = Color.black;
            PlaceEye(lyingEye, lyingYaw, 82f);
            yield return new WaitForSeconds(2.5f);
            yield return hud.Fade(Color.black, new Color(0, 0, 0, 0), 5f);
            StartCoroutine(Knocks());

            // she gets up when you do something (any key, a click, moving the mouse a lot)
            hud.SetPrompt("Press [E] to get up");
            float waited = 0, mouse = 0;
            while (waited < getUpAnyway)
            {
                waited += Time.deltaTime;
                var kb = Keyboard.current; var m = Mouse.current;
                if (kb != null && kb.anyKey.wasPressedThisFrame) break;
                if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)) break;
                if (m != null) { mouse += m.delta.ReadValue().magnitude; if (mouse > 900) break; }
                // let the eyes wander a little while lying there
                if (m != null)
                {
                    var l = GetLook(); var d = m.delta.ReadValue() * 0.05f;
                    SetLook(Mathf.Clamp(Mathf.DeltaAngle(lyingYaw, l.x + d.x), -25, 25) + lyingYaw, Mathf.Clamp(l.y + d.y, 60, 88));
                }
                yield return null;
            }
            hud.SetPrompt("");
            yield return GetUp();
        }

        /// Sit up, swing the legs off the left side, stand.
        private IEnumerator GetUp()
        {
            Vector2 l0 = GetLook();
            Vector3 e0 = EyeNow();
            Vector3 sit = new Vector3(lyingEye.x - 0.1f, 1.05f, lyingEye.z - 0.35f);
            for (float t = 0; t < 1.6f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 1.6f);
                PlaceEye(Vector3.Lerp(e0, sit, k), l0.x + Mathf.DeltaAngle(l0.x, lyingYaw) * k, Mathf.Lerp(l0.y, 8f, k));
                yield return null;
            }
            Vector3 standEye = standPosition + Quaternion.Euler(0, standYaw, 0) * _eyeOffset;
            float y0 = lyingYaw, y1 = lyingYaw + Mathf.DeltaAngle(lyingYaw, standYaw);
            for (float t = 0; t < 2.2f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 2.2f);
                Vector3 p = Vector3.Lerp(sit, standEye, k);
                p.y = Mathf.Lerp(sit.y, standEye.y, Mathf.SmoothStep(0, 1, Mathf.Clamp01(k * 1.6f - 0.4f)));
                PlaceEye(p, Mathf.Lerp(y0, y1, k), Mathf.Lerp(8f, 0f, k));
                yield return null;
            }
            StandAt(standPosition, standYaw);
            OutOfBed = true;
            FreezePlayer(false);
        }

        private void Update()
        {
            if (heartbeat && !_ending)
            {
                _spike = Mathf.MoveTowards(_spike, 0, Time.deltaTime * 0.22f);
                heartbeat.volume = Mathf.Lerp(heartbeat.volume, heartbeatBase + _spike * 0.55f, Time.deltaTime * 3f);
                heartbeat.pitch = Mathf.Lerp(heartbeat.pitch, 1f + _spike * 0.5f, Time.deltaTime * 2f);
            }
        }

        public void OnExamined(Interactable it)
        {
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
                foreach (var mb in player.GetComponents<MonoBehaviour>())
                    if (mb.enabled && mb.GetType().Namespace == "Cart315.FirstPerson") { mb.enabled = false; _frozen.Add(mb); }
                if (hud) hud.SetPrompt("");
            }
            else if (!_ending)
            {
                foreach (var b in _frozen) if (b) b.enabled = true;
                _frozen.Clear();
            }
        }

        /// Puts the camera at a world position (lying, sitting, crouched...). The body goes wherever that needs,
        /// so collisions are off until StandAt is called.
        public void PlaceEye(Vector3 eye, float yaw, float pitch)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.position = eye - Quaternion.Euler(0, yaw, 0) * _eyeOffset;
            SetLook(yaw, pitch);
        }

        public Vector3 EyeNow() => player.position + player.rotation * _eyeOffset;
        public Vector3 EyeOffset => _eyeOffset;

        /// Back on her feet at a spot on the floor, with collisions on.
        public void StandAt(Vector3 feet, float yaw, float pitch = 0f)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.position = feet;
            SetLook(yaw, pitch);
            if (cc) cc.enabled = true;
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

        /// Mouse look while the controller is switched off (lying, walking on the pad). Keeps within a range around a centre.
        public void FreeLook(float centreYaw, float yawRange, float minPitch, float maxPitch, float sensitivity = 0.06f)
        {
            var m = Mouse.current; if (m == null) return;
            var l = GetLook(); var d = m.delta.ReadValue() * sensitivity;
            float yaw = centreYaw + Mathf.Clamp(Mathf.DeltaAngle(centreYaw, l.x + d.x), -yawRange, yawRange);
            SetLook(yaw, Mathf.Clamp(l.y + d.y, minPitch, maxPitch));
        }

        /// E, space or escape: get off / get up.
        public static bool LeavePressed()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
        }

        public void BeginEnding()
        {
            if (_ending) return;
            _ending = true;
            StartCoroutine(Ending());
        }

        private IEnumerator Ending()
        {
            if (interactor) interactor.inputEnabled = false;
            hud.SetPrompt("");
            if (mood) mood.enabled = false;

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
        }
    }
}
