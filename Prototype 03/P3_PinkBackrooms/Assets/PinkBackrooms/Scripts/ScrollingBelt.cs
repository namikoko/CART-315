using System.Collections;
using UnityEngine;

namespace PinkBackrooms
{
    /// Scrolls the walking pad's belt so it keeps running with nobody on it.
    public class ScrollingBelt : MonoBehaviour
    {
        public Renderer belt;
        public float speed = 0.6f;
        private Material _m;
        private float _offset;

        private void Start() { if (belt) _m = belt.material; }

        private void Update()
        {
            if (!_m) return;
            _offset += speed * Time.deltaTime;
            _m.SetTextureOffset("_BaseMap", new Vector2(0, _offset));
        }
    }
}
