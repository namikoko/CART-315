using UnityEngine;
using UnityEngine.InputSystem;

namespace PinkBackrooms
{
    /// Looks straight ahead from the camera; if an Interactable is in reach, shows the prompt and handles [E].
    public class PlayerInteractor : MonoBehaviour
    {
        public Camera cam;
        public HUD hud;
        public float reach = 2.3f;
        public bool inputEnabled = true;

        private Interactable _current;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        /// Small things sit inside bigger things' zones (perfume on the vanity, the laptop on the bed),
        /// so of everything the look ray passes through before it hits something solid, the smallest wins.
        private Interactable Pick()
        {
            int n = Physics.RaycastNonAlloc(cam.transform.position, cam.transform.forward, Hits, reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float solid = reach;
            for (int i = 0; i < n; i++)
                if (!Hits[i].collider.isTrigger && Hits[i].distance < solid) solid = Hits[i].distance;

            Interactable best = null;
            float bestSize = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].distance > solid + 0.02f) continue;
                var it = Hits[i].collider.GetComponentInParent<Interactable>();
                if (it == null || !it.isActiveAndEnabled || !it.CanUse) continue;
                var s = Hits[i].collider.bounds.size;
                float size = s.x * s.y * s.z;
                if (size < bestSize) { bestSize = size; best = it; }
            }
            return best;
        }

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            if (hud == null) return;

            if (!inputEnabled) { _current = null; return; }   // a ritual is running and shows its own prompt
            _current = cam != null ? Pick() : null;

            hud.SetPrompt(_current != null ? "Press [E] to " + _current.verb : "");

            var kb = Keyboard.current;
            if (_current != null && kb != null && kb.eKey.wasPressedThisFrame)
                _current.Interact(hud);
        }
    }
}
