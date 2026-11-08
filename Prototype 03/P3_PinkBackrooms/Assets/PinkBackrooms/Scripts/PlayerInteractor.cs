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

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            if (hud == null) return;

            _current = null;
            if (inputEnabled && cam != null &&
                Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, reach,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                _current = hit.collider.GetComponentInParent<Interactable>();
                if (_current != null && !_current.isActiveAndEnabled) _current = null;
            }

            hud.SetPrompt(_current != null ? "Press [E] to " + _current.verb : "");

            var kb = Keyboard.current;
            if (_current != null && kb != null && kb.eKey.wasPressedThisFrame)
                _current.Interact(hud);
        }
    }
}
