using System.Collections;
using UnityEngine;

namespace PinkBackrooms
{
    /// Lying on the bed rotting, the laptop playing a villa dating show. You stay as long as you want.
    /// When you get up time has passed, and the room is a little worse than before.
    public class RotSesh : MonoBehaviour
    {
        [Header("Laptop")]
        public Renderer screen;
        public Texture idleScreen;
        public Texture2D[] showFrames = new Texture2D[0];
        public Color[] frameLight = new Color[0];
        public AudioSource showAudio;
        public Light screenLight;

        [Header("Pose")]
        [Tooltip("Where her eyes are, lying on her front propped up on the pillow, facing the laptop.")]
        public Vector3 eye = new Vector3(-0.22f, 0.86f, 1.3f);
        public Vector3 lookAt = new Vector3(-0.19f, 0.74f, 1.78f);

        [Header("After")]
        public AudioClip rustle;
        [Tooltip("Turned on one per sesh, in order.")]
        public GameObject[] messBatches = new GameObject[0];

        public int Seshes { get; private set; }
        private bool _busy;
        private Material _screenMat;
        private Color _lightColor; private float _lightIntensity;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private Color _idleEmission;
        [Tooltip("How bright the show is on the screen. Over ~1 it blooms into a white box.")]
        public float showBrightness = 0.7f;

        private void Start()
        {
            if (screen) { _screenMat = screen.material; _idleEmission = _screenMat.GetColor(EmissionColor); }
            if (screenLight) { _lightColor = screenLight.color; _lightIntensity = screenLight.intensity; }
        }

        /// Hooked to the bed's Interactable.
        public void LieDown()
        {
            var d = BedroomDirector.Instance;
            if (_busy || d == null || d.IsEnding) return;
            StartCoroutine(Run(d));
        }

        private IEnumerator Run(BedroomDirector d)
        {
            _busy = true;
            d.FreezePlayer(true);
            if (rustle) AudioSource.PlayClipAtPoint(rustle, transform.position + Vector3.up * 0.6f, 0.7f);

            // climb on and flop down facing the screen
            Vector3 dir = lookAt - eye;
            float yaw = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)).eulerAngles.y;
            float pitch = Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            Vector3 e0 = d.EyeNow(); Vector2 l0 = d.GetLook();
            for (float t = 0; t < 1.8f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 1.8f);
                Vector3 p = Vector3.Lerp(e0, eye, k);
                p.y = Mathf.Lerp(e0.y, eye.y, Mathf.SmoothStep(0, 1, Mathf.Clamp01(k * 1.4f)));
                d.PlaceEye(p, Mathf.LerpAngle(l0.x, yaw, k), Mathf.Lerp(l0.y, pitch, k));
                yield return null;
            }

            // the show
            if (_screenMat) _screenMat.SetColor(EmissionColor, Color.white * showBrightness);
            if (showAudio) { showAudio.time = Random.Range(0f, showAudio.clip ? showAudio.clip.length * 0.6f : 0f); showAudio.Play(); }
            int f = Random.Range(0, Mathf.Max(1, showFrames.Length));
            float cut = 0;
            yield return null; // don't let the same E press get you straight up
            d.hud.SetPrompt("Press [E] to get up");
            while (!BedroomDirector.LeavePressed() && !d.IsEnding)
            {
                cut -= Time.deltaTime;
                if (cut <= 0 && showFrames.Length > 0)
                {
                    f = (f + 1 + (Random.value < 0.3f ? 1 : 0)) % showFrames.Length;
                    SetScreen(showFrames[f]);
                    if (screenLight)
                    {
                        screenLight.color = f < frameLight.Length ? frameLight[f] : Color.white;
                        screenLight.intensity = _lightIntensity * 1.2f;
                    }
                    cut = Random.Range(1.8f, 4.5f);
                }
                d.FreeLook(yaw, 35f, pitch - 25f, pitch + 30f);
                yield return null;
            }

            // getting up, just getting up. Behind her back the room has gotten worse.
            d.hud.SetPrompt("");
            if (showAudio) showAudio.Stop();
            SetScreen(idleScreen);
            if (_screenMat) _screenMat.SetColor(EmissionColor, _idleEmission);
            if (screenLight) { screenLight.color = _lightColor; screenLight.intensity = _lightIntensity; }
            if (Seshes < messBatches.Length && messBatches[Seshes]) messBatches[Seshes].SetActive(true);
            Seshes++;
            if (rustle) AudioSource.PlayClipAtPoint(rustle, transform.position + Vector3.up * 0.6f, 0.5f);

            // push up off the bed, then step down beside it
            Vector2 la = d.GetLook(); Vector3 ea = d.EyeNow();
            Vector3 feet = d.StandPosition; float standYaw = d.StandRotation.eulerAngles.y;
            Vector3 eStand = feet + Quaternion.Euler(0, standYaw, 0) * d.EyeOffset;
            Vector3 kneel = new Vector3(Mathf.Lerp(ea.x, eStand.x, 0.35f), ea.y + 0.3f, Mathf.Lerp(ea.z, eStand.z, 0.35f));
            for (float t = 0; t < 1.0f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 1.0f);
                d.PlaceEye(Vector3.Lerp(ea, kneel, k), Mathf.LerpAngle(la.x, la.x + Mathf.DeltaAngle(la.x, standYaw) * 0.5f, k), Mathf.Lerp(la.y, -10f, k));
                yield return null;
            }
            Vector2 lb = d.GetLook();
            for (float t = 0; t < 1.3f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 1.3f);
                Vector3 p = Vector3.Lerp(kneel, eStand, k);
                p.y = Mathf.Lerp(kneel.y, eStand.y, Mathf.SmoothStep(0, 1, Mathf.Clamp01(k * 1.5f - 0.3f)));
                d.PlaceEye(p, Mathf.LerpAngle(lb.x, standYaw, k), Mathf.Lerp(lb.y, 0f, k));
                yield return null;
            }
            d.StandAt(feet, standYaw);
            d.FreezePlayer(false);
            _busy = false;
        }

        private void SetScreen(Texture tex)
        {
            if (!_screenMat || !tex) return;
            _screenMat.SetTexture(BaseMap, tex);
            _screenMat.SetTexture(EmissionMap, tex);
        }
    }
}
