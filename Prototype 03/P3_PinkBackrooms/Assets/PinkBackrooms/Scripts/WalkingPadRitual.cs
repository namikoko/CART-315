using System.Collections;
using UnityEngine;

namespace PinkBackrooms
{
    /// Getting on the walking pad. You walk, and walk, and walk. The belt runs, your steps keep time, the room
    /// gets darker the longer you stay. You can look around. E / space gets you off.
    public class WalkingPadRitual : MonoBehaviour
    {
        public ScrollingBelt belt;
        public AudioSource motor;
        public AudioClip[] steps = new AudioClip[0];
        public float beltWalking = 1.6f;
        public float stepInterval = 0.52f;

        private bool _on;
        private float _beltIdle, _motorPitch;

        /// Hooked to the Interactable.
        public void StepOn()
        {
            var d = BedroomDirector.Instance;
            if (_on || d == null || d.IsEnding) return;
            StartCoroutine(Run(d));
        }

        private IEnumerator Run(BedroomDirector d)
        {
            _on = true;
            d.FreezePlayer(true);
            if (belt) _beltIdle = belt.speed;
            if (motor) _motorPitch = motor.pitch;

            // up onto the belt, facing the front (where the display is)
            float yaw = transform.eulerAngles.y;
            Vector3 feet = transform.TransformPoint(new Vector3(0, 0.09f, -0.1f));
            Vector3 e0 = d.EyeNow(); Vector2 l0 = d.GetLook();
            Vector3 e1 = feet + Quaternion.Euler(0, yaw, 0) * d.EyeOffset;
            for (float t = 0; t < 0.8f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 0.8f);
                d.PlaceEye(Vector3.Lerp(e0, e1, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.06f,
                    Mathf.LerpAngle(l0.x, yaw, k), Mathf.Lerp(l0.y, -5f, k));
                yield return null;
            }

            yield return null; // don't let the same E press take you straight off
            d.hud.SetPrompt("Press [E] to step off");
            float walk = 0, nextStep = 0; int foot = 0;
            if (d.mood) d.mood.dimTarget = 0.85f;
            while (!BedroomDirector.LeavePressed() && !d.IsEnding)
            {
                float dt = Time.deltaTime;
                walk += dt;
                if (belt) belt.speed = Mathf.Lerp(belt.speed, beltWalking, dt * 2f);
                if (motor) motor.pitch = Mathf.Lerp(motor.pitch, _motorPitch * 1.25f, dt * 2f);

                // head bob and a little sway, step by step
                float ph = walk / stepInterval * Mathf.PI;
                Vector3 bob = Vector3.up * (Mathf.Abs(Mathf.Sin(ph)) * 0.035f - 0.02f)
                            + Quaternion.Euler(0, yaw, 0) * Vector3.right * Mathf.Sin(ph * 0.5f) * 0.015f;
                var look = d.GetLook();
                d.PlaceEye(e1 + bob, look.x, look.y);
                d.FreeLook(yaw, 100f, -60f, 45f);

                if (walk >= nextStep && steps.Length > 0)
                {
                    nextStep += stepInterval;
                    AudioSource.PlayClipAtPoint(steps[(foot++ * 3) % steps.Length], feet, 0.25f);
                }
                yield return null;
            }

            // step off to the left side
            d.hud.SetPrompt("");
            if (d.mood) d.mood.dimTarget = 0f;
            if (belt) belt.speed = _beltIdle;
            if (motor) motor.pitch = _motorPitch;
            Vector3 off = transform.TransformPoint(new Vector3(-0.55f, 0, -0.1f)); off.y = 0.05f;
            var lk = d.GetLook();
            Vector3 eA = d.EyeNow(), eB = off + Quaternion.Euler(0, lk.x, 0) * d.EyeOffset;
            for (float t = 0; t < 0.6f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 0.6f);
                d.PlaceEye(Vector3.Lerp(eA, eB, k), lk.x, lk.y);
                yield return null;
            }
            d.StandAt(off, lk.x, lk.y);
            d.FreezePlayer(false);
            _on = false;
        }
    }
}
