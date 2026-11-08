using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PinkBackrooms
{
    /// Objective text (top left), interact prompt (centre), thought lines (bottom), and a full-screen fader.
    /// The canvas renders through the player camera so the text gets pixelated and grained like the room.
    public class HUD : MonoBehaviour
    {
        public Text objective;
        public Text prompt;
        public Text line;
        public Text title;
        public Image fader;

        [Tooltip("Tried in order at runtime for a typewriter / VHS feel. Falls back to the default font.")]
        public string[] fontNames = { "Courier New", "Courier", "Menlo", "Monaco", "Consolas" };
        public float typeSpeed = 0.035f;

        private Coroutine _lineRoutine;

        private void Awake()
        {
            var font = Font.CreateDynamicFontFromOSFont(fontNames, 24);
            if (font != null)
                foreach (var t in GetComponentsInChildren<Text>(true)) t.font = font;
            if (line) line.text = "";
            if (prompt) prompt.text = "";
            if (title) title.text = "";
        }

        public void SetObjective(string s)
        {
            if (objective) objective.text = string.IsNullOrEmpty(s) ? "" : "Objective: " + s;
        }

        public void SetPrompt(string s)
        {
            if (prompt && prompt.text != s) prompt.text = s;
        }

        public void ShowLine(string s)
        {
            if (!line) return;
            if (_lineRoutine != null) StopCoroutine(_lineRoutine);
            _lineRoutine = StartCoroutine(TypeLine(s));
        }

        private IEnumerator TypeLine(string s)
        {
            var c = line.color; c.a = 1; line.color = c;
            for (int i = 0; i <= s.Length; i++)
            {
                line.text = s.Substring(0, i);
                yield return new WaitForSeconds(typeSpeed);
            }
            yield return new WaitForSeconds(2.2f + s.Length * 0.035f);
            for (float t = 0; t < 1; t += Time.deltaTime)
            {
                c.a = 1 - t; line.color = c;
                yield return null;
            }
            line.text = "";
        }

        public IEnumerator Fade(Color from, Color to, float duration)
        {
            if (!fader) yield break;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                fader.color = Color.Lerp(from, to, t / duration);
                yield return null;
            }
            fader.color = to;
        }

        public IEnumerator ShowTitle(string s, float hold)
        {
            if (!title) yield break;
            title.text = s;
            var c = title.color;
            for (float t = 0; t < 1.5f; t += Time.deltaTime) { c.a = t / 1.5f; title.color = c; yield return null; }
            yield return new WaitForSeconds(hold);
            for (float t = 0; t < 1.5f; t += Time.deltaTime) { c.a = 1 - t / 1.5f; title.color = c; yield return null; }
            c.a = 0; title.color = c;
        }
    }
}
