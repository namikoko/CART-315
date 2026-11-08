using System.Collections;
using UnityEngine;

namespace PinkBackrooms
{
    /// Live static on the CRT screen, and the cold light it throws on the bed.
    public class TVStatic : MonoBehaviour
    {
        public Renderer screen;
        public Light glow;
        public int width = 48, height = 36;
        public float frameTime = 0.06f;

        private Texture2D _tex;
        private Color32[] _px;
        private float _timer, _glowBase;

        private void Start()
        {
            _tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            _px = new Color32[width * height];
            if (screen)
            {
                var m = screen.material;
                m.SetTexture("_BaseMap", _tex);
                m.SetTexture("_EmissionMap", _tex);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.8f, 0.85f, 1f) * 1.4f);
            }
            if (glow) _glowBase = glow.intensity;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < frameTime) return;
            _timer = 0;
            int roll = Random.Range(0, height);
            for (int y = 0; y < height; y++)
            {
                byte band = (byte)(y == roll || y == (roll + 1) % height ? 60 : 0);
                for (int x = 0; x < width; x++)
                {
                    byte v = (byte)Mathf.Min(255, Random.Range(20, 200) + band);
                    _px[y * width + x] = new Color32(v, v, (byte)Mathf.Min(255, v + 12), 255);
                }
            }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
            if (glow) glow.intensity = _glowBase * Random.Range(0.75f, 1.1f);
        }
    }
}
