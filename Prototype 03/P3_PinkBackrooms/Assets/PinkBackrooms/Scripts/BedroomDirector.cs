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

        [Header("Ending")]
        [TextArea(1, 2)] public string[] endingLines = { "it lets you go.", "for now." };
        public string endTitle = "PINK BACKROOMS\n\npart one: the room";

        public bool MirrorAwake { get; private set; }

        private readonly HashSet<Interactable> _seen = new HashSet<Interactable>();
        private Vector3 _start;
        private int _stage;
        private bool _ending;

        private void Awake() { Instance = this; }

        private IEnumerator Start()
        {
            if (player) _start = player.position;
            hud.SetObjective("");
            if (hud.fader) hud.fader.color = Color.black;
            yield return new WaitForSeconds(1.0f);
            yield return hud.Fade(Color.black, new Color(0, 0, 0, 0), 3.5f);
            hud.SetObjective(objectiveStart);
            StartCoroutine(Knocks());
        }

        private void Update()
        {
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
                yield return null;
            }

            yield return hud.Fade(new Color(1f, 0.85f, 0.9f, 0f), new Color(1f, 0.85f, 0.9f, 1f), 0.6f);
            yield return hud.Fade(new Color(1f, 0.85f, 0.9f, 1f), Color.black, 1.5f);
            foreach (var a in ambienceToSilence) if (a) a.Stop();

            foreach (var l in endingLines)
            {
                hud.ShowLine(l);
                yield return new WaitForSeconds(2.2f + l.Length * 0.08f);
            }
            yield return hud.ShowTitle(endTitle, 6f);
        }
    }
}
